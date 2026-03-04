using Flue.Core.Abstractions;
using Flue.Core.Models;

namespace Flue.Infrastructure.Logic;

public sealed partial class TypeScriptLogicBridge : ILogicBridge
{
    private static readonly FrozenDictionary<string, string> TypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "String",
        ["number"] = "double",
        ["boolean"] = "bool",
        ["any"] = "dynamic",
        ["unknown"] = "dynamic",
        ["void"] = "void"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("const\\s+(?<name>[A-Za-z_]\\w*)\\s*=\\s*ref(?:<(?<type>[^>]+)>)?\\((?<value>[^)]*)\\)", RegexOptions.Compiled)]
    private static partial Regex RefRegex ();

    [GeneratedRegex("^(?<keyword>const|let|var)\\s+(?<name>[A-Za-z_]\\w*)(?:\\s*:\\s*(?<type>[^=]+))?(?:\\s*=\\s*(?<value>.+))?;?$", RegexOptions.Compiled)]
    private static partial Regex VariableRegex ();

    [GeneratedRegex("(?:const|let|var)\\s+(?<name>[A-Za-z_]\\w*)\\s*=\\s*(?<async>async\\s*)?\\((?<params>[^)]*)\\)\\s*=>\\s*(?<body>\\{[\\s\\S]*?\\}|[^;]+);", RegexOptions.Compiled)]
    private static partial Regex ArrowMethodRegex ();

    [GeneratedRegex("(?<async>async\\s+)?function\\s+(?<name>[A-Za-z_]\\w*)\\s*\\((?<params>[^)]*)\\)\\s*\\{(?<body>[\\s\\S]*?)\\}", RegexOptions.Compiled)]
    private static partial Regex FunctionMethodRegex ();

    [GeneratedRegex("^(?<name>[A-Za-z_]\\w*)\\s*(=|\\+=|-=|\\*=|/=|%=).+$", RegexOptions.Compiled)]
    private static partial Regex AssignmentRegex ();

    [GeneratedRegex("^(?<name>[A-Za-z_]\\w*)(\\+\\+|--)$", RegexOptions.Compiled)]
    private static partial Regex IncrementRegex ();

    [GeneratedRegex("^console\\.log\\((?<args>[\\s\\S]*)\\)$", RegexOptions.Compiled)]
    private static partial Regex ConsoleRegex ();

    [GeneratedRegex("^(?:(?<keyword>const|let|var)\\s+(?<name>[A-Za-z_]\\w*)(?:\\s*:\\s*(?<type>[^=]+))?\\s*=\\s*)?(?<await>await\\s+)?fetch\\((?<args>[\\s\\S]+)\\)$", RegexOptions.Compiled)]
    private static partial Regex FetchRegex ();

    [GeneratedRegex("^(?:(?<keyword>const|let|var)\\s+(?<name>[A-Za-z_]\\w*)(?:\\s*:\\s*(?<type>[^=]+))?\\s*=\\s*)?(?:await\\s+)?(?<response>[A-Za-z_]\\w*)\\.json\\(\\)$", RegexOptions.Compiled)]
    private static partial Regex JsonResponseRegex ();

    [GeneratedRegex("^(?:(?<keyword>const|let|var)\\s+(?<name>[A-Za-z_]\\w*)(?:\\s*:\\s*(?<type>[^=]+))?\\s*=\\s*)?(?:await\\s+)?(?<response>[A-Za-z_]\\w*)\\.text\\(\\)$", RegexOptions.Compiled)]
    private static partial Regex TextResponseRegex ();

    [GeneratedRegex("^(?<await>await\\s+)?(?<router>[A-Za-z_]\\w*)\\.(?<action>push|replace)\\((?<target>[\\s\\S]+)\\)$", RegexOptions.Compiled)]
    private static partial Regex RouterNavigateRegex ();

    [GeneratedRegex("^(?<await>await\\s+)?(?<router>[A-Za-z_]\\w*)\\.back\\(\\)$", RegexOptions.Compiled)]
    private static partial Regex RouterBackRegex ();

    [GeneratedRegex("`(?<content>(?:\\\\.|[^`])*)`", RegexOptions.Compiled)]
    private static partial Regex TemplateRegex ();

    public LogicBridgeResult Parse (string scriptContent)
    {
        if (string.IsNullOrWhiteSpace(scriptContent))
        {
            return new LogicBridgeResult(ImmutableArray<StateField>.Empty, ImmutableArray<DartMethod>.Empty);
        }

        var stateFields = ParseStateFields(scriptContent);
        var stateNames = stateFields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var methods = ParseMethods(scriptContent, stateNames, out var requiresHttp, out var requiresJson, out var requiresRouter);
        return new LogicBridgeResult(stateFields, methods, requiresHttp, requiresJson, requiresRouter);
    }

    private static ImmutableArray<StateField> ParseStateFields (string scriptContent)
    {
        var fields = ImmutableArray.CreateBuilder<StateField>();
        var knownNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in RefRegex().Matches(scriptContent))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!knownNames.Add(name))
            {
                continue;
            }

            var tsType = match.Groups["type"].Value.Trim();
            var value = match.Groups["value"].Value.Trim();
            fields.Add(new StateField(name, ResolveDartType(tsType, value), ResolveInitializer(value, ResolveDartType(tsType, value))));
        }

        var braceDepth = 0;
        foreach (var rawLine in scriptContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (braceDepth == 0 &&
                !line.Contains("=>", StringComparison.Ordinal) &&
                !line.StartsWith("function", StringComparison.Ordinal) &&
                !line.StartsWith("async function", StringComparison.Ordinal) &&
                !line.StartsWith("import ", StringComparison.Ordinal) &&
                !line.Contains("ref(", StringComparison.Ordinal) &&
                !line.Contains("useRouter(", StringComparison.Ordinal))
            {
                var match = VariableRegex().Match(line);
                if (match.Success)
                {
                    var name = match.Groups["name"].Value.Trim();
                    if (knownNames.Add(name))
                    {
                        var tsType = match.Groups["type"].Value.Trim();
                        var rawValue = match.Groups["value"].Value.Trim();
                        var dartType = ResolveDartType(tsType, rawValue);
                        fields.Add(new StateField(name, dartType, ResolveInitializer(rawValue, dartType)));
                    }
                }
            }

            braceDepth += line.Count(character => character == '{');
            braceDepth -= line.Count(character => character == '}');
            braceDepth = Math.Max(0, braceDepth);
        }

        return fields.ToImmutable();
    }

    private static ImmutableArray<DartMethod> ParseMethods (string scriptContent, HashSet<string> stateNames, out bool requiresHttp, out bool requiresJson, out bool requiresRouter)
    {
        var methods = ImmutableArray.CreateBuilder<DartMethod>();
        var knownNames = new HashSet<string>(StringComparer.Ordinal);
        requiresHttp = false;
        requiresJson = false;
        requiresRouter = false;

        foreach (Match match in ArrowMethodRegex().Matches(scriptContent))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!knownNames.Add(name))
            {
                continue;
            }

            var body = ConvertMethodBody(match.Groups["body"].Value, stateNames, ref requiresHttp, ref requiresJson, ref requiresRouter);
            var isAsync = !string.IsNullOrWhiteSpace(match.Groups["async"].Value) || body.Contains("await ", StringComparison.Ordinal);
            methods.Add(new DartMethod(name, ConvertParameters(match.Groups["params"].Value), body, isAsync, isAsync ? "Future<void>" : "void"));
        }

        foreach (Match match in FunctionMethodRegex().Matches(scriptContent))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!knownNames.Add(name))
            {
                continue;
            }

            var body = ConvertMethodBody(match.Groups["body"].Value, stateNames, ref requiresHttp, ref requiresJson, ref requiresRouter);
            var isAsync = !string.IsNullOrWhiteSpace(match.Groups["async"].Value) || body.Contains("await ", StringComparison.Ordinal);
            methods.Add(new DartMethod(name, ConvertParameters(match.Groups["params"].Value), body, isAsync, isAsync ? "Future<void>" : "void"));
        }

        return methods.ToImmutable();
    }

    private static string ConvertParameters (string tsParameters)
    {
        if (string.IsNullOrWhiteSpace(tsParameters))
        {
            return string.Empty;
        }

        var parameters = tsParameters
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parameter =>
            {
                var token = parameter.Split('=', 2, StringSplitOptions.TrimEntries)[0].Trim();
                var separator = token.IndexOf(':');
                if (separator < 0)
                {
                    return $"dynamic {token}";
                }

                var name = token[..separator].Trim();
                var type = token[(separator + 1)..].Trim();
                return $"{ResolveDartType(type, string.Empty)} {name}";
            });

        return string.Join(", ", parameters);
    }

    private static string ConvertMethodBody (string tsBody, HashSet<string> stateNames, ref bool requiresHttp, ref bool requiresJson, ref bool requiresRouter)
    {
        var body = tsBody.Trim();
        if (body.StartsWith('{') && body.EndsWith('}'))
        {
            body = body[1..^1];
        }

        if (!body.Contains('\n') && !body.Contains(';') && !body.Contains('{'))
        {
            return ConvertStatement(body + ";", stateNames, ref requiresHttp, ref requiresJson, ref requiresRouter);
        }

        var lines = MergeLines(body);
        var output = new List<string>(lines.Count);
        foreach (var statement in lines)
        {
            var converted = ConvertStatement(statement, stateNames, ref requiresHttp, ref requiresJson, ref requiresRouter);
            if (converted.Length > 0)
            {
                output.Add(converted);
            }
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string ConvertStatement (string statement, HashSet<string> stateNames, ref bool requiresHttp, ref bool requiresJson, ref bool requiresRouter)
    {
        var line = statement.Trim();
        if (line.Length == 0)
        {
            return string.Empty;
        }

        if (line is "{" or "}" || line.EndsWith("{", StringComparison.Ordinal) || line.StartsWith("}", StringComparison.Ordinal))
        {
            return NormalizeOperators(ReplaceRefAccess(line, stateNames));
        }

        line = NormalizeOperators(ReplaceRefAccess(line.TrimEnd(';'), stateNames));

        if (TryConvertConsole(line, stateNames, ref requiresJson, out var consoleLine))
        {
            return consoleLine;
        }

        if (TryConvertFetch(line, stateNames, ref requiresHttp, ref requiresJson, out var fetchLine))
        {
            return fetchLine;
        }

        if (TryConvertResponseBody(line, ref requiresJson, out var responseLine))
        {
            return responseLine;
        }

        if (TryConvertRouterNavigation(line, stateNames, ref requiresJson, ref requiresRouter, out var routerLine))
        {
            return routerLine;
        }

        if (TryConvertVariableDeclaration(line, stateNames, ref requiresJson, out var declarationLine))
        {
            return declarationLine;
        }

        var expression = ConvertExpression(line, stateNames, ref requiresJson);
        if (IsStateMutation(expression, stateNames))
        {
            return $"setState(() {{ {expression}; }});";
        }

        return IsControlFlow(expression) ? expression : $"{expression};";
    }
    private static bool TryConvertConsole (string line, HashSet<string> stateNames, ref bool requiresJson, out string converted)
    {
        var match = ConsoleRegex().Match(line);
        if (!match.Success)
        {
            converted = string.Empty;
            return false;
        }

        var args = new List<string>();
        foreach (var arg in SplitTopLevel(match.Groups["args"].Value, ','))
        {
            var convertedArg = ConvertExpression(arg, stateNames, ref requiresJson);
            if (convertedArg.Length > 0)
            {
                args.Add(convertedArg);
            }
        }

        if (args.Count == 0)
        {
            converted = "debugPrint('');";
            return true;
        }

        if (args.Count == 1)
        {
            converted = $"debugPrint(({args[0]}).toString());";
            return true;
        }

        var joined = string.Join(" ", args.Select(arg => $"${{{arg}}}"));
        converted = $"debugPrint('{joined}');";
        return true;
    }

    private static bool TryConvertFetch (string line, HashSet<string> stateNames, ref bool requiresHttp, ref bool requiresJson, out string converted)
    {
        var match = FetchRegex().Match(line);
        if (!match.Success)
        {
            converted = string.Empty;
            return false;
        }

        var arguments = SplitTopLevel(match.Groups["args"].Value, ',');
        if (arguments.Count == 0)
        {
            converted = string.Empty;
            return false;
        }

        var url = ConvertExpression(arguments[0], stateNames, ref requiresJson);
        var method = "get";
        var headers = string.Empty;
        var body = string.Empty;

        if (arguments.Count > 1)
        {
            ParseFetchOptions(arguments[1], stateNames, ref requiresJson, out method, out headers, out body);
        }

        var awaitPrefix = string.IsNullOrWhiteSpace(match.Groups["await"].Value) ? string.Empty : "await ";
        var call = new StringBuilder();
        call.Append(awaitPrefix).Append("http.").Append(method).Append("(Uri.parse(").Append(url).Append(')');
        if (!string.IsNullOrWhiteSpace(headers))
        {
            call.Append(", headers: ").Append(headers);
        }

        if (!string.IsNullOrWhiteSpace(body) && method is not "get" and not "head")
        {
            call.Append(", body: ").Append(body);
        }

        call.Append(')');
        requiresHttp = true;

        var keyword = match.Groups["keyword"].Value;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            converted = call + ";";
            return true;
        }

        converted = BuildDeclaration(keyword, match.Groups["name"].Value, match.Groups["type"].Value, call.ToString()) + ";";
        return true;
    }

    private static void ParseFetchOptions (string optionLiteral, HashSet<string> stateNames, ref bool requiresJson, out string method, out string headers, out string body)
    {
        method = "get";
        headers = string.Empty;
        body = string.Empty;

        var methodMatch = Regex.Match(optionLiteral, "method\\s*:\\s*['\"]?(?<m>[A-Za-z]+)['\"]?", RegexOptions.IgnoreCase);
        if (methodMatch.Success)
        {
            method = methodMatch.Groups["m"].Value.ToLowerInvariant() switch
            {
                "post" => "post",
                "put" => "put",
                "patch" => "patch",
                "delete" => "delete",
                "head" => "head",
                _ => "get"
            };
        }

        var headersMatch = Regex.Match(optionLiteral, "headers\\s*:\\s*(?<h>\\{[\\s\\S]*?\\})(?:,\\s*\\w+\\s*:|$)", RegexOptions.IgnoreCase);
        if (headersMatch.Success)
        {
            headers = ConvertObjectLiteral(headersMatch.Groups["h"].Value, stateNames, ref requiresJson);
        }

        var bodyMatch = Regex.Match(optionLiteral, "body\\s*:\\s*(?<b>[\\s\\S]+?)(?:,\\s*\\w+\\s*:|$)", RegexOptions.IgnoreCase);
        if (bodyMatch.Success)
        {
            body = ConvertExpression(bodyMatch.Groups["b"].Value.Trim(), stateNames, ref requiresJson);
        }
    }

    private static string ConvertObjectLiteral (string literal, HashSet<string> stateNames, ref bool requiresJson)
    {
        var value = literal.Trim();
        if (!value.StartsWith('{') || !value.EndsWith('}'))
        {
            return ConvertExpression(value, stateNames, ref requiresJson);
        }

        var content = value[1..^1];
        var pairs = new List<string>();
        foreach (var pair in SplitTopLevel(content, ','))
        {
            var separator = pair.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = pair[..separator].Trim().Trim('"', '\'', '`');
            var raw = pair[(separator + 1)..].Trim();
            pairs.Add($"'{key}': {ConvertExpression(raw, stateNames, ref requiresJson)}");
        }

        return $"<String, dynamic>{{{string.Join(", ", pairs)}}}";
    }

    private static bool TryConvertResponseBody (string line, ref bool requiresJson, out string converted)
    {
        var jsonMatch = JsonResponseRegex().Match(line);
        if (jsonMatch.Success)
        {
            requiresJson = true;
            var expression = $"jsonDecode({jsonMatch.Groups["response"].Value}.body)";
            var keyword = jsonMatch.Groups["keyword"].Value;
            converted = string.IsNullOrWhiteSpace(keyword)
                ? expression + ";"
                : BuildDeclaration(keyword, jsonMatch.Groups["name"].Value, jsonMatch.Groups["type"].Value, expression) + ";";
            return true;
        }

        var textMatch = TextResponseRegex().Match(line);
        if (textMatch.Success)
        {
            var expression = $"{textMatch.Groups["response"].Value}.body";
            var keyword = textMatch.Groups["keyword"].Value;
            converted = string.IsNullOrWhiteSpace(keyword)
                ? expression + ";"
                : BuildDeclaration(keyword, textMatch.Groups["name"].Value, textMatch.Groups["type"].Value, expression) + ";";
            return true;
        }

        converted = string.Empty;
        return false;
    }

    private static bool TryConvertRouterNavigation (string line, HashSet<string> stateNames, ref bool requiresJson, ref bool requiresRouter, out string converted)
    {
        var backMatch = RouterBackRegex().Match(line);
        if (backMatch.Success)
        {
            var awaitPrefix = string.IsNullOrWhiteSpace(backMatch.Groups["await"].Value) ? string.Empty : "await ";
            converted = $"{awaitPrefix}Navigator.of(context).pop();";
            return true;
        }

        var navigateMatch = RouterNavigateRegex().Match(line);
        if (!navigateMatch.Success)
        {
            converted = string.Empty;
            return false;
        }

        var action = navigateMatch.Groups["action"].Value;
        var navigatorMethod = action.Equals("replace", StringComparison.Ordinal)
            ? "pushReplacementNamed"
            : "pushNamed";
        var navigateAwaitPrefix = string.IsNullOrWhiteSpace(navigateMatch.Groups["await"].Value) ? string.Empty : "await ";
        var target = navigateMatch.Groups["target"].Value.Trim();
        var routeExpression = ResolveRouterTargetExpression(target, stateNames, ref requiresJson, ref requiresRouter);

        converted = $"{navigateAwaitPrefix}Navigator.of(context).{navigatorMethod}({routeExpression});";
        return true;
    }

    private static string ResolveRouterTargetExpression (string target, HashSet<string> stateNames, ref bool requiresJson, ref bool requiresRouter)
    {
        if (target.StartsWith('{') && target.EndsWith('}'))
        {
            var pathMatch = Regex.Match(target, "path\\s*:\\s*['\"](?<path>[^'\"]+)['\"]", RegexOptions.IgnoreCase);
            if (pathMatch.Success)
            {
                return $"'{pathMatch.Groups["path"].Value}'";
            }

            var nameMatch = Regex.Match(target, "name\\s*:\\s*['\"](?<name>[^'\"]+)['\"]", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                requiresRouter = true;
                return $"FlueAppRouter.pathByName('{nameMatch.Groups["name"].Value}') ?? '/'";
            }
        }

        if (target.StartsWith("/", StringComparison.Ordinal))
        {
            return $"'{target}'";
        }

        return ConvertExpression(target, stateNames, ref requiresJson);
    }

    private static bool TryConvertVariableDeclaration (string line, HashSet<string> stateNames, ref bool requiresJson, out string converted)
    {
        var match = VariableRegex().Match(line);
        if (!match.Success || line.Contains("=>", StringComparison.Ordinal))
        {
            converted = string.Empty;
            return false;
        }

        var keyword = match.Groups["keyword"].Value;
        var name = match.Groups["name"].Value;
        var type = match.Groups["type"].Value.Trim();
        var rawValue = match.Groups["value"].Value.Trim();
        if (rawValue.Contains("useRouter(", StringComparison.Ordinal))
        {
            converted = string.Empty;
            return true;
        }

        var dartType = ResolveDartType(type, rawValue);
        var initializer = rawValue.Length == 0
            ? DefaultValueForType(dartType)
            : ConvertExpression(rawValue, stateNames, ref requiresJson);
        converted = BuildDeclaration(keyword, name, type, initializer) + ";";
        return true;
    }

    private static string BuildDeclaration (string keyword, string name, string type, string initializer)
    {
        if (keyword.Equals("const", StringComparison.OrdinalIgnoreCase))
        {
            return $"final {name} = {initializer}";
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            return $"var {name} = {initializer}";
        }

        return $"{ResolveDartType(type, initializer)} {name} = {initializer}";
    }

    private static string ConvertExpression (string expression, HashSet<string> stateNames, ref bool requiresJson)
    {
        var value = NormalizeOperators(ReplaceRefAccess(ConvertTemplate(expression.Trim()), stateNames));

        if (value.Contains("JSON.stringify", StringComparison.Ordinal))
        {
            requiresJson = true;
            value = value.Replace("JSON.stringify", "jsonEncode", StringComparison.Ordinal);
        }

        if (value.Contains("JSON.parse", StringComparison.Ordinal))
        {
            requiresJson = true;
            value = value.Replace("JSON.parse", "jsonDecode", StringComparison.Ordinal);
        }

        if (Regex.IsMatch(value, "(?:await\\s+)?[A-Za-z_]\\w*\\.json\\(\\)"))
        {
            requiresJson = true;
            value = Regex.Replace(value, "(?:await\\s+)?(?<r>[A-Za-z_]\\w*)\\.json\\(\\)", "jsonDecode(${r}.body)");
        }

        if (Regex.IsMatch(value, "(?:await\\s+)?[A-Za-z_]\\w*\\.text\\(\\)"))
        {
            value = Regex.Replace(value, "(?:await\\s+)?(?<r>[A-Za-z_]\\w*)\\.text\\(\\)", "${r}.body");
        }

        return value;
    }

    private static string ConvertTemplate (string expression)
    {
        return TemplateRegex().Replace(expression, match =>
        {
            var content = match.Groups["content"].Value
                .Replace("\\`", "`", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            return $"'{content}'";
        });
    }
    private static string ReplaceRefAccess (string expression, HashSet<string> stateNames)
    {
        var value = expression;
        foreach (var name in stateNames)
        {
            value = value.Replace($"{name}.value", name, StringComparison.Ordinal);
        }

        return value.Replace("this.", string.Empty, StringComparison.Ordinal);
    }

    private static string NormalizeOperators (string line)
    {
        return line
            .Replace("===", "==", StringComparison.Ordinal)
            .Replace("!==", "!=", StringComparison.Ordinal);
    }

    private static List<string> MergeLines (string body)
    {
        var rows = body
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var merged = new List<string>(rows.Length);
        var pending = new StringBuilder();
        foreach (var row in rows)
        {
            if (row.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (pending.Length > 0)
            {
                pending.Append(' ');
            }

            pending.Append(row);
            var candidate = pending.ToString().Trim();
            if (!(candidate.EndsWith(';') || candidate.EndsWith('{') || candidate == "{" || candidate == "}" || candidate.StartsWith("}")))
            {
                continue;
            }

            merged.Add(candidate);
            pending.Clear();
        }

        if (pending.Length > 0)
        {
            merged.Add(pending.ToString().Trim());
        }

        return merged;
    }

    private static List<string> SplitTopLevel (string text, char separator)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        var braces = 0;
        var brackets = 0;
        var parens = 0;
        var inString = false;
        var escaped = false;
        var quote = '\0';

        foreach (var character in text)
        {
            if (inString)
            {
                token.Append(character);
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (character is '\'' or '"' or '`')
            {
                inString = true;
                quote = character;
                token.Append(character);
                continue;
            }

            switch (character)
            {
                case '{': braces++; break;
                case '}': braces--; break;
                case '[': brackets++; break;
                case ']': brackets--; break;
                case '(': parens++; break;
                case ')': parens--; break;
            }

            if (character == separator && braces == 0 && brackets == 0 && parens == 0)
            {
                var piece = token.ToString().Trim();
                if (piece.Length > 0)
                {
                    result.Add(piece);
                }

                token.Clear();
                continue;
            }

            token.Append(character);
        }

        var tail = token.ToString().Trim();
        if (tail.Length > 0)
        {
            result.Add(tail);
        }

        return result;
    }

    private static bool IsStateMutation (string line, HashSet<string> stateNames)
    {
        if (line.StartsWith("setState", StringComparison.Ordinal) ||
            line.StartsWith("return ", StringComparison.Ordinal) ||
            line.StartsWith("final ", StringComparison.Ordinal) ||
            line.StartsWith("var ", StringComparison.Ordinal) ||
            line.StartsWith("if ", StringComparison.Ordinal) ||
            line.StartsWith("for ", StringComparison.Ordinal) ||
            line.StartsWith("while ", StringComparison.Ordinal))
        {
            return false;
        }

        var assign = AssignmentRegex().Match(line);
        if (assign.Success && stateNames.Contains(assign.Groups["name"].Value))
        {
            return true;
        }

        var inc = IncrementRegex().Match(line);
        return inc.Success && stateNames.Contains(inc.Groups["name"].Value);
    }

    private static bool IsControlFlow (string line)
    {
        return line is "{" or "}" ||
               line.EndsWith("{", StringComparison.Ordinal) ||
               line.StartsWith("}", StringComparison.Ordinal) ||
               line.StartsWith("else", StringComparison.Ordinal) ||
               line.StartsWith("case ", StringComparison.Ordinal) ||
               line.StartsWith("default", StringComparison.Ordinal);
    }

    private static string ResolveDartType (string tsType, string rawValue)
    {
        if (!string.IsNullOrWhiteSpace(tsType))
        {
            var normalized = tsType.Trim();
            if (TypeMap.TryGetValue(normalized, out var mapped))
            {
                return mapped;
            }

            if (normalized.EndsWith("[]", StringComparison.Ordinal))
            {
                return $"List<{ResolveDartType(normalized[..^2], string.Empty)}>";
            }

            if (normalized.StartsWith("Array<", StringComparison.Ordinal) && normalized.EndsWith('>'))
            {
                return $"List<{ResolveDartType(normalized[6..^1].Trim(), string.Empty)}>";
            }

            if (normalized.StartsWith("Promise<", StringComparison.Ordinal) && normalized.EndsWith('>'))
            {
                return $"Future<{ResolveDartType(normalized[8..^1].Trim(), string.Empty)}>";
            }

            return normalized;
        }

        var value = rawValue.Trim();
        if (bool.TryParse(value, out _))
        {
            return "bool";
        }

        if (int.TryParse(value, out _))
        {
            return "int";
        }

        if (double.TryParse(value, out _))
        {
            return "double";
        }

        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')) || (value.StartsWith('`') && value.EndsWith('`')))
        {
            return "String";
        }

        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            return "List<dynamic>";
        }

        if (value.StartsWith('{') && value.EndsWith('}'))
        {
            return "Map<String, dynamic>";
        }

        return "dynamic";
    }

    private static string ResolveInitializer (string rawValue, string dartType)
    {
        var value = rawValue.Trim();
        if (value.Length == 0)
        {
            return DefaultValueForType(dartType);
        }

        if (value.Equals("undefined", StringComparison.OrdinalIgnoreCase) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            return $"'{value[1..^1].Replace("'", "\\'", StringComparison.Ordinal)}'";
        }

        if (value.StartsWith('`') && value.EndsWith('`'))
        {
            return ConvertTemplate(value);
        }

        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            return value.Replace("'", "\"", StringComparison.Ordinal);
        }

        if (value.StartsWith('{') && value.EndsWith('}'))
        {
            return "<String, dynamic>{}";
        }

        return value.Replace(".value", string.Empty, StringComparison.Ordinal);
    }

    private static string DefaultValueForType (string dartType)
    {
        return dartType switch
        {
            "int" => "0",
            "double" => "0.0",
            "bool" => "false",
            "String" => "''",
            "List<dynamic>" => "[]",
            "Map<String, dynamic>" => "<String, dynamic>{}",
            _ => "null"
        };
    }
}

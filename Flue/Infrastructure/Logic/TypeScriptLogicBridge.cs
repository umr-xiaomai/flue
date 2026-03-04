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
    private static partial Regex RefRegex();

    [GeneratedRegex("const\\s+(?<name>[A-Za-z_]\\w*)\\s*=\\s*\\((?<params>[^)]*)\\)\\s*=>\\s*(?<body>\\{[\\s\\S]*?\\}|[^;]+);", RegexOptions.Compiled)]
    private static partial Regex ArrowFunctionRegex();

    [GeneratedRegex("function\\s+(?<name>[A-Za-z_]\\w*)\\s*\\((?<params>[^)]*)\\)\\s*\\{(?<body>[\\s\\S]*?)\\}", RegexOptions.Compiled)]
    private static partial Regex FunctionRegex();

    [GeneratedRegex("^(?<name>[A-Za-z_]\\w*)\\s*(=|\\+=|-=|\\*=|/=).+$", RegexOptions.Compiled)]
    private static partial Regex AssignmentRegex();

    [GeneratedRegex("^(?<name>[A-Za-z_]\\w*)(\\+\\+|--)$", RegexOptions.Compiled)]
    private static partial Regex IncrementRegex();

    [GeneratedRegex("\\bconsole\\.log\\b", RegexOptions.Compiled)]
    private static partial Regex ConsoleLogRegex();

    [GeneratedRegex("^(const|let)\\s+", RegexOptions.Compiled)]
    private static partial Regex LetConstRegex();

    public LogicBridgeResult Parse(string scriptContent)
    {
        if (string.IsNullOrWhiteSpace(scriptContent))
        {
            return new LogicBridgeResult(ImmutableArray<StateField>.Empty, ImmutableArray<DartMethod>.Empty);
        }

        var stateFields = ParseStateFields(scriptContent);
        var refNames = stateFields
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        var methods = ParseMethods(scriptContent, refNames);

        return new LogicBridgeResult(stateFields, methods);
    }

    private static ImmutableArray<StateField> ParseStateFields(string scriptContent)
    {
        var builder = ImmutableArray.CreateBuilder<StateField>();
        var knownNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in RefRegex().Matches(scriptContent))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!knownNames.Add(name))
            {
                continue;
            }

            var tsType = match.Groups["type"].Value.Trim();
            var rawValue = match.Groups["value"].Value.Trim();
            var dartType = ResolveDartType(tsType, rawValue);
            var initializer = ResolveInitializer(rawValue, dartType);
            builder.Add(new StateField(name, dartType, initializer));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<DartMethod> ParseMethods(string scriptContent, HashSet<string> refNames)
    {
        var builder = ImmutableArray.CreateBuilder<DartMethod>();
        var knownNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ArrowFunctionRegex().Matches(scriptContent))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!knownNames.Add(name))
            {
                continue;
            }

            var parameters = ConvertParameters(match.Groups["params"].Value);
            var body = ConvertMethodBody(match.Groups["body"].Value, refNames);
            builder.Add(new DartMethod(name, parameters, body));
        }

        foreach (Match match in FunctionRegex().Matches(scriptContent))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!knownNames.Add(name))
            {
                continue;
            }

            var parameters = ConvertParameters(match.Groups["params"].Value);
            var body = ConvertMethodBody(match.Groups["body"].Value, refNames);
            builder.Add(new DartMethod(name, parameters, body));
        }

        return builder.ToImmutable();
    }

    private static string ConvertParameters(string tsParameters)
    {
        if (string.IsNullOrWhiteSpace(tsParameters))
        {
            return string.Empty;
        }

        var parameters = tsParameters
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ConvertParameter)
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter));

        return string.Join(", ", parameters);
    }

    private static string ConvertParameter(string tsParameter)
    {
        var token = tsParameter.Trim();
        if (token.Length == 0)
        {
            return string.Empty;
        }

        var definition = token.Split('=', 2, StringSplitOptions.TrimEntries)[0];
        var separatorIndex = definition.IndexOf(':');
        if (separatorIndex < 0)
        {
            return $"dynamic {definition}";
        }

        var name = definition[..separatorIndex].Trim();
        var tsType = definition[(separatorIndex + 1)..].Trim();
        var dartType = ResolveDartType(tsType, string.Empty);
        return $"{dartType} {name}";
    }

    private static string ConvertMethodBody(string tsBody, HashSet<string> refNames)
    {
        var body = tsBody.Trim();
        if (body.StartsWith('{') && body.EndsWith('}'))
        {
            body = body[1..^1];
        }

        var builder = new StringBuilder();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            line = line.TrimEnd(';');
            line = line
                .Replace("===", "==", StringComparison.Ordinal)
                .Replace("!==", "!=", StringComparison.Ordinal);
            line = ConsoleLogRegex().Replace(line, "debugPrint");
            line = LetConstRegex().Replace(line, "final ");

            foreach (var refName in refNames)
            {
                line = line.Replace($"{refName}.value", refName, StringComparison.Ordinal);
            }

            if (IsControlFlowLine(line))
            {
                builder.AppendLine(line);
                continue;
            }

            if (IsRefMutation(line, refNames))
            {
                builder.AppendLine($"setState(() {{ {line}; }});");
                continue;
            }

            builder.AppendLine($"{line};");
        }

        return builder.ToString().TrimEnd();
    }

    private static bool IsControlFlowLine(string line)
    {
        if (line is "{" or "}")
        {
            return true;
        }

        return line.EndsWith("{", StringComparison.Ordinal) ||
               line.StartsWith("else", StringComparison.Ordinal);
    }

    private static bool IsRefMutation(string line, HashSet<string> refNames)
    {
        var assignmentMatch = AssignmentRegex().Match(line);
        if (assignmentMatch.Success && refNames.Contains(assignmentMatch.Groups["name"].Value))
        {
            return true;
        }

        var incrementMatch = IncrementRegex().Match(line);
        return incrementMatch.Success && refNames.Contains(incrementMatch.Groups["name"].Value);
    }

    private static string ResolveDartType(string tsType, string rawValue)
    {
        if (!string.IsNullOrWhiteSpace(tsType))
        {
            var normalized = tsType.Trim();
            if (TypeMap.TryGetValue(normalized, out var mappedType))
            {
                return mappedType;
            }

            if (normalized.EndsWith("[]", StringComparison.Ordinal))
            {
                var itemType = ResolveDartType(normalized[..^2], string.Empty);
                return $"List<{itemType}>";
            }

            if (normalized.StartsWith("Array<", StringComparison.Ordinal) && normalized.EndsWith('>'))
            {
                var itemType = normalized[6..^1].Trim();
                return $"List<{ResolveDartType(itemType, string.Empty)}>";
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

        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
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

    private static string ResolveInitializer(string rawValue, string dartType)
    {
        var value = rawValue.Trim();
        if (value.Length == 0)
        {
            return DefaultValueForType(dartType);
        }

        if (value.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            var stringValue = value[1..^1].Replace("'", "\\'", StringComparison.Ordinal);
            return $"'{stringValue}'";
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

    private static string DefaultValueForType(string dartType)
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

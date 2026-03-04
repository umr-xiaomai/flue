using Flue.Core.Abstractions;
using Flue.Core.Models;

namespace Flue.Infrastructure.Generation;

public sealed partial class DartWidgetRenderer (ITailwindConverter tailwindConverter)
{
    private static readonly ImmutableArray<string> ClickAttributeKeys =
    [
        "@click",
        "v-on:click",
        "onclick",
        "onClick",
        "onTap",
        "@tap",
        "v-on:tap"
    ];

    private static readonly ImmutableArray<string> RouterTargetKeys =
    [
        "to",
        ":to",
        "v-bind:to"
    ];

    public string RenderRoot (TemplateNode root, int indentLevel)
    {
        if (root.Children.Length == 0)
        {
            return $"{Indent(indentLevel)}const SizedBox.shrink()";
        }

        if (root.Children.Length == 1)
        {
            return RenderNode(root.Children[0], indentLevel);
        }

        var syntheticColumn = new TemplateNode(
            WidgetKind.Column,
            "div",
            null,
            ImmutableArray<string>.Empty,
            root.Attributes,
            root.Children);

        return RenderNode(syntheticColumn, indentLevel);
    }

    private string RenderNode (TemplateNode node, int indentLevel)
    {
        var style = tailwindConverter.Convert(node.Classes);
        var rendered = node.TagName.Equals("router-link", StringComparison.OrdinalIgnoreCase)
            ? RenderRouterLink(node, style, indentLevel)
            : node.Kind switch
            {
                WidgetKind.Row => RenderFlexWidget("Row", node, style, indentLevel),
                WidgetKind.Column => RenderFlexWidget("Column", node, style, indentLevel),
                WidgetKind.Text => RenderText(node, style, indentLevel),
                WidgetKind.Button => RenderButton(node, style, indentLevel),
                _ => RenderContainer(node, style, indentLevel)
            };

        if (node.Kind is WidgetKind.Button || node.TagName.Equals("router-link", StringComparison.OrdinalIgnoreCase))
        {
            return rendered;
        }

        if (!TryGetClickHandler(node.Attributes, out var handler))
        {
            return rendered;
        }

        return WrapWithTapCallback(rendered, handler, indentLevel);
    }

    private string RenderContainer (TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}Container(");
        foreach (var property in style.WidgetProperties)
        {
            sb.AppendLine($"{inner}{property},");
        }

        if (style.DecorationProperties.Length > 0)
        {
            sb.AppendLine($"{inner}decoration: BoxDecoration(");
            foreach (var property in style.DecorationProperties)
            {
                sb.AppendLine($"{Indent(indentLevel + 2)}{property},");
            }

            sb.AppendLine($"{inner}),");
        }

        AppendContainerChild(sb, node.Children, indentLevel + 1);
        sb.Append($"{indent})");

        return sb.ToString();
    }

    private string RenderFlexWidget (string widgetName, TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}{widgetName}(");
        if (!string.IsNullOrWhiteSpace(style.MainAxisAlignment))
        {
            sb.AppendLine($"{inner}mainAxisAlignment: {style.MainAxisAlignment},");
        }

        if (!string.IsNullOrWhiteSpace(style.CrossAxisAlignment))
        {
            sb.AppendLine($"{inner}crossAxisAlignment: {style.CrossAxisAlignment},");
        }

        sb.AppendLine($"{inner}children: [");
        if (node.Children.Length == 0)
        {
            sb.AppendLine($"{Indent(indentLevel + 2)}const SizedBox.shrink(),");
        }
        else
        {
            foreach (var child in node.Children)
            {
                AppendWidgetWithComma(sb, RenderNode(child, indentLevel + 2));
            }
        }

        sb.AppendLine($"{inner}],");
        sb.Append($"{indent})");

        if (style.WidgetProperties.Length == 0 && style.DecorationProperties.Length == 0)
        {
            return sb.ToString();
        }

        return WrapWithContainer(sb.ToString(), style, indentLevel);
    }

    private string RenderButton (TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);
        var callback = TryGetClickHandler(node.Attributes, out var handler)
            ? BuildTapCallback(handler)
            : "null";

        sb.AppendLine($"{indent}ElevatedButton(");
        sb.AppendLine($"{inner}onPressed: {callback},");
        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, RenderButtonChild(node, indentLevel + 1));
        sb.Append($"{indent})");

        if (style.WidgetProperties.Length == 0 && style.DecorationProperties.Length == 0)
        {
            return sb.ToString();
        }

        return WrapWithContainer(sb.ToString(), style, indentLevel);
    }

    private string RenderRouterLink (TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);
        var routeExpression = ResolveRouterTargetExpression(node.Attributes);

        sb.AppendLine($"{indent}TextButton(");
        sb.AppendLine($"{inner}onPressed: () {{ Navigator.of(context).pushNamed({routeExpression}); }},");
        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, RenderButtonChild(node, indentLevel + 1));
        sb.Append($"{indent})");

        if (style.WidgetProperties.Length == 0 && style.DecorationProperties.Length == 0)
        {
            return sb.ToString();
        }

        return WrapWithContainer(sb.ToString(), style, indentLevel);
    }

    private string RenderButtonChild (TemplateNode node, int indentLevel)
    {
        if (node.Children.Length == 0)
        {
            return $"{Indent(indentLevel)}const Text('Button')";
        }

        if (node.Children.Length == 1)
        {
            return RenderNode(node.Children[0], indentLevel);
        }

        var syntheticRow = new TemplateNode(
            WidgetKind.Row,
            "div",
            null,
            ImmutableArray<string>.Empty,
            node.Attributes,
            node.Children);

        return RenderNode(syntheticRow, indentLevel);
    }

    private void AppendContainerChild (StringBuilder sb, ImmutableArray<TemplateNode> children, int indentLevel)
    {
        var inner = Indent(indentLevel);
        if (children.Length == 0)
        {
            sb.AppendLine($"{inner}child: const SizedBox.shrink(),");
            return;
        }

        if (children.Length == 1)
        {
            sb.AppendLine($"{inner}child:");
            AppendWidgetWithComma(sb, RenderNode(children[0], indentLevel + 1));
            return;
        }

        var columnNode = new TemplateNode(
            WidgetKind.Column,
            "div",
            null,
            ImmutableArray<string>.Empty,
            children[0].Attributes,
            children);

        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, RenderNode(columnNode, indentLevel + 1));
    }

    private string RenderText (TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var content = node.TextContent ?? string.Empty;
        var (expression, isConst) = BuildTextExpression(content);
        var semanticStyle = BuildSemanticTextStyle(node.TagName);
        var textStyles = MergeTextStyles(semanticStyle, style.TextStyleProperties);

        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);
        var constPrefix = isConst && textStyles.Length == 0 ? "const " : string.Empty;

        sb.AppendLine($"{indent}{constPrefix}Text(");
        sb.AppendLine($"{inner}{expression},");
        if (textStyles.Length > 0)
        {
            sb.AppendLine($"{inner}style: const TextStyle(");
            foreach (var property in textStyles)
            {
                sb.AppendLine($"{Indent(indentLevel + 2)}{property},");
            }

            sb.AppendLine($"{inner}),");
        }

        sb.Append($"{indent})");
        return sb.ToString();
    }

    private static ImmutableArray<string> BuildSemanticTextStyle (string tagName)
    {
        return tagName.ToLowerInvariant() switch
        {
            "h1" => ["fontSize: 32.0", "fontWeight: FontWeight.w700"],
            "h2" => ["fontSize: 28.0", "fontWeight: FontWeight.w700"],
            "h3" => ["fontSize: 24.0", "fontWeight: FontWeight.w600"],
            "h4" => ["fontSize: 20.0", "fontWeight: FontWeight.w600"],
            "h5" => ["fontSize: 18.0", "fontWeight: FontWeight.w600"],
            "h6" => ["fontSize: 16.0", "fontWeight: FontWeight.w600"],
            "strong" or "b" => ["fontWeight: FontWeight.w700"],
            _ => ImmutableArray<string>.Empty
        };
    }

    private static ImmutableArray<string> MergeTextStyles (ImmutableArray<string> semanticStyles, ImmutableArray<string> tailwindStyles)
    {
        if (semanticStyles.Length == 0)
        {
            return tailwindStyles;
        }

        if (tailwindStyles.Length == 0)
        {
            return semanticStyles;
        }

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in semanticStyles)
        {
            merged[StyleKey(style)] = style;
        }

        foreach (var style in tailwindStyles)
        {
            merged[StyleKey(style)] = style;
        }

        return [.. merged.Values];
    }

    private static string StyleKey (string style)
    {
        var separator = style.IndexOf(':');
        return separator < 0 ? style.Trim() : style[..separator].Trim();
    }

    private string WrapWithContainer (string widget, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}Container(");
        foreach (var property in style.WidgetProperties)
        {
            sb.AppendLine($"{inner}{property},");
        }

        if (style.DecorationProperties.Length > 0)
        {
            sb.AppendLine($"{inner}decoration: BoxDecoration(");
            foreach (var property in style.DecorationProperties)
            {
                sb.AppendLine($"{Indent(indentLevel + 2)}{property},");
            }

            sb.AppendLine($"{inner}),");
        }

        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, widget);
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string WrapWithTapCallback (string widget, string handler, int indentLevel)
    {
        var callback = BuildTapCallback(handler);
        if (callback is "null")
        {
            return widget;
        }

        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}GestureDetector(");
        sb.AppendLine($"{inner}onTap: {callback},");
        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, widget);
        sb.Append($"{indent})");

        return sb.ToString();
    }

    private static string ResolveRouterTargetExpression (FrozenDictionary<string, string> attributes)
    {
        if (!TryGetRouterTarget(attributes, out var target))
        {
            return "'/'";
        }

        return ResolveRouteExpression(target);
    }

    private static bool TryGetClickHandler (FrozenDictionary<string, string> attributes, out string handler)
    {
        foreach (var key in ClickAttributeKeys)
        {
            if (attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                handler = value;
                return true;
            }
        }

        handler = string.Empty;
        return false;
    }

    private static bool TryGetRouterTarget (FrozenDictionary<string, string> attributes, out string target)
    {
        foreach (var key in RouterTargetKeys)
        {
            if (attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                target = value;
                return true;
            }
        }

        target = string.Empty;
        return false;
    }

    private static string BuildTapCallback (string rawHandler)
    {
        var handler = rawHandler
            .Trim()
            .Replace(".value", string.Empty, StringComparison.Ordinal)
            .TrimEnd(';');

        if (handler.Length == 0)
        {
            return "null";
        }

        if (handler.StartsWith("() =>", StringComparison.Ordinal))
        {
            handler = handler[5..].Trim();
        }

        if (InlineRouterBackRegex().IsMatch(handler))
        {
            return "() { Navigator.of(context).pop(); }";
        }

        var navigateMatch = InlineRouterNavigateRegex().Match(handler);
        if (navigateMatch.Success)
        {
            var action = navigateMatch.Groups["action"].Value;
            var navigatorMethod = action.Equals("replace", StringComparison.Ordinal)
                ? "pushReplacementNamed"
                : "pushNamed";
            var routeExpression = ResolveRouteExpression(navigateMatch.Groups["target"].Value.Trim());
            return $"() {{ Navigator.of(context).{navigatorMethod}({routeExpression}); }}";
        }

        if (IdentifierRegex().IsMatch(handler))
        {
            return handler;
        }

        if (MutationRegex().IsMatch(handler))
        {
            return $"() {{ setState(() {{ {handler}; }}); }}";
        }

        if (handler.Contains("await ", StringComparison.Ordinal))
        {
            return $"() async {{ {handler}; }}";
        }

        return $"() {{ {handler}; }}";
    }

    private static string ResolveRouteExpression (string rawTarget)
    {
        var target = rawTarget.Trim();
        if (target.StartsWith('{') && target.EndsWith('}'))
        {
            var pathMatch = RouterObjectPathRegex().Match(target);
            if (pathMatch.Success)
            {
                return $"'{EscapeDartString(pathMatch.Groups["path"].Value)}'";
            }

            var nameMatch = RouterObjectNameRegex().Match(target);
            if (nameMatch.Success)
            {
                return $"FlueAppRouter.pathByName('{EscapeDartString(nameMatch.Groups["name"].Value)}') ?? '/'";
            }
        }

        if ((target.StartsWith('"') && target.EndsWith('"')) || (target.StartsWith('\'') && target.EndsWith('\'')))
        {
            return $"'{EscapeDartString(target[1..^1])}'";
        }

        if (target.StartsWith("/", StringComparison.Ordinal))
        {
            return $"'{EscapeDartString(target)}'";
        }

        return target.Replace(".value", string.Empty, StringComparison.Ordinal);
    }

    private static (string Expression, bool IsConst) BuildTextExpression (string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return ("''", true);
        }

        var matches = InterpolationRegex().Matches(trimmed);
        if (matches.Count == 0)
        {
            return ($"'{EscapeDartString(trimmed)}'", true);
        }

        var sb = new StringBuilder("'");
        var index = 0;
        foreach (Match match in matches)
        {
            var staticText = trimmed[index..match.Index];
            sb.Append(EscapeDartString(staticText));

            var expression = match.Groups["expr"].Value.Trim()
                .Replace(".value", string.Empty, StringComparison.Ordinal);
            sb.Append("${").Append(expression).Append('}');
            index = match.Index + match.Length;
        }

        sb.Append(EscapeDartString(trimmed[index..]));
        sb.Append('\'');
        return (sb.ToString(), false);
    }

    [GeneratedRegex("\\{\\{\\s*(?<expr>[^}]+)\\s*\\}\\}", RegexOptions.Compiled)]
    private static partial Regex InterpolationRegex ();

    [GeneratedRegex("^[A-Za-z_]\\w*$", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex ();

    [GeneratedRegex("(^[A-Za-z_]\\w*\\s*(=|\\+=|-=|\\*=|/=|%=).+)|(^[A-Za-z_]\\w*(\\+\\+|--)$)", RegexOptions.Compiled)]
    private static partial Regex MutationRegex ();

    [GeneratedRegex("^[A-Za-z_]\\w*\\.back\\(\\)$", RegexOptions.Compiled)]
    private static partial Regex InlineRouterBackRegex ();

    [GeneratedRegex("^[A-Za-z_]\\w*\\.(?<action>push|replace)\\((?<target>[\\s\\S]+)\\)$", RegexOptions.Compiled)]
    private static partial Regex InlineRouterNavigateRegex ();

    [GeneratedRegex("path\\s*:\\s*['\"](?<path>[^'\"]+)['\"]", RegexOptions.Compiled)]
    private static partial Regex RouterObjectPathRegex ();

    [GeneratedRegex("name\\s*:\\s*['\"](?<name>[^'\"]+)['\"]", RegexOptions.Compiled)]
    private static partial Regex RouterObjectNameRegex ();

    private static string EscapeDartString (string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static void AppendWidgetWithComma (StringBuilder sb, string widget)
    {
        var lines = widget.Split(Environment.NewLine, StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (index == lines.Length - 1)
            {
                sb.AppendLine($"{line},");
                continue;
            }

            sb.AppendLine(line);
        }
    }

    private static string Indent (int level)
    {
        return new string(' ', level * 2);
    }
}

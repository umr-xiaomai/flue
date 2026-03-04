using Flue.Core.Abstractions;
using Flue.Core.Models;

namespace Flue.Infrastructure.Generation;

public sealed partial class DartWidgetRenderer(ITailwindConverter tailwindConverter)
{
    public string RenderRoot(TemplateNode root, int indentLevel)
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

    private string RenderNode(TemplateNode node, int indentLevel)
    {
        var style = tailwindConverter.Convert(node.Classes);
        return node.Kind switch
        {
            WidgetKind.Row => RenderFlexWidget("Row", node, style, indentLevel),
            WidgetKind.Column => RenderFlexWidget("Column", node, style, indentLevel),
            WidgetKind.Text => RenderText(node, style, indentLevel),
            _ => RenderContainer(node, style, indentLevel)
        };
    }

    private string RenderContainer(TemplateNode node, TailwindStyle style, int indentLevel)
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

    private string RenderFlexWidget(string widgetName, TemplateNode node, TailwindStyle style, int indentLevel)
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

    private void AppendContainerChild(StringBuilder sb, ImmutableArray<TemplateNode> children, int indentLevel)
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

    private string RenderText(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var content = node.TextContent ?? string.Empty;
        var (expression, isConst) = BuildTextExpression(content);

        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);
        var constPrefix = isConst && style.TextStyleProperties.Length == 0 ? "const " : string.Empty;

        sb.AppendLine($"{indent}{constPrefix}Text(");
        sb.AppendLine($"{inner}{expression},");
        if (style.TextStyleProperties.Length > 0)
        {
            sb.AppendLine($"{inner}style: const TextStyle(");
            foreach (var property in style.TextStyleProperties)
            {
                sb.AppendLine($"{Indent(indentLevel + 2)}{property},");
            }

            sb.AppendLine($"{inner}),");
        }

        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string WrapWithContainer(string widget, TailwindStyle style, int indentLevel)
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

    private static (string Expression, bool IsConst) BuildTextExpression(string text)
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
    private static partial Regex InterpolationRegex();

    private static string EscapeDartString(string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static void AppendWidgetWithComma(StringBuilder sb, string widget)
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

    private static string Indent(int level)
    {
        return new string(' ', level * 2);
    }
}

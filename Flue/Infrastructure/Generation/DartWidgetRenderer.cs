using Flue.Core.Abstractions;
using Flue.Core.Models;

namespace Flue.Infrastructure.Generation;

public sealed partial class DartWidgetRenderer(ITailwindConverter tailwindConverter)
{
    private static readonly ImmutableArray<string> ClickAttributeKeys =
    [
        "@click", "v-on:click", "onclick", "onClick", "onTap", "@tap", "v-on:tap"
    ];

    private static readonly ImmutableArray<string> RouterTargetKeys =
    [
        "to", ":to", "v-bind:to", "href"
    ];

    private int vForCounter;

    public string RenderRoot(TemplateNode root, int indentLevel)
    {
        vForCounter = 0;
        if (root.Children.Length == 0)
        {
            return $"{Indent(indentLevel)}const SizedBox.shrink()";
        }

        if (root.Children.Length == 1 && root.Children[0].Directives.Length == 0)
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
        var rendered = RenderNodeCore(node, indentLevel);

        if (TryGetDirective(node, "show", out var showExpr))
        {
            rendered = WrapWithVisibilityCondition(rendered, showExpr, indentLevel);
        }

        if (TryGetDirective(node, "if", out var ifExpr))
        {
            rendered = WrapWithCondition(rendered, ifExpr, indentLevel);
        }

        if (TryGetDirective(node, "for", out var forExpr))
        {
            rendered = WrapWithForLoop(rendered, forExpr, indentLevel);
        }

        if (TryGetDirective(node, "model", out var modelExpr))
        {
            rendered = WrapWithModelBinding(rendered, modelExpr, indentLevel);
        }

        return rendered;
    }

    private string RenderNodeCore(TemplateNode node, int indentLevel)
    {
        var style = tailwindConverter.Convert(node.Classes);

        if (node.TagName.Equals("router-link", StringComparison.OrdinalIgnoreCase))
            return RenderRouterLink(node, style, indentLevel);

        return node.Kind switch
        {
            WidgetKind.Row => RenderFlexWidget("Row", node, style, indentLevel),
            WidgetKind.Column => RenderFlexWidget("Column", node, style, indentLevel),
            WidgetKind.Text => RenderText(node, style, indentLevel),
            WidgetKind.Button => RenderButton(node, style, indentLevel),
            WidgetKind.Image => RenderImage(node, style, indentLevel),
            WidgetKind.TextField => RenderTextField(node, style, indentLevel),
            WidgetKind.TextArea => RenderTextField(node, style, indentLevel, multiline: true),
            WidgetKind.Divider => RenderDivider(node, style, indentLevel),
            WidgetKind.SizedBox => $"{Indent(indentLevel)}const SizedBox(height: 16.0)",
            WidgetKind.ListView => RenderListView(node, style, indentLevel),
            WidgetKind.ListTile => RenderListTile(node, style, indentLevel),
            WidgetKind.Link => RenderLink(node, style, indentLevel),
            WidgetKind.Checkbox => RenderCheckbox(node, style, indentLevel),
            WidgetKind.Radio => RenderRadio(node, style, indentLevel),
            WidgetKind.Select => RenderDropdownButton(node, style, indentLevel),
            WidgetKind.Progress => RenderProgress(node, style, indentLevel),
            WidgetKind.Card => RenderCard(node, style, indentLevel),
            WidgetKind.Table => RenderTable(node, style, indentLevel),
            _ => RenderContainer(node, style, indentLevel)
        };
    }

    private static readonly FrozenSet<string> NonWrappableKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "router-link"
    }.ToFrozenSet(StringComparer.Ordinal);

    private string RenderContainer(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var innerWidget = RenderContainerCore(node, style, indentLevel);

        if (!TryGetClickHandler(node.Attributes, out var handler))
            return innerWidget;

        return WrapWithTapCallback(innerWidget, handler, indentLevel);
    }

    private string RenderContainerCore(TemplateNode node, TailwindStyle style, int indentLevel)
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
            sb.AppendLine($"{inner}{style.MainAxisAlignment},");
        }

        if (!string.IsNullOrWhiteSpace(style.CrossAxisAlignment))
        {
            sb.AppendLine($"{inner}{style.CrossAxisAlignment},");
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
            return sb.ToString();

        return WrapWithContainer(sb.ToString(), style, indentLevel);
    }

    private string RenderButton(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);
        var callback = TryGetClickHandler(node.Attributes, out var handler)
            ? BuildTapCallback(handler)
            : "null";

        sb.AppendLine($"{indent}ElevatedButton(");
        sb.AppendLine($"{inner}onPressed: {callback},");

        if (style.DecorationProperties.Length > 0 || style.WidgetProperties.Length > 0)
        {
            sb.AppendLine($"{inner}style: ElevatedButton.styleFrom(");
            foreach (var prop in style.DecorationProperties)
            {
                sb.AppendLine($"{Indent(indentLevel + 2)}{prop},");
            }

            foreach (var prop in style.WidgetProperties)
            {
                sb.AppendLine($"{Indent(indentLevel + 2)}{prop},");
            }

            sb.AppendLine($"{inner}),");
        }

        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, RenderButtonChild(node, indentLevel + 1));
        sb.Append($"{indent})");

        return sb.ToString();
    }

    private string RenderImage(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var src = node.Attributes.GetValueOrDefault("src", string.Empty);
        if (string.IsNullOrWhiteSpace(src))
            src = node.Attributes.GetValueOrDefault(":src", string.Empty);

        var isNetwork = src.StartsWith("http", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        if (isNetwork)
        {
            // Escape $ interpolation in src
            if (src.Contains("{{", StringComparison.Ordinal))
            {
                var match = InterpolationRegex().Match(src);
                if (match.Success)
                {
                    var expr = match.Groups["expr"].Value.Trim().Replace(".value", string.Empty, StringComparison.Ordinal);
                    sb.AppendLine($"{indent}Image.network(");
                    sb.AppendLine($"{inner}${expr},");
                }
                else
                {
                    sb.AppendLine($"{indent}Image.network(");
                    sb.AppendLine($"{inner}'{EscapeDartString(src)}',");
                }
            }
            else
            {
                sb.AppendLine($"{indent}Image.network(");
                sb.AppendLine($"{inner}'{EscapeDartString(src)}',");
            }
        }
        else
        {
            sb.AppendLine($"{indent}Image.asset(");
            sb.AppendLine($"{inner}'{EscapeDartString(src)}',");
        }

        foreach (var prop in style.WidgetProperties)
        {
            sb.AppendLine($"{inner}{prop},");
        }

        sb.AppendLine($"{inner}fit: BoxFit.cover,");
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string RenderTextField(TemplateNode node, TailwindStyle style, int indentLevel, bool multiline = false)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);
        var widgetName = multiline ? "TextFormField" : "TextFormField";
        var placeholder = node.Attributes.GetValueOrDefault("placeholder", string.Empty);
        var label = node.Attributes.GetValueOrDefault("label", string.Empty);
        var value = node.TextContent ?? string.Empty;

        if (!multiline)
        {
            sb.AppendLine($"{indent}SizedBox(");
            sb.AppendLine($"{inner}width: 256.0,");
            sb.AppendLine($"{inner}child: {widgetName}(");
        }
        else
        {
            sb.AppendLine($"{indent}{widgetName}(");
            sb.AppendLine($"{inner}maxLines: null,");
            sb.AppendLine($"{inner}minLines: 3,");
        }

        var inner2 = multiline ? Indent(indentLevel + 1) : Indent(indentLevel + 2);

        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            sb.AppendLine($"{inner2}decoration: InputDecoration(");
            sb.AppendLine($"{Indent(indentLevel + (multiline ? 2 : 3))}hintText: '{EscapeDartString(placeholder)}',");
            if (!string.IsNullOrWhiteSpace(label))
                sb.AppendLine($"{Indent(indentLevel + (multiline ? 2 : 3))}labelText: '{EscapeDartString(label)}',");
            sb.AppendLine($"{inner2}),");
        }

        if (!string.IsNullOrWhiteSpace(value) && !value.Contains("{{", StringComparison.Ordinal))
        {
            sb.AppendLine($"{inner2}initialValue: '{EscapeDartString(value)}',");
        }

        sb.Append(multiline ? $"{indent})" : $"{Indent(indentLevel + 1)}),\n{indent})");
        return sb.ToString();
    }

    private string RenderDivider(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        return $"{Indent(indentLevel)}const Divider()";
    }

    private string RenderListView(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}ListView(");
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
        return sb.ToString();
    }

    private string RenderListTile(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        if (node.Children.Length == 0 && string.IsNullOrWhiteSpace(node.TextContent))
            return $"{Indent(indentLevel)}const ListTile(title: Text(''))";

        if (node.Children.Length == 0)
        {
            var content = BuildTextExpression(node.TextContent ?? string.Empty);
            return node.TextContent is not null
                ? $"{Indent(indentLevel)}ListTile(title: Text('{EscapeDartString(node.TextContent)}'))"
                : $"{Indent(indentLevel)}ListTile(title: Text({content.Expression}))";
        }

        if (node.Children.Length == 1)
        {
            return $"{Indent(indentLevel)}ListTile(title: {RenderNode(node.Children[0], indentLevel + 1).Trim()})";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indentLevel)}ListTile(");
        if (node.Children.Length > 0)
        {
            sb.AppendLine($"{Indent(indentLevel + 1)}title: {RenderNode(node.Children[0], indentLevel + 1).Trim()}");
        }

        if (node.Children.Length > 1)
        {
            sb.AppendLine($"{Indent(indentLevel + 1)}subtitle: {RenderNode(node.Children[1], indentLevel + 1).Trim()}");
        }

        sb.Append($"{Indent(indentLevel)})");
        return sb.ToString();
    }

    private string RenderLink(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var href = node.Attributes.GetValueOrDefault("href", string.Empty);
        var target = node.Attributes.GetValueOrDefault("target", string.Empty);

        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}TextButton(");

        if (!string.IsNullOrWhiteSpace(href))
        {
            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"{inner}onPressed: () {{ launchUrl(Uri.parse('{EscapeDartString(href)}')); }},");
            }
            else
            {
                sb.AppendLine($"{inner}onPressed: () {{ Navigator.of(context).pushNamed('{EscapeDartString(href)}'); }},");
            }
        }
        else
        {
            sb.AppendLine($"{inner}onPressed: null,");
        }

        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, RenderLinkChild(node, indentLevel + 1));
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string RenderLinkChild(TemplateNode node, int indentLevel)
    {
        if (node.Children.Length == 0 && !string.IsNullOrWhiteSpace(node.TextContent))
            return $"{Indent(indentLevel)}Text('{EscapeDartString(node.TextContent)}')";

        if (node.Children.Length == 1)
            return RenderNode(node.Children[0], indentLevel);

        var row = new TemplateNode(WidgetKind.Row, "span", null,
            ImmutableArray<string>.Empty, node.Attributes, node.Children);
        return RenderNode(row, indentLevel);
    }

    private string RenderCheckbox(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var label = node.Attributes.GetValueOrDefault("label", string.Empty);
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);

        if (string.IsNullOrWhiteSpace(label))
        {
            sb.AppendLine($"{indent}Checkbox(");
            sb.AppendLine($"{Indent(indentLevel + 1)}value: false,");
            sb.AppendLine($"{Indent(indentLevel + 1)}onChanged: (value) {{ setState(() {{ }}); }},");
            sb.Append($"{indent})");
            return sb.ToString();
        }

        sb.AppendLine($"{indent}CheckboxListTile(");
        sb.AppendLine($"{Indent(indentLevel + 1)}title: Text('{EscapeDartString(label)}'),");
        sb.AppendLine($"{Indent(indentLevel + 1)}value: false,");
        sb.AppendLine($"{Indent(indentLevel + 1)}onChanged: (value) {{ setState(() {{ }}); }},");
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string RenderRadio(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var label = node.Attributes.GetValueOrDefault("label", string.Empty);
        var value = node.Attributes.GetValueOrDefault("value", "''");
        var group = node.Attributes.GetValueOrDefault("group", "_groupValue");

        var sb = new StringBuilder();
        var indent = Indent(indentLevel);

        if (string.IsNullOrWhiteSpace(label))
        {
            sb.AppendLine($"{indent}Radio(");
            sb.AppendLine($"{Indent(indentLevel + 1)}value: {value},");
            sb.AppendLine($"{Indent(indentLevel + 1)}groupValue: {group},");
            sb.AppendLine($"{Indent(indentLevel + 1)}onChanged: (value) {{ setState(() {{ {group} = value as String; }}); }},");
            sb.Append($"{indent})");
            return sb.ToString();
        }

        sb.AppendLine($"{indent}RadioListTile(");
        sb.AppendLine($"{Indent(indentLevel + 1)}title: Text('{EscapeDartString(label)}'),");
        sb.AppendLine($"{Indent(indentLevel + 1)}value: {value},");
        sb.AppendLine($"{Indent(indentLevel + 1)}groupValue: {group},");
        sb.AppendLine($"{Indent(indentLevel + 1)}onChanged: (value) {{ setState(() {{ {group} = value as String; }}); }},");
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string RenderDropdownButton(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        var items = new List<string>();
        foreach (var child in node.Children)
        {
            if (child.Kind == WidgetKind.Text && !string.IsNullOrWhiteSpace(child.TextContent))
            {
                items.Add($"DropdownMenuItem(child: Text('{EscapeDartString(child.TextContent)}'))");
            }
        }

        if (items.Count == 0)
            items.Add("DropdownMenuItem(child: Text('Option'))");

        sb.AppendLine($"{indent}DropdownButton<String>(");
        sb.AppendLine($"{inner}items: [");
        foreach (var item in items)
        {
            sb.AppendLine($"{Indent(indentLevel + 2)}{item},");
        }

        sb.AppendLine($"{inner}],");
        sb.AppendLine($"{inner}onChanged: (value) {{ setState(() {{ }}); }},");
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string RenderProgress(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        return $"{Indent(indentLevel)}const LinearProgressIndicator()";
    }

    private string RenderCard(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}Card(");
        sb.AppendLine($"{inner}child:");

        if (node.Children.Length == 0)
        {
            sb.AppendLine($"{Indent(indentLevel + 2)}const SizedBox.shrink()");
        }
        else if (node.Children.Length == 1)
        {
            sb.AppendLine(RenderNode(node.Children[0], indentLevel + 2));
        }
        else
        {
            var col = new TemplateNode(WidgetKind.Column, "div", null,
                ImmutableArray<string>.Empty, node.Attributes, node.Children);
            sb.AppendLine(RenderNode(col, indentLevel + 2));
        }

        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string RenderTable(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        var inner = Indent(indentLevel + 1);

        sb.AppendLine($"{indent}Table(");
        sb.AppendLine($"{inner}children: [");

        foreach (var row in node.Children)
        {
            sb.AppendLine($"{Indent(indentLevel + 2)}TableRow(children: [");
            foreach (var cell in row.Children)
            {
                AppendWidgetWithComma(sb, $"{Indent(indentLevel + 3)}TableCell(child: {RenderNode(cell, indentLevel + 3)})");
            }

            sb.AppendLine($"{Indent(indentLevel + 2)}]),");
        }

        sb.AppendLine($"{inner}],");
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private string RenderRouterLink(TemplateNode node, TailwindStyle style, int indentLevel)
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
            return sb.ToString();

        return WrapWithContainer(sb.ToString(), style, indentLevel);
    }

    private string RenderButtonChild(TemplateNode node, int indentLevel)
    {
        if (node.Children.Length == 0)
            return $"{Indent(indentLevel)}const Text('Button')";

        if (node.Children.Length == 1)
            return RenderNode(node.Children[0], indentLevel);

        var syntheticRow = new TemplateNode(
            WidgetKind.Row, "div", null,
            ImmutableArray<string>.Empty, node.Attributes, node.Children);

        return RenderNode(syntheticRow, indentLevel);
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
            WidgetKind.Column, "div", null,
            ImmutableArray<string>.Empty, children[0].Attributes, children);

        sb.AppendLine($"{inner}child:");
        AppendWidgetWithComma(sb, RenderNode(columnNode, indentLevel + 1));
    }

    private string RenderText(TemplateNode node, TailwindStyle style, int indentLevel)
    {
        var content = node.TextContent ?? string.Empty;

        if (TryGetDirective(node, "html", out var htmlExpr))
        {
            return RenderHtmlText(htmlExpr, indentLevel);
        }

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
            sb.AppendLine($"{inner}style: TextStyle(");
            foreach (var property in textStyles)
            {
                sb.AppendLine($"{Indent(indentLevel + 2)}{property},");
            }

            sb.AppendLine($"{inner}),");
        }

        sb.Append($"{indent})");
        return sb.ToString();
    }

    private static string RenderHtmlText(string htmlExpr, int indentLevel)
    {
        var sb = new StringBuilder();
        var indent = Indent(indentLevel);
        sb.AppendLine($"{indent}HtmlWidget(");
        sb.AppendLine($"{Indent(indentLevel + 1)}html: {htmlExpr},");
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private static ImmutableArray<string> BuildSemanticTextStyle(string tagName)
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
            "small" or "sub" or "sup" => ["fontSize: 12.0"],
            "code" or "kbd" or "samp" => ["fontFamily: 'monospace'"],
            _ => ImmutableArray<string>.Empty
        };
    }

    private static ImmutableArray<string> MergeTextStyles(ImmutableArray<string> semanticStyles,
        ImmutableArray<string> tailwindStyles)
    {
        if (semanticStyles.Length == 0) return tailwindStyles;
        if (tailwindStyles.Length == 0) return semanticStyles;

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in semanticStyles)
            merged[StyleKey(style)] = style;
        foreach (var style in tailwindStyles)
            merged[StyleKey(style)] = style;

        return [.. merged.Values];
    }

    private static string StyleKey(string style)
    {
        var separator = style.IndexOf(':');
        return separator < 0 ? style.Trim() : style[..separator].Trim();
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

    private string WrapWithTapCallback(string widget, string handler, int indentLevel)
    {
        var callback = BuildTapCallback(handler);
        if (callback is "null")
            return widget;

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

    private static string WrapWithCondition(string widget, string condition, int indentLevel)
    {
        var indent = Indent(indentLevel);
        var sb = new StringBuilder();
        var cleanCondition = condition.Replace(".value", string.Empty, StringComparison.Ordinal);
        sb.AppendLine($"{indent}{cleanCondition} ?");
        sb.AppendLine($"{widget}");
        sb.Append($"{indent}: const SizedBox.shrink()");
        return sb.ToString();
    }

    private static string WrapWithVisibilityCondition(string widget, string condition, int indentLevel)
    {
        return WrapWithCondition(widget, condition, indentLevel);
    }

    private string WrapWithForLoop(string widget, string forExpr, int indentLevel)
    {
        var indent = Indent(indentLevel);
        vForCounter++;
        var parts = forExpr.Split(" in ", 2, StringSplitOptions.TrimEntries);
        var itemVar = parts.Length >= 1 ? parts[0].Trim() : "item";
        var collection = parts.Length >= 2
            ? parts[1].Trim().Replace(".value", string.Empty, StringComparison.Ordinal)
            : "[]";
        var indexVar = $"__i{vForCounter}";

        var sb = new StringBuilder();
        sb.AppendLine($"{indent}...{collection}.asMap().entries.map(({indexVar}) {{");
        sb.AppendLine($"{Indent(indentLevel + 1)}final {itemVar} = {indexVar}.value;");
        sb.AppendLine($"{Indent(indentLevel + 1)}return {widget.Trim()};");
        sb.Append($"{indent}}}).toList(),");
        return sb.ToString();
    }

    private static string WrapWithModelBinding(string widget, string modelExpr, int indentLevel)
    {
        // V-model: wrap display widget with onChanged to update state
        var indent = Indent(indentLevel);
        var cleanExpr = modelExpr.Replace(".value", string.Empty, StringComparison.Ordinal);

        var sb = new StringBuilder();
        sb.AppendLine($"{indent}TextFormField(");
        sb.AppendLine($"{Indent(indentLevel + 1)}initialValue: {cleanExpr}.toString(),");
        sb.AppendLine($"{Indent(indentLevel + 1)}onChanged: (value) {{ setState(() {{ {cleanExpr} = value; }}); }},");
        sb.AppendLine($"{Indent(indentLevel + 1)}decoration: InputDecoration(");
        sb.AppendLine($"{Indent(indentLevel + 2)}hintText: '{cleanExpr}',");
        sb.AppendLine($"{Indent(indentLevel + 1)}),");
        sb.Append($"{indent})");
        return sb.ToString();
    }

    private static bool TryGetDirective(TemplateNode node, string kind, out string expression)
    {
        if (node.Directives.IsDefaultOrEmpty)
        {
            expression = string.Empty;
            return false;
        }

        foreach (var directive in node.Directives)
        {
            if (directive.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            {
                expression = directive.Expression;
                return true;
            }
        }

        expression = string.Empty;
        return false;
    }

    private static string ResolveRouterTargetExpression(FrozenDictionary<string, string> attributes)
    {
        if (!TryGetRouterTarget(attributes, out var target))
            return "'/'";

        return ResolveRouteExpression(target);
    }

    private static bool TryGetClickHandler(FrozenDictionary<string, string> attributes, out string handler)
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

    private static bool TryGetRouterTarget(FrozenDictionary<string, string> attributes, out string target)
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

    private static string BuildTapCallback(string rawHandler)
    {
        var handler = rawHandler
            .Trim()
            .Replace(".value", string.Empty, StringComparison.Ordinal)
            .TrimEnd(';');

        if (handler.Length == 0)
            return "null";

        if (handler.StartsWith("() =>", StringComparison.Ordinal))
            handler = handler[5..].Trim();

        if (InlineRouterBackRegex().IsMatch(handler))
            return "() { Navigator.of(context).pop(); }";

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
            return handler;

        if (MutationRegex().IsMatch(handler))
            return $"() {{ setState(() {{ {handler}; }}); }}";

        if (handler.Contains("await ", StringComparison.Ordinal))
            return $"() async {{ {handler}; }}";

        return $"() {{ {handler}; }}";
    }

    private static string ResolveRouteExpression(string rawTarget)
    {
        var target = rawTarget.Trim();
        if (target.StartsWith('{') && target.EndsWith('}'))
        {
            var pathMatch = RouterObjectPathRegex().Match(target);
            if (pathMatch.Success)
                return $"'{EscapeDartString(pathMatch.Groups["path"].Value)}'";

            var nameMatch = RouterObjectNameRegex().Match(target);
            if (nameMatch.Success)
                return $"FlueAppRouter.pathByName('{EscapeDartString(nameMatch.Groups["name"].Value)}') ?? '/'";
        }

        if ((target.StartsWith('"') && target.EndsWith('"')) ||
            (target.StartsWith('\'') && target.EndsWith('\'')))
            return $"'{EscapeDartString(target[1..^1])}'";

        if (target.StartsWith("/", StringComparison.Ordinal))
            return $"'{EscapeDartString(target)}'";

        return target.Replace(".value", string.Empty, StringComparison.Ordinal);
    }

    private static (string Expression, bool IsConst) BuildTextExpression(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return ("''", true);

        var matches = InterpolationRegex().Matches(trimmed);
        if (matches.Count == 0)
            return ($"'{EscapeDartString(trimmed)}'", true);

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

    [GeneratedRegex("^[A-Za-z_]\\w*$", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("(^[A-Za-z_]\\w*\\s*(=|\\+=|-=|\\*=|/=|%=).+)|(^[A-Za-z_]\\w*(\\+\\+|--)$)", RegexOptions.Compiled)]
    private static partial Regex MutationRegex();

    [GeneratedRegex("^[A-Za-z_]\\w*\\.back\\(\\)$", RegexOptions.Compiled)]
    private static partial Regex InlineRouterBackRegex();

    [GeneratedRegex("^[A-Za-z_]\\w*\\.(?<action>push|replace)\\((?<target>[\\s\\S]+)\\)$", RegexOptions.Compiled)]
    private static partial Regex InlineRouterNavigateRegex();

    [GeneratedRegex("path\\s*:\\s*['\"](?<path>[^'\"]+)['\"]", RegexOptions.Compiled)]
    private static partial Regex RouterObjectPathRegex();

    [GeneratedRegex("name\\s*:\\s*['\"](?<name>[^'\"]+)['\"]", RegexOptions.Compiled)]
    private static partial Regex RouterObjectNameRegex();

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

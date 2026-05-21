using Flue.Core.Abstractions;
using Flue.Core.Models;
using HtmlAgilityPack;

namespace Flue.Infrastructure.Parsing;

public sealed class TemplateParser : ITemplateParser
{
    private static readonly FrozenDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> TextTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "span", "p", "label", "small", "strong", "em", "b", "i", "u", "s", "del", "ins",
        "h1", "h2", "h3", "h4", "h5", "h6", "sub", "sup", "mark", "cite", "q", "code",
        "kbd", "samp", "var", "abbr", "dt", "dd", "figcaption", "legend", "time", "address"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> FlexCapableTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "div", "header", "footer", "main", "section", "article", "nav", "aside", "form",
        "fieldset", "details", "summary", "dialog", "figure", "blockquote", "caption"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, WidgetKind> ExplicitTagKindMap = new Dictionary<string, WidgetKind>(StringComparer.OrdinalIgnoreCase)
    {
        ["img"] = WidgetKind.Image,
        ["image"] = WidgetKind.Image,
        ["input"] = WidgetKind.TextField,
        ["textarea"] = WidgetKind.TextArea,
        ["select"] = WidgetKind.Select,
        ["button"] = WidgetKind.Button,
        ["a"] = WidgetKind.Link,
        ["hr"] = WidgetKind.Divider,
        ["br"] = WidgetKind.SizedBox,
        ["ul"] = WidgetKind.ListView,
        ["ol"] = WidgetKind.ListView,
        ["li"] = WidgetKind.ListTile,
        ["table"] = WidgetKind.Table,
        ["progress"] = WidgetKind.Progress,
        ["video"] = WidgetKind.Container,
        ["audio"] = WidgetKind.Container,
        ["canvas"] = WidgetKind.Container,
        ["iframe"] = WidgetKind.Container,
        ["svg"] = WidgetKind.Container,
        ["pre"] = WidgetKind.Container,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> VoidElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "br", "hr", "img", "input", "meta", "link", "area", "base", "col", "embed",
        "source", "track", "wbr"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> InputTextTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "text", "email", "password", "number", "tel", "url", "search", "date", "time",
        "datetime-local", "month", "week", "color"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> DirectiveAttributeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "v-if", "v-else-if", "v-else", "v-for", "v-model", "v-show", "v-html",
        ":if", ":else-if", ":for", ":model", ":show", ":html"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public TemplateNode Parse(string templateContent)
    {
        var document = new HtmlDocument();
        document.LoadHtml($"<flue-root>{templateContent}</flue-root>");

        var root = document.DocumentNode.SelectSingleNode("//flue-root") ?? document.DocumentNode;
        var children = root.ChildNodes
            .SelectMany(ParseNode)
            .ToImmutableArray();

        return new TemplateNode(
            WidgetKind.Column,
            "flue-root",
            null,
            ImmutableArray<string>.Empty,
            EmptyAttributes,
            children,
            ImmutableArray<TemplateDirective>.Empty);
    }

    private static IEnumerable<TemplateNode> ParseNode(HtmlNode node)
    {
        if (node.NodeType is HtmlNodeType.Comment)
        {
            yield break;
        }

        if (node.NodeType is HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            yield return new TemplateNode(
                WidgetKind.Text,
                "#text",
                text,
                ImmutableArray<string>.Empty,
                EmptyAttributes,
                ImmutableArray<TemplateNode>.Empty,
                ImmutableArray<TemplateDirective>.Empty);
            yield break;
        }

        if (node.NodeType is not HtmlNodeType.Element)
        {
            yield break;
        }

        var classTokens = ParseClasses(node.GetAttributeValue("class", string.Empty));
        var allAttributes = node.Attributes
            .ToDictionary(attribute => attribute.Name, attribute => attribute.Value, StringComparer.OrdinalIgnoreCase);

        var directives = ExtractDirectives(allAttributes);
        var cleanAttributes = allAttributes
            .Where(kv => !DirectiveAttributeKeys.Contains(kv.Key))
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        var tagName = node.Name.ToLowerInvariant();
        var kind = ResolveKind(tagName, classTokens, allAttributes);

        if (kind is WidgetKind.SizedBox)
        {
            yield return new TemplateNode(
                WidgetKind.SizedBox,
                "br",
                null,
                ImmutableArray<string>.Empty,
                cleanAttributes,
                ImmutableArray<TemplateNode>.Empty,
                directives);
            yield break;
        }

        if (kind is WidgetKind.Divider)
        {
            yield return new TemplateNode(
                WidgetKind.Divider,
                "hr",
                null,
                classTokens,
                cleanAttributes,
                ImmutableArray<TemplateNode>.Empty,
                directives);
            yield break;
        }

        if (kind is WidgetKind.Image)
        {
            yield return new TemplateNode(
                WidgetKind.Image,
                tagName,
                null,
                classTokens,
                cleanAttributes,
                ImmutableArray<TemplateNode>.Empty,
                directives);
            yield break;
        }

        if (kind is WidgetKind.TextField)
        {
            yield return new TemplateNode(
                WidgetKind.TextField,
                tagName,
                null,
                classTokens,
                cleanAttributes,
                ImmutableArray<TemplateNode>.Empty,
                directives);
            yield break;
        }

        if (kind is WidgetKind.TextArea)
        {
            var initialValue = HtmlEntity.DeEntitize(node.InnerText).Trim();
            yield return new TemplateNode(
                WidgetKind.TextArea,
                tagName,
                initialValue.Length > 0 ? initialValue : null,
                classTokens,
                cleanAttributes,
                ImmutableArray<TemplateNode>.Empty,
                directives);
            yield break;
        }

        if (kind is WidgetKind.Select)
        {
            var optionNodes = node.ChildNodes
                .Where(child => child.NodeType == HtmlNodeType.Element &&
                               child.Name.Equals("option", StringComparison.OrdinalIgnoreCase))
                .SelectMany(ParseNode)
                .ToImmutableArray();

            yield return new TemplateNode(
                WidgetKind.Select,
                tagName,
                null,
                classTokens,
                cleanAttributes,
                optionNodes,
                directives);
            yield break;
        }

        if (kind is WidgetKind.Text)
        {
            var inlineText = HtmlEntity.DeEntitize(node.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(inlineText) && directives.Length == 0)
            {
                yield break;
            }

            yield return new TemplateNode(
                WidgetKind.Text,
                tagName,
                inlineText.Length > 0 ? inlineText : null,
                classTokens,
                cleanAttributes,
                ImmutableArray<TemplateNode>.Empty,
                directives);
            yield break;
        }

        if (kind is WidgetKind.ListView)
        {
            var listChildren = node.ChildNodes
                .SelectMany(ParseNode)
                .ToImmutableArray();

            yield return new TemplateNode(
                WidgetKind.ListView,
                tagName,
                null,
                classTokens,
                cleanAttributes,
                listChildren,
                directives);
            yield break;
        }

        if (kind is WidgetKind.ListTile)
        {
            var tileChildren = node.ChildNodes
                .SelectMany(ParseNode)
                .ToImmutableArray();

            yield return new TemplateNode(
                WidgetKind.ListTile,
                tagName,
                null,
                classTokens,
                cleanAttributes,
                tileChildren,
                directives);
            yield break;
        }

        if (kind is WidgetKind.Link)
        {
            var linkChildren = node.ChildNodes
                .SelectMany(ParseNode)
                .ToImmutableArray();

            var effectiveChildren = linkChildren.Length > 0
                ? linkChildren
                : ImmutableArray.Create(new TemplateNode(
                    WidgetKind.Text,
                    "#text",
                    cleanAttributes.GetValueOrDefault("href", node.InnerText.Trim()),
                    ImmutableArray<string>.Empty,
                    EmptyAttributes,
                    ImmutableArray<TemplateNode>.Empty,
                    ImmutableArray<TemplateDirective>.Empty));

            yield return new TemplateNode(
                WidgetKind.Link,
                tagName,
                null,
                classTokens,
                cleanAttributes,
                effectiveChildren,
                directives);
            yield break;
        }

        if (kind is WidgetKind.Table)
        {
            var tableChildren = node.ChildNodes
                .SelectMany(ParseNode)
                .ToImmutableArray();

            yield return new TemplateNode(
                WidgetKind.Table,
                tagName,
                null,
                classTokens,
                cleanAttributes,
                tableChildren,
                directives);
            yield break;
        }

        var children = node.ChildNodes
            .SelectMany(ParseNode)
            .ToImmutableArray();

        yield return new TemplateNode(
            kind,
            tagName,
            null,
            classTokens,
            cleanAttributes,
            children,
            directives);
    }

    private static ImmutableArray<string> ParseClasses(string classValue)
    {
        if (string.IsNullOrWhiteSpace(classValue))
        {
            return ImmutableArray<string>.Empty;
        }

        return [.. classValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)];
    }

    private static ImmutableArray<TemplateDirective> ExtractDirectives(Dictionary<string, string> attributes)
    {
        var directives = ImmutableArray.CreateBuilder<TemplateDirective>();

        if (attributes.TryGetValue("v-if", out var vIf) && !string.IsNullOrWhiteSpace(vIf))
        {
            directives.Add(new TemplateDirective("if", vIf.Trim()));
        }
        else if (attributes.TryGetValue(":if", out var colonIf) && !string.IsNullOrWhiteSpace(colonIf))
        {
            directives.Add(new TemplateDirective("if", colonIf.Trim()));
        }

        if (attributes.TryGetValue("v-else-if", out var vElseIf) && !string.IsNullOrWhiteSpace(vElseIf))
        {
            directives.Add(new TemplateDirective("else-if", vElseIf.Trim()));
        }

        if (attributes.TryGetValue("v-else", out var vElse))
        {
            directives.Add(new TemplateDirective("else", string.Empty));
        }

        if (attributes.TryGetValue("v-for", out var vFor) && !string.IsNullOrWhiteSpace(vFor))
        {
            directives.Add(new TemplateDirective("for", vFor.Trim()));
        }

        if (attributes.TryGetValue("v-model", out var vModel) && !string.IsNullOrWhiteSpace(vModel))
        {
            directives.Add(new TemplateDirective("model", vModel.Trim()));
        }

        if (attributes.TryGetValue("v-show", out var vShow) && !string.IsNullOrWhiteSpace(vShow))
        {
            directives.Add(new TemplateDirective("show", vShow.Trim()));
        }

        if (attributes.TryGetValue("v-html", out var vHtml) && !string.IsNullOrWhiteSpace(vHtml))
        {
            directives.Add(new TemplateDirective("html", vHtml.Trim()));
        }

        return directives.ToImmutable();
    }

    private static WidgetKind ResolveKind(string tagName, ImmutableArray<string> classes,
        Dictionary<string, string> attributes)
    {
        if (ExplicitTagKindMap.TryGetValue(tagName, out var explicitKind))
        {
            if (explicitKind is WidgetKind.TextField && attributes.TryGetValue("type", out var inputType))
            {
                if (inputType.Equals("checkbox", StringComparison.OrdinalIgnoreCase))
                    return WidgetKind.Checkbox;
                if (inputType.Equals("radio", StringComparison.OrdinalIgnoreCase))
                    return WidgetKind.Radio;
                if (inputType.Equals("range", StringComparison.OrdinalIgnoreCase))
                    return WidgetKind.Slider;
                if (inputType.Equals("file", StringComparison.OrdinalIgnoreCase))
                    return WidgetKind.Button;
            }

            return explicitKind;
        }

        if (TextTags.Contains(tagName))
        {
            return WidgetKind.Text;
        }

        if (tagName.Equals("tr", StringComparison.OrdinalIgnoreCase))
            return WidgetKind.Row;
        if (tagName is "td" or "th")
            return WidgetKind.Container;

        if (FlexCapableTags.Contains(tagName))
        {
            var classSet = classes.ToHashSet(StringComparer.Ordinal);
            if (classSet.Contains("flex-col"))
            {
                return WidgetKind.Column;
            }

            if (classSet.Contains("flex-row") || classSet.Contains("flex"))
            {
                return WidgetKind.Row;
            }

            return WidgetKind.Container;
        }

        return WidgetKind.Container;
    }
}

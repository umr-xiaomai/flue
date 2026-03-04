using Flue.Core.Abstractions;
using Flue.Core.Models;
using HtmlAgilityPack;

namespace Flue.Infrastructure.Parsing;

public sealed class TemplateParser : ITemplateParser
{
    private static readonly FrozenDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>().ToFrozenDictionary(StringComparer.Ordinal);

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
            children);
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
                ImmutableArray<TemplateNode>.Empty);
            yield break;
        }

        if (node.NodeType is not HtmlNodeType.Element)
        {
            yield break;
        }

        var classTokens = ParseClasses(node.GetAttributeValue("class", string.Empty));
        var attributes = node.Attributes
            .ToDictionary(attribute => attribute.Name, attribute => attribute.Value, StringComparer.Ordinal)
            .ToFrozenDictionary(StringComparer.Ordinal);

        var kind = ResolveKind(node.Name, classTokens);
        if (kind is WidgetKind.Text)
        {
            var inlineText = HtmlEntity.DeEntitize(node.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(inlineText))
            {
                yield break;
            }

            yield return new TemplateNode(
                WidgetKind.Text,
                node.Name,
                inlineText,
                classTokens,
                attributes,
                ImmutableArray<TemplateNode>.Empty);
            yield break;
        }

        var children = node.ChildNodes
            .SelectMany(ParseNode)
            .ToImmutableArray();

        yield return new TemplateNode(
            kind,
            node.Name,
            null,
            classTokens,
            attributes,
            children);
    }

    private static ImmutableArray<string> ParseClasses(string classValue)
    {
        if (string.IsNullOrWhiteSpace(classValue))
        {
            return ImmutableArray<string>.Empty;
        }

        return classValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static WidgetKind ResolveKind(string tagName, ImmutableArray<string> classes)
    {
        if (tagName.Equals("span", StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals("p", StringComparison.OrdinalIgnoreCase))
        {
            return WidgetKind.Text;
        }

        if (!tagName.Equals("div", StringComparison.OrdinalIgnoreCase))
        {
            return WidgetKind.Container;
        }

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
}

namespace Flue.Core.Models;

public sealed record TemplateNode(
    WidgetKind Kind,
    string TagName,
    string? TextContent,
    ImmutableArray<string> Classes,
    FrozenDictionary<string, string> Attributes,
    ImmutableArray<TemplateNode> Children);

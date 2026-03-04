namespace Flue.Core.Models;

public sealed record TailwindStyle (
    ImmutableArray<string> WidgetProperties,
    ImmutableArray<string> DecorationProperties,
    ImmutableArray<string> TextStyleProperties,
    string? MainAxisAlignment,
    string? CrossAxisAlignment);

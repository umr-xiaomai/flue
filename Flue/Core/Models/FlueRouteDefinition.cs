namespace Flue.Core.Models;

public sealed record FlueRouteDefinition (
    string Path,
    string? Name,
    string ComponentSourceFilePath);

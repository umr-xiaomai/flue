namespace Flue.Core.Models;

public sealed record FlueRouterManifest (
    string RouterFilePath,
    string InitialRoute,
    ImmutableArray<FlueRouteDefinition> Routes);

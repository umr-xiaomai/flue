namespace Flue.Core.Models;

public sealed record LogicBridgeResult (
    ImmutableArray<StateField> StateFields,
    ImmutableArray<DartMethod> Methods,
    bool RequiresHttpClient = false,
    bool RequiresJsonConvert = false,
    bool RequiresRouterSupport = false);

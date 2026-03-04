namespace Flue.Core.Models;

public sealed record LogicBridgeResult(
    ImmutableArray<StateField> StateFields,
    ImmutableArray<DartMethod> Methods);

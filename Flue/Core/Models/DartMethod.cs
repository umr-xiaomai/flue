namespace Flue.Core.Models;

public sealed record DartMethod (
    string Name,
    string Parameters,
    string Body,
    bool IsAsync = false,
    string ReturnType = "void");

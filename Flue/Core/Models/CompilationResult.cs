namespace Flue.Core.Models;

public sealed record CompilationResult (
    string SourceFile,
    string TargetFile,
    bool Success,
    long DurationMs,
    string? ErrorMessage = null);

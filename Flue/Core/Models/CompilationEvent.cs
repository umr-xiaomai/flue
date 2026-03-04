namespace Flue.Core.Models;

public enum CompilationEventType
{
    Started,
    Completed
}

public sealed record CompilationEvent (
    CompilationEventType EventType,
    string SourceFile,
    string TargetFile,
    bool Success,
    long DurationMs,
    string? ErrorMessage = null);

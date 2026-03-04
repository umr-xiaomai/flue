namespace Flue.Infrastructure.Terminal;

public sealed class DashboardState
{
    private const int MaxLogLines = 18;

    private readonly Lock stateLock = new();
    private readonly Queue<string> logs = new();

    private long totalCompilations;
    private long successfulCompilations;
    private string currentFile = "-";
    private bool compiling;
    private DateTimeOffset compileStartAt;
    private long lastDurationMs;

    public void MarkCompilationStarted (string sourceFile)
    {
        lock (stateLock)
        {
            compiling = true;
            currentFile = string.IsNullOrWhiteSpace(sourceFile) ? "-" : sourceFile;
            compileStartAt = DateTimeOffset.UtcNow;
        }
    }

    public void MarkCompilationCompleted (string sourceFile, bool success, long durationMs, string? errorMessage)
    {
        lock (stateLock)
        {
            compiling = false;
            currentFile = string.IsNullOrWhiteSpace(sourceFile) ? "-" : sourceFile;
            lastDurationMs = durationMs;
            totalCompilations++;
            if (success)
            {
                successfulCompilations++;
            }

            AddLogUnsafe(success
                ? $"OK {sourceFile} ({durationMs}ms)"
                : $"ERR {sourceFile} ({durationMs}ms): {errorMessage}");
        }
    }

    public void AddLog (string message)
    {
        lock (stateLock)
        {
            AddLogUnsafe(message);
        }
    }

    public void ClearLogs ()
    {
        lock (stateLock)
        {
            logs.Clear();
        }
    }

    public DashboardSnapshot Snapshot ()
    {
        lock (stateLock)
        {
            var elapsed = compiling
                ? Math.Max(0, (long)(DateTimeOffset.UtcNow - compileStartAt).TotalMilliseconds)
                : lastDurationMs;

            return new DashboardSnapshot(
                totalCompilations,
                successfulCompilations,
                currentFile,
                elapsed,
                [.. logs],
                compiling);
        }
    }

    private void AddLogUnsafe (string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        logs.Enqueue(line);
        while (logs.Count > MaxLogLines)
        {
            logs.Dequeue();
        }
    }
}

public sealed record DashboardSnapshot (
    long TotalCompilations,
    long SuccessfulCompilations,
    string CurrentFile,
    long ElapsedMs,
    ImmutableArray<string> Logs,
    bool IsCompiling);

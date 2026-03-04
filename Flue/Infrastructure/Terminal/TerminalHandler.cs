using Flue.Core.Abstractions;
using Flue.Core.Models;
using Flue.Infrastructure.Configuration;
using Flue.Infrastructure.FileSystem;
using Spectre.Console;

namespace Flue.Infrastructure.Terminal;

public sealed class TerminalHandler (
    FileSystemService fileSystemService,
    IFlueCompiler compiler,
    FluePaths paths)
{
    private readonly DashboardState dashboard = new();
    private readonly Lock outputLock = new();

    public async Task RunAsync (CancellationToken cancellationToken = default)
    {
        using var runtimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        compiler.CompilationProgress += OnCompilationProgress;
        fileSystemService.StatusChanged += OnStatusChanged;

        try
        {
            WriteInfo("Bootstrapping Flue...");
            await fileSystemService.StartAsync(runtimeCts.Token);
            WriteInfo("Watcher active. Hotkeys: r=rebuild, c=clear, q=quit.");

            var keyLoopTask = KeyLoopAsync(runtimeCts);
            var tickerTask = RenderRealtimeStatsAsync(runtimeCts.Token);
            await Task.WhenAll(keyLoopTask, tickerTask);
        }
        finally
        {
            runtimeCts.Cancel();
            await fileSystemService.StopAsync();
            compiler.CompilationProgress -= OnCompilationProgress;
            fileSystemService.StatusChanged -= OnStatusChanged;
        }
    }

    private void OnCompilationProgress (object? sender, CompilationEvent compilationEvent)
    {
        var relativeSource = ToRelative(compilationEvent.SourceFile);
        if (compilationEvent.EventType is CompilationEventType.Started)
        {
            dashboard.MarkCompilationStarted(relativeSource);
            var startedSnapshot = dashboard.Snapshot();
            WriteAction($"Compiling {relativeSource} | elapsed={startedSnapshot.ElapsedMs} ms | {FormatRate(startedSnapshot)}");
            return;
        }

        dashboard.MarkCompilationCompleted(
            relativeSource,
            compilationEvent.Success,
            compilationEvent.DurationMs,
            compilationEvent.ErrorMessage);

        var completedSnapshot = dashboard.Snapshot();
        if (compilationEvent.Success)
        {
            WriteSuccess($"Compiled {relativeSource} | elapsed={compilationEvent.DurationMs} ms | {FormatRate(completedSnapshot)}");
            return;
        }

        var errorDetail = string.IsNullOrWhiteSpace(compilationEvent.ErrorMessage)
            ? "Unknown error."
            : compilationEvent.ErrorMessage!;
        WriteError($"Compile failed {relativeSource} | elapsed={compilationEvent.DurationMs} ms | {FormatRate(completedSnapshot)} | {errorDetail}");
    }

    private void OnStatusChanged (object? sender, string message)
    {
        dashboard.AddLog(message);

        if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            WriteError(message);
            return;
        }

        if (message.Contains("removed", StringComparison.OrdinalIgnoreCase))
        {
            WriteWarning(message);
            return;
        }

        if (message.Contains("renamed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("started", StringComparison.OrdinalIgnoreCase))
        {
            WriteAction(message);
            return;
        }

        if (message.Contains("completed", StringComparison.OrdinalIgnoreCase))
        {
            WriteSuccess(message);
            return;
        }

        WriteInfo(message);
    }

    private async Task KeyLoopAsync (CancellationTokenSource runtimeCts)
    {
        var token = runtimeCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (IsConsoleKeyAvailable())
                {
                    var key = char.ToLowerInvariant(Console.ReadKey(intercept: true).KeyChar);
                    switch (key)
                    {
                        case 'r':
                            WriteAction("Manual full rebuild requested.");
                            await fileSystemService.TriggerFullRebuildAsync(token);
                            break;
                        case 'c':
                            AnsiConsole.Clear();
                            dashboard.ClearLogs();
                            WriteInfo("Console cleared. Hotkeys: r=rebuild, c=clear, q=quit.");
                            break;
                        case 'q':
                            WriteWarning("Graceful shutdown requested.");
                            runtimeCts.Cancel();
                            return;
                    }
                }

                await Task.Delay(50, token);
            }
        }
        catch (InvalidOperationException)
        {
            WriteWarning("Interactive key input unavailable in this host. Use Ctrl+C to exit.");
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation on shutdown.
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation on shutdown.
        }
    }

    private async Task RenderRealtimeStatsAsync (CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = dashboard.Snapshot();
                if (snapshot.IsCompiling)
                {
                    WriteTrace($"In progress | file={snapshot.CurrentFile} | elapsed={snapshot.ElapsedMs} ms | {FormatRate(snapshot)}");
                }

                await Task.Delay(500, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation on shutdown.
        }
    }

    private static bool IsConsoleKeyAvailable ()
    {
        try
        {
            return Console.KeyAvailable;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string FormatRate (DashboardSnapshot snapshot)
    {
        var rate = snapshot.TotalCompilations == 0
            ? 100.0
            : snapshot.SuccessfulCompilations * 100.0 / snapshot.TotalCompilations;
        return $"success={rate:F2}% ({snapshot.SuccessfulCompilations}/{snapshot.TotalCompilations})";
    }

    private void WriteInfo (string message)
    {
        WriteLine("deepskyblue2", "INFO", message);
    }

    private void WriteSuccess (string message)
    {
        WriteLine("springgreen2", "OK", message);
    }

    private void WriteWarning (string message)
    {
        WriteLine("gold1", "WARN", message);
    }

    private void WriteError (string message)
    {
        WriteLine("red1", "ERR", message);
    }

    private void WriteAction (string message)
    {
        WriteLine("mediumpurple3", "ACT", message);
    }

    private void WriteTrace (string message)
    {
        WriteLine("grey62", "TRACE", message);
    }

    private void WriteLine (string color, string level, string message)
    {
        var escapedMessage = Markup.Escape(message);
        lock (outputLock)
        {
            AnsiConsole.MarkupLine($"[{color}][[{DateTime.Now:HH:mm:ss}]] {level,-5} {escapedMessage}[/]");
        }
    }

    private string ToRelative (string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            return "-";
        }

        var relative = Path.GetRelativePath(paths.SourceRoot, sourceFile);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return sourceFile;
        }

        return relative.Replace('\\', '/');
    }
}

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

    public async Task RunAsync (CancellationToken cancellationToken = default)
    {
        using var runtimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        compiler.CompilationProgress += OnCompilationProgress;
        fileSystemService.StatusChanged += OnStatusChanged;

        try
        {
            dashboard.AddLog("Bootstrapping Flue...");
            await fileSystemService.StartAsync(runtimeCts.Token);
            dashboard.AddLog("Watcher is active. Press r/c/q.");

            var keyLoopTask = Task.Run(() => KeyLoopAsync(runtimeCts), CancellationToken.None);
            await RenderDashboardAsync(runtimeCts.Token);
            runtimeCts.Cancel();
            await keyLoopTask;
        }
        finally
        {
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
            return;
        }

        dashboard.MarkCompilationCompleted(
            relativeSource,
            compilationEvent.Success,
            compilationEvent.DurationMs,
            compilationEvent.ErrorMessage);
    }

    private void OnStatusChanged (object? sender, string message)
    {
        dashboard.AddLog(message);
    }

    private async Task KeyLoopAsync (CancellationTokenSource runtimeCts)
    {
        var token = runtimeCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = char.ToLowerInvariant(Console.ReadKey(intercept: true).KeyChar);
                    switch (key)
                    {
                        case 'r':
                            dashboard.AddLog("Manual full rebuild requested.");
                            await fileSystemService.TriggerFullRebuildAsync(token);
                            break;
                        case 'c':
                            AnsiConsole.Clear();
                            dashboard.ClearLogs();
                            dashboard.AddLog("Log panel cleared.");
                            break;
                        case 'q':
                            dashboard.AddLog("Graceful shutdown requested.");
                            runtimeCts.Cancel();
                            return;
                    }
                }

                await Task.Delay(50, token);
            }
        }
        catch (InvalidOperationException)
        {
            dashboard.AddLog("Interactive key input is not available in this host.");
            runtimeCts.Cancel();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation on shutdown.
        }
    }

    private async Task RenderDashboardAsync (CancellationToken cancellationToken)
    {
        try
        {
            await AnsiConsole
                .Live(BuildDashboard())
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Top)
                .StartAsync(async context =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        context.UpdateTarget(BuildDashboard());
                        context.Refresh();
                        await Task.Delay(80, cancellationToken);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation on shutdown.
        }
    }

    private Rows BuildDashboard ()
    {
        var snapshot = dashboard.Snapshot();
        var rate = snapshot.TotalCompilations == 0
            ? 100.0
            : snapshot.SuccessfulCompilations * 100.0 / snapshot.TotalCompilations;

        var metrics = new Table().Border(TableBorder.Rounded).Expand();
        metrics.AddColumn("Metric");
        metrics.AddColumn("Value");
        metrics.AddRow("Compile Success Rate", $"{rate:F2}% ({snapshot.SuccessfulCompilations}/{snapshot.TotalCompilations})");
        metrics.AddRow("Current File", snapshot.CurrentFile);
        metrics.AddRow("Elapsed", $"{snapshot.ElapsedMs} ms");
        metrics.AddRow("Status", snapshot.IsCompiling ? "Compiling" : "Idle");
        metrics.AddRow("Keys", "r = rebuild | c = clear | q = quit");

        var logTable = new Table().Border(TableBorder.MinimalHeavyHead).Expand();
        logTable.AddColumn("Logs");
        foreach (var line in snapshot.Logs)
        {
            logTable.AddRow(new Markup(Markup.Escape(line)));
        }

        if (snapshot.Logs.Length == 0)
        {
            logTable.AddRow("(no log yet)");
        }

        return new Rows(
            new Panel(metrics).Header("Flue Dashboard", Justify.Left),
            new Panel(logTable).Header("Runtime Logs", Justify.Left));
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

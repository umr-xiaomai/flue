using Flue.Core.Abstractions;
using Flue.Infrastructure.Configuration;
using System.Threading.Channels;

namespace Flue.Infrastructure.FileSystem;

public sealed class FileSystemService(
    FluePaths paths,
    IFlueCompiler compiler,
    PubspecManager pubspecManager) : IAsyncDisposable
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(120);

    private readonly Channel<WatchCommand> commandChannel = Channel.CreateUnbounded<WatchCommand>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly ConcurrentDictionary<string, DateTimeOffset> debounceBook =
        new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? watcher;
    private CancellationTokenSource? runtimeCts;
    private Task? workerTask;
    private int started;

    public event EventHandler<string>? StatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref started, 1) == 1)
        {
            return;
        }

        paths.EnsureBaseDirectories();
        await pubspecManager.EnsureDependenciesAsync(cancellationToken);

        runtimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        workerTask = Task.Run(() => ProcessQueueAsync(runtimeCts.Token), runtimeCts.Token);

        StartWatcher();
        SyncExistingDirectories();
        Enqueue(new FullRebuildCommand());
        PublishStatus("File watcher started.");
    }

    public Task TriggerFullRebuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Enqueue(new FullRebuildCommand());
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref started, 0) == 0)
        {
            return;
        }

        watcher?.Dispose();
        watcher = null;

        if (runtimeCts is not null)
        {
            runtimeCts.Cancel();
        }

        commandChannel.Writer.TryComplete();

        if (workerTask is not null)
        {
            try
            {
                await workerTask;
            }
            catch (OperationCanceledException)
            {
                // Ignore expected cancellation when shutting down.
            }
        }

        runtimeCts?.Dispose();
        runtimeCts = null;

        PublishStatus("File watcher stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private void StartWatcher()
    {
        watcher = new FileSystemWatcher(paths.SourceRoot)
        {
            IncludeSubdirectories = true,
            Filter = "*.*",
            NotifyFilter = NotifyFilters.DirectoryName |
                           NotifyFilters.FileName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.CreationTime
        };

        watcher.Created += OnCreatedOrChanged;
        watcher.Changed += OnCreatedOrChanged;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnWatcherError;
        watcher.EnableRaisingEvents = true;
    }

    private void SyncExistingDirectories()
    {
        Directory.CreateDirectory(paths.SourceRoot);
        Directory.CreateDirectory(paths.DartLibRoot);

        foreach (var sourceDirectory in Directory.EnumerateDirectories(paths.SourceRoot, "*", SearchOption.AllDirectories))
        {
            var targetDirectory = paths.ToDartDirectoryPath(sourceDirectory);
            Directory.CreateDirectory(targetDirectory);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var command in commandChannel.Reader.ReadAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                switch (command)
                {
                    case CompileFileCommand compileFileCommand:
                        await HandleCompileFileAsync(compileFileCommand.SourceFilePath, cancellationToken);
                        break;
                    case DeleteFileCommand deleteFileCommand:
                        HandleDeleteFile(deleteFileCommand.SourceFilePath);
                        break;
                    case EnsureDirectoryCommand ensureDirectoryCommand:
                        HandleEnsureDirectory(ensureDirectoryCommand.SourceDirectoryPath);
                        break;
                    case FullRebuildCommand:
                        await HandleFullRebuildAsync(cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                PublishStatus($"Queue error: {ex.Message}");
            }
        }
    }

    private async Task HandleCompileFileAsync(string sourceFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceFilePath))
        {
            return;
        }

        await WaitForReadAccessAsync(sourceFilePath, cancellationToken);
        var result = await compiler.CompileFileAsync(sourceFilePath, cancellationToken);
        if (!result.Success)
        {
            PublishStatus($"Compile failed: {paths.ToRelativeSourcePath(sourceFilePath)} - {result.ErrorMessage}");
        }
    }

    private void HandleDeleteFile(string sourceFilePath)
    {
        if (!TryMapDartFile(sourceFilePath, out var targetDartFile))
        {
            return;
        }

        if (!File.Exists(targetDartFile))
        {
            return;
        }

        File.Delete(targetDartFile);
        PublishStatus($"Removed: {Path.GetRelativePath(paths.DartLibRoot, targetDartFile)}");
    }

    private void HandleEnsureDirectory(string sourceDirectoryPath)
    {
        if (!TryMapDartDirectory(sourceDirectoryPath, out var targetDartDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDartDirectory);
    }

    private async Task HandleFullRebuildAsync(CancellationToken cancellationToken)
    {
        PublishStatus("Full rebuild started.");
        SyncExistingDirectories();

        var results = await compiler.CompileAllAsync(cancellationToken);
        var successCount = results.Count(result => result.Success);
        PublishStatus($"Full rebuild completed: {successCount}/{results.Count} succeeded.");
    }

    private void OnCreatedOrChanged(object sender, FileSystemEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        if (Directory.Exists(args.FullPath))
        {
            Enqueue(new EnsureDirectoryCommand(args.FullPath));
            return;
        }

        if (!IsVueFile(args.FullPath) || ShouldDebounce(args.FullPath))
        {
            return;
        }

        Enqueue(new CompileFileCommand(args.FullPath));
    }

    private void OnDeleted(object sender, FileSystemEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        if (!IsVueFile(args.FullPath))
        {
            return;
        }

        Enqueue(new DeleteFileCommand(args.FullPath));
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        if (IsVueFile(args.OldFullPath))
        {
            Enqueue(new DeleteFileCommand(args.OldFullPath));
        }

        if (Directory.Exists(args.FullPath))
        {
            Enqueue(new EnsureDirectoryCommand(args.FullPath));
            return;
        }

        if (IsVueFile(args.FullPath))
        {
            Enqueue(new CompileFileCommand(args.FullPath));
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs args)
    {
        PublishStatus($"Watcher error: {args.GetException().Message}");
    }

    private void Enqueue(WatchCommand command)
    {
        commandChannel.Writer.TryWrite(command);
    }

    private bool ShouldDebounce(string sourceFilePath)
    {
        var now = DateTimeOffset.UtcNow;
        if (debounceBook.TryGetValue(sourceFilePath, out var previous) && now - previous < DebounceWindow)
        {
            return true;
        }

        debounceBook[sourceFilePath] = now;
        return false;
    }

    private static bool IsVueFile(string fullPath)
    {
        return fullPath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryMapDartFile(string sourceFilePath, out string targetDartPath)
    {
        targetDartPath = string.Empty;
        if (!IsVueFile(sourceFilePath))
        {
            return false;
        }

        var relative = Path.GetRelativePath(paths.SourceRoot, sourceFilePath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        targetDartPath = Path.Combine(paths.DartLibRoot, Path.ChangeExtension(relative, ".dart")!);
        return true;
    }

    private bool TryMapDartDirectory(string sourceDirectoryPath, out string targetDartDirectory)
    {
        targetDartDirectory = string.Empty;

        var relative = Path.GetRelativePath(paths.SourceRoot, sourceDirectoryPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        targetDartDirectory = Path.Combine(paths.DartLibRoot, relative);
        return true;
    }

    private void PublishStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }

    private static async Task WaitForReadAccessAsync(string filePath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length >= 0)
                {
                    return;
                }
            }
            catch (IOException)
            {
                // File is still being written, wait and retry.
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private abstract record WatchCommand;

    private sealed record CompileFileCommand(string SourceFilePath) : WatchCommand;

    private sealed record DeleteFileCommand(string SourceFilePath) : WatchCommand;

    private sealed record EnsureDirectoryCommand(string SourceDirectoryPath) : WatchCommand;

    private sealed record FullRebuildCommand : WatchCommand;
}

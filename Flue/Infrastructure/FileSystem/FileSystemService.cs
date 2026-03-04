using Flue.Core.Abstractions;
using Flue.Infrastructure.Configuration;
using System.Threading.Channels;

namespace Flue.Infrastructure.FileSystem;

public sealed class FileSystemService (
    FluePaths paths,
    IFlueCompiler compiler,
    PubspecManager pubspecManager,
    IFlueRouterGenerator routerGenerator) : IAsyncDisposable
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
    private readonly ConcurrentDictionary<string, byte> knownSourceDirectories =
        new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? watcher;
    private FileSystemWatcher? rootRouterWatcher;
    private CancellationTokenSource? runtimeCts;
    private Task? workerTask;
    private int started;

    public event EventHandler<string>? StatusChanged;

    public async Task StartAsync (CancellationToken cancellationToken = default)
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

    public Task TriggerFullRebuildAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Enqueue(new FullRebuildCommand());
        return Task.CompletedTask;
    }

    public async Task StopAsync ()
    {
        if (Interlocked.Exchange(ref started, 0) == 0)
        {
            return;
        }

        watcher?.Dispose();
        watcher = null;
        rootRouterWatcher?.Dispose();
        rootRouterWatcher = null;

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

    public async ValueTask DisposeAsync ()
    {
        await StopAsync();
    }

    private void StartWatcher ()
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

        rootRouterWatcher = new FileSystemWatcher(paths.ProjectRoot)
        {
            IncludeSubdirectories = false,
            Filter = "router.ts",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        rootRouterWatcher.Created += OnRootRouterChanged;
        rootRouterWatcher.Changed += OnRootRouterChanged;
        rootRouterWatcher.Deleted += OnRootRouterChanged;
        rootRouterWatcher.Renamed += OnRootRouterRenamed;
        rootRouterWatcher.Error += OnWatcherError;
        rootRouterWatcher.EnableRaisingEvents = true;
    }

    private void SyncExistingDirectories ()
    {
        Directory.CreateDirectory(paths.SourceRoot);
        Directory.CreateDirectory(paths.DartLibRoot);
        knownSourceDirectories.Clear();
        RegisterKnownDirectory(paths.SourceRoot);

        foreach (var sourceDirectory in Directory.EnumerateDirectories(paths.SourceRoot, "*", SearchOption.AllDirectories))
        {
            RegisterKnownDirectory(sourceDirectory);
            var targetDirectory = paths.ToDartDirectoryPath(sourceDirectory);
            Directory.CreateDirectory(targetDirectory);
        }
    }

    private async Task ProcessQueueAsync (CancellationToken cancellationToken)
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
                    case DeleteDirectoryCommand deleteDirectoryCommand:
                        HandleDeleteDirectory(deleteDirectoryCommand.SourceDirectoryPath);
                        break;
                    case EnsureDirectoryCommand ensureDirectoryCommand:
                        HandleEnsureDirectory(ensureDirectoryCommand.SourceDirectoryPath);
                        break;
                    case RenamePathCommand renamePathCommand:
                        await HandleRenameAsync(renamePathCommand, cancellationToken);
                        break;
                    case SyncRouterCommand:
                        await routerGenerator.SyncAsync(cancellationToken);
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

    private async Task HandleCompileFileAsync (string sourceFilePath, CancellationToken cancellationToken)
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

    private void HandleDeleteFile (string sourceFilePath)
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

    private void HandleEnsureDirectory (string sourceDirectoryPath)
    {
        RegisterKnownDirectory(sourceDirectoryPath);

        if (!TryMapDartDirectory(sourceDirectoryPath, out var targetDartDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDartDirectory);
    }

    private void HandleDeleteDirectory (string sourceDirectoryPath)
    {
        UnregisterKnownDirectoryTree(sourceDirectoryPath);

        if (!TryMapDartDirectory(sourceDirectoryPath, out var targetDartDirectory))
        {
            return;
        }

        if (!Directory.Exists(targetDartDirectory))
        {
            return;
        }

        Directory.Delete(targetDartDirectory, recursive: true);
        PublishStatus($"Removed folder: {Path.GetRelativePath(paths.DartLibRoot, targetDartDirectory)}");
    }

    private async Task HandleRenameAsync (RenamePathCommand renamePathCommand, CancellationToken cancellationToken)
    {
        if (renamePathCommand.IsDirectory)
        {
            await HandleDirectoryRenameAsync(renamePathCommand.OldPath, renamePathCommand.NewPath, cancellationToken);
        }
        else
        {
            await HandleFileRenameAsync(renamePathCommand.OldPath, renamePathCommand.NewPath, cancellationToken);
        }

        await routerGenerator.SyncAsync(cancellationToken);
    }

    private async Task HandleFileRenameAsync (string oldSourcePath, string newSourcePath, CancellationToken cancellationToken)
    {
        var wasVueFile = IsVueFile(oldSourcePath);
        var isVueFile = IsVueFile(newSourcePath);

        if (wasVueFile && TryMapDartFile(oldSourcePath, out var oldTargetFile) && File.Exists(oldTargetFile))
        {
            if (isVueFile && TryMapDartFile(newSourcePath, out var newTargetFile))
            {
                if (string.Equals(oldTargetFile, newTargetFile, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(newSourcePath))
                    {
                        await HandleCompileFileAsync(newSourcePath, cancellationToken);
                    }

                    return;
                }

                var targetDirectory = Path.GetDirectoryName(newTargetFile);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                if (File.Exists(newTargetFile))
                {
                    File.Delete(newTargetFile);
                }

                File.Move(oldTargetFile, newTargetFile);
                PublishStatus($"Renamed output: {Path.GetRelativePath(paths.DartLibRoot, oldTargetFile)} -> {Path.GetRelativePath(paths.DartLibRoot, newTargetFile)}");
            }
            else
            {
                File.Delete(oldTargetFile);
                PublishStatus($"Removed: {Path.GetRelativePath(paths.DartLibRoot, oldTargetFile)}");
            }
        }

        if (isVueFile && File.Exists(newSourcePath))
        {
            await HandleCompileFileAsync(newSourcePath, cancellationToken);
        }
    }

    private async Task HandleDirectoryRenameAsync (string oldDirectoryPath, string newDirectoryPath, CancellationToken cancellationToken)
    {
        UpdateKnownDirectoriesOnRename(oldDirectoryPath, newDirectoryPath);

        if (!TryMapDartDirectory(oldDirectoryPath, out var oldTargetDirectory) ||
            !TryMapDartDirectory(newDirectoryPath, out var newTargetDirectory))
        {
            return;
        }

        if (Directory.Exists(oldTargetDirectory))
        {
            if (Directory.Exists(newTargetDirectory))
            {
                MoveDirectoryContents(oldTargetDirectory, newTargetDirectory);
                Directory.Delete(oldTargetDirectory, recursive: true);
            }
            else
            {
                var parent = Path.GetDirectoryName(newTargetDirectory);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                Directory.Move(oldTargetDirectory, newTargetDirectory);
            }

            PublishStatus($"Renamed folder: {Path.GetRelativePath(paths.DartLibRoot, oldTargetDirectory)} -> {Path.GetRelativePath(paths.DartLibRoot, newTargetDirectory)}");
        }
        else
        {
            Directory.CreateDirectory(newTargetDirectory);
        }

        if (!Directory.Exists(newDirectoryPath))
        {
            return;
        }

        foreach (var vueFile in Directory.EnumerateFiles(newDirectoryPath, "*.vue", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await HandleCompileFileAsync(vueFile, cancellationToken);
        }
    }

    private static void MoveDirectoryContents (string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativeDirectory));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(sourceDirectory, sourceFile);
            var targetFile = Path.Combine(targetDirectory, relativeFile);
            var targetParent = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetParent))
            {
                Directory.CreateDirectory(targetParent);
            }

            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }

            File.Move(sourceFile, targetFile);
        }
    }

    private async Task HandleFullRebuildAsync (CancellationToken cancellationToken)
    {
        PublishStatus("Full rebuild started.");
        SyncExistingDirectories();

        var results = await compiler.CompileAllAsync(cancellationToken);
        await routerGenerator.SyncAsync(cancellationToken);
        var successCount = results.Count(result => result.Success);
        PublishStatus($"Full rebuild completed: {successCount}/{results.Count} succeeded.");
    }

    private void OnCreatedOrChanged (object sender, FileSystemEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        if (routerGenerator.IsRouterFile(args.FullPath))
        {
            Enqueue(new SyncRouterCommand());
            return;
        }

        if (Directory.Exists(args.FullPath))
        {
            RegisterKnownDirectory(args.FullPath);
            Enqueue(new EnsureDirectoryCommand(args.FullPath));
            return;
        }

        if (!IsVueFile(args.FullPath) || ShouldDebounce(args.FullPath))
        {
            return;
        }

        Enqueue(new CompileFileCommand(args.FullPath));
    }

    private void OnDeleted (object sender, FileSystemEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        if (routerGenerator.IsRouterFile(args.FullPath))
        {
            Enqueue(new SyncRouterCommand());
            return;
        }

        if (IsKnownSourceDirectory(args.FullPath))
        {
            Enqueue(new DeleteDirectoryCommand(args.FullPath));
            return;
        }

        if (!IsVueFile(args.FullPath))
        {
            return;
        }

        Enqueue(new DeleteFileCommand(args.FullPath));
    }

    private void OnRenamed (object sender, RenamedEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        if (routerGenerator.IsRouterFile(args.OldFullPath) || routerGenerator.IsRouterFile(args.FullPath))
        {
            Enqueue(new SyncRouterCommand());
            if (!IsVueFile(args.OldFullPath) && !IsVueFile(args.FullPath))
            {
                return;
            }
        }

        var isDirectoryRename = IsDirectoryRename(args);
        if (isDirectoryRename)
        {
            UpdateKnownDirectoriesOnRename(args.OldFullPath, args.FullPath);
        }

        Enqueue(new RenamePathCommand(
            args.OldFullPath,
            args.FullPath,
            isDirectoryRename));
    }

    private void OnWatcherError (object sender, ErrorEventArgs args)
    {
        PublishStatus($"Watcher error: {args.GetException().Message}");
    }

    private void OnRootRouterChanged (object sender, FileSystemEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        Enqueue(new SyncRouterCommand());
    }

    private void OnRootRouterRenamed (object sender, RenamedEventArgs args)
    {
        if (runtimeCts?.IsCancellationRequested is true)
        {
            return;
        }

        Enqueue(new SyncRouterCommand());
    }

    private void Enqueue (WatchCommand command)
    {
        commandChannel.Writer.TryWrite(command);
    }

    private bool ShouldDebounce (string sourceFilePath)
    {
        var now = DateTimeOffset.UtcNow;
        if (debounceBook.TryGetValue(sourceFilePath, out var previous) && now - previous < DebounceWindow)
        {
            return true;
        }

        debounceBook[sourceFilePath] = now;
        return false;
    }

    private static bool IsVueFile (string fullPath)
    {
        return fullPath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectoryRename (RenamedEventArgs args)
    {
        if (Directory.Exists(args.FullPath) || Directory.Exists(args.OldFullPath))
        {
            return true;
        }

        if (IsVueFile(args.FullPath) || IsVueFile(args.OldFullPath))
        {
            return false;
        }

        return !Path.HasExtension(args.FullPath) && !Path.HasExtension(args.OldFullPath);
    }

    private bool TryMapDartFile (string sourceFilePath, out string targetDartPath)
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

    private bool TryMapDartDirectory (string sourceDirectoryPath, out string targetDartDirectory)
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

    private bool IsKnownSourceDirectory (string sourcePath)
    {
        var normalized = NormalizePath(sourcePath);
        return knownSourceDirectories.ContainsKey(normalized);
    }

    private void RegisterKnownDirectory (string sourceDirectoryPath)
    {
        var normalized = NormalizePath(sourceDirectoryPath);
        if (normalized.Length == 0)
        {
            return;
        }

        if (Path.GetRelativePath(paths.SourceRoot, normalized).StartsWith("..", StringComparison.Ordinal))
        {
            return;
        }

        knownSourceDirectories[normalized] = 0;
    }

    private void UnregisterKnownDirectoryTree (string sourceDirectoryPath)
    {
        var normalized = NormalizePath(sourceDirectoryPath);
        if (normalized.Length == 0)
        {
            return;
        }

        foreach (var directory in knownSourceDirectories.Keys)
        {
            if (!IsSameOrChildPath(directory, normalized))
            {
                continue;
            }

            knownSourceDirectories.TryRemove(directory, out _);
        }
    }

    private void UpdateKnownDirectoriesOnRename (string oldDirectoryPath, string newDirectoryPath)
    {
        var normalizedOld = NormalizePath(oldDirectoryPath);
        var normalizedNew = NormalizePath(newDirectoryPath);
        if (normalizedOld.Length == 0 || normalizedNew.Length == 0)
        {
            return;
        }

        var affectedDirectories = knownSourceDirectories.Keys
            .Where(directory => IsSameOrChildPath(directory, normalizedOld))
            .ToArray();

        foreach (var oldKnownDirectory in affectedDirectories)
        {
            knownSourceDirectories.TryRemove(oldKnownDirectory, out _);

            var suffix = oldKnownDirectory.Length == normalizedOld.Length
                ? string.Empty
                : oldKnownDirectory[normalizedOld.Length..];
            var renamedDirectory = NormalizePath(normalizedNew + suffix);
            knownSourceDirectories[renamedDirectory] = 0;
        }

        if (affectedDirectories.Length == 0)
        {
            knownSourceDirectories[normalizedNew] = 0;
        }
    }

    private static bool IsSameOrChildPath (string candidatePath, string parentPath)
    {
        if (string.Equals(candidatePath, parentPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return candidatePath.StartsWith(parentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               candidatePath.StartsWith(parentPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath (string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void PublishStatus (string message)
    {
        StatusChanged?.Invoke(this, message);
    }

    private static async Task WaitForReadAccessAsync (string filePath, CancellationToken cancellationToken)
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

    private sealed record CompileFileCommand (string SourceFilePath) : WatchCommand;

    private sealed record DeleteFileCommand (string SourceFilePath) : WatchCommand;

    private sealed record DeleteDirectoryCommand (string SourceDirectoryPath) : WatchCommand;

    private sealed record EnsureDirectoryCommand (string SourceDirectoryPath) : WatchCommand;

    private sealed record RenamePathCommand (string OldPath, string NewPath, bool IsDirectory) : WatchCommand;

    private sealed record SyncRouterCommand : WatchCommand;

    private sealed record FullRebuildCommand : WatchCommand;
}

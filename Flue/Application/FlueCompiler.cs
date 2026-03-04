using Flue.Core.Abstractions;
using Flue.Core.Models;
using Flue.Core.Utilities;
using Flue.Infrastructure.Configuration;

namespace Flue.Application;

public sealed class FlueCompiler (
    FluePaths paths,
    IVueSfcParser sfcParser,
    ITemplateParser templateParser,
    ILogicBridge logicBridge,
    IDartCodeGenerator dartCodeGenerator) : IFlueCompiler
{
    public event EventHandler<CompilationEvent>? CompilationProgress;

    public async Task<CompilationResult> CompileFileAsync (string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var targetFilePath = paths.ToDartFilePath(sourceFilePath);
        PublishEvent(new CompilationEvent(
            CompilationEventType.Started,
            sourceFilePath,
            targetFilePath,
            Success: true,
            DurationMs: 0));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(sourceFilePath))
            {
                stopwatch.Stop();
                var missingResult = new CompilationResult(
                    sourceFilePath,
                    targetFilePath,
                    Success: false,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    ErrorMessage: "Source file was not found.");

                PublishEvent(new CompilationEvent(
                    CompilationEventType.Completed,
                    sourceFilePath,
                    targetFilePath,
                    missingResult.Success,
                    missingResult.DurationMs,
                    missingResult.ErrorMessage));
                return missingResult;
            }

            var source = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);
            var sfcDocument = sfcParser.Parse(source);
            var templateRoot = templateParser.Parse(sfcDocument.Template);
            var logic = logicBridge.Parse(sfcDocument.Script);
            var className = DartNaming.BuildWidgetClassName(sourceFilePath);

            var dartSource = dartCodeGenerator.Generate(
                className,
                templateRoot,
                logic,
                paths.ToRelativeSourcePath(sourceFilePath));

            var outputDirectory = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(targetFilePath, dartSource, cancellationToken);

            stopwatch.Stop();
            var successResult = new CompilationResult(
                sourceFilePath,
                targetFilePath,
                Success: true,
                DurationMs: stopwatch.ElapsedMilliseconds);

            PublishEvent(new CompilationEvent(
                CompilationEventType.Completed,
                sourceFilePath,
                targetFilePath,
                successResult.Success,
                successResult.DurationMs));
            return successResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var failureResult = new CompilationResult(
                sourceFilePath,
                targetFilePath,
                Success: false,
                DurationMs: stopwatch.ElapsedMilliseconds,
                ErrorMessage: ex.Message);

            PublishEvent(new CompilationEvent(
                CompilationEventType.Completed,
                sourceFilePath,
                targetFilePath,
                failureResult.Success,
                failureResult.DurationMs,
                failureResult.ErrorMessage));
            return failureResult;
        }
    }

    public async Task<IReadOnlyList<CompilationResult>> CompileAllAsync (CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.SourceRoot))
        {
            return [];
        }

        var sourceFiles = Directory
            .EnumerateFiles(paths.SourceRoot, "*.vue", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<CompilationResult>(sourceFiles.Length);
        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await CompileFileAsync(sourceFile, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private void PublishEvent (CompilationEvent compilationEvent)
    {
        CompilationProgress?.Invoke(this, compilationEvent);
    }
}

using Flue.Core.Models;

namespace Flue.Core.Abstractions;

public interface IFlueCompiler
{
    event EventHandler<CompilationEvent>? CompilationProgress;

    Task<CompilationResult> CompileFileAsync (string sourceFilePath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompilationResult>> CompileAllAsync (CancellationToken cancellationToken = default);
}

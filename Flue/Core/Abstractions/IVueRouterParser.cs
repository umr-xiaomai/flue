using Flue.Core.Models;

namespace Flue.Core.Abstractions;

public interface IVueRouterParser
{
    Task<FlueRouterManifest?> ParseAsync (CancellationToken cancellationToken = default);

    bool IsRouterFile (string path);
}

namespace Flue.Core.Abstractions;

public interface IFlueRouterGenerator
{
    Task SyncAsync (CancellationToken cancellationToken = default);

    bool IsRouterFile (string path);
}

using Flue.Core.Models;

namespace Flue.Core.Abstractions;

public interface ILogicBridge
{
    LogicBridgeResult Parse(string scriptContent);
}

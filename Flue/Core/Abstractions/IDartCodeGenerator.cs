using Flue.Core.Models;

namespace Flue.Core.Abstractions;

public interface IDartCodeGenerator
{
    string Generate (string className, TemplateNode templateRoot, LogicBridgeResult logicBridgeResult, string sourceRelativePath);
}

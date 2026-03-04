using Flue.Core.Models;

namespace Flue.Core.Abstractions;

public interface IVueSfcParser
{
    VueSfcDocument Parse(string source);
}

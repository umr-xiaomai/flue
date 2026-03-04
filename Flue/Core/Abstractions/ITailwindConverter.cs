using Flue.Core.Models;

namespace Flue.Core.Abstractions;

public interface ITailwindConverter
{
    TailwindStyle Convert(IEnumerable<string> classNames);
}

using Flue.Core.Models;

namespace Flue.Core.Abstractions;

public interface ITemplateParser
{
    TemplateNode Parse(string templateContent);
}

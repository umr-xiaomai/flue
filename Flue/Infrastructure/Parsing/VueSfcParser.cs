using Flue.Core.Abstractions;
using Flue.Core.Models;

namespace Flue.Infrastructure.Parsing;

public sealed partial class VueSfcParser : IVueSfcParser
{
    [GeneratedRegex("<template[^>]*>(?<content>[\\s\\S]*?)</template>", RegexOptions.IgnoreCase)]
    private static partial Regex TemplateRegex();

    [GeneratedRegex("<script(?=[^>]*\\blang\\s*=\\s*[\"']ts[\"'])[^>]*>(?<content>[\\s\\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptRegex();

    public VueSfcDocument Parse(string source)
    {
        var template = ExtractContent(TemplateRegex(), source);
        var script = ExtractContent(ScriptRegex(), source);
        return new VueSfcDocument(template, script);
    }

    private static string ExtractContent(Regex regex, string source)
    {
        var match = regex.Match(source);
        if (!match.Success)
        {
            return string.Empty;
        }

        return match.Groups["content"].Value.Trim();
    }
}

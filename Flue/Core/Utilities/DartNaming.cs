namespace Flue.Core.Utilities;

public static class DartNaming
{
    public static string BuildWidgetClassName (string sourcePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var parts = Regex.Split(fileName, "[^a-zA-Z0-9]+")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]);

        var baseName = string.Concat(parts);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "FlueView";
        }

        if (char.IsDigit(baseName[0]))
        {
            baseName = "F" + baseName;
        }

        return baseName.EndsWith("View", StringComparison.Ordinal) ? baseName : baseName + "View";
    }
}

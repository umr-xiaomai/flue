using Flue.Core.Abstractions;
using Flue.Core.Models;

namespace Flue.Infrastructure.Styling;

public sealed class TailwindConverter : ITailwindConverter
{
    private static readonly FrozenDictionary<string, string> WidgetPropertyMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["p-0"] = "padding: EdgeInsets.zero",
        ["p-1"] = "padding: const EdgeInsets.all(4.0)",
        ["p-2"] = "padding: const EdgeInsets.all(8.0)",
        ["p-3"] = "padding: const EdgeInsets.all(12.0)",
        ["p-4"] = "padding: const EdgeInsets.all(16.0)",
        ["p-6"] = "padding: const EdgeInsets.all(24.0)",
        ["px-4"] = "padding: const EdgeInsets.symmetric(horizontal: 16.0)",
        ["py-4"] = "padding: const EdgeInsets.symmetric(vertical: 16.0)",
        ["m-0"] = "margin: EdgeInsets.zero",
        ["m-2"] = "margin: const EdgeInsets.all(8.0)",
        ["m-4"] = "margin: const EdgeInsets.all(16.0)",
        ["w-full"] = "width: double.infinity",
        ["h-full"] = "height: double.infinity"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> BackgroundColorMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bg-blue-500"] = "0xFF3B82F6",
        ["bg-red-500"] = "0xFFEF4444",
        ["bg-green-500"] = "0xFF22C55E",
        ["bg-gray-100"] = "0xFFF3F4F6",
        ["bg-white"] = "0xFFFFFFFF",
        ["bg-black"] = "0xFF000000"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> BorderRadiusMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["rounded"] = "borderRadius: BorderRadius.circular(4.0)",
        ["rounded-md"] = "borderRadius: BorderRadius.circular(6.0)",
        ["rounded-lg"] = "borderRadius: BorderRadius.circular(8.0)",
        ["rounded-xl"] = "borderRadius: BorderRadius.circular(12.0)",
        ["rounded-2xl"] = "borderRadius: BorderRadius.circular(16.0)",
        ["rounded-full"] = "borderRadius: BorderRadius.circular(999.0)"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> TextStyleMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["text-white"] = "color: Color(0xFFFFFFFF)",
        ["text-black"] = "color: Color(0xFF000000)",
        ["text-blue-500"] = "color: Color(0xFF3B82F6)",
        ["text-sm"] = "fontSize: 14.0",
        ["text-base"] = "fontSize: 16.0",
        ["text-lg"] = "fontSize: 18.0",
        ["text-xl"] = "fontSize: 20.0",
        ["font-medium"] = "fontWeight: FontWeight.w500",
        ["font-semibold"] = "fontWeight: FontWeight.w600",
        ["font-bold"] = "fontWeight: FontWeight.w700"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> MainAxisAlignmentMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["justify-start"] = "MainAxisAlignment.start",
        ["justify-center"] = "MainAxisAlignment.center",
        ["justify-end"] = "MainAxisAlignment.end",
        ["justify-between"] = "MainAxisAlignment.spaceBetween",
        ["justify-around"] = "MainAxisAlignment.spaceAround",
        ["justify-evenly"] = "MainAxisAlignment.spaceEvenly"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> CrossAxisAlignmentMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["items-start"] = "CrossAxisAlignment.start",
        ["items-center"] = "CrossAxisAlignment.center",
        ["items-end"] = "CrossAxisAlignment.end",
        ["items-stretch"] = "CrossAxisAlignment.stretch"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public TailwindStyle Convert(IEnumerable<string> classNames)
    {
        var widgetProperties = new HashSet<string>(StringComparer.Ordinal);
        var decorationProperties = new HashSet<string>(StringComparer.Ordinal);
        var textStyleProperties = new HashSet<string>(StringComparer.Ordinal);
        string? mainAxisAlignment = null;
        string? crossAxisAlignment = null;

        foreach (var className in classNames)
        {
            if (WidgetPropertyMap.TryGetValue(className, out var widgetProperty))
            {
                widgetProperties.Add(widgetProperty);
            }

            if (BackgroundColorMap.TryGetValue(className, out var backgroundColor))
            {
                decorationProperties.Add($"color: const Color({backgroundColor})");
            }

            if (BorderRadiusMap.TryGetValue(className, out var borderRadius))
            {
                decorationProperties.Add(borderRadius);
            }

            if (TextStyleMap.TryGetValue(className, out var textStyle))
            {
                textStyleProperties.Add(textStyle);
            }

            if (MainAxisAlignmentMap.TryGetValue(className, out var mainAxis))
            {
                mainAxisAlignment = mainAxis;
            }

            if (CrossAxisAlignmentMap.TryGetValue(className, out var crossAxis))
            {
                crossAxisAlignment = crossAxis;
            }
        }

        return new TailwindStyle(
            [.. widgetProperties],
            [.. decorationProperties],
            [.. textStyleProperties],
            mainAxisAlignment,
            crossAxisAlignment);
    }
}

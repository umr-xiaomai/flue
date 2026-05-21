using Flue.Core.Abstractions;
using Flue.Core.Models;

namespace Flue.Infrastructure.Styling;

public sealed class TailwindConverter : ITailwindConverter
{
    private static readonly FrozenDictionary<string, string> ColorHexMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Slate
        ["slate-50"] = "0xFFF8FAFC", ["slate-100"] = "0xFFF1F5F9", ["slate-200"] = "0xFFE2E8F0",
        ["slate-300"] = "0xFFCBD5E1", ["slate-400"] = "0xFF94A3B8", ["slate-500"] = "0xFF64748B",
        ["slate-600"] = "0xFF475569", ["slate-700"] = "0xFF334155", ["slate-800"] = "0xFF1E293B",
        ["slate-900"] = "0xFF0F172A",
        // Gray
        ["gray-50"] = "0xFFF9FAFB", ["gray-100"] = "0xFFF3F4F6", ["gray-200"] = "0xFFE5E7EB",
        ["gray-300"] = "0xFFD1D5DB", ["gray-400"] = "0xFF9CA3AF", ["gray-500"] = "0xFF6B7280",
        ["gray-600"] = "0xFF4B5563", ["gray-700"] = "0xFF374151", ["gray-800"] = "0xFF1F2937",
        ["gray-900"] = "0xFF111827",
        // Zinc
        ["zinc-50"] = "0xFFFAFAFA", ["zinc-100"] = "0xFFF4F4F5", ["zinc-200"] = "0xFFE4E4E7",
        ["zinc-300"] = "0xFFD4D4D8", ["zinc-400"] = "0xFFA1A1AA", ["zinc-500"] = "0xFF71717A",
        ["zinc-600"] = "0xFF52525B", ["zinc-700"] = "0xFF3F3F46", ["zinc-800"] = "0xFF27272A",
        ["zinc-900"] = "0xFF18181B",
        // Neutral
        ["neutral-50"] = "0xFFFAFAFA", ["neutral-100"] = "0xFFF5F5F5", ["neutral-200"] = "0xFFE5E5E5",
        ["neutral-300"] = "0xFFD4D4D4", ["neutral-400"] = "0xFFA3A3A3", ["neutral-500"] = "0xFF737373",
        ["neutral-600"] = "0xFF525252", ["neutral-700"] = "0xFF404040", ["neutral-800"] = "0xFF262626",
        ["neutral-900"] = "0xFF171717",
        // Stone
        ["stone-50"] = "0xFFFAFAF9", ["stone-100"] = "0xFFF5F5F4", ["stone-200"] = "0xFFE7E5E4",
        ["stone-300"] = "0xFFD6D3D1", ["stone-400"] = "0xFFA8A29E", ["stone-500"] = "0xFF78716C",
        ["stone-600"] = "0xFF57534E", ["stone-700"] = "0xFF44403C", ["stone-800"] = "0xFF292524",
        ["stone-900"] = "0xFF1C1917",
        // Red
        ["red-50"] = "0xFFFEF2F2", ["red-100"] = "0xFFFEE2E2", ["red-200"] = "0xFFFECACA",
        ["red-300"] = "0xFFFCA5A5", ["red-400"] = "0xFFF87171", ["red-500"] = "0xFFEF4444",
        ["red-600"] = "0xFFDC2626", ["red-700"] = "0xFFB91C1C", ["red-800"] = "0xFF991B1B",
        ["red-900"] = "0xFF7F1D1D",
        // Orange
        ["orange-50"] = "0xFFFFF7ED", ["orange-100"] = "0xFFFFEDD5", ["orange-200"] = "0xFFFED7AA",
        ["orange-300"] = "0xFFFDBA74", ["orange-400"] = "0xFFFB923C", ["orange-500"] = "0xFFF97316",
        ["orange-600"] = "0xFFEA580C", ["orange-700"] = "0xFFC2410C", ["orange-800"] = "0xFF9A3412",
        ["orange-900"] = "0xFF7C2D12",
        // Amber
        ["amber-50"] = "0xFFFFFBEB", ["amber-100"] = "0xFFFEF3C7", ["amber-200"] = "0xFFFDE68A",
        ["amber-300"] = "0xFFFCD34D", ["amber-400"] = "0xFFFBBF24", ["amber-500"] = "0xFFF59E0B",
        ["amber-600"] = "0xFFD97706", ["amber-700"] = "0xFFB45309", ["amber-800"] = "0xFF92400E",
        ["amber-900"] = "0xFF78350F",
        // Yellow
        ["yellow-50"] = "0xFFFEFCE8", ["yellow-100"] = "0xFFFEF9C3", ["yellow-200"] = "0xFFFEF08A",
        ["yellow-300"] = "0xFFFDE047", ["yellow-400"] = "0xFFFACC15", ["yellow-500"] = "0xFFEAB308",
        ["yellow-600"] = "0xFFCA8A04", ["yellow-700"] = "0xFFA16207", ["yellow-800"] = "0xFF854D0E",
        ["yellow-900"] = "0xFF713F12",
        // Lime
        ["lime-50"] = "0xFFF7FEE7", ["lime-100"] = "0xFFECFCCB", ["lime-200"] = "0xFFD9F99D",
        ["lime-300"] = "0xFFBEF264", ["lime-400"] = "0xFFA3E635", ["lime-500"] = "0xFF84CC16",
        ["lime-600"] = "0xFF65A30D", ["lime-700"] = "0xFF4D7C0F", ["lime-800"] = "0xFF3F6212",
        ["lime-900"] = "0xFF365314",
        // Green
        ["green-50"] = "0xFFF0FDF4", ["green-100"] = "0xFFDCFCE7", ["green-200"] = "0xFFBBF7D0",
        ["green-300"] = "0xFF86EFAC", ["green-400"] = "0xFF4ADE80", ["green-500"] = "0xFF22C55E",
        ["green-600"] = "0xFF16A34A", ["green-700"] = "0xFF15803D", ["green-800"] = "0xFF166534",
        ["green-900"] = "0xFF14532D",
        // Emerald
        ["emerald-50"] = "0xFFECFDF5", ["emerald-100"] = "0xFFD1FAE5", ["emerald-200"] = "0xFFA7F3D0",
        ["emerald-300"] = "0xFF6EE7B7", ["emerald-400"] = "0xFF34D399", ["emerald-500"] = "0xFF10B981",
        ["emerald-600"] = "0xFF059669", ["emerald-700"] = "0xFF047857", ["emerald-800"] = "0xFF065F46",
        ["emerald-900"] = "0xFF064E3B",
        // Teal
        ["teal-50"] = "0xFFF0FDFA", ["teal-100"] = "0xFFCCFBF1", ["teal-200"] = "0xFF99F6E4",
        ["teal-300"] = "0xFF5EEAD4", ["teal-400"] = "0xFF2DD4BF", ["teal-500"] = "0xFF14B8A6",
        ["teal-600"] = "0xFF0D9488", ["teal-700"] = "0xFF0F766E", ["teal-800"] = "0xFF115E59",
        ["teal-900"] = "0xFF134E4A",
        // Cyan
        ["cyan-50"] = "0xFFECFEFF", ["cyan-100"] = "0xFFCFFAFE", ["cyan-200"] = "0xFFA5F3FC",
        ["cyan-300"] = "0xFF67E8F9", ["cyan-400"] = "0xFF22D3EE", ["cyan-500"] = "0xFF06B6D4",
        ["cyan-600"] = "0xFF0891B2", ["cyan-700"] = "0xFF0E7490", ["cyan-800"] = "0xFF155E75",
        ["cyan-900"] = "0xFF164E63",
        // Sky
        ["sky-50"] = "0xFFF0F9FF", ["sky-100"] = "0xFFE0F2FE", ["sky-200"] = "0xFFBAE6FD",
        ["sky-300"] = "0xFF7DD3FC", ["sky-400"] = "0xFF38BDF8", ["sky-500"] = "0xFF0EA5E9",
        ["sky-600"] = "0xFF0284C7", ["sky-700"] = "0xFF0369A1", ["sky-800"] = "0xFF075985",
        ["sky-900"] = "0xFF0C4A6E",
        // Blue
        ["blue-50"] = "0xFFEFF6FF", ["blue-100"] = "0xFFDBEAFE", ["blue-200"] = "0xFFBFDBFE",
        ["blue-300"] = "0xFF93C5FD", ["blue-400"] = "0xFF60A5FA", ["blue-500"] = "0xFF3B82F6",
        ["blue-600"] = "0xFF2563EB", ["blue-700"] = "0xFF1D4ED8", ["blue-800"] = "0xFF1E40AF",
        ["blue-900"] = "0xFF1E3A8A",
        // Indigo
        ["indigo-50"] = "0xFFEEF2FF", ["indigo-100"] = "0xFFE0E7FF", ["indigo-200"] = "0xFFC7D2FE",
        ["indigo-300"] = "0xFFA5B4FC", ["indigo-400"] = "0xFF818CF8", ["indigo-500"] = "0xFF6366F1",
        ["indigo-600"] = "0xFF4F46E5", ["indigo-700"] = "0xFF4338CA", ["indigo-800"] = "0xFF3730A3",
        ["indigo-900"] = "0xFF312E81",
        // Violet
        ["violet-50"] = "0xFFF5F3FF", ["violet-100"] = "0xFFEDE9FE", ["violet-200"] = "0xFFDDD6FE",
        ["violet-300"] = "0xFFC4B5FD", ["violet-400"] = "0xFFA78BFA", ["violet-500"] = "0xFF8B5CF6",
        ["violet-600"] = "0xFF7C3AED", ["violet-700"] = "0xFF6D28D9", ["violet-800"] = "0xFF5B21B6",
        ["violet-900"] = "0xFF4C1D95",
        // Purple
        ["purple-50"] = "0xFFFAF5FF", ["purple-100"] = "0xFFF3E8FF", ["purple-200"] = "0xFFE9D5FF",
        ["purple-300"] = "0xFFD8B4FE", ["purple-400"] = "0xFFC084FC", ["purple-500"] = "0xFFA855F7",
        ["purple-600"] = "0xFF9333EA", ["purple-700"] = "0xFF7E22CE", ["purple-800"] = "0xFF6B21A8",
        ["purple-900"] = "0xFF581C87",
        // Fuchsia
        ["fuchsia-50"] = "0xFFFDF4FF", ["fuchsia-100"] = "0xFFFAE8FF", ["fuchsia-200"] = "0xFFF5D0FE",
        ["fuchsia-300"] = "0xFFF0ABFC", ["fuchsia-400"] = "0xFFE879F9", ["fuchsia-500"] = "0xFFD946EF",
        ["fuchsia-600"] = "0xFFC026D3", ["fuchsia-700"] = "0xFFA21CAF", ["fuchsia-800"] = "0xFF86198F",
        ["fuchsia-900"] = "0xFF701A75",
        // Pink
        ["pink-50"] = "0xFFFDF2F8", ["pink-100"] = "0xFFFCE7F3", ["pink-200"] = "0xFFFBCFE8",
        ["pink-300"] = "0xFFF9A8D4", ["pink-400"] = "0xFFF472B6", ["pink-500"] = "0xFFEC4899",
        ["pink-600"] = "0xFFDB2777", ["pink-700"] = "0xFFBE185D", ["pink-800"] = "0xFF9D174D",
        ["pink-900"] = "0xFF831843",
        // Rose
        ["rose-50"] = "0xFFFFF1F2", ["rose-100"] = "0xFFFFE4E6", ["rose-200"] = "0xFFFECDD3",
        ["rose-300"] = "0xFFFDA4AF", ["rose-400"] = "0xFFFB7185", ["rose-500"] = "0xFFF43F5E",
        ["rose-600"] = "0xFFE11D48", ["rose-700"] = "0xFFBE123C", ["rose-800"] = "0xFF9F1239",
        ["rose-900"] = "0xFF881337",
        // White / Black / Transparent
        ["white"] = "0xFFFFFFFF",
        ["black"] = "0xFF000000",
        ["transparent"] = "0x00000000",
        ["current"] = "0x00000000",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> SpacingMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["p-0"] = "padding: EdgeInsets.zero",
        ["p-px"] = "padding: const EdgeInsets.all(1.0)",
        ["p-0.5"] = "padding: const EdgeInsets.all(2.0)",
        ["p-1"] = "padding: const EdgeInsets.all(4.0)",
        ["p-1.5"] = "padding: const EdgeInsets.all(6.0)",
        ["p-2"] = "padding: const EdgeInsets.all(8.0)",
        ["p-2.5"] = "padding: const EdgeInsets.all(10.0)",
        ["p-3"] = "padding: const EdgeInsets.all(12.0)",
        ["p-3.5"] = "padding: const EdgeInsets.all(14.0)",
        ["p-4"] = "padding: const EdgeInsets.all(16.0)",
        ["p-5"] = "padding: const EdgeInsets.all(20.0)",
        ["p-6"] = "padding: const EdgeInsets.all(24.0)",
        ["p-7"] = "padding: const EdgeInsets.all(28.0)",
        ["p-8"] = "padding: const EdgeInsets.all(32.0)",
        ["p-9"] = "padding: const EdgeInsets.all(36.0)",
        ["p-10"] = "padding: const EdgeInsets.all(40.0)",
        ["p-11"] = "padding: const EdgeInsets.all(44.0)",
        ["p-12"] = "padding: const EdgeInsets.all(48.0)",
        ["p-14"] = "padding: const EdgeInsets.all(56.0)",
        ["p-16"] = "padding: const EdgeInsets.all(64.0)",
        ["p-20"] = "padding: const EdgeInsets.all(80.0)",
        ["p-24"] = "padding: const EdgeInsets.all(96.0)",
        ["px-0"] = "padding: const EdgeInsets.symmetric(horizontal: 0.0)",
        ["px-px"] = "padding: const EdgeInsets.symmetric(horizontal: 1.0)",
        ["px-1"] = "padding: const EdgeInsets.symmetric(horizontal: 4.0)",
        ["px-2"] = "padding: const EdgeInsets.symmetric(horizontal: 8.0)",
        ["px-3"] = "padding: const EdgeInsets.symmetric(horizontal: 12.0)",
        ["px-4"] = "padding: const EdgeInsets.symmetric(horizontal: 16.0)",
        ["px-5"] = "padding: const EdgeInsets.symmetric(horizontal: 20.0)",
        ["px-6"] = "padding: const EdgeInsets.symmetric(horizontal: 24.0)",
        ["px-8"] = "padding: const EdgeInsets.symmetric(horizontal: 32.0)",
        ["px-10"] = "padding: const EdgeInsets.symmetric(horizontal: 40.0)",
        ["px-12"] = "padding: const EdgeInsets.symmetric(horizontal: 48.0)",
        ["py-0"] = "padding: const EdgeInsets.symmetric(vertical: 0.0)",
        ["py-px"] = "padding: const EdgeInsets.symmetric(vertical: 1.0)",
        ["py-1"] = "padding: const EdgeInsets.symmetric(vertical: 4.0)",
        ["py-2"] = "padding: const EdgeInsets.symmetric(vertical: 8.0)",
        ["py-3"] = "padding: const EdgeInsets.symmetric(vertical: 12.0)",
        ["py-4"] = "padding: const EdgeInsets.symmetric(vertical: 16.0)",
        ["py-5"] = "padding: const EdgeInsets.symmetric(vertical: 20.0)",
        ["py-6"] = "padding: const EdgeInsets.symmetric(vertical: 24.0)",
        ["py-8"] = "padding: const EdgeInsets.symmetric(vertical: 32.0)",
        ["py-10"] = "padding: const EdgeInsets.symmetric(vertical: 40.0)",
        ["py-12"] = "padding: const EdgeInsets.symmetric(vertical: 48.0)",
        ["pt-1"] = "padding: const EdgeInsets.only(top: 4.0)",
        ["pt-2"] = "padding: const EdgeInsets.only(top: 8.0)",
        ["pt-3"] = "padding: const EdgeInsets.only(top: 12.0)",
        ["pt-4"] = "padding: const EdgeInsets.only(top: 16.0)",
        ["pt-6"] = "padding: const EdgeInsets.only(top: 24.0)",
        ["pt-8"] = "padding: const EdgeInsets.only(top: 32.0)",
        ["pb-1"] = "padding: const EdgeInsets.only(bottom: 4.0)",
        ["pb-2"] = "padding: const EdgeInsets.only(bottom: 8.0)",
        ["pb-3"] = "padding: const EdgeInsets.only(bottom: 12.0)",
        ["pb-4"] = "padding: const EdgeInsets.only(bottom: 16.0)",
        ["pb-6"] = "padding: const EdgeInsets.only(bottom: 24.0)",
        ["pb-8"] = "padding: const EdgeInsets.only(bottom: 32.0)",
        ["pl-1"] = "padding: const EdgeInsets.only(left: 4.0)",
        ["pl-2"] = "padding: const EdgeInsets.only(left: 8.0)",
        ["pl-3"] = "padding: const EdgeInsets.only(left: 12.0)",
        ["pl-4"] = "padding: const EdgeInsets.only(left: 16.0)",
        ["pl-6"] = "padding: const EdgeInsets.only(left: 24.0)",
        ["pl-8"] = "padding: const EdgeInsets.only(left: 32.0)",
        ["pr-1"] = "padding: const EdgeInsets.only(right: 4.0)",
        ["pr-2"] = "padding: const EdgeInsets.only(right: 8.0)",
        ["pr-3"] = "padding: const EdgeInsets.only(right: 12.0)",
        ["pr-4"] = "padding: const EdgeInsets.only(right: 16.0)",
        ["pr-6"] = "padding: const EdgeInsets.only(right: 24.0)",
        ["pr-8"] = "padding: const EdgeInsets.only(right: 32.0)",
        ["m-0"] = "margin: EdgeInsets.zero",
        ["m-1"] = "margin: const EdgeInsets.all(4.0)",
        ["m-2"] = "margin: const EdgeInsets.all(8.0)",
        ["m-3"] = "margin: const EdgeInsets.all(12.0)",
        ["m-4"] = "margin: const EdgeInsets.all(16.0)",
        ["m-5"] = "margin: const EdgeInsets.all(20.0)",
        ["m-6"] = "margin: const EdgeInsets.all(24.0)",
        ["m-8"] = "margin: const EdgeInsets.all(32.0)",
        ["m-10"] = "margin: const EdgeInsets.all(40.0)",
        ["m-12"] = "margin: const EdgeInsets.all(48.0)",
        ["mx-1"] = "margin: const EdgeInsets.symmetric(horizontal: 4.0)",
        ["mx-2"] = "margin: const EdgeInsets.symmetric(horizontal: 8.0)",
        ["mx-3"] = "margin: const EdgeInsets.symmetric(horizontal: 12.0)",
        ["mx-4"] = "margin: const EdgeInsets.symmetric(horizontal: 16.0)",
        ["mx-6"] = "margin: const EdgeInsets.symmetric(horizontal: 24.0)",
        ["mx-8"] = "margin: const EdgeInsets.symmetric(horizontal: 32.0)",
        ["my-1"] = "margin: const EdgeInsets.symmetric(vertical: 4.0)",
        ["my-2"] = "margin: const EdgeInsets.symmetric(vertical: 8.0)",
        ["my-3"] = "margin: const EdgeInsets.symmetric(vertical: 12.0)",
        ["my-4"] = "margin: const EdgeInsets.symmetric(vertical: 16.0)",
        ["my-6"] = "margin: const EdgeInsets.symmetric(vertical: 24.0)",
        ["my-8"] = "margin: const EdgeInsets.symmetric(vertical: 32.0)",
        ["mt-1"] = "margin: const EdgeInsets.only(top: 4.0)",
        ["mt-2"] = "margin: const EdgeInsets.only(top: 8.0)",
        ["mt-3"] = "margin: const EdgeInsets.only(top: 12.0)",
        ["mt-4"] = "margin: const EdgeInsets.only(top: 16.0)",
        ["mt-6"] = "margin: const EdgeInsets.only(top: 24.0)",
        ["mt-8"] = "margin: const EdgeInsets.only(top: 32.0)",
        ["mb-1"] = "margin: const EdgeInsets.only(bottom: 4.0)",
        ["mb-2"] = "margin: const EdgeInsets.only(bottom: 8.0)",
        ["mb-3"] = "margin: const EdgeInsets.only(bottom: 12.0)",
        ["mb-4"] = "margin: const EdgeInsets.only(bottom: 16.0)",
        ["mb-6"] = "margin: const EdgeInsets.only(bottom: 24.0)",
        ["mb-8"] = "margin: const EdgeInsets.only(bottom: 32.0)",
        ["ml-1"] = "margin: const EdgeInsets.only(left: 4.0)",
        ["ml-2"] = "margin: const EdgeInsets.only(left: 8.0)",
        ["ml-3"] = "margin: const EdgeInsets.only(left: 12.0)",
        ["ml-4"] = "margin: const EdgeInsets.only(left: 16.0)",
        ["mr-1"] = "margin: const EdgeInsets.only(right: 4.0)",
        ["mr-2"] = "margin: const EdgeInsets.only(right: 8.0)",
        ["mr-3"] = "margin: const EdgeInsets.only(right: 12.0)",
        ["mr-4"] = "margin: const EdgeInsets.only(right: 16.0)",
        ["gap-0"] = "gap: 0.0",
        ["gap-1"] = "gap: 4.0",
        ["gap-2"] = "gap: 8.0",
        ["gap-3"] = "gap: 12.0",
        ["gap-4"] = "gap: 16.0",
        ["gap-5"] = "gap: 20.0",
        ["gap-6"] = "gap: 24.0",
        ["gap-8"] = "gap: 32.0",
        ["gap-10"] = "gap: 40.0",
        ["gap-12"] = "gap: 48.0",
        ["gap-x-1"] = "horizontalGap: 4.0",
        ["gap-x-2"] = "horizontalGap: 8.0",
        ["gap-x-3"] = "horizontalGap: 12.0",
        ["gap-x-4"] = "horizontalGap: 16.0",
        ["gap-x-6"] = "horizontalGap: 24.0",
        ["gap-x-8"] = "horizontalGap: 32.0",
        ["gap-y-1"] = "verticalGap: 4.0",
        ["gap-y-2"] = "verticalGap: 8.0",
        ["gap-y-3"] = "verticalGap: 12.0",
        ["gap-y-4"] = "verticalGap: 16.0",
        ["gap-y-6"] = "verticalGap: 24.0",
        ["gap-y-8"] = "verticalGap: 32.0",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> SizeMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["w-full"] = "width: double.infinity",
        ["w-screen"] = "width: double.infinity",
        ["w-1/2"] = "width: MediaQuery.of(context).size.width * 0.5",
        ["w-1/3"] = "width: MediaQuery.of(context).size.width / 3",
        ["w-2/3"] = "width: MediaQuery.of(context).size.width * 2 / 3",
        ["w-1/4"] = "width: MediaQuery.of(context).size.width * 0.25",
        ["w-3/4"] = "width: MediaQuery.of(context).size.width * 0.75",
        ["h-full"] = "height: double.infinity",
        ["h-screen"] = "height: double.infinity",
        ["h-1/2"] = "height: MediaQuery.of(context).size.height * 0.5",
        ["h-1/3"] = "height: MediaQuery.of(context).size.height / 3",
        ["h-2/3"] = "height: MediaQuery.of(context).size.height * 2 / 3",
        ["h-1/4"] = "height: MediaQuery.of(context).size.height * 0.25",
        ["h-3/4"] = "height: MediaQuery.of(context).size.height * 0.75",
        ["size-4"] = "width: 16.0, height: 16.0",
        ["size-5"] = "width: 20.0, height: 20.0",
        ["size-6"] = "width: 24.0, height: 24.0",
        ["size-8"] = "width: 32.0, height: 32.0",
        ["size-10"] = "width: 40.0, height: 40.0",
        ["size-12"] = "width: 48.0, height: 48.0",
        ["size-16"] = "width: 64.0, height: 64.0",
        ["size-20"] = "width: 80.0, height: 80.0",
        ["size-24"] = "width: 96.0, height: 96.0",
        ["size-32"] = "width: 128.0, height: 128.0",
        ["size-40"] = "width: 160.0, height: 160.0",
        ["w-4"] = "width: 16.0",
        ["w-5"] = "width: 20.0",
        ["w-6"] = "width: 24.0",
        ["w-8"] = "width: 32.0",
        ["w-10"] = "width: 40.0",
        ["w-12"] = "width: 48.0",
        ["w-16"] = "width: 64.0",
        ["w-20"] = "width: 80.0",
        ["w-24"] = "width: 96.0",
        ["w-32"] = "width: 128.0",
        ["w-40"] = "width: 160.0",
        ["w-48"] = "width: 192.0",
        ["w-56"] = "width: 224.0",
        ["w-64"] = "width: 256.0",
        ["w-80"] = "width: 320.0",
        ["w-96"] = "width: 384.0",
        ["h-4"] = "height: 16.0",
        ["h-5"] = "height: 20.0",
        ["h-6"] = "height: 24.0",
        ["h-8"] = "height: 32.0",
        ["h-10"] = "height: 40.0",
        ["h-12"] = "height: 48.0",
        ["h-16"] = "height: 64.0",
        ["h-20"] = "height: 80.0",
        ["h-24"] = "height: 96.0",
        ["h-32"] = "height: 128.0",
        ["h-40"] = "height: 160.0",
        ["h-48"] = "height: 192.0",
        ["h-56"] = "height: 224.0",
        ["h-64"] = "height: 256.0",
        ["h-80"] = "height: 320.0",
        ["h-96"] = "height: 384.0",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> BorderRadiusMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["rounded-none"] = "borderRadius: BorderRadius.zero",
        ["rounded-sm"] = "borderRadius: BorderRadius.circular(2.0)",
        ["rounded"] = "borderRadius: BorderRadius.circular(4.0)",
        ["rounded-md"] = "borderRadius: BorderRadius.circular(6.0)",
        ["rounded-lg"] = "borderRadius: BorderRadius.circular(8.0)",
        ["rounded-xl"] = "borderRadius: BorderRadius.circular(12.0)",
        ["rounded-2xl"] = "borderRadius: BorderRadius.circular(16.0)",
        ["rounded-3xl"] = "borderRadius: BorderRadius.circular(24.0)",
        ["rounded-full"] = "borderRadius: BorderRadius.circular(999.0)",
        ["rounded-t-lg"] = "borderRadius: const BorderRadius.vertical(top: Radius.circular(8.0))",
        ["rounded-b-lg"] = "borderRadius: const BorderRadius.vertical(bottom: Radius.circular(8.0))",
        ["rounded-l-lg"] = "borderRadius: const BorderRadius.horizontal(left: Radius.circular(8.0))",
        ["rounded-r-lg"] = "borderRadius: const BorderRadius.horizontal(right: Radius.circular(8.0))",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> BorderMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["border"] = "border: Border.all()",
        ["border-0"] = "border: Border.all(width: 0.0)",
        ["border-2"] = "border: Border.all(width: 2.0)",
        ["border-4"] = "border: Border.all(width: 4.0)",
        ["border-8"] = "border: Border.all(width: 8.0)",
        ["border-t"] = "border: Border(top: BorderSide())",
        ["border-b"] = "border: Border(bottom: BorderSide())",
        ["border-l"] = "border: Border(left: BorderSide())",
        ["border-r"] = "border: Border(right: BorderSide())",
        ["border-dashed"] = "border: Border.all(style: BorderStyle.dashed)",
        ["border-dotted"] = "border: Border.all(style: BorderStyle.dotted)",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> ShadowMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["shadow-sm"] = "boxShadow: [const BoxShadow(blurRadius: 2.0, offset: Offset(0, 1), color: Color(0x0D000000))]",
        ["shadow"] = "boxShadow: [const BoxShadow(blurRadius: 4.0, offset: Offset(0, 2), color: Color(0x1A000000))]",
        ["shadow-md"] = "boxShadow: [const BoxShadow(blurRadius: 6.0, offset: Offset(0, 4), color: Color(0x1A000000))]",
        ["shadow-lg"] = "boxShadow: [const BoxShadow(blurRadius: 10.0, offset: Offset(0, 10), color: Color(0x1A000000))]",
        ["shadow-xl"] = "boxShadow: [const BoxShadow(blurRadius: 20.0, offset: Offset(0, 20), color: Color(0x1A000000))]",
        ["shadow-2xl"] = "boxShadow: [const BoxShadow(blurRadius: 25.0, offset: Offset(0, 25), color: Color(0x29000000))]",
        ["shadow-none"] = "boxShadow: []",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> TextStyleMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Font sizes
        ["text-xs"] = "fontSize: 12.0",
        ["text-sm"] = "fontSize: 14.0",
        ["text-base"] = "fontSize: 16.0",
        ["text-lg"] = "fontSize: 18.0",
        ["text-xl"] = "fontSize: 20.0",
        ["text-2xl"] = "fontSize: 24.0",
        ["text-3xl"] = "fontSize: 30.0",
        ["text-4xl"] = "fontSize: 36.0",
        ["text-5xl"] = "fontSize: 48.0",
        ["text-6xl"] = "fontSize: 60.0",
        ["text-7xl"] = "fontSize: 72.0",
        ["text-8xl"] = "fontSize: 96.0",
        ["text-9xl"] = "fontSize: 128.0",
        // Font weights
        ["font-thin"] = "fontWeight: FontWeight.w100",
        ["font-extralight"] = "fontWeight: FontWeight.w200",
        ["font-light"] = "fontWeight: FontWeight.w300",
        ["font-normal"] = "fontWeight: FontWeight.w400",
        ["font-medium"] = "fontWeight: FontWeight.w500",
        ["font-semibold"] = "fontWeight: FontWeight.w600",
        ["font-bold"] = "fontWeight: FontWeight.w700",
        ["font-extrabold"] = "fontWeight: FontWeight.w800",
        ["font-black"] = "fontWeight: FontWeight.w900",
        // Text alignment
        ["text-left"] = "textAlign: TextAlign.left",
        ["text-center"] = "textAlign: TextAlign.center",
        ["text-right"] = "textAlign: TextAlign.right",
        ["text-justify"] = "textAlign: TextAlign.justify",
        // Text decoration
        ["underline"] = "decoration: TextDecoration.underline",
        ["line-through"] = "decoration: TextDecoration.lineThrough",
        ["overline"] = "decoration: TextDecoration.overline",
        ["no-underline"] = "decoration: TextDecoration.none",
        // Font style
        ["italic"] = "fontStyle: FontStyle.italic",
        ["not-italic"] = "fontStyle: FontStyle.normal",
        // Text overflow
        ["truncate"] = "overflow: TextOverflow.ellipsis",
        ["text-clip"] = "overflow: TextOverflow.clip",
        // Letter spacing
        ["tracking-tighter"] = "letterSpacing: -0.8",
        ["tracking-tight"] = "letterSpacing: -0.4",
        ["tracking-normal"] = "letterSpacing: 0.0",
        ["tracking-wide"] = "letterSpacing: 0.4",
        ["tracking-wider"] = "letterSpacing: 0.8",
        ["tracking-widest"] = "letterSpacing: 1.6",
        // Line height
        ["leading-none"] = "height: 1.0",
        ["leading-tight"] = "height: 1.25",
        ["leading-snug"] = "height: 1.375",
        ["leading-normal"] = "height: 1.5",
        ["leading-relaxed"] = "height: 1.625",
        ["leading-loose"] = "height: 2.0",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> MainAxisAlignmentMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["justify-start"] = "mainAxisAlignment: MainAxisAlignment.start",
        ["justify-center"] = "mainAxisAlignment: MainAxisAlignment.center",
        ["justify-end"] = "mainAxisAlignment: MainAxisAlignment.end",
        ["justify-between"] = "mainAxisAlignment: MainAxisAlignment.spaceBetween",
        ["justify-around"] = "mainAxisAlignment: MainAxisAlignment.spaceAround",
        ["justify-evenly"] = "mainAxisAlignment: MainAxisAlignment.spaceEvenly",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> CrossAxisAlignmentMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["items-start"] = "crossAxisAlignment: CrossAxisAlignment.start",
        ["items-center"] = "crossAxisAlignment: CrossAxisAlignment.center",
        ["items-end"] = "crossAxisAlignment: CrossAxisAlignment.end",
        ["items-stretch"] = "crossAxisAlignment: CrossAxisAlignment.stretch",
        ["items-baseline"] = "crossAxisAlignment: CrossAxisAlignment.baseline",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DisplayClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "hidden", "invisible", "visible"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> FlexWrapClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "flex-wrap", "flex-wrap-reverse"
    }.ToFrozenSet(StringComparer.Ordinal);

    public TailwindStyle Convert(IEnumerable<string> classNames)
    {
        var widgetProperties = new HashSet<string>(StringComparer.Ordinal);
        var decorationProperties = new HashSet<string>(StringComparer.Ordinal);
        var textStyleProperties = new HashSet<string>(StringComparer.Ordinal);
        string? mainAxisAlignment = null;
        string? crossAxisAlignment = null;
        bool? flexWrap = null;

        foreach (var className in classNames)
        {
            if (SpacingMap.TryGetValue(className, out var spacing))
            {
                widgetProperties.Add(spacing);
                continue;
            }

            if (SizeMap.TryGetValue(className, out var size))
            {
                widgetProperties.Add(size);
                continue;
            }

            if (BorderMap.TryGetValue(className, out var border))
            {
                decorationProperties.Add(border);
                continue;
            }

            if (BorderRadiusMap.TryGetValue(className, out var borderRadius))
            {
                decorationProperties.Add(borderRadius);
                continue;
            }

            if (ShadowMap.TryGetValue(className, out var shadow))
            {
                decorationProperties.Add(shadow);
                continue;
            }

            if (DisplayClasses.Contains(className))
            {
                // Display classes like hidden/invisible are handled by v-show or are no-ops
                continue;
            }

            if (TryParseColorPrefix("bg-", className, out var bgColor))
            {
                decorationProperties.Add($"color: const Color({bgColor})");
                continue;
            }

            if (TryParseColorPrefix("text-", className, out var textColor))
            {
                textStyleProperties.Add($"color: Color({textColor})");
                continue;
            }

            if (TryParseColorPrefix("border-", className, out var borderColor))
            {
                decorationProperties.Add($"border: Border.all(color: Color({borderColor}))");
                continue;
            }

            if (className.Equals("flex-wrap", StringComparison.Ordinal))
            {
                flexWrap = true;
                continue;
            }

            if (className.Equals("flex-wrap-reverse", StringComparison.Ordinal))
            {
                flexWrap = true;
                continue;
            }

            if (TextStyleMap.TryGetValue(className, out var textStyle))
            {
                textStyleProperties.Add(textStyle);
                continue;
            }

            if (MainAxisAlignmentMap.TryGetValue(className, out var mainAxis))
            {
                mainAxisAlignment = mainAxis;
                continue;
            }

            if (CrossAxisAlignmentMap.TryGetValue(className, out var crossAxis))
            {
                crossAxisAlignment = crossAxis;
                continue;
            }

            // Opacity
            if (TryParseOpacity(className, out var opacity))
            {
                decorationProperties.Add(opacity);
                continue;
            }

            // Aspect ratio
            if (TryParseAspectRatio(className, out var aspectRatio))
            {
                widgetProperties.Add(aspectRatio);
                continue;
            }
        }

        if (flexWrap.HasValue && mainAxisAlignment is null)
        {
            mainAxisAlignment = "mainAxisAlignment: MainAxisAlignment.start";
        }

        return new TailwindStyle(
            [.. widgetProperties],
            [.. decorationProperties],
            [.. textStyleProperties],
            mainAxisAlignment,
            crossAxisAlignment);
    }

    private static bool TryParseColorPrefix(string prefix, string className, out string hexColor)
    {
        if (!className.StartsWith(prefix, StringComparison.Ordinal))
        {
            hexColor = string.Empty;
            return false;
        }

        var colorName = className[prefix.Length..];
        if (ColorHexMap.TryGetValue(colorName, out var hex))
        {
            hexColor = hex;
            return true;
        }

        hexColor = string.Empty;
        return false;
    }

    private static bool TryParseOpacity(string className, out string opacityProperty)
    {
        if (!className.StartsWith("opacity-", StringComparison.Ordinal))
        {
            opacityProperty = string.Empty;
            return false;
        }

        var valueStr = className["opacity-".Length..];
        if (int.TryParse(valueStr, out var percent))
        {
            opacityProperty = $"opacity: {percent / 100.0}";
            return true;
        }

        opacityProperty = string.Empty;
        return false;
    }

    private static bool TryParseAspectRatio(string className, out string aspectRatio)
    {
        if (!className.StartsWith("aspect-", StringComparison.Ordinal))
        {
            aspectRatio = string.Empty;
            return false;
        }

        var ratio = className["aspect-".Length..];
        if (ratio.Equals("square", StringComparison.Ordinal))
        {
            aspectRatio = "aspectRatio: 1.0";
            return true;
        }

        if (ratio.Equals("video", StringComparison.Ordinal))
        {
            aspectRatio = "aspectRatio: 16.0 / 9.0";
            return true;
        }

        aspectRatio = string.Empty;
        return false;
    }
}

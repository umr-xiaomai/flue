using Flue.Core.Abstractions;
using Flue.Core.Models;
using Flue.Infrastructure.Configuration;

namespace Flue.Infrastructure.Routing;

public sealed partial class VueRouterParser (FluePaths paths) : IVueRouterParser
{
    [GeneratedRegex("import\\s+(?<name>[A-Za-z_]\\w*)\\s+from\\s+['\"](?<path>[^'\"]+)['\"]", RegexOptions.Compiled)]
    private static partial Regex ImportRegex ();

    [GeneratedRegex("\\bpath\\s*:\\s*['\"](?<path>[^'\"]+)['\"]", RegexOptions.Compiled)]
    private static partial Regex RoutePathRegex ();

    [GeneratedRegex("\\bname\\s*:\\s*['\"](?<name>[^'\"]+)['\"]", RegexOptions.Compiled)]
    private static partial Regex RouteNameRegex ();

    [GeneratedRegex("\\bcomponent\\s*:\\s*\\(\\s*\\)\\s*=>\\s*import\\(\\s*['\"](?<path>[^'\"]+)['\"]\\s*\\)", RegexOptions.Compiled)]
    private static partial Regex DynamicComponentRegex ();

    [GeneratedRegex("\\bcomponent\\s*:\\s*(?<name>[A-Za-z_]\\w*)", RegexOptions.Compiled)]
    private static partial Regex ReferencedComponentRegex ();

    [GeneratedRegex("const\\s+routes\\s*=\\s*\\[", RegexOptions.Compiled)]
    private static partial Regex ConstRoutesRegex ();

    [GeneratedRegex("\\broutes\\s*:\\s*\\[", RegexOptions.Compiled)]
    private static partial Regex ConfigRoutesRegex ();

    [GeneratedRegex("^@/", RegexOptions.Compiled)]
    private static partial Regex AliasRegex ();

    public bool IsRouterFile (string path)
    {
        return paths.IsRouterFile(path);
    }

    public async Task<FlueRouterManifest?> ParseAsync (CancellationToken cancellationToken = default)
    {
        var routerPath = paths.ResolveRouterFile();
        if (string.IsNullOrWhiteSpace(routerPath))
        {
            return null;
        }

        var source = await File.ReadAllTextAsync(routerPath, cancellationToken);
        var routesBlock = ExtractRoutesBlock(source);
        if (string.IsNullOrWhiteSpace(routesBlock))
        {
            return null;
        }

        var importMap = ParseImports(source, routerPath);
        var routeObjects = SplitRouteObjects(routesBlock);
        var routes = ImmutableArray.CreateBuilder<FlueRouteDefinition>();

        foreach (var routeObject in routeObjects)
        {
            if (!TryParseRoute(routeObject, importMap, routerPath, out var routeDefinition))
            {
                continue;
            }

            routes.Add(routeDefinition);
        }

        if (routes.Count == 0)
        {
            return null;
        }

        var initialRoute = routes
            .Select(route => route.Path)
            .FirstOrDefault(path => path.Equals("/", StringComparison.Ordinal))
            ?? routes[0].Path;

        return new FlueRouterManifest(routerPath, initialRoute, routes.ToImmutable());
    }

    private Dictionary<string, string> ParseImports (string source, string routerPath)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ImportRegex().Matches(source))
        {
            var importName = match.Groups["name"].Value.Trim();
            var importPath = match.Groups["path"].Value.Trim();
            var resolved = ResolveVueComponentPath(importPath, routerPath);
            if (resolved is null)
            {
                continue;
            }

            map[importName] = resolved;
        }

        return map;
    }

    private static string? ExtractRoutesBlock (string source)
    {
        var match = ConstRoutesRegex().Match(source);
        if (!match.Success)
        {
            match = ConfigRoutesRegex().Match(source);
        }

        if (!match.Success)
        {
            return null;
        }

        var arrayStart = source.IndexOf('[', match.Index);
        if (arrayStart < 0)
        {
            return null;
        }

        var arrayEnd = FindMatchingBracket(source, arrayStart, '[', ']');
        if (arrayEnd <= arrayStart)
        {
            return null;
        }

        return source[(arrayStart + 1)..arrayEnd];
    }

    private static ImmutableArray<string> SplitRouteObjects (string routesBlock)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var start = -1;
        var depth = 0;
        var inString = false;
        var quote = '\0';
        var escaped = false;

        for (var index = 0; index < routesBlock.Length; index++)
        {
            var character = routesBlock[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (character is '\'' or '"' or '`')
            {
                inString = true;
                quote = character;
                continue;
            }

            if (character == '{')
            {
                if (depth == 0)
                {
                    start = index;
                }

                depth++;
                continue;
            }

            if (character == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    builder.Add(routesBlock[start..(index + 1)]);
                    start = -1;
                }
            }
        }

        return builder.ToImmutable();
    }

    private bool TryParseRoute (
        string routeObject,
        Dictionary<string, string> importMap,
        string routerPath,
        out FlueRouteDefinition routeDefinition)
    {
        routeDefinition = default!;

        var pathMatch = RoutePathRegex().Match(routeObject);
        if (!pathMatch.Success)
        {
            return false;
        }

        var routePath = pathMatch.Groups["path"].Value.Trim();
        if (routePath.Length == 0)
        {
            return false;
        }

        var nameMatch = RouteNameRegex().Match(routeObject);
        var routeName = nameMatch.Success ? nameMatch.Groups["name"].Value.Trim() : null;

        var dynamicComponentMatch = DynamicComponentRegex().Match(routeObject);
        string? componentPath = null;

        if (dynamicComponentMatch.Success)
        {
            componentPath = ResolveVueComponentPath(dynamicComponentMatch.Groups["path"].Value.Trim(), routerPath);
        }
        else
        {
            var referencedComponentMatch = ReferencedComponentRegex().Match(routeObject);
            if (referencedComponentMatch.Success)
            {
                var reference = referencedComponentMatch.Groups["name"].Value.Trim();
                if (importMap.TryGetValue(reference, out var resolvedImport))
                {
                    componentPath = resolvedImport;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(componentPath))
        {
            return false;
        }

        routeDefinition = new FlueRouteDefinition(routePath, routeName, componentPath);
        return true;
    }

    private string? ResolveVueComponentPath (string componentPath, string routerPath)
    {
        if (string.IsNullOrWhiteSpace(componentPath))
        {
            return null;
        }

        var normalized = componentPath.Trim();
        if (AliasRegex().IsMatch(normalized))
        {
            normalized = normalized[2..];
            var aliased = Path.GetFullPath(Path.Combine(paths.SourceRoot, normalized));
            return EnsureVueExtension(aliased);
        }

        string resolved;
        if (normalized.StartsWith("./", StringComparison.Ordinal) || normalized.StartsWith("../", StringComparison.Ordinal))
        {
            var routerDirectory = Path.GetDirectoryName(routerPath) ?? string.Empty;
            resolved = Path.GetFullPath(Path.Combine(routerDirectory, normalized));
        }
        else if (Path.IsPathRooted(normalized))
        {
            resolved = Path.GetFullPath(normalized);
        }
        else
        {
            var routerDirectory = Path.GetDirectoryName(routerPath) ?? string.Empty;
            resolved = Path.GetFullPath(Path.Combine(routerDirectory, normalized));
        }

        var candidate = EnsureVueExtension(resolved);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var fallbackRelative = normalized
            .TrimStart('.', '/')
            .Replace('/', Path.DirectorySeparatorChar);
        var sourceFallback = EnsureVueExtension(Path.GetFullPath(Path.Combine(paths.SourceRoot, fallbackRelative)));
        return sourceFallback;
    }

    private static string EnsureVueExtension (string path)
    {
        return path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".vue";
    }

    private static int FindMatchingBracket (string text, int openIndex, char openChar, char closeChar)
    {
        var depth = 0;
        var inString = false;
        var quote = '\0';
        var escaped = false;

        for (var index = openIndex; index < text.Length; index++)
        {
            var character = text[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (character is '\'' or '"' or '`')
            {
                inString = true;
                quote = character;
                continue;
            }

            if (character == openChar)
            {
                depth++;
                continue;
            }

            if (character == closeChar)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }
}

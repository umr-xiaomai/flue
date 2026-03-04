using Flue.Infrastructure.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Flue.Infrastructure.FileSystem;

public sealed class PubspecManager (FluePaths paths)
{
    private static readonly FrozenDictionary<string, string> RequiredDependencies = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["google_fonts"] = "^6.2.1",
        ["flue_ui"] = "^0.1.0",
        ["http"] = "^1.2.2"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task EnsureDependenciesAsync (CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.FlutterBridgeRoot);

        var pubspecPath = Path.Combine(paths.FlutterBridgeRoot, "pubspec.yaml");
        var model = await LoadPubspecAsync(pubspecPath, cancellationToken);

        if (!model.ContainsKey("name"))
        {
            model["name"] = "flutter_bridge";
        }

        if (!model.ContainsKey("description"))
        {
            model["description"] = "Generated Flutter bridge by Flue";
        }

        if (!model.ContainsKey("publish_to"))
        {
            model["publish_to"] = "none";
        }

        if (!model.ContainsKey("version"))
        {
            model["version"] = "0.1.0+1";
        }

        if (!model.ContainsKey("environment"))
        {
            model["environment"] = new Dictionary<object, object?>
            {
                ["sdk"] = ">=3.4.0 <4.0.0"
            };
        }

        var dependencies = EnsureMap(model, "dependencies");
        if (!dependencies.ContainsKey("flutter"))
        {
            dependencies["flutter"] = new Dictionary<object, object?>
            {
                ["sdk"] = "flutter"
            };
        }

        foreach (var dependency in RequiredDependencies)
        {
            if (!dependencies.ContainsKey(dependency.Key))
            {
                dependencies[dependency.Key] = dependency.Value;
            }
        }

        var flutterMap = EnsureMap(model, "flutter");
        if (!flutterMap.ContainsKey("uses-material-design"))
        {
            flutterMap["uses-material-design"] = true;
        }

        model["dependencies"] = dependencies;
        model["flutter"] = flutterMap;

        var yaml = YamlSerializer.Serialize(model);
        await File.WriteAllTextAsync(pubspecPath, yaml, cancellationToken);
    }

    private static async Task<Dictionary<object, object?>> LoadPubspecAsync (string pubspecPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(pubspecPath))
        {
            return new Dictionary<object, object?>();
        }

        var yaml = await File.ReadAllTextAsync(pubspecPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new Dictionary<object, object?>();
        }

        var parsed = YamlDeserializer.Deserialize<object?>(yaml);
        return NormalizeToMap(parsed);
    }

    private static Dictionary<object, object?> EnsureMap (Dictionary<object, object?> root, string key)
    {
        if (root.TryGetValue(key, out var value))
        {
            var normalized = NormalizeToMap(value);
            root[key] = normalized;
            return normalized;
        }

        var created = new Dictionary<object, object?>();
        root[key] = created;
        return created;
    }

    private static Dictionary<object, object?> NormalizeToMap (object? value)
    {
        if (value is Dictionary<object, object?> typedMap)
        {
            return typedMap;
        }

        if (value is IDictionary<object, object?> objectDictionary)
        {
            return objectDictionary.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        if (value is IDictionary<object, object> objectObjectDictionary)
        {
            return objectObjectDictionary.ToDictionary(entry => entry.Key, entry => (object?)entry.Value);
        }

        if (value is IDictionary<string, object?> stringDictionary)
        {
            return stringDictionary.ToDictionary(entry => (object)entry.Key, entry => entry.Value);
        }

        if (value is IDictionary<string, object> stringObjectDictionary)
        {
            return stringObjectDictionary.ToDictionary(entry => (object)entry.Key, entry => (object?)entry.Value);
        }

        return new Dictionary<object, object?>();
    }
}

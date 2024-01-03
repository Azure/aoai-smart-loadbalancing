namespace openai_loadbalancer;

public class BackendConfig
{
    public required string Url { get; set; }
    public int Priority { get; set; }
    public required string ApiKey { get; set; }

    public static IReadOnlyDictionary<string, BackendConfig> LoadConfig(IConfiguration config)
    {
        var returnDictionary = new Dictionary<string, BackendConfig>();

        var environmentVariables = config.AsEnumerable().Where(x => x.Key.ToUpperInvariant().StartsWith("BACKEND_")).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        var numberOfBackends = environmentVariables.Select(x => x.Key.Split('_')[1]).Distinct();

        if (environmentVariables.Count() == 0 || numberOfBackends.Count() == 0)
        {
            throw new Exception("Could not find any environment variable starting with 'BACKEND_[x]'... please define your backend endpoints");
        }

        foreach (var backendIndex in numberOfBackends)
        {
            var key = $"BACKEND_{backendIndex}";
            var url = LoadEnvironmentVariable(environmentVariables, backendIndex, "URL");
            var apiKey = LoadEnvironmentVariable(environmentVariables, backendIndex, "APIKEY");
            var priority = Convert.ToInt32(LoadEnvironmentVariable(environmentVariables, backendIndex, "PRIORITY"));

            returnDictionary.Add(key, new BackendConfig { Url = url, ApiKey = apiKey, Priority = priority });
        }

        return returnDictionary;
    }

    private static string LoadEnvironmentVariable(IDictionary<string, string?> variables, string backendIndex, string property)
    {
        var key = $"BACKEND_{backendIndex}_{property}";

        if (!variables.TryGetValue(key, out var value))
        {
            throw new Exception($"Missing environment variable {key}");
        }

        return value!.Trim();
    }
}
    

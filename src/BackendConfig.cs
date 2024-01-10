namespace openai_loadbalancer;

public class BackendConfig
{
    public static int HttpTimeoutSeconds = 100;

    public required string Url { get; set; }
    public string? DeploymentName { get; set; }
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
            var deploymentName = LoadEnvironmentVariable(environmentVariables, backendIndex, "DEPLOYMENT_NAME", isMandatory: false);
            var apiKey = LoadEnvironmentVariable(environmentVariables, backendIndex, "APIKEY");
            var priority = Convert.ToInt32(LoadEnvironmentVariable(environmentVariables, backendIndex, "PRIORITY"));

            returnDictionary.Add(key, new BackendConfig { Url = url, ApiKey = apiKey, Priority = priority, DeploymentName = deploymentName });
        }

        //Load the general settings not in scope only for specific backends
        var httpTimeout = Environment.GetEnvironmentVariable("HTTP_TIMEOUT_SECONDS");

        if (httpTimeout != null)
        {
            HttpTimeoutSeconds = Convert.ToInt32(httpTimeout);
        }

        return returnDictionary;
    }

    private static string? LoadEnvironmentVariable(IDictionary<string, string?> variables, string backendIndex, string property, bool isMandatory = true)
    {
        var key = $"BACKEND_{backendIndex}_{property}";

        if (!variables.TryGetValue(key, out var value) && isMandatory)
        {
            throw new Exception($"Missing environment variable {key}");
        }

        if (value != null)
        {
            return value.Trim();
        }
        else
        {
            return null;
        }
    }
}
    

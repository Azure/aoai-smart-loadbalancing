using Yarp.ReverseProxy.Model;

namespace openai_loadbalancer;

public class RetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Dictionary<string, BackendConfig> _backends;
    private readonly ILogger _logger;

    public RetryMiddleware(RequestDelegate next, Dictionary<string, BackendConfig> backends, ILoggerFactory loggerFactory)
    {
        _next = next;
        _backends = backends;
        _logger = loggerFactory.CreateLogger<RetryMiddleware>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        var shouldRetry = true;
        var retryCount = 0;

        while (shouldRetry)
        {
            var reverseProxyFeature = context.GetReverseProxyFeature();
            var destination = PickOneDestination(context);

            reverseProxyFeature.AvailableDestinations = new List<DestinationState>(destination);

            if (retryCount > 0)
            {
                //If this is a retry, we must reset the request body to initial position and clear the current response
                context.Request.Body.Position = 0;
                reverseProxyFeature.ProxiedDestination = null;
                context.Response.Clear();
            }

            await _next(context);

            var statusCode = context.Response.StatusCode;
            var atLeastOneBackendHealthy = GetNumberHealthyEndpoints(context) > 0;
            retryCount++;

            shouldRetry = (statusCode is 429 or >= 500) && atLeastOneBackendHealthy;
        }
    }

    private static int GetNumberHealthyEndpoints(HttpContext context)
    {
        return context.GetReverseProxyFeature().AllDestinations.Count(m => m.Health.Passive is DestinationHealth.Healthy or DestinationHealth.Unknown);
    }

    private DestinationState PickOneDestination(HttpContext context)
    {
        var reverseProxyFeature = context.GetReverseProxyFeature();
        var allDestinations = reverseProxyFeature.AllDestinations;

        var selectedPriority = int.MaxValue;
        var availableBackends = new List<int>();

        for (var i = 0; i < allDestinations.Count; i++)
        {
            var destination = allDestinations[i];

            if (destination.Health.Passive != DestinationHealth.Unhealthy)
            {
                var destinationPriority = _backends[destination.DestinationId].Priority;

                if (destinationPriority < selectedPriority)
                {
                    selectedPriority = destinationPriority;
                    availableBackends.Clear();
                    availableBackends.Add(i);
                }
                else if (destinationPriority == selectedPriority)
                {
                    availableBackends.Add(i);
                }
            }
        }

        int backendIndex;

        if (availableBackends.Count == 1)
        {
            //Returns the only available backend if we have only one available
            backendIndex = availableBackends[0];
        }
        else
        if (availableBackends.Count > 0)
        {
            //Returns a random backend from the list if we have more than one available with the same priority
            backendIndex = availableBackends[new Random().Next(0, availableBackends.Count)];
        }
        else
        {
            //Returns a random  backend if all backends are unhealthy
            _logger.LogWarning($"All backends are unhealthy. Picking a random backend...");
            backendIndex = new Random().Next(0, allDestinations.Count);
        }

        var pickedDestination = allDestinations[backendIndex];

        return pickedDestination;
    }
}

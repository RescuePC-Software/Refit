using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace RescuePC.Software.Refit;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddRefitClient<TApi>(
        this IServiceCollection services,
        string clientName,
        Action<HttpClient> client,
        RefitSettings? refitSettings = null)
        where TApi : class
    {

        var defaultRefitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            })
        };

        return services.AddRefitClient<TApi>(refitSettings ?? defaultRefitSettings)
            .ConfigureHttpClient(client)
            .AddHttpMessageHandler(sp => new HttpFailureLoggingHandler(
                sp.GetRequiredService<ILogger<HttpFailureLoggingHandler>>(),
                clientName));
    }
}
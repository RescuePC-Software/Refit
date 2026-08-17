using Microsoft.Extensions.Logging;

namespace RescuePC.Software.Refit;

public class HttpFailureLoggingHandler : DelegatingHandler
{
    private readonly ILogger<HttpFailureLoggingHandler> _logger;
    private readonly string _clientName;

    public HttpFailureLoggingHandler(ILogger<HttpFailureLoggingHandler> logger, string clientName)
    {
        _logger = logger;
        _clientName = clientName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "{ClientName} request to '{RequestUri}' failed with status {StatusCode}: {Content}",
                _clientName, request.RequestUri, (int)response.StatusCode, content);
        }
        return response;
    }
}

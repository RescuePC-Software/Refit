using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Refit;
using RescuePC.Software.Refit;

namespace RescuePC.Software.Refit.UnitTests;

public interface IEchoApi
{
    [Post("/echo")]
    Task<EchoBody> Echo([Body] EchoBody body);
}

public class EchoBody
{
    public string FirstName { get; set; } = string.Empty;
}

public class AddRefitClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

            // A real handler sets this; Refit needs it to build an ApiException from a failed response.
            var response = respond(request);
            response.RequestMessage = request;

            return response;
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Errors { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Errors);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> errors) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Error)
                {
                    errors.Add(formatter(state, exception));
                }
            }
        }
    }

    private static ServiceProvider CreateProvider(StubHandler handler, CapturingLoggerProvider? logger = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logger ?? new CapturingLoggerProvider()));
        services.AddRefitClient<IEchoApi>("echo", client => client.BaseAddress = new Uri("https://example.test"));
        services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));

        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public void A_client_can_be_created_without_the_application_referencing_Refit_Reflection()
    {
        // In Refit 15 AddRefitClient asks for the reflection request builder when the client is created, even for a
        // fully generated interface, so this package has to bring Refit.Reflection along.
        using var provider = CreateProvider(new StubHandler(_ => Json("{}")));

        Assert.NotNull(provider.GetRequiredService<IEchoApi>());
    }

    [Fact]
    public async Task Bodies_are_sent_and_read_in_camel_case_by_default()
    {
        var handler = new StubHandler(_ => Json("""{"firstName":"Ada"}"""));
        using var provider = CreateProvider(handler);

        var response = await provider.GetRequiredService<IEchoApi>().Echo(new EchoBody { FirstName = "Ada" });

        Assert.Equal("""{"firstName":"Ada"}""", Assert.Single(handler.Bodies));
        Assert.Equal("Ada", response.FirstName);
    }

    [Fact]
    public async Task A_failed_response_is_logged_with_the_client_name_status_and_content()
    {
        var logger = new CapturingLoggerProvider();
        using var provider = CreateProvider(new StubHandler(_ => Json("""{"error":"boom"}""", HttpStatusCode.InternalServerError)), logger);

        await Assert.ThrowsAsync<ApiException>(() => provider.GetRequiredService<IEchoApi>().Echo(new EchoBody()));

        var message = Assert.Single(logger.Errors);
        Assert.Contains("echo", message);
        Assert.Contains("500", message);
        Assert.Contains("boom", message);
    }
}

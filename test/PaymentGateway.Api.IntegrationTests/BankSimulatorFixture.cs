using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentGateway.Api.IntegrationTests;

public class BankSimulatorFixture : IDisposable
{
    private static readonly Uri SimulatorUrl = new("http://localhost:8080/");

    public BankSimulatorFixture()
    {
        EnsureSimulatorIsRunning();

        Factory = new WebApplicationFactory<Program>();
    }

    public WebApplicationFactory<Program> Factory { get; }

    public void Dispose()
    {
        Factory.Dispose();
    }

    private static void EnsureSimulatorIsRunning()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            client.GetAsync(SimulatorUrl).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"The bank simulator is not reachable at {SimulatorUrl}. Start it with `docker compose up -d`, " +
                "or skip these tests with `dotnet test --filter Category!=Integration`.",
                exception);
        }
    }
}

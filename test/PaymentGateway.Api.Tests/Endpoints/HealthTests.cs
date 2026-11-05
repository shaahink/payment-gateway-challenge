using System.Net;

using PaymentGateway.Api.Bank;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Endpoints;

public sealed class HealthTests : IDisposable
{
    private readonly PaymentGatewayFactory _factory = new();
    private readonly HttpClient _client;

    public HealthTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ReportsHealthyEvenWhileTheBankIsDown()
    {
        _factory.Bank.Throws = new BankException(BankFailure.Unavailable, "The bank returned 503.");

        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

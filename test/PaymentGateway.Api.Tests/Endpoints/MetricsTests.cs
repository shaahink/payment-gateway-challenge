using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Bank;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Endpoints;

public sealed class MetricsTests : IDisposable
{
    private readonly PaymentGatewayFactory _factory = new();
    private readonly HttpClient _client;
    private readonly OutcomeRecorder _recorder;

    public MetricsTests()
    {
        _client = _factory.CreateClient();
        _recorder = new OutcomeRecorder(_factory.Services.GetRequiredService<IMeterFactory>());
    }

    [Fact]
    public async Task CountsEveryPaymentByOutcome()
    {
        // Act
        _factory.Bank.Authorization = new BankAuthorization(Authorized: true, AuthorizationCode: "code");
        await _client.PostAsJsonAsync("/api/Payments", PaymentRequestBody.Valid);

        _factory.Bank.Authorization = new BankAuthorization(Authorized: false, AuthorizationCode: null);
        await _client.PostAsJsonAsync("/api/Payments", PaymentRequestBody.Valid);

        await _client.PostAsJsonAsync("/api/Payments", PaymentRequestBody.Valid with { Currency = "JPY" });

        _factory.Bank.Throws = new BankException(BankFailure.Unavailable, "The bank returned 503.");
        await _client.PostAsJsonAsync("/api/Payments", PaymentRequestBody.Valid);

        // Assert
        Assert.Equal(["Authorized", "Declined", "Rejected", "BankUnavailable"], _recorder.Outcomes);
    }

    public void Dispose()
    {
        _recorder.Dispose();
        _client.Dispose();
        _factory.Dispose();
    }
}

using System.Net.Http.Json;
using System.Text.RegularExpressions;

using PaymentGateway.Api.Bank;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Endpoints;

public class LoggingTests : IDisposable
{
    private const string CardNumber = "4111111111111111";
    private const string Cvv = "9871";

    private static readonly PaymentRequestBody Request = PaymentRequestBody.Valid with { CardNumber = CardNumber, Cvv = Cvv };

    private readonly PaymentGatewayFactory _factory = new();
    private readonly HttpClient _client;

    public LoggingTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task NeverLogsCardDataForAProcessedPayment()
    {
        await _client.PostAsJsonAsync("/api/Payments", Request);

        AssertNoCardDataLogged();
    }

    [Fact]
    public async Task NeverLogsCardDataForARejectedPayment()
    {
        await _client.PostAsJsonAsync("/api/Payments", Request with { Currency = "JPY" });

        AssertNoCardDataLogged();
    }

    [Fact]
    public async Task NeverLogsCardDataWhenTheBankFails()
    {
        _factory.Bank.Throws = new BankException(BankFailure.Unavailable, "The bank returned 503.");

        await _client.PostAsJsonAsync("/api/Payments", Request);

        AssertNoCardDataLogged();
    }

    [Fact]
    public async Task LogsEachProcessedPaymentWithItsIdAndStatus()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", PaymentRequestBody.Valid);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponseBody>();

        Assert.Contains(_factory.Logs.Entries, entry => entry.Contains($"Payment {payment!.Id} processed: Authorized"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private void AssertNoCardDataLogged()
    {
        Assert.NotEmpty(_factory.Logs.Entries);
        Assert.DoesNotContain(_factory.Logs.Entries, entry => entry.Contains(CardNumber));
        Assert.DoesNotContain(_factory.Logs.Entries, entry => Regex.IsMatch(entry, $@"\b{Cvv}\b"));
    }
}

using System.Net;
using System.Net.Http.Json;

using PaymentGateway.Api.Tests.Endpoints;

namespace PaymentGateway.Api.IntegrationTests;

[Trait("Category", "Integration")]
public class BankSimulatorTests : IClassFixture<BankSimulatorFixture>
{
    private readonly HttpClient _client;

    public BankSimulatorTests(BankSimulatorFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Theory]
    [InlineData("2222405343248877", "Authorized")]
    [InlineData("2222405343248878", "Declined")]
    public async Task ProcessesAPaymentThroughTheSimulator(string cardNumber, string expectedStatus)
    {
        // Arrange
        var request = PaymentRequestBody.Valid with { CardNumber = cardNumber };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponseBody>();
        Assert.Equal(expectedStatus, payment?.Status);

        var stored = await _client.GetFromJsonAsync<PaymentResponseBody>(response.Headers.Location);
        Assert.Equal(expectedStatus, stored?.Status);
        Assert.Equal(cardNumber[^4..], stored?.CardNumberLastFour);
    }

    [Fact]
    public async Task ReportsTheBankUnavailableForACardEndingInZero()
    {
        // Arrange
        var request = PaymentRequestBody.Valid with { CardNumber = "2222405343248870" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

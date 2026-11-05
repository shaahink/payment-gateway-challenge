using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Storage;

namespace PaymentGateway.Api.Tests.Endpoints;

public sealed class GetPaymentTests : IDisposable
{
    private readonly PaymentGatewayFactory _factory = new();
    private readonly HttpClient _client;

    public GetPaymentTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Declined, "8878", 12, 2027, "EUR", 1050, "auth-code");
        _factory.Services.GetRequiredService<PaymentsRepository>().Add(payment);

        // Act
        var response = await _client.GetAsync($"/api/Payments/{payment.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new PaymentResponseBody(payment.Id, "Declined", "8878", 12, 2027, "EUR", 1050),
            await response.Content.ReadFromJsonAsync<PaymentResponseBody>());
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

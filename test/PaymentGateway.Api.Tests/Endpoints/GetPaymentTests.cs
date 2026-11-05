using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Endpoints;

public class GetPaymentTests : IDisposable
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
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Authorized, "8877", 4, 2027, "GBP", 100);
        _factory.Services.GetRequiredService<PaymentsRepository>().Add(payment);

        // Act
        var response = await _client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponseBody>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
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

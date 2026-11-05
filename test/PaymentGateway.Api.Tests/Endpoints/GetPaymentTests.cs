using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Endpoints;

public class GetPaymentTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public GetPaymentTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = 8877,
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100
        };
        _factory.Services.GetRequiredService<PaymentsRepository>().Add(payment);

        // Act
        var response = await _client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

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

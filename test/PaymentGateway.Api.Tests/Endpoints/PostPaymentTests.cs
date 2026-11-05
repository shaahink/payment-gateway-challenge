using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentGateway.Api.Tests.Endpoints;

public class PostPaymentTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public PostPaymentTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task RejectsAPaymentWithAnInvalidCardNumber()
    {
        // Arrange
        var request = PaymentRequestBody.Valid with { CardNumber = "1234" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("CardNumber", problem.Errors.Keys);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

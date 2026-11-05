using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Bank;

namespace PaymentGateway.Api.Tests.Endpoints;

public class PostPaymentTests : IDisposable
{
    private readonly PaymentGatewayFactory _factory = new();
    private readonly HttpClient _client;

    public PostPaymentTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task AuthorizesAPaymentTheBankApproves()
    {
        // Arrange
        _factory.Bank.Authorization = new BankAuthorization(Authorized: true, AuthorizationCode: "0bb07405-6d44-4b50-a14f-7ae0beff13ad");
        var request = PaymentRequestBody.Valid;

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponseBody>();
        Assert.NotNull(payment);
        Assert.Equal("Authorized", payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, payment.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, payment.ExpiryYear);
        Assert.Equal(request.Currency, payment.Currency);
        Assert.Equal(request.Amount, payment.Amount);
        Assert.Single(_factory.Bank.Requests);
        Assert.Equal($"/api/Payments/{payment.Id}", response.Headers.Location?.AbsolutePath);

        var stored = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
    }

    [Fact]
    public async Task DeclinesAPaymentTheBankDeclines()
    {
        // Arrange
        _factory.Bank.Authorization = new BankAuthorization(Authorized: false, AuthorizationCode: null);
        var request = PaymentRequestBody.Valid;

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponseBody>();
        Assert.NotNull(payment);
        Assert.Equal("Declined", payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);

        var stored = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
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
        Assert.Null(response.Headers.Location);
        Assert.Empty(_factory.Bank.Requests);
    }

    [Fact]
    public async Task ReportsEveryInvalidFieldInOneResponse()
    {
        // Arrange
        var request = new PaymentRequestBody(
            CardNumber: "1",
            ExpiryMonth: 13,
            ExpiryYear: null,
            Currency: "XXX",
            Amount: 0,
            Cvv: "1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(
            ["Amount", "CardNumber", "Currency", "Cvv", "ExpiryMonth", "ExpiryYear"],
            problem.Errors.Keys.Order());
        Assert.Empty(_factory.Bank.Requests);
    }

    [Fact]
    public async Task RejectsAFieldOfTheWrongTypeWithTheSameProblemShape()
    {
        // Arrange
        var json = """{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2027,"currency":"GBP","amount":"abc","cvv":"123"}""";

        // Act
        var response = await _client.PostAsync("/api/Payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("$.amount", problem.Errors.Keys);
        Assert.Empty(_factory.Bank.Requests);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

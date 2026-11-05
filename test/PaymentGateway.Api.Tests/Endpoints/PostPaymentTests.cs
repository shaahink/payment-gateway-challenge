using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Bank;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Storage;

namespace PaymentGateway.Api.Tests.Endpoints;

public sealed class PostPaymentTests : IDisposable
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
        var stored = _factory.Services.GetRequiredService<PaymentsRepository>().Get(payment.Id);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", stored?.AuthorizationCode);

        var fetched = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task NeverReturnsTheCardNumberOrCvv()
    {
        // Arrange
        var request = PaymentRequestBody.Valid with { CardNumber = "4111111111111111", Cvv = "987" };
        string[] contract = ["id", "status", "cardNumberLastFour", "expiryMonth", "expiryYear", "currency", "amount"];

        // Act
        var posted = await _client.PostAsJsonAsync("/api/Payments", request);
        var fetched = await _client.GetAsync(posted.Headers.Location);

        // Assert
        foreach (var response in new[] { posted, fetched })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(request.CardNumber, body);

            using var json = JsonDocument.Parse(body);
            Assert.Equal(contract, json.RootElement.EnumerateObject().Select(property => property.Name));
        }
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
        Assert.Equal("The payment was rejected.", problem.Title);
        Assert.Equal(["cardNumber"], problem.Errors.Keys);
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
            ["amount", "cardNumber", "currency", "cvv", "expiryMonth", "expiryYear"],
            problem.Errors.Keys.Order());
        Assert.Empty(_factory.Bank.Requests);
    }

    [Fact]
    public async Task RejectsAFieldOfTheWrongTypeWithTheSameProblemShapeAndNoInternals()
    {
        // Arrange
        var json = """{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2027,"currency":"GBP","amount":"abc","cvv":"123"}""";

        // Act
        var response = await _client.PostAsync("/api/Payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("The payment was rejected.", problem.Title);
        Assert.Equal(["$.amount"], problem.Errors.Keys);
        Assert.DoesNotContain("System.", problem.Errors["$.amount"].Single());
        Assert.Empty(_factory.Bank.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    public async Task RejectsAMissingBody(string json)
    {
        // Act
        var response = await _client.PostAsync("/api/Payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(PaymentsController.RejectedTitle, problem?.Title);
        Assert.Empty(_factory.Bank.Requests);
    }

    [Theory]
    [InlineData(BankFailure.Unavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(BankFailure.TimedOut, HttpStatusCode.GatewayTimeout)]
    [InlineData(BankFailure.InvalidResponse, HttpStatusCode.BadGateway)]
    public async Task ReportsABankFailureAsProblemDetailsAndStoresNothing(BankFailure failure, HttpStatusCode expected)
    {
        // Arrange
        _factory.Bank.Throws = new BankException(failure, "The bank failed.");

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", PaymentRequestBody.Valid);

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        var paymentId = Assert.IsType<JsonElement>(problem?.Extensions["paymentId"]).GetGuid();
        var lookup = await _client.GetAsync($"/api/Payments/{paymentId}");
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
    }

    [Fact]
    public async Task RejectsANumberSentAsAString()
    {
        // Arrange
        var json = """{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2027,"currency":"GBP","amount":"100","cvv":"123"}""";

        // Act
        var response = await _client.PostAsync("/api/Payments", new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["$.amount"], problem?.Errors.Keys);
        Assert.Empty(_factory.Bank.Requests);
    }

    [Fact]
    public async Task HidesUnexpectedErrorsBehindProblemDetailsOutsideDevelopment()
    {
        // Arrange
        _factory.Bank.Throws = new InvalidOperationException("Something nobody planned for.");
        var client = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production")).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", PaymentRequestBody.Valid);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Something nobody planned for", body);
        Assert.DoesNotContain(" at PaymentGateway", body);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

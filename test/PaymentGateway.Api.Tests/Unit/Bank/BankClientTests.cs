using System.Net;

using PaymentGateway.Api.Bank;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.Unit.Bank;

public class BankClientTests
{
    private static readonly BankPaymentRequest Request = new("2222405343248877", 4, 2027, "GBP", 100, "123");

    [Fact]
    public async Task SendsThePaymentInTheBanksWireFormat()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""");

        await CreateClient(handler).AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("http://bank.test/payments", handler.Request?.RequestUri?.ToString());
        Assert.Equal(
            """{"card_number":"2222405343248877","expiry_date":"04/2027","currency":"GBP","amount":100,"cvv":"123"}""",
            handler.RequestBody);
    }

    [Fact]
    public async Task ReturnsTheAuthorizationWhenTheBankApproves()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""");

        var authorization = await CreateClient(handler).AuthorizeAsync(Request, CancellationToken.None);

        Assert.True(authorization.Authorized);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", authorization.AuthorizationCode);
    }

    [Fact]
    public async Task ReturnsADeclineWithNoCodeWhenTheBankDeclines()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"authorized":false,"authorization_code":""}""");

        var authorization = await CreateClient(handler).AuthorizeAsync(Request, CancellationToken.None);

        Assert.False(authorization.Authorized);
        Assert.Null(authorization.AuthorizationCode);
    }

    [Fact]
    public async Task ReportsTheBankUnavailableWhenItReturns503()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}");

        var exception = await Assert.ThrowsAsync<BankException>(() => CreateClient(handler).AuthorizeAsync(Request, CancellationToken.None));

        Assert.Equal(BankFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task ReportsTheBankUnavailableWhenItCannotBeReached()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));

        var exception = await Assert.ThrowsAsync<BankException>(() => CreateClient(handler).AuthorizeAsync(Request, CancellationToken.None));

        Assert.Equal(BankFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task ReportsATimeoutWhenTheBankDoesNotAnswerInTime()
    {
        var client = CreateClient(new HangingHttpMessageHandler(), TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<BankException>(() => client.AuthorizeAsync(Request, CancellationToken.None));

        Assert.Equal(BankFailure.TimedOut, exception.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error_message":"Not all required properties were sent in the request"}""")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task ReportsAnInvalidResponseForAnythingElse(HttpStatusCode statusCode, string body)
    {
        var handler = new StubHttpMessageHandler(statusCode, body);

        var exception = await Assert.ThrowsAsync<BankException>(() => CreateClient(handler).AuthorizeAsync(Request, CancellationToken.None));

        Assert.Equal(BankFailure.InvalidResponse, exception.Failure);
    }

    private static BankClient CreateClient(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        if (timeout is not null)
        {
            httpClient.Timeout = timeout.Value;
        }

        return new BankClient(httpClient);
    }
}

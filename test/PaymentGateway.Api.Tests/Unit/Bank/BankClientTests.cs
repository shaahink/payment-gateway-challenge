using System.Net;

using PaymentGateway.Api.Bank;

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

    private static BankClient CreateClient(HttpMessageHandler handler)
    {
        return new BankClient(new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") });
    }
}

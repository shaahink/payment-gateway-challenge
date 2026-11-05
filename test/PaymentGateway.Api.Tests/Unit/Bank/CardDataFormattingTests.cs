using PaymentGateway.Api.Bank;

namespace PaymentGateway.Api.Tests.Unit.Bank;

public class CardDataFormattingTests
{
    [Fact]
    public void ABankPaymentRequestNeverPrintsTheCardNumberOrCvv()
    {
        var request = new BankPaymentRequest("4111111111111111", 4, 2027, "GBP", 100, "9871");

        var text = request.ToString();

        Assert.DoesNotContain("4111111111111111", text);
        Assert.DoesNotContain("9871", text);
        Assert.Contains("1111", text);
    }

    [Fact]
    public void ABankWireRequestNeverPrintsTheCardNumberOrCvv()
    {
        var request = new BankWireRequest("4111111111111111", "04/2027", "GBP", 100, "9871");

        var text = request.ToString();

        Assert.DoesNotContain("4111111111111111", text);
        Assert.DoesNotContain("9871", text);
    }
}

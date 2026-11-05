using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Unit.Services;

public class PaymentRequestValidatorTests
{
    private readonly PaymentRequestValidator _validator =
        new(new FixedTimeProvider(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void AcceptsAValidRequest()
    {
        Assert.Empty(_validator.Validate(ValidRequest()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2222405343248")] // 13 digits
    [InlineData("22224053432488771234")] // 20 digits
    [InlineData("2222 4053 4324 8877")]
    [InlineData("22224053432488ab")]
    [InlineData("٢٢٢٢٤٠٥٣٤٣٢٤٨٨٧٧")] // Arabic-Indic digits
    public void RejectsAnInvalidCardNumber(string? cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        AssertRejectedOn(nameof(PostPaymentRequest.CardNumber), request);
    }

    [Theory]
    [InlineData("22224053432488")] // 14 digits
    [InlineData("2222405343248877123")] // 19 digits
    public void AcceptsACardNumberAtTheLengthLimits(string cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(13)]
    public void RejectsAnInvalidExpiryMonth(int? month)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;

        AssertRejectedOn(nameof(PostPaymentRequest.ExpiryMonth), request);
    }

    [Theory]
    [InlineData(4, null)]
    [InlineData(10, 2026)] // last month
    [InlineData(12, 2025)]
    [InlineData(11, 26)] // two-digit year
    public void RejectsAMissingOrPastExpiry(int month, int? year)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;

        AssertRejectedOn(nameof(PostPaymentRequest.ExpiryYear), request);
    }

    [Theory]
    [InlineData(1, 2027)]
    [InlineData(11, 2026)] // the current month: valid until it ends (D9)
    [InlineData(12, 2026)]
    public void AcceptsAnExpiryFromTheCurrentMonthOnwards(int month, int year)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("GB")]
    [InlineData("GBPX")]
    [InlineData("gbp")] // D10: rejected, not normalised
    [InlineData("JPY")] // a real ISO code outside the three (D4)
    public void RejectsAnUnsupportedCurrency(string? currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        AssertRejectedOn(nameof(PostPaymentRequest.Currency), request);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("USD")]
    public void AcceptsTheSupportedCurrencies(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)] // D11
    [InlineData(-1)]
    public void RejectsAnAmountBelowOne(int? amount)
    {
        var request = ValidRequest();
        request.Amount = amount;

        AssertRejectedOn(nameof(PostPaymentRequest.Amount), request);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void AcceptsAPositiveAmount(int amount)
    {
        var request = ValidRequest();
        request.Amount = amount;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    public void RejectsAnInvalidCvv(string? cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        AssertRejectedOn(nameof(PostPaymentRequest.Cvv), request);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    [InlineData("012")] // the leading zero an int would lose
    public void AcceptsAValidCvv(string cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Empty(_validator.Validate(request));
    }

    private void AssertRejectedOn(string field, PostPaymentRequest request)
    {
        var error = Assert.Single(_validator.Validate(request));
        Assert.Equal(field, error.Field);
    }

    private static PostPaymentRequest ValidRequest()
    {
        return new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }
}

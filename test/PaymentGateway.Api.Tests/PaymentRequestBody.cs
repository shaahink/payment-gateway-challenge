namespace PaymentGateway.Api.Tests;

public record PaymentRequestBody(
    string? CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string? Currency,
    int? Amount,
    string? Cvv)
{
    public static PaymentRequestBody Valid => new(
        CardNumber: "2222405343248877",
        ExpiryMonth: 4,
        ExpiryYear: DateTime.UtcNow.Year + 1,
        Currency: "GBP",
        Amount: 100,
        Cvv: "123");
}

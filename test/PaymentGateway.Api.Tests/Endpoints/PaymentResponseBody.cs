namespace PaymentGateway.Api.Tests.Endpoints;

public record PaymentResponseBody(
    Guid Id,
    string Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount);

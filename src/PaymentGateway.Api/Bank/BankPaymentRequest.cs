namespace PaymentGateway.Api.Bank;

public record BankPaymentRequest(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv);

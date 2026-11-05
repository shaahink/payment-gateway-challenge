namespace PaymentGateway.Api.Bank;

public interface IBankClient
{
    Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}

public record BankPaymentRequest(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv);

public record BankAuthorization(bool Authorized, string? AuthorizationCode);

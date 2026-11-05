namespace PaymentGateway.Api.Bank;

public interface IBankClient
{
    Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}

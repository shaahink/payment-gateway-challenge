using PaymentGateway.Api.Bank;

namespace PaymentGateway.Api.Tests.Endpoints;

public class FakeBankClient : IBankClient
{
    public BankAuthorization Authorization { get; set; } = new(Authorized: true, AuthorizationCode: "auth-code");

    public List<BankPaymentRequest> Requests { get; } = [];

    public Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        return Task.FromResult(Authorization);
    }
}

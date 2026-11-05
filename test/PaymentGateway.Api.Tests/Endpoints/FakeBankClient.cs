using PaymentGateway.Api.Bank;

namespace PaymentGateway.Api.Tests.Endpoints;

public class FakeBankClient : IBankClient
{
    public BankAuthorization Authorization { get; set; } = new(Authorized: true, AuthorizationCode: "auth-code");

    public Exception? Throws { get; set; }

    public List<BankPaymentRequest> Requests { get; } = [];

    public Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (Throws is not null)
        {
            throw Throws;
        }

        return Task.FromResult(Authorization);
    }
}

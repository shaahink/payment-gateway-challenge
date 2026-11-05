using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Bank;

public class BankException : Exception
{
    public BankException(BankFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    public BankFailure Failure { get; }
}

namespace PaymentGateway.Api.Domain;

public abstract record PaymentOutcome
{
    public sealed record Processed(Payment Payment) : PaymentOutcome;

    public sealed record Rejected(IReadOnlyList<ValidationError> Errors) : PaymentOutcome;

    public sealed record BankFailed(Guid PaymentId, BankFailure Failure) : PaymentOutcome;
}

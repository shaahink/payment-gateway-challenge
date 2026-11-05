using PaymentGateway.Api.Models;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services;

public abstract record PaymentOutcome
{
    public sealed record Processed(Payment Payment) : PaymentOutcome;

    public sealed record Rejected(IReadOnlyList<ValidationError> Errors) : PaymentOutcome;
}

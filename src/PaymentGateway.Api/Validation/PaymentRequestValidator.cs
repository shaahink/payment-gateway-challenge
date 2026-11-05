using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validation;

public record ValidationError(string Field, string Message);

public class PaymentRequestValidator
{
    private static readonly string[] SupportedCurrencies = ["GBP", "EUR", "USD"];

    private readonly TimeProvider _timeProvider;

    public PaymentRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<ValidationError> Validate(PostPaymentRequest request)
    {
        var errors = new List<ValidationError>();

        if (!IsDigits(request.CardNumber, 14, 19))
        {
            errors.Add(new(nameof(request.CardNumber), "Card number must be 14-19 digits."));
        }

        if (request.ExpiryMonth is not (>= 1 and <= 12))
        {
            errors.Add(new(nameof(request.ExpiryMonth), "Expiry month must be between 1 and 12."));
        }

        if (request.ExpiryYear is null)
        {
            errors.Add(new(nameof(request.ExpiryYear), "Expiry year is required."));
        }
        else if (request.ExpiryMonth is >= 1 and <= 12 && HasExpired(request.ExpiryMonth.Value, request.ExpiryYear.Value))
        {
            errors.Add(new(nameof(request.ExpiryYear), "Card expiry must be in the future."));
        }

        if (request.Currency is null || !SupportedCurrencies.Contains(request.Currency))
        {
            errors.Add(new(nameof(request.Currency), $"Currency must be one of {string.Join(", ", SupportedCurrencies)}."));
        }

        if (request.Amount is not >= 1)
        {
            errors.Add(new(nameof(request.Amount), "Amount must be a positive integer in minor units."));
        }

        if (!IsDigits(request.Cvv, 3, 4))
        {
            errors.Add(new(nameof(request.Cvv), "CVV must be 3-4 digits."));
        }

        return errors;
    }

    private bool HasExpired(int month, int year)
    {
        var now = _timeProvider.GetUtcNow();

        return year < now.Year || (year == now.Year && month < now.Month);
    }

    private static bool IsDigits(string? value, int minLength, int maxLength)
    {
        return value is not null
            && value.Length >= minLength
            && value.Length <= maxLength
            && value.All(char.IsAsciiDigit);
    }
}

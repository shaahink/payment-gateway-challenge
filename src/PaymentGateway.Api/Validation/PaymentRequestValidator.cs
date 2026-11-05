using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validation;

public record ValidationError(string Field, string Message);

public class PaymentRequestValidator
{
    public IReadOnlyList<ValidationError> Validate(PostPaymentRequest request)
    {
        var errors = new List<ValidationError>();

        if (!IsDigits(request.CardNumber, 14, 19))
        {
            errors.Add(new(nameof(request.CardNumber), "Card number must be 14-19 digits."));
        }

        return errors;
    }

    private static bool IsDigits(string? value, int minLength, int maxLength)
    {
        return value is not null
            && value.Length >= minLength
            && value.Length <= maxLength
            && value.All(char.IsAsciiDigit);
    }
}

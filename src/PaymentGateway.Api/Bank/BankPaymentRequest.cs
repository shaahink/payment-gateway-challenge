namespace PaymentGateway.Api.Bank;

public record BankPaymentRequest(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv)
{
    public override string ToString()
    {
        return $"{nameof(BankPaymentRequest)} {{ CardNumber = {CardData.Mask(CardNumber)}, ExpiryMonth = {ExpiryMonth}, " +
            $"ExpiryYear = {ExpiryYear}, Currency = {Currency}, Amount = {Amount}, Cvv = *** }}";
    }
}

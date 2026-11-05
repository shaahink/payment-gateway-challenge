namespace PaymentGateway.Api.Bank;

internal record BankWireRequest(string CardNumber, string ExpiryDate, string Currency, int Amount, string Cvv)
{
    public override string ToString()
    {
        return $"{nameof(BankWireRequest)} {{ CardNumber = {CardData.Mask(CardNumber)}, ExpiryDate = {ExpiryDate}, " +
            $"Currency = {Currency}, Amount = {Amount}, Cvv = *** }}";
    }
}

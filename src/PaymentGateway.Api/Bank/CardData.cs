namespace PaymentGateway.Api.Bank;

internal static class CardData
{
    public static string Mask(string cardNumber)
    {
        return cardNumber.Length > 4 ? $"****{cardNumber[^4..]}" : "****";
    }
}

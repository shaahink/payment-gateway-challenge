namespace PaymentGateway.Api.Bank;

internal record BankWireRequest(string CardNumber, string ExpiryDate, string Currency, int Amount, string Cvv);

namespace PaymentGateway.Api.Bank;

internal record BankWireResponse(bool Authorized, string? AuthorizationCode);

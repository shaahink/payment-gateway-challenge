namespace PaymentGateway.Api.Bank;

public record BankAuthorization(bool Authorized, string? AuthorizationCode);

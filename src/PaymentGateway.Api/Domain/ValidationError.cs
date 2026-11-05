namespace PaymentGateway.Api.Domain;

public record ValidationError(string Field, string Message);

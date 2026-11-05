using System.Text.Json;

namespace PaymentGateway.Api.Bank;

public class BankClient : IBankClient
{
    private static readonly JsonSerializerOptions WireFormat = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly HttpClient _httpClient;

    public BankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        var wireRequest = new BankWireRequest(
            request.CardNumber,
            $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            request.Currency,
            request.Amount,
            request.Cvv);

        using var response = await _httpClient.PostAsJsonAsync("payments", wireRequest, WireFormat, cancellationToken);
        response.EnsureSuccessStatusCode();

        var wireResponse = await response.Content.ReadFromJsonAsync<BankWireResponse>(WireFormat, cancellationToken)
            ?? throw new InvalidOperationException("The bank returned an empty response.");

        return new BankAuthorization(
            wireResponse.Authorized,
            string.IsNullOrEmpty(wireResponse.AuthorizationCode) ? null : wireResponse.AuthorizationCode);
    }
}

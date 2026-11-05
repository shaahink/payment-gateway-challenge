using System.Net;
using System.Text.Json;

using PaymentGateway.Api.Domain;

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

        using var response = await SendAsync(wireRequest, cancellationToken);

        return await ReadAuthorizationAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(BankWireRequest wireRequest, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.PostAsJsonAsync("payments", wireRequest, WireFormat, cancellationToken);
        }
        catch (TaskCanceledException exception) when (exception.InnerException is TimeoutException)
        {
            throw new BankException(BankFailure.TimedOut, "The bank did not respond in time.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new BankException(BankFailure.Unavailable, "The bank could not be reached.", exception);
        }
    }

    private static async Task<BankAuthorization> ReadAuthorizationAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            throw new BankException(BankFailure.Unavailable, "The bank returned 503.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new BankException(BankFailure.InvalidResponse, $"The bank returned {(int)response.StatusCode}.");
        }

        BankWireResponse? wireResponse;
        try
        {
            wireResponse = await response.Content.ReadFromJsonAsync<BankWireResponse>(WireFormat, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new BankException(BankFailure.InvalidResponse, "The bank's response could not be read.", exception);
        }

        if (wireResponse is null)
        {
            throw new BankException(BankFailure.InvalidResponse, "The bank returned an empty response.");
        }

        return new BankAuthorization(
            wireResponse.Authorized,
            string.IsNullOrEmpty(wireResponse.AuthorizationCode) ? null : wireResponse.AuthorizationCode);
    }
}

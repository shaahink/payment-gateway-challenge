using System.Diagnostics;

namespace PaymentGateway.Api.Tests.Unit.Bank;

public class HangingHttpMessageHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);

        throw new UnreachableException();
    }
}

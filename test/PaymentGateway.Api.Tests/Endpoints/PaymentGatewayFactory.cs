using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Bank;

namespace PaymentGateway.Api.Tests.Endpoints;

public class PaymentGatewayFactory : WebApplicationFactory<Program>
{
    public FakeBankClient Bank { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services => services.AddSingleton<IBankClient>(Bank));
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Bank;

namespace PaymentGateway.Api.Tests.Endpoints;

public class PaymentGatewayFactory : WebApplicationFactory<Program>
{
    public FakeBankClient Bank { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services => services.AddSingleton<IBankClient>(Bank));
    }
}

using Microsoft.AspNetCore.Hosting;

namespace PaymentGateway.Api.Tests.Endpoints;

public class StartupTests
{
    [Theory]
    [InlineData("Bank:BaseUrl")]
    [InlineData("Bank:TimeoutSeconds")]
    public void RefusesToStartWithoutTheBankSettings(string setting)
    {
        using var factory = new PaymentGatewayFactory().WithWebHostBuilder(builder => builder.UseSetting(setting, ""));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}

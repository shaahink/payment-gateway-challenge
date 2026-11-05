using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

using PaymentGateway.Api.Bank;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    {
        var problems = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var details = problems.CreateValidationProblemDetails(
            context.HttpContext, context.ModelState, title: PaymentsController.RejectedTitle);

        return new BadRequestObjectResult(details) { ContentTypes = { "application/problem+json" } };
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentRequestValidator>();
builder.Services.AddSingleton<PaymentMetrics>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHttpClient<IBankClient, BankClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Bank:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Bank:TimeoutSeconds"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler();
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }

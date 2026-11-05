using System.Diagnostics;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    public const string RejectedTitle = "The payment was rejected.";

    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var payment = _paymentService.Get(id);

        return payment is null ? NotFound() : Ok(GetPaymentResponse.From(payment));
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(PostPaymentRequest request)
    {
        var outcome = await _paymentService.ProcessAsync(request);

        return outcome switch
        {
            PaymentOutcome.Processed processed => CreatedAtAction(
                nameof(GetPayment),
                new { id = processed.Payment.Id },
                PostPaymentResponse.From(processed.Payment)),
            PaymentOutcome.Rejected rejected => ValidationProblemFor(rejected.Errors),
            PaymentOutcome.BankFailed failed => BankProblemFor(failed),
            _ => throw new UnreachableException()
        };
    }

    private ObjectResult BankProblemFor(PaymentOutcome.BankFailed failed)
    {
        var (statusCode, title, detail) = failed.Failure switch
        {
            BankFailure.Unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                "The bank is unavailable.",
                "The payment was not processed and is safe to retry."),
            BankFailure.TimedOut => (
                StatusCodes.Status504GatewayTimeout,
                "The bank did not respond in time.",
                "The payment outcome is unknown."),
            _ => (
                StatusCodes.Status502BadGateway,
                "The bank returned an unexpected response.",
                "The payment outcome could not be confirmed.")
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, title, detail: detail);
        problem.Extensions["paymentId"] = failed.PaymentId;

        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    private ActionResult ValidationProblemFor(IReadOnlyList<ValidationError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(error.Field), error.Message);
        }

        return ValidationProblem(title: RejectedTitle);
    }
}

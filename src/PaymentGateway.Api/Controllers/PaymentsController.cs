using System.Diagnostics;

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
            _ => throw new UnreachableException()
        };
    }

    private ActionResult ValidationProblemFor(IReadOnlyList<ValidationError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem();
    }
}

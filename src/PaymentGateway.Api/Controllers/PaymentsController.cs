using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentsRepository paymentsRepository, PaymentService paymentService)
    {
        _paymentsRepository = paymentsRepository;
        _paymentService = paymentService;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(GetPaymentResponse.From(payment));
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(PostPaymentRequest request)
    {
        var outcome = await _paymentService.ProcessAsync(request);

        switch (outcome)
        {
            case PaymentOutcome.Rejected rejected:
                foreach (var error in rejected.Errors)
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }

                return ValidationProblem();

            case PaymentOutcome.Processed processed:
                return CreatedAtAction(
                    nameof(GetPayment),
                    new { id = processed.Payment.Id },
                    PostPaymentResponse.From(processed.Payment));

            default:
                throw new UnreachableException();
        }
    }
}

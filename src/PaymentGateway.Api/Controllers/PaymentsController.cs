using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly PaymentRequestValidator _validator;

    public PaymentsController(PaymentsRepository paymentsRepository, PaymentRequestValidator validator)
    {
        _paymentsRepository = paymentsRepository;
        _validator = validator;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PostPaymentResponse> GetPayment(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(payment);
    }

    [HttpPost]
    public ActionResult<PostPaymentResponse> PostPayment(PostPaymentRequest request)
    {
        foreach (var error in _validator.Validate(request))
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        throw new NotImplementedException();
    }
}
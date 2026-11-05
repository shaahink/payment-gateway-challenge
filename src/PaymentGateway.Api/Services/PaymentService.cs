using PaymentGateway.Api.Bank;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services;

public class PaymentService
{
    private readonly PaymentRequestValidator _validator;
    private readonly IBankClient _bank;
    private readonly PaymentsRepository _repository;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentRequestValidator validator,
        IBankClient bank,
        PaymentsRepository repository,
        ILogger<PaymentService> logger)
    {
        _validator = validator;
        _bank = bank;
        _repository = repository;
        _logger = logger;
    }

    public async Task<PaymentOutcome> ProcessAsync(PostPaymentRequest request)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            _logger.LogInformation("Payment rejected on {Fields}", errors.Select(e => e.Field));
            return new PaymentOutcome.Rejected(errors);
        }

        var bankRequest = new BankPaymentRequest(
            request.CardNumber!,
            request.ExpiryMonth!.Value,
            request.ExpiryYear!.Value,
            request.Currency!,
            request.Amount!.Value,
            request.Cvv!);

        // Not tied to the merchant's request: abandoning a call the bank may already have
        // authorised would leave a payment nobody has a record of.
        var authorization = await _bank.AuthorizeAsync(bankRequest, CancellationToken.None);

        var payment = new Payment(
            Guid.NewGuid(),
            authorization.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            bankRequest.CardNumber[^4..],
            bankRequest.ExpiryMonth,
            bankRequest.ExpiryYear,
            bankRequest.Currency,
            bankRequest.Amount);

        _repository.Add(payment);
        _logger.LogInformation("Payment {PaymentId} processed: {Status}", payment.Id, payment.Status);

        return new PaymentOutcome.Processed(payment);
    }
}

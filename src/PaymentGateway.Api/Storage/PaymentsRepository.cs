using System.Collections.Concurrent;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Storage;

public class PaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public void Add(Payment payment)
    {
        _payments[payment.Id] = payment;
    }

    public Payment? Get(Guid id)
    {
        return _payments.TryGetValue(id, out var payment) ? payment : null;
    }
}

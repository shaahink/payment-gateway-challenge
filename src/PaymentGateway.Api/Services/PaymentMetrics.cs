using System.Diagnostics.Metrics;

namespace PaymentGateway.Api.Services;

public class PaymentMetrics
{
    public const string MeterName = "PaymentGateway";
    public const string PaymentsCounterName = "payment_gateway.payments";

    private readonly Counter<long> _payments;

    public PaymentMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _payments = meter.CreateCounter<long>(PaymentsCounterName, description: "Payment requests by outcome.");
    }

    public void Record(string outcome)
    {
        _payments.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}

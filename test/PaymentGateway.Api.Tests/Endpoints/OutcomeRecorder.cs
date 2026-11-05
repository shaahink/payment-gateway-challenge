using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Endpoints;

public sealed class OutcomeRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<string> _outcomes = new();

    public OutcomeRecorder(IMeterFactory meterFactory)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, meterFactory) && instrument.Name == PaymentMetrics.PaymentsCounterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    _outcomes.Enqueue((string)tag.Value!);
                }
            }
        });
        _listener.Start();
    }

    public IEnumerable<string> Outcomes => _outcomes;

    public void Dispose()
    {
        _listener.Dispose();
    }
}

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests.Endpoints;

public class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IEnumerable<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(_entries);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _entries;

        public CapturingLogger(ConcurrentQueue<string> entries)
        {
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(" ", pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                : string.Empty;

            _entries.Enqueue($"{formatter(state, exception)} {values} {exception}");
        }
    }
}

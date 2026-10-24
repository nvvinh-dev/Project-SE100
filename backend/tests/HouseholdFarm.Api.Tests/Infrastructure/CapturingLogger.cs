using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HouseholdFarm.Api.Tests.Infrastructure;

// Logger ghi lại các dòng log để test kiểm tra lỗi có được ghi log hay không.
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, Exception? Exception, string Message)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, Exception? Exception, string Message)> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue((logLevel, exception, formatter(state, exception)));
}

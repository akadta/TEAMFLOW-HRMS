using System.Collections.Concurrent;

namespace hrms.Shared.Services;

public class LoginThrottle
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(3);

    private sealed class Entry
    {
        public readonly object Gate = new();
        public int Failures;
        public DateTime? WindowStart;
        public DateTime? LockedUntil;
        public DateTime LastSeen;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>Time left on the lock, or null if the key is not locked.</summary>
    public TimeSpan? GetRemainingLock(string key)
    {
        if (!_entries.TryGetValue(key, out var e)) return null;

        var now = DateTime.UtcNow;
        lock (e.Gate)
        {
            Refresh(e, now);
            return e.LockedUntil is { } until ? until - now : null;
        }
    }

    /// <summary>Records a failed attempt. Returns attempts left, or the lock time if this attempt triggered it.</summary>
    public (int AttemptsLeft, TimeSpan? LockedFor) RegisterFailure(string key)
    {
        var now = DateTime.UtcNow;
        var e = Get(key, now);

        lock (e.Gate)
        {
            Refresh(e, now);

            e.WindowStart ??= now;
            e.Failures++;

            if (e.Failures >= MaxFailures)
            {
                e.LockedUntil = now + LockDuration;
                return (0, LockDuration);
            }

            return (MaxFailures - e.Failures, null);
        }
    }

    private static void Refresh(Entry e, DateTime now)
    {
        // lock finished
        if (e.LockedUntil is { } until && until <= now)
        {
            Reset(e);
            return;
        }

        // failures older than the window are forgotten
        if (e.LockedUntil is null && e.WindowStart is { } start && now - start >= LockDuration)
            Reset(e);
    }

    private static void Reset(Entry e)
    {
        e.Failures = 0;
        e.WindowStart = null;
        e.LockedUntil = null;
    }

    private Entry Get(string key, DateTime now)
    {
        if (_entries.Count > 10_000) Sweep(now);

        var e = _entries.GetOrAdd(key, _ => new Entry());
        e.LastSeen = now;
        return e;
    }

    private void Sweep(DateTime now)
    {
        foreach (var (k, v) in _entries)
            if (now - v.LastSeen > LockDuration * 2)
                _entries.TryRemove(k, out _);
    }
}
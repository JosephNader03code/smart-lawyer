namespace SmartLawyer.Core.Timeline;

/// <summary>قيد زمني: min ≤ t(To) − t(From) ≤ max (بالدقايق). Reason بيوضح جه منين (مستند أو قاعدة منطقية).</summary>
public sealed record TemporalConstraint(string From, string To, double MinMinutes, double MaxMinutes, string Reason);

public sealed record TemporalConflict(IReadOnlyList<string> Cycle, IReadOnlyList<TemporalConstraint> Constraints, double ViolationMinutes);

/// <summary>
/// Simple Temporal Network: كل حدث نقطة زمنية، وكل قيد بيتحول لـ edges في graph:
///   t(To) − t(From) ≤ max  ⇒  edge From→To بوزن max
///   t(To) − t(From) ≥ min  ⇒  edge To→From بوزن −min
/// القيود متسقة لو مفيش negative cycle. بنكشفه بـ Floyd-Warshall، وبنرجّع الأحداث والقيود اللي عاملة التعارض
/// عشان المحامي يفهم التعارض جه منين.
/// </summary>
public sealed class SimpleTemporalNetwork
{
    /// <summary>نقطة مرجعية: الأحداث اللي تاريخها معروف بتتربط بيها.</summary>
    public const string Origin = "__origin__";
    private static readonly DateTime Epoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly List<TemporalConstraint> _constraints = [];
    private readonly List<string> _nodes = [Origin];

    public IReadOnlyList<TemporalConstraint> Constraints => _constraints;

    public void AddConstraint(TemporalConstraint c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.MinMinutes > c.MaxMinutes)
        {
            throw new ArgumentException("الحد الأدنى أكبر من الأقصى", nameof(c));
        }
        AddNode(c.From);
        AddNode(c.To);
        _constraints.Add(c);
    }

    /// <summary>حدث وقته معروف من مستند، بهامش (مثلاً ±0 لو بالدقيقة، أو اليوم كله لو التاريخ من غير ساعة).</summary>
    public void AddKnownTime(string eventId, DateTime earliest, DateTime latest, string reason) =>
        AddConstraint(new(Origin, eventId, (earliest - Epoch).TotalMinutes, (latest - Epoch).TotalMinutes, reason));

    /// <summary>قاعدة منطقية: الحدث second لازم يحصل بعد first (أو في نفس الوقت).</summary>
    public void AddMustFollow(string first, string second, string reason) =>
        AddConstraint(new(first, second, 0, double.PositiveInfinity, reason));

    public TemporalConflict? FindConflict()
    {
        var n = _nodes.Count;
        var dist = new double[n, n];
        var via = new TemporalConstraint?[n, n];
        var next = new int[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                dist[i, j] = i == j ? 0 : double.PositiveInfinity;
                next[i, j] = j;
            }
        }

        foreach (var c in _constraints)
        {
            int f = _nodes.IndexOf(c.From), t = _nodes.IndexOf(c.To);
            if (c.MaxMinutes < dist[f, t])
            {
                dist[f, t] = c.MaxMinutes;
                via[f, t] = c;
            }
            if (-c.MinMinutes < dist[t, f])
            {
                dist[t, f] = -c.MinMinutes;
                via[t, f] = c;
            }
        }

        for (var k = 0; k < n; k++)
        {
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    if (dist[i, k] + dist[k, j] < dist[i, j])
                    {
                        dist[i, j] = dist[i, k] + dist[k, j];
                        next[i, j] = next[i, k];
                    }
                }
                if (dist[i, i] < -1e-9)
                {
                    return BuildConflict(i, dist[i, i], next, via);
                }
            }
        }
        return null;
    }

    private TemporalConflict BuildConflict(int start, double violation, int[,] next, TemporalConstraint?[,] via)
    {
        var cycle = new List<string> { _nodes[start] };
        var used = new List<TemporalConstraint>();
        var current = start;
        for (var guard = 0; guard <= _nodes.Count; guard++)
        {
            var step = next[current, start];
            if (via[current, step] is { } c && !used.Contains(c))
            {
                used.Add(c);
            }
            current = step;
            if (current == start)
            {
                break;
            }
            cycle.Add(_nodes[current]);
        }
        return new TemporalConflict(cycle.Where(x => x != Origin).ToList(), used, -violation);
    }

    private void AddNode(string id)
    {
        if (!_nodes.Contains(id))
        {
            _nodes.Add(id);
        }
    }
}

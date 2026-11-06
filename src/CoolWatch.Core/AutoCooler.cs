namespace CoolWatch.Core;

/// <summary>
/// Automatyczne chłodzenie: gdy temperatura przekroczy próg, obniża limit procesora,
/// a po ochłodzeniu (histereza) przywraca poprzedni limit.
/// </summary>
public class AutoCooler
{
    readonly int _threshold, _hysteresis, _coolLimit;
    int _restore = CpuLimiter.Max;

    public bool Active { get; private set; }

    public AutoCooler(int thresholdC = 85, int hysteresisC = 8, int coolLimit = 70)
    {
        _threshold = thresholdC;
        _hysteresis = hysteresisC;
        _coolLimit = CpuLimiter.Clamp(coolLimit);
    }

    /// <summary>Zwraca nowy limit do ustawienia albo null, gdy nic nie trzeba zmieniać.</summary>
    public int? Evaluate(double tempC, int currentLimit)
    {
        if (!Active && tempC >= _threshold)
        {
            Active = true;
            _restore = currentLimit;
            return Math.Min(_coolLimit, currentLimit);
        }
        if (Active && tempC <= _threshold - _hysteresis)
        {
            Active = false;
            return _restore;
        }
        return null;
    }
}

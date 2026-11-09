namespace CoolWatch.Core;

/// <summary>Bufor kołowy ostatnich N pomiarów (do wykresu).</summary>
public class History
{
    readonly Queue<double> _items = new();
    readonly int _capacity;

    public History(int capacity = 60) => _capacity = Math.Max(1, capacity);

    public void Add(double value)
    {
        _items.Enqueue(value);
        while (_items.Count > _capacity) _items.Dequeue();
    }

    public IReadOnlyList<double> Values => _items.ToList();
}

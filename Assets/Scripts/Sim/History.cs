namespace Meridian.Sim
{
    // Daily history of the player country's key indicators, for the panel sparkline charts.
    // Fixed-capacity ring buffers (4 years of daily samples ≈ one full term) — old samples
    // fall off the far end, so memory is constant no matter how long a game runs.

    public class HistorySeries
    {
        readonly float[] buf;
        int head;   // index of the next write
        int count;

        public HistorySeries(int capacity) { buf = new float[capacity]; }

        public int Count => count;

        public void Add(float v)
        {
            buf[head] = v;
            head = (head + 1) % buf.Length;
            if (count < buf.Length) count++;
        }

        // i = 0 is the OLDEST retained sample, i = Count-1 the newest.
        public float this[int i] => buf[(head - count + i + buf.Length * 2) % buf.Length];

        public void Clear() { head = 0; count = 0; }

        // Chronological snapshot / restore, for the save system.
        public float[] ToArray()
        {
            var arr = new float[count];
            for (int i = 0; i < count; i++) arr[i] = this[i];
            return arr;
        }

        public void LoadFrom(float[] values)
        {
            Clear();
            if (values == null) return;
            // If the snapshot somehow exceeds capacity, keep the newest samples (Add discards
            // oldest naturally).
            foreach (var v in values) Add(v);
        }

        public (float min, float max) Range()
        {
            if (count == 0) return (0f, 1f);
            float mn = this[0], mx = this[0];
            for (int i = 1; i < count; i++)
            {
                float v = this[i];
                if (v < mn) mn = v;
                if (v > mx) mx = v;
            }
            return (mn, mx);
        }
    }

    public static class PlayerHistory
    {
        public const int Capacity = 1460; // one 4-year term of daily samples

        public static readonly HistorySeries Gdp = new(Capacity);
        public static readonly HistorySeries Growth = new(Capacity);
        public static readonly HistorySeries Approval = new(Capacity);
        public static readonly HistorySeries Treasury = new(Capacity);
        public static readonly HistorySeries Unemployment = new(Capacity);
        public static readonly HistorySeries Inflation = new(Capacity);

        public static void Record(EconomyState e, NationalState n)
        {
            Gdp.Add((float)e.Gdp);
            Growth.Add(e.GrowthRate);
            Treasury.Add((float)e.Treasury);
            Unemployment.Add(e.Unemployment);
            Inflation.Add(e.Inflation);
            if (n != null) Approval.Add(n.ApprovalRating);
        }

        public static void Reset()
        {
            Gdp.Clear(); Growth.Clear(); Approval.Clear();
            Treasury.Clear(); Unemployment.Clear(); Inflation.Clear();
        }
    }
}

using System.Collections.Generic;

namespace Meridian.Sim
{
    // One-way headline queue from the simulation to the UI toast feed. Sim code pushes
    // human-readable world events (war declarations, peaces, AI agreements); GameUIRoot
    // drains it on its refresh tick. Keeps Sim/ free of any UI dependency.
    public static class WorldFeed
    {
        static readonly Queue<(string source, string message)> queue = new();

        public static void Push(string source, string message) => queue.Enqueue((source, message));

        public static bool TryDequeue(out string source, out string message)
        {
            if (queue.Count > 0)
            {
                (source, message) = queue.Dequeue();
                return true;
            }
            source = message = null;
            return false;
        }

        public static void Clear() => queue.Clear();
    }
}

namespace Meridian.UI
{
    // Shared UI state (which ministry category is active) — single-player, single-scene, so a
    // small static holder is simpler than threading a reference through every UI component.
    public static class UIState
    {
        public static NationCategory ActiveCategory = NationCategory.Economy;

        // null/empty = show the category's general overview. Set when a specific topic is
        // picked from a ministry's hover dropdown (e.g. "Tax Rates" under Economy), so the side
        // panel can jump straight to that focused sub-view instead of the whole category mixed
        // together.
        public static string ActiveTopic = null;
    }
}

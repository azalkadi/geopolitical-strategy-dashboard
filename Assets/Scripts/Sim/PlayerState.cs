namespace Meridian.Sim
{
    public enum GameState { NotStarted, Playing, GameOver }

    // Minimal game-layer state: which country the human is playing, and the election/term
    // mechanic that gives the simulation real stakes. Re-election is decided by
    // NationalState.ApprovalRating — the exact same number already shown on the Politics tab
    // and already driven by how well the economy is run — so there's no separate "win" stat
    // bolted on, just consequences attached to a number that was already meaningful.
    public static class PlayerState
    {
        public const long TermLengthDays = 1460; // one 4-year term

        public static GameState State = GameState.NotStarted;
        public static int CountryIndex = -1;
        public static string CountryName = "";
        public static long TermStartDay;
        public static int TermsServed;
        public static string LastResultMessage = "";
        public static bool WonLastElection = true;

        public static void Reset()
        {
            State = GameState.NotStarted;
            CountryIndex = -1;
            CountryName = "";
            TermStartDay = 0;
            TermsServed = 0;
            LastResultMessage = "";
            WonLastElection = true;
        }

        public static void Begin(int countryIndex, string countryName, long currentDay)
        {
            CountryIndex = countryIndex;
            CountryName = countryName;
            TermStartDay = currentDay;
            TermsServed = 0;
            LastResultMessage = "";
            State = GameState.Playing;
        }
    }
}

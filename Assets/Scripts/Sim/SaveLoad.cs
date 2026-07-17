using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace Meridian.Sim
{
    // Whole-simulation snapshot to a single JSON file in persistentDataPath. The sim classes
    // keep ALL state in public members precisely so this can be a dumb, complete serialization
    // — a loaded game is bit-for-bit the same simulation, not a reconstruction.
    //
    // Geography (countries/roads/cities meshes) is NOT saved — it reloads from StreamingAssets
    // on boot exactly as for a new game; only the mutable simulation layered on top is here.
    // The CountryCount guard refuses a save made against different geo data (indices would
    // point at the wrong countries).
    public class SaveGame
    {
        public int Version = 1;
        public string SavedAtUtc;
        public int CountryCount;

        public long SimDay;
        public float DaysPerSecond;

        // PlayerState
        public GameState State;
        public int PlayerCountryIndex;
        public string PlayerCountryName;
        public long TermStartDay;
        public int TermsServed;
        public string LastResultMessage;
        public bool WonLastElection;

        public long NextEventDay;

        public List<EconomyState> Economies;
        public List<NationalState> Nationals;
        public DiplomacySystem Diplomacy;
        public WarSystem Wars;
        public InfrastructureSystem Infrastructure;

        public Dictionary<string, float[]> History;
    }

    public static class SaveLoad
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "meridian_save.json");

        public static bool SaveExists() => File.Exists(SavePath);

        public static bool Save(long simDay, float daysPerSecond, EconomySystem econ, NationalSystem nat, DiplomacySystem dip, WarSystem wars, InfrastructureSystem infra)
        {
            try
            {
                var save = new SaveGame
                {
                    SavedAtUtc = System.DateTime.UtcNow.ToString("o"),
                    CountryCount = econ.States.Count,
                    SimDay = simDay,
                    DaysPerSecond = daysPerSecond,
                    State = PlayerState.State,
                    PlayerCountryIndex = PlayerState.CountryIndex,
                    PlayerCountryName = PlayerState.CountryName,
                    TermStartDay = PlayerState.TermStartDay,
                    TermsServed = PlayerState.TermsServed,
                    LastResultMessage = PlayerState.LastResultMessage,
                    WonLastElection = PlayerState.WonLastElection,
                    NextEventDay = EventSystem.NextEventDay,
                    Economies = econ.States,
                    Nationals = nat.States,
                    Diplomacy = dip,
                    Wars = wars,
                    Infrastructure = infra,
                    History = new Dictionary<string, float[]>
                    {
                        ["gdp"] = PlayerHistory.Gdp.ToArray(),
                        ["growth"] = PlayerHistory.Growth.ToArray(),
                        ["approval"] = PlayerHistory.Approval.ToArray(),
                        ["treasury"] = PlayerHistory.Treasury.ToArray(),
                        ["unemployment"] = PlayerHistory.Unemployment.ToArray(),
                        ["inflation"] = PlayerHistory.Inflation.ToArray(),
                    },
                };

                // Write-then-rename so a crash mid-write can't leave a corrupt save as the
                // only copy.
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(save));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
                Debug.Log($"[save] game saved (day {simDay}) to {SavePath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[save] save failed: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        // Reads and validates the save. Returns null (with a log line) on any problem —
        // callers treat null as "no usable save" and fall back to a fresh start.
        public static SaveGame TryRead(int expectedCountryCount)
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                var save = JsonConvert.DeserializeObject<SaveGame>(File.ReadAllText(SavePath));
                if (save == null) { Debug.LogWarning("[save] save file unreadable"); return null; }
                if (save.CountryCount != expectedCountryCount)
                {
                    Debug.LogWarning($"[save] save was made against different geo data ({save.CountryCount} countries vs {expectedCountryCount}) — ignoring it");
                    return null;
                }
                if (save.Economies == null || save.Economies.Count != expectedCountryCount ||
                    save.Nationals == null || save.Nationals.Count != expectedCountryCount ||
                    save.Diplomacy == null || save.Wars == null)
                {
                    Debug.LogWarning("[save] save file incomplete — ignoring it");
                    return null;
                }
                return save;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[save] couldn't read save: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // Applies a validated save onto the live systems. PlayerHistory and PlayerState are
        // statics; economy/national lists are replaced wholesale.
        public static void Apply(SaveGame save, EconomySystem econ, NationalSystem nat)
        {
            econ.States = save.Economies;
            nat.States = save.Nationals;

            PlayerState.State = save.State;
            PlayerState.CountryIndex = save.PlayerCountryIndex;
            PlayerState.CountryName = save.PlayerCountryName;
            PlayerState.TermStartDay = save.TermStartDay;
            PlayerState.TermsServed = save.TermsServed;
            PlayerState.LastResultMessage = save.LastResultMessage;
            PlayerState.WonLastElection = save.WonLastElection;

            EventSystem.Pending = null;
            EventSystem.NextEventDay = save.NextEventDay;
            WorldFeed.Clear();

            PlayerHistory.Gdp.LoadFrom(save.History?.GetValueOrDefault("gdp"));
            PlayerHistory.Growth.LoadFrom(save.History?.GetValueOrDefault("growth"));
            PlayerHistory.Approval.LoadFrom(save.History?.GetValueOrDefault("approval"));
            PlayerHistory.Treasury.LoadFrom(save.History?.GetValueOrDefault("treasury"));
            PlayerHistory.Unemployment.LoadFrom(save.History?.GetValueOrDefault("unemployment"));
            PlayerHistory.Inflation.LoadFrom(save.History?.GetValueOrDefault("inflation"));

            Debug.Log($"[save] game loaded: day {save.SimDay}, governing {save.PlayerCountryName}");
        }
    }
}

namespace Meridian.Sim
{
    // First slice of the "Economic Sectors and Companies" vision pillar (see
    // docs/obsidian-vault/Vision/Economic Sectors and Companies.md) — real named companies
    // within real industry sectors, with a player-changeable ownership model. Deliberately
    // scoped: this does NOT yet feed sector output into the GDP formula (EconomyState.Tick
    // still runs its existing macro model untouched) — OutputBillions only sizes the one-time
    // buyout/sale cash flow when ownership changes (see LegislatureSystem). Modeling sectors as
    // truly composing GDP is real future work, not silently faked here.

    public enum Sector { Energy, Agriculture, Manufacturing, Technology, Finance, Services, Mining, Construction, Defense, Healthcare }

    public enum Ownership { Public, Private, Mixed }

    // Static seed data (CountryProfiles) — real, well-known companies, not fabricated. Revenue
    // figures are approximate/rounded public-knowledge figures for gameplay sizing, not audited
    // financials.
    public class CompanySeed
    {
        public string Name = "";
        public Sector Sector;
        public Ownership Ownership;
        public double OutputBillions;
        public CompanySeed() { }
        public CompanySeed(string name, Sector sector, Ownership ownership, double outputBillions)
        { Name = name; Sector = sector; Ownership = ownership; OutputBillions = outputBillions; }
    }

    // The mutable per-game copy — EconomyState.Seed copies from CompanySeed so ownership
    // changes during play never mutate the shared static CountryProfiles data (which is reused
    // across every new game/save in the process).
    public class Company
    {
        public string Name = "";
        public Sector Sector;
        public Ownership Ownership;
        public double OutputBillions;

        public string SectorLabel => Sector switch
        {
            Sector.Energy => "Energy", Sector.Agriculture => "Agriculture", Sector.Manufacturing => "Manufacturing",
            Sector.Technology => "Technology", Sector.Finance => "Finance", Sector.Services => "Services",
            Sector.Mining => "Mining", Sector.Construction => "Construction", Sector.Defense => "Defense",
            _ => "Healthcare",
        };

        public string OwnershipLabel => Ownership switch
        {
            Ownership.Public => "Public", Ownership.Mixed => "Mixed", _ => "Private",
        };
    }
}

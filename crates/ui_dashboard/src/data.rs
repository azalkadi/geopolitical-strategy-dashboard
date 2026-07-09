//! Mock/placeholder world data for the UI prototype. Entirely fictional (Velmoria and
//! neighbors) per the design handoff — swap for live simulation data once sim_core exists.

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Alliance {
    Home,
    Ally,
    Neutral,
    Rival,
}

impl Alliance {
    pub fn label(&self) -> &'static str {
        match self {
            Alliance::Home => "HOME NATION",
            Alliance::Ally => "ALLY",
            Alliance::Neutral => "NEUTRAL",
            Alliance::Rival => "RIVAL",
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Trend {
    Improving,
    Stable,
    Worsening,
}

impl Trend {
    pub fn arrow_label(&self) -> &'static str {
        match self {
            Trend::Improving => "▲ Improving",
            Trend::Stable => "– Stable",
            Trend::Worsening => "▼ Worsening",
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Resource {
    Energy,
    Mineral,
    Agri,
    Mixed,
}

#[derive(Clone, Debug)]
pub struct Nation {
    pub id: &'static str,
    pub name: &'static str,
    pub code: &'static str,
    pub region: &'static str,
    pub gov_type: &'static str,
    pub capital: &'static str,
    pub x: f32,
    pub y: f32,
    pub pop: &'static str,
    pub gdp: i32,
    pub growth: f32,
    pub approval: i32,
    pub unrest: i32,
    pub readiness: i32,
    pub climate_risk: i32,
    pub resource: Resource,
    pub alliance: Alliance,
    pub election_days: Option<i32>,
    pub trend: Trend,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Severity {
    Info,
    Notice,
    Warning,
    Critical,
}

impl Severity {
    pub fn label(&self) -> &'static str {
        match self {
            Severity::Info => "INFO",
            Severity::Notice => "NOTE",
            Severity::Warning => "WARN",
            Severity::Critical => "CRIT",
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Category {
    Economy,
    Trade,
    Politics,
    Diplomacy,
    Military,
    Unrest,
    Climate,
}

impl Category {
    pub fn label(&self) -> &'static str {
        match self {
            Category::Economy => "Economy",
            Category::Trade => "Trade",
            Category::Politics => "Politics",
            Category::Diplomacy => "Diplomacy",
            Category::Military => "Military",
            Category::Unrest => "Unrest",
            Category::Climate => "Climate",
        }
    }
}

#[derive(Clone, Debug)]
pub struct WorldEvent {
    pub id: &'static str,
    pub severity: Severity,
    pub category: Category,
    pub ts: &'static str,
    pub title: &'static str,
    pub why: &'static str,
    pub country: Option<&'static str>,
    pub chain: Vec<&'static str>,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum MinisterStatus {
    Clear,
    Watch,
    Scandal,
}

#[derive(Clone, Debug)]
pub struct Minister {
    pub name: &'static str,
    pub portfolio: &'static str,
    pub party: &'static str,
    pub approval: i32,
    pub status: MinisterStatus,
}

#[derive(Clone, Debug)]
pub struct Sector {
    pub name: &'static str,
    pub gdp_share: f32,
    pub growth: f32,
    pub employ: i32,
    pub note: &'static str,
}

#[derive(Clone, Debug)]
pub struct BudgetItem {
    pub label: &'static str,
    pub pct: i32,
    pub delta: f32,
}

#[derive(Clone, Debug)]
pub struct Party {
    pub name: &'static str,
    pub seats: i32,
    pub stance: &'static str,
    pub is_accent: bool,
}

#[derive(Clone, Debug)]
pub struct CorruptionFlag {
    pub severity: Severity,
    pub title: &'static str,
    pub desc: &'static str,
}

#[derive(Clone, Debug)]
pub struct NamedStat {
    pub label: &'static str,
    pub value: &'static str,
    pub why: &'static str,
}

#[derive(Clone, Debug)]
pub struct Hotspot {
    pub name: &'static str,
    pub value: i32,
    pub note: &'static str,
}

#[derive(Clone, Debug)]
pub struct Branch {
    pub name: &'static str,
    pub personnel: &'static str,
    pub readiness: i32,
    pub note: &'static str,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum DeploymentStatus {
    Ongoing,
    Concluded,
}

#[derive(Clone, Debug)]
pub struct Deployment {
    pub name: &'static str,
    pub location: &'static str,
    pub personnel: &'static str,
    pub status: DeploymentStatus,
}

#[derive(Clone, Debug)]
pub struct Treaty {
    pub name: &'static str,
    pub parties: &'static str,
    pub status: &'static str,
}

pub struct World {
    pub nations: Vec<Nation>,
    pub events: Vec<WorldEvent>,
    pub ministers: Vec<Minister>,
    pub sectors: Vec<Sector>,
    pub budget: Vec<BudgetItem>,
    pub parties: Vec<Party>,
    pub corruption: Vec<CorruptionFlag>,
    pub approval_series: Vec<f32>,
    pub approval_marker_idx: Vec<usize>,
    pub society_stats: Vec<NamedStat>,
    pub hotspots: Vec<Hotspot>,
    pub military_stats: Vec<NamedStat>,
    pub branches: Vec<Branch>,
    pub deployments: Vec<Deployment>,
    pub treaties: Vec<Treaty>,
    pub gdp_series: Vec<f32>,
    pub approval_series_dash: Vec<f32>,
    pub unrest_series: Vec<f32>,
    pub readiness_series: Vec<f32>,
}

impl World {
    pub fn home_id(&self) -> &'static str {
        "velmoria"
    }

    pub fn nation(&self, id: &str) -> Option<&Nation> {
        self.nations.iter().find(|n| n.id == id)
    }

    /// Simple placeholder model: base GDP * blended effective rate, just enough to make
    /// the tax sliders visibly move a derived number (see the "Tax Policy" card).
    pub fn projected_annual_revenue(&self, corporate: f32, income: f32, vat: f32) -> f32 {
        let home_gdp = self.nation(self.home_id()).map(|n| n.gdp as f32).unwrap_or(0.0);
        let blended = (corporate * 0.35 + income * 0.45 + vat * 0.20) / 100.0;
        home_gdp * blended
    }

    pub fn mock() -> Self {
        use Alliance::*;
        use Resource::*;
        use Trend::*;

        let nations = vec![
            Nation { id: "velmoria", name: "Velmoria", code: "VLM", region: "Home Nation", gov_type: "Federal Republic", capital: "Astervale", x: 22.0, y: 36.0, pop: "84M", gdp: 2140, growth: 1.8, approval: 47, unrest: 38, readiness: 71, climate_risk: 34, resource: Mixed, alliance: Home, election_days: Some(214), trend: Stable },
            Nation { id: "kesh", name: "Kesh Federation", code: "KSH", region: "Northern Bloc", gov_type: "One-Party State", capital: "Kesh-Ordu", x: 56.0, y: 26.0, pop: "210M", gdp: 3760, growth: 2.4, approval: 61, unrest: 22, readiness: 88, climate_risk: 41, resource: Energy, alliance: Rival, election_days: None, trend: Worsening },
            Nation { id: "adrenne", name: "Adrenne Republic", code: "ADR", region: "Southern Basin", gov_type: "Presidential Republic", capital: "Adrenne City", x: 50.0, y: 56.0, pop: "46M", gdp: 610, growth: -0.6, approval: 33, unrest: 58, readiness: 44, climate_risk: 66, resource: Energy, alliance: Neutral, election_days: Some(41), trend: Worsening },
            Nation { id: "thurland", name: "Thurland", code: "THL", region: "Eastern Rim", gov_type: "Parliamentary Democracy", capital: "Shinkoto", x: 80.0, y: 33.0, pop: "128M", gdp: 4310, growth: 3.1, approval: 58, unrest: 19, readiness: 66, climate_risk: 37, resource: Mixed, alliance: Ally, election_days: Some(302), trend: Improving },
            Nation { id: "sundara", name: "Sundara Isles", code: "SND", region: "Eastern Rim", gov_type: "Constitutional Monarchy", capital: "Palur", x: 83.0, y: 61.0, pop: "31M", gdp: 340, growth: 4.2, approval: 64, unrest: 15, readiness: 38, climate_risk: 78, resource: Agri, alliance: Ally, election_days: None, trend: Stable },
            Nation { id: "meraux", name: "Meraux Union", code: "MRX", region: "Western Basin", gov_type: "Federal Republic", capital: "Meraux", x: 21.0, y: 63.0, pop: "96M", gdp: 980, growth: 1.2, approval: 41, unrest: 47, readiness: 52, climate_risk: 55, resource: Mineral, alliance: Neutral, election_days: Some(88), trend: Stable },
            Nation { id: "norvask", name: "Norvask", code: "NVK", region: "Northern Bloc", gov_type: "Parliamentary Democracy", capital: "Norvik", x: 46.0, y: 13.0, pop: "9M", gdp: 410, growth: 2.0, approval: 71, unrest: 8, readiness: 44, climate_risk: 22, resource: Energy, alliance: Ally, election_days: Some(190), trend: Stable },
            Nation { id: "kalidane", name: "Kalidane", code: "KLD", region: "Central Rim", gov_type: "Parliamentary Democracy", capital: "Kalipur", x: 64.0, y: 50.0, pop: "340M", gdp: 3980, growth: 5.8, approval: 55, unrest: 44, readiness: 60, climate_risk: 61, resource: Mixed, alliance: Neutral, election_days: Some(132), trend: Improving },
            Nation { id: "ostrava", name: "Ostrava Pact", code: "OSV", region: "Northern Bloc", gov_type: "Military Junta", capital: "Ostrava", x: 59.0, y: 18.0, pop: "24M", gdp: 260, growth: -1.4, approval: 29, unrest: 63, readiness: 79, climate_risk: 44, resource: Mineral, alliance: Rival, election_days: None, trend: Worsening },
            Nation { id: "barrow", name: "Barrow Coast", code: "BRW", region: "Arctic Rim", gov_type: "Constitutional Monarchy", capital: "Barrowhead", x: 11.0, y: 21.0, pop: "18M", gdp: 720, growth: 2.6, approval: 68, unrest: 11, readiness: 41, climate_risk: 29, resource: Energy, alliance: Ally, election_days: Some(260), trend: Improving },
            Nation { id: "tallissa", name: "Tallissa", code: "TLS", region: "Southern Basin", gov_type: "Presidential Republic", capital: "Tallissa City", x: 37.0, y: 70.0, pop: "58M", gdp: 390, growth: 3.4, approval: 49, unrest: 36, readiness: 33, climate_risk: 70, resource: Agri, alliance: Neutral, election_days: Some(55), trend: Worsening },
            Nation { id: "firaq", name: "Firaq Emirates", code: "FRQ", region: "Central Rim", gov_type: "Monarchy", capital: "Firaq", x: 55.0, y: 44.0, pop: "12M", gdp: 890, growth: 1.1, approval: 74, unrest: 14, readiness: 55, climate_risk: 82, resource: Energy, alliance: Ally, election_days: None, trend: Stable },
        ];

        let events = vec![
            WorldEvent { id: "e1", severity: Severity::Critical, category: Category::Trade, ts: "09:41 · Today", title: "Steel export volume falls 12%", why: "Velmorian steel exports drop sharply after Kesh tariff retaliation", country: Some("velmoria"), chain: vec!["Kesh Federation enacts 18% retaliatory tariff on Velmorian steel (Mar 6)", "Contract cancellations reported at 3 major exporters (Mar 8)", "Steel export volume falls 12% week-over-week (today)"] },
            WorldEvent { id: "e2", severity: Severity::Warning, category: Category::Politics, ts: "08:15 · Today", title: "Coalition partner threatens walkout", why: "Agrarian Bloc unhappy with subsidy cuts in draft budget", country: Some("velmoria"), chain: vec!["Draft budget cuts agricultural subsidies 9% (Mar 5)", "Agrarian Bloc leadership convenes emergency session (Mar 7)", "Public walkout threat issued (today)"] },
            WorldEvent { id: "e3", severity: Severity::Notice, category: Category::Diplomacy, ts: "Yesterday · 18:22", title: "Thurland proposes joint patrol pact", why: "Response to rising tension in shared trade corridor", country: Some("thurland"), chain: vec!["Piracy incidents rise in shared shipping lane (Feb 20)", "Thurland navy conducts unilateral patrol (Mar 1)", "Formal joint-patrol proposal delivered (yesterday)"] },
            WorldEvent { id: "e4", severity: Severity::Critical, category: Category::Military, ts: "Yesterday · 06:03", title: "Ostrava Pact mobilizes reserve units", why: "Border posture shift follows failed grain talks", country: Some("ostrava"), chain: vec!["Grain export talks with Ostrava collapse (Feb 26)", "Ostrava recalls ambassador (Mar 2)", "Reserve mobilization order detected (yesterday)"] },
            WorldEvent { id: "e5", severity: Severity::Info, category: Category::Economy, ts: "2 days ago", title: "Central bank holds rate at 4.25%", why: "Inflation steady at 3.1%, no action needed this cycle", country: Some("velmoria"), chain: vec!["Inflation reading holds at 3.1% (Mar 3)", "Bank board votes 6-1 to hold (Mar 4)"] },
            WorldEvent { id: "e6", severity: Severity::Warning, category: Category::Unrest, ts: "2 days ago", title: "Transit strike enters day 4 in Astervale", why: "Union rejects revised wage offer", country: Some("velmoria"), chain: vec!["Union rejects 4% wage offer (Feb 28)", "Talks stall after mediator withdrawal (Mar 1)", "Strike enters day 4, ridership down 60% (2 days ago)"] },
            WorldEvent { id: "e7", severity: Severity::Notice, category: Category::Diplomacy, ts: "3 days ago", title: "Barrow Coast extends Arctic basing rights", why: "Ten-year renewal signed ahead of schedule", country: Some("barrow"), chain: vec!["Basing agreement set to expire in 14 months (Jan)", "Renewal talks accelerate after Kesh Arctic survey (Feb 10)", "Ten-year extension signed (3 days ago)"] },
            WorldEvent { id: "e8", severity: Severity::Critical, category: Category::Economy, ts: "4 days ago", title: "Adrenne currency falls 6% overnight", why: "Oil revenue miss triggers reserve concerns", country: Some("adrenne"), chain: vec!["Q1 oil export volumes miss forecast by 15% (Mar 1)", "Sovereign reserves fall below 3-month import cover (Mar 2)", "Currency depreciates 6% overnight (4 days ago)"] },
            WorldEvent { id: "e9", severity: Severity::Notice, category: Category::Politics, ts: "5 days ago", title: "Kalidane calls snap provincial elections", why: "Ruling coalition seeks mandate after redistricting", country: Some("kalidane"), chain: vec!["Supreme court upholds new district map (Feb 25)", "Ruling coalition polls ahead by 8pts (Mar 1)", "Snap election called for 132 days out (5 days ago)"] },
            WorldEvent { id: "e10", severity: Severity::Warning, category: Category::Climate, ts: "6 days ago", title: "Sundara Isles issues coastal flood advisory", why: "Third advisory this quarter amid rising sea levels", country: Some("sundara"), chain: vec!["Sea-level monitoring shows +4cm since January (ongoing)", "Storm surge model flags 3 low-lying districts (Mar 1)", "Advisory issued for coming spring tides (6 days ago)"] },
            WorldEvent { id: "e11", severity: Severity::Info, category: Category::Trade, ts: "1 week ago", title: "Firaq Emirates increases crude output quota", why: "Regional bloc raises quota 200kbd to ease prices", country: Some("firaq"), chain: vec!["Bloc members agree to ease output caps (Feb 20)", "Firaq allocated additional 200kbd quota (Feb 24)", "New output begins (1 week ago)"] },
            WorldEvent { id: "e12", severity: Severity::Notice, category: Category::Military, ts: "1 week ago", title: "Joint readiness exercise concludes with Thurland", why: "Annual exercise rated 'high effectiveness' by joint command", country: Some("thurland"), chain: vec!["Exercise planning finalized (January)", "Two-week joint exercise conducted (Feb 20 - Mar 3)", "After-action report rates effectiveness 'high' (1 week ago)"] },
            WorldEvent { id: "e13", severity: Severity::Critical, category: Category::Unrest, ts: "1 week ago", title: "Tallissa curfew imposed in three provinces", why: "Unrest index crosses threshold after fuel price protests", country: Some("tallissa"), chain: vec!["Fuel subsidy cut announced (Feb 22)", "Protests spread to 3 provinces (Feb 27)", "Curfew imposed after unrest index crosses 70 (1 week ago)"] },
        ];

        let ministers = vec![
            Minister { name: "Elena Voss", portfolio: "Finance", party: "Reform", approval: 52, status: MinisterStatus::Clear },
            Minister { name: "Dmitri Kalu", portfolio: "Defense", party: "Reform", approval: 61, status: MinisterStatus::Clear },
            Minister { name: "Priya Anand", portfolio: "Foreign Affairs", party: "Coalition Ind.", approval: 58, status: MinisterStatus::Clear },
            Minister { name: "Marcus Feld", portfolio: "Interior", party: "Agrarian Bloc", approval: 39, status: MinisterStatus::Watch },
            Minister { name: "Sara Lindqvist", portfolio: "Energy", party: "Reform", approval: 46, status: MinisterStatus::Clear },
            Minister { name: "Tomas Reyes", portfolio: "Trade", party: "Reform", approval: 33, status: MinisterStatus::Scandal },
            Minister { name: "Anya Brenner", portfolio: "Health", party: "Coalition Ind.", approval: 65, status: MinisterStatus::Clear },
        ];

        let sectors = vec![
            Sector { name: "Manufacturing", gdp_share: 18.2, growth: -1.4, employ: 1840, note: "Steel tariff retaliation hits export orders" },
            Sector { name: "Financial Services", gdp_share: 16.5, growth: 2.1, employ: 620, note: "Cross-border fintech licensing boosts activity" },
            Sector { name: "Agriculture", gdp_share: 11.0, growth: 0.4, employ: 1490, note: "Stable yields offset lower subsidy support" },
            Sector { name: "Technology", gdp_share: 10.8, growth: 4.6, employ: 510, note: "AI infrastructure investment wave continues" },
            Sector { name: "Energy", gdp_share: 9.4, growth: -0.8, employ: 340, note: "Grid modernization delays push back output" },
            Sector { name: "Construction", gdp_share: 8.6, growth: 1.2, employ: 990, note: "Public infrastructure spend front-loaded this year" },
            Sector { name: "Retail & Consumer", gdp_share: 8.1, growth: -0.3, employ: 1620, note: "Consumer confidence soft amid transit strike" },
            Sector { name: "Healthcare", gdp_share: 7.9, growth: 1.8, employ: 880, note: "Aging population sustains steady demand" },
        ];

        let budget = vec![
            BudgetItem { label: "Defense", pct: 21, delta: 1.2 },
            BudgetItem { label: "Health", pct: 18, delta: 0.4 },
            BudgetItem { label: "Education", pct: 14, delta: -0.6 },
            BudgetItem { label: "Infrastructure", pct: 16, delta: 2.1 },
            BudgetItem { label: "Debt Service", pct: 12, delta: 0.8 },
            BudgetItem { label: "Welfare", pct: 19, delta: -1.5 },
        ];

        let parties = vec![
            Party { name: "Reform Party", seats: 138, stance: "Governing", is_accent: true },
            Party { name: "Agrarian Bloc", seats: 44, stance: "Coalition", is_accent: false },
            Party { name: "Coalition Independents", seats: 22, stance: "Coalition", is_accent: false },
            Party { name: "National Union", seats: 96, stance: "Opposition", is_accent: false },
            Party { name: "Civic Front", seats: 40, stance: "Opposition", is_accent: false },
        ];

        let corruption = vec![
            CorruptionFlag { severity: Severity::Warning, title: "Trade Ministry procurement probe", desc: "Reyes' office under review for steel-quota allocation irregularities" },
            CorruptionFlag { severity: Severity::Notice, title: "Campaign finance disclosure delay", desc: "Reform Party files Q1 disclosure 6 days late, no penalty expected" },
        ];

        let society_stats = vec![
            NamedStat { label: "Population Growth", value: "0.6%/yr", why: "Net migration turns positive for first time in 3 years" },
            NamedStat { label: "Unrest Index", value: "38", why: "Elevated by transit strike and subsidy protests" },
            NamedStat { label: "HDI Composite", value: "0.81", why: "Steady gains in education spending since 2041" },
        ];
        let hotspots = vec![
            Hotspot { name: "Astervale Transit Corridor", value: 71, note: "4th day of transit strike" },
            Hotspot { name: "Northern Agrarian Belt", value: 54, note: "Subsidy cut protests" },
            Hotspot { name: "Port Callas", value: 29, note: "Dockworker pay dispute, talks ongoing" },
        ];

        let military_stats = vec![
            NamedStat { label: "Overall Readiness", value: "71%", why: "Reserve call-up compensates for equipment backlog" },
            NamedStat { label: "Active Personnel", value: "340k", why: "Voluntary enlistment up 4% amid recruitment drive" },
            NamedStat { label: "Modernization", value: "58%", why: "Delayed by supply chain bottleneck on avionics" },
        ];
        let branches = vec![
            Branch { name: "Army", personnel: "210k", readiness: 74, note: "Reserve units activated for eastern drills" },
            Branch { name: "Navy", personnel: "48k", readiness: 61, note: "Two frigates in extended maintenance" },
            Branch { name: "Air Force", personnel: "52k", readiness: 69, note: "New squadron declared IOC this month" },
            Branch { name: "Cyber Command", personnel: "6k", readiness: 83, note: "Recently expanded after Kesh intrusion attempt" },
        ];
        let deployments = vec![
            Deployment { name: "Joint Exercise — Thurland", location: "Shinkoto Strait", personnel: "1,200", status: DeploymentStatus::Concluded },
            Deployment { name: "Arctic Basing Rotation", location: "Barrow Coast", personnel: "400", status: DeploymentStatus::Ongoing },
            Deployment { name: "Peacekeeping Contribution", location: "Adrenne border", personnel: "180", status: DeploymentStatus::Ongoing },
        ];
        let treaties = vec![
            Treaty { name: "Mutual Defense Pact", parties: "Thurland · Barrow Coast · Norvask · Sundara Isles", status: "Active since 2038" },
            Treaty { name: "Regional Trade Framework", parties: "Kalidane · Meraux Union", status: "Active — renegotiation in 2027" },
            Treaty { name: "Arctic Basing Agreement", parties: "Barrow Coast", status: "Renewed 3 days ago" },
        ];

        World {
            nations,
            events,
            ministers,
            sectors,
            budget,
            parties,
            corruption,
            approval_series: vec![52.0, 51.0, 50.0, 50.0, 49.0, 48.0, 49.0, 48.0, 47.0, 46.0, 47.0, 46.0, 45.0, 47.0],
            approval_marker_idx: vec![4, 9, 12],
            society_stats,
            hotspots,
            military_stats,
            branches,
            deployments,
            treaties,
            gdp_series: vec![2.4, 2.3, 2.2, 2.1, 2.0, 1.9, 1.9, 1.8],
            approval_series_dash: vec![50.0, 50.0, 49.0, 49.0, 48.0, 48.0, 47.0, 47.0],
            unrest_series: vec![26.0, 28.0, 29.0, 31.0, 33.0, 35.0, 36.0, 38.0],
            readiness_series: vec![66.0, 67.0, 67.0, 68.0, 69.0, 70.0, 70.0, 71.0],
        }
    }
}

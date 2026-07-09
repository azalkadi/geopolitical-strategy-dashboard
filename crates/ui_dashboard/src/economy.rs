//! A first real simulation mechanic: a ticking economy for every country on the map, not
//! just the one the player is looking at (per Phase 1 Challenge 4 — no simulation-quality
//! LOD). Seeded from Natural Earth's real POP_EST/GDP_MD where available; evolves forward
//! deterministically each simulated day using a deliberately simple, transparent model —
//! this is a first mechanic, not a claim to economic realism.

use crate::geo::CountryGeo;

/// Cheap deterministic PRNG (xorshift32) — per-country seeded noise, not `rand`/`thread_rng`,
/// so the same save/seed always produces the same trajectory (Phase 1 determinism goal).
fn xorshift32(state: &mut u32) -> u32 {
    let mut x = *state;
    if x == 0 {
        x = 0x9e3779b9;
    }
    x ^= x << 13;
    x ^= x >> 17;
    x ^= x << 5;
    *state = x;
    x
}

/// Roughly uniform in [-1.0, 1.0].
fn noise(seed: &mut u32) -> f32 {
    (xorshift32(seed) as f32 / u32::MAX as f32) * 2.0 - 1.0
}

fn hash_seed(s: &str, salt: u32) -> u32 {
    let mut h: u32 = 0x811c9dc5 ^ salt;
    for b in s.bytes() {
        h ^= b as u32;
        h = h.wrapping_mul(0x01000193);
    }
    h
}

pub struct EconomyState {
    /// Billions of USD.
    pub gdp: f64,
    /// % annualized, smoothed.
    pub growth_rate: f32,
    /// Long-run growth this country trends toward absent tax drag/noise; derived once at
    /// seed time from GDP-per-capita (a crude real-world convergence effect: poorer
    /// economies trend toward faster catch-up growth).
    base_growth_target: f32,
    /// %.
    pub unemployment: f32,
    /// %.
    pub inflation: f32,
    /// Four independent, player-adjustable tax levers (% rate each) instead of one
    /// aggregate rate — this is the "control over every tax" mechanic.
    pub tax_income: f32,
    pub tax_corporate: f32,
    pub tax_vat: f32,
    pub tax_tariff: f32,
    /// Central bank policy rate, % — the other independently adjustable lever requested.
    pub interest_rate: f32,
    /// Billions of USD, cumulative (can go negative — a deficit).
    pub treasury: f64,
    rng: u32,
    pub last_why: Option<String>,
    /// False when Natural Earth had no GDP/population estimate and we seeded a nominal
    /// placeholder instead — surfaced in the UI so a fabricated baseline is never presented
    /// as real.
    pub has_real_baseline: bool,
}

impl EconomyState {
    pub fn seed(c: &CountryGeo, salt: u32) -> Self {
        let has_real_baseline = c.gdp_md > 0 && c.pop_est > 0;
        let gdp = if has_real_baseline {
            c.gdp_md as f64 / 1000.0
        } else {
            (c.pop_est.max(10_000) as f64) * 3_000.0 / 1e9
        };
        let gdp_per_capita = if c.pop_est > 0 { (gdp * 1e9) / c.pop_est as f64 } else { 3_000.0 };
        let base_growth_target: f32 = if gdp_per_capita < 5_000.0 {
            4.5
        } else if gdp_per_capita < 15_000.0 {
            3.0
        } else if gdp_per_capita < 30_000.0 {
            2.0
        } else {
            1.2
        };

        Self {
            gdp,
            growth_rate: base_growth_target,
            base_growth_target,
            unemployment: 7.0,
            inflation: 2.5,
            tax_income: 25.0,
            tax_corporate: 22.0,
            tax_vat: 15.0,
            tax_tariff: 5.0,
            interest_rate: 4.0,
            treasury: 0.0,
            rng: hash_seed(&c.iso_a3, salt),
            last_why: None,
            has_real_baseline,
        }
    }

    /// Blended effective tax burden across the four independent levers — weighted the way
    /// income/corporate taxes touch a bigger share of the economy than VAT/tariffs do.
    pub fn effective_tax_rate(&self) -> f32 {
        self.tax_income * 0.4 + self.tax_corporate * 0.3 + self.tax_vat * 0.2 + self.tax_tariff * 0.1
    }

    /// Advances the economy by exactly one simulated day.
    pub fn tick(&mut self) {
        let prev_growth = self.growth_rate;

        let effective_tax = self.effective_tax_rate();
        let tax_drag = (effective_tax - 25.0) * 0.04;
        let rate_drag = (self.interest_rate - 4.0) * 0.05;
        let target = self.base_growth_target - tax_drag - rate_drag + noise(&mut self.rng) * 0.15;
        self.growth_rate = (self.growth_rate * 0.98 + target * 0.02).clamp(-15.0, 15.0);

        self.gdp *= 1.0 + (self.growth_rate as f64) / 100.0 / 365.0;
        self.gdp = self.gdp.max(0.01);

        self.unemployment = (self.unemployment + (2.0 - self.growth_rate) * 0.01).clamp(2.0, 35.0);
        self.inflation = (self.inflation + (self.growth_rate - 2.0) * 0.005 - (self.interest_rate - 4.0) * 0.03 + noise(&mut self.rng) * 0.02)
            .clamp(-3.0, 40.0);

        self.treasury += self.gdp * (effective_tax as f64) / 100.0 / 365.0 - self.gdp * 0.20 / 365.0;

        self.last_why = if prev_growth >= 0.0 && self.growth_rate < 0.0 {
            Some("GDP growth turned negative — economy entering recession".to_string())
        } else if prev_growth < 0.0 && self.growth_rate >= 0.0 {
            Some("GDP growth turned positive — recession easing".to_string())
        } else if self.unemployment > 12.0 && self.unemployment - (2.0 - self.growth_rate) * 0.01 <= 12.0 {
            Some("Unemployment crossed 12% amid weak growth".to_string())
        } else if self.inflation > 8.0 && self.inflation - (self.growth_rate - 2.0) * 0.005 <= 8.0 {
            Some("Inflation crossed 8% as growth accelerates".to_string())
        } else {
            self.last_why.take()
        };
    }
}

pub struct EconomySystem {
    pub states: Vec<EconomyState>,
}

impl EconomySystem {
    pub fn seed(countries: &[CountryGeo]) -> Self {
        let states = countries.iter().enumerate().map(|(i, c)| EconomyState::seed(c, i as u32)).collect();
        Self { states }
    }

    /// Ticks every country's economy once — always all of them, never just the one the
    /// player is currently looking at (Phase 1 Challenge 4).
    pub fn tick_all(&mut self) {
        for s in &mut self.states {
            s.tick();
        }
    }
}

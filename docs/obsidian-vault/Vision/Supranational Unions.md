---
tags: [vision, game-design, politics]
---

# Supranational Unions

Part of [[Vision Overview|the vision]]. Real geopolitics happens at more than one level —
countries belong to blocs that have their own governance layered above individual national
politics. Meridian currently has no concept of this at all; every country is fully independent.

## The unions
- **UN** — the broadest layer; largely a backdrop/legitimacy mechanic (sanctions, resolutions)
  rather than something the player directly governs.
- **EU** — member states keep [[Government, Legislature and Real Taxes|their own legislature]],
  but some laws get reviewed/decided at the union level. Playing a member state should surface
  an "EU" tab where union-wide legislation can be proposed and is reviewed by the union as a
  whole, not just your own parliament.
- **GCC** — explicitly described as *trying* to become more like a real union (closer to the EU
  model) rather than staying a loose cooperation council — the simulation should let it evolve
  in that direction, not treat it as static.
- **Federal internal structure (USA)** — the reverse direction: *within* one country, individual
  states can have their own distinct laws, not just one national policy. The USA is the named
  example, but the mechanic (a federation where sub-units have real legislative autonomy) should
  be general.

## The point
Playing a country inside a union should visibly change the UI/options available — a country in
the EU gets a union tab a non-member doesn't; a federal country gets a states sub-view a
unitary one doesn't. This is explicitly an example of wanting **per-country-type UI**, not one
generic panel set for all 258 countries.

## Where this plugs into existing code
- No existing analog — this is new. Likely a `Union` class (member list, its own simplified
  legislature/voting body) sitting above [[Diplomacy System]]'s bilateral relations, since a
  union is really a standing multilateral structure.
- UI: conditionally-shown tabs in `GameUIRoot.cs` based on the selected/played country's union
  membership and federal structure — ties into [[Government, Legislature and Real Taxes]]'s
  regime-type work, since "does this country have sub-national legislatures" is the same kind
  of per-country structural fact as "is this a monarchy."
- Reasonable to sequence AFTER [[Government, Legislature and Real Taxes]] ships for a single
  country, since unions are really "the same legislature mechanic, one level up (or down, for
  federal states)."

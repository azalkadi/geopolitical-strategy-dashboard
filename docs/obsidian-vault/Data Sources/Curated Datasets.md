---
tags: [data]
---

# Curated Datasets

Everything here has **no** public bundled dataset behind it — either filtered out of an
existing Natural Earth file by its own attributes, hand-researched, or computed at load time.
None of these are exhaustive registries; each is a notable/major subset picked for global spread
and confidence in both existence and rough location. See [[Geo Pipeline]] for the loading code.

## Air bases (44)
Not a separate dataset — filtered out of the existing `ne_10m_airports` file by its own `type`
property containing "military".

## Oil ports (28) and nuclear plants (56)
Hand-curated. Real, well-known facilities with approximate (not survey-precise) coordinates.
Nuclear alone has roughly 440 real reactors worldwide — this is the notable/major subset, not a
complete registry. Making either list more complete would be a real data-sourcing task, not a
quick edit.

## Water crossings (6) — real intercountry causeways/bridges
Coordinates are approximate landfall points (accurate to a few km — fine at this map's scale),
sourced from public references on each structure:
- King Fahd Causeway (Saudi Arabia ↔ Bahrain)
- Øresund Bridge (Denmark ↔ Sweden)
- Johor–Singapore Causeway and Tuas Second Link (Malaysia ↔ Singapore)
- Hong Kong–Zhuhai–Macau Bridge (Hong Kong S.A.R. ↔ China)
- Thai–Lao Friendship Bridge, 1st (Thailand ↔ Laos)

Deliberately **excludes** anything not actually built: no Qatar–Bahrain Friendship Bridge
(announced ~2005, never built), and no Kuwait entry — Kuwait's only major causeway (Sheikh
Jaber Al-Ahmad Al-Sabah) is entirely domestic (Kuwait City to Subiya/Bubiyan Island), not a link
to another country. This was researched specifically in response to a "look up the water
bridges between countries... or kuwait" request — the omission is intentional, not a gap.

## Border crossings (181) — computed, not curated
Not sourced from any dataset at all. Computed by walking every **named** road in the
[[Natural Earth Datasets|roads dataset]] and finding where consecutive sampled points resolve to
different countries — see [[Geo Pipeline]] for the (fairly involved) performance story behind
making this computation fast enough to run at every launch. Results are cached after the first
computation.

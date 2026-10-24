# Ninetyfold evaluation

Generated 2026-10-24 13:07 UTC by `eval/MatchDirector.Eval`. All matches are synthetic and seeded, so the deterministic results reproduce exactly; model results vary slightly between runs.

## Headlines

- **100% of 1,959 moments cite valid, earlier events** as evidence (50 matches, 96,650 events).
- **100% of fallback narrations pass the number check**, so the safe path never states an uncomputed number.
- **100% of invented statistics are blocked by code** before anything reaches the screen.
- **The verifier critic catches 94% of meaning errors** (wrong team, swapped fan lines, wrong score story, invented history) with **0% false alarms** on correct lines.
- **Live studio:** the critic reviewed 36 moments, approved 26 and flagged 10. Flagged lines were rewritten and rechecked (5 rewritten, 1 replaced by the template); flagged insights were dropped. Narrator latency p50 1089 ms, p95 1469 ms.
- **Translation:** 100% of translated lines kept exactly the numbers of the checked English line across 5 languages; the rest fall back to English on screen.

## Deterministic checks (no model)

| Check | Result |
|---|---|
| Matches simulated | 50 (96,650 events) |
| Moments with valid evidence | 1,959 / 1,959 (100%) |
| Goals with a goal moment | 144 / 144 (100%) |
| Template narrations passing the number check | 1,959 / 1,959 (100%) |
| Full-time template recaps passing the number check | 50 / 50 (100%) |
| Stats engine throughput | 211,445 events/second |
| Realism per match | 2.88 goals, 27.5 shots, 78.6% pass accuracy |

Moments per match: HighRegain 8.4, DifficultPass 5.4, PassingMove 4.8, RocketShot 3.3, KeySave 2.9, Goal 2.9, MomentumShift 2.6, DrivingRun 2.3, DistanceMilestone 2, TempoShift 1.4, BigChanceMissed 1.2, HalfTime 1, FullTime 1.

## Red team: deliberately wrong narration

Each attack edits correct (template) lines for 15 non-routine moments. Code checks numbers first; the critic sees only what passes.

| Attack | What it does | Expected catcher | Caught by code | Caught by critic | Total caught |
|---|---|---|---|---|---|
| Invented statistic | adds a season total that was never computed | code | 15/15 | 0/15 | 100% |
| Wrong team | credits the moment to the other club | critic | 0/5 | 5/5 | 100% |
| Swapped fan lines | home supporters cheer for the away club and vice versa | critic | 0/15 | 12/15 | 80% |
| Wrong score story | says the wrong club is ahead | critic | 0/15 | 15/15 | 100% |
| Invented history | adds a historical claim with no number in it | critic | 0/15 | 15/15 | 100% |

False alarms: the critic flagged 0 of 15 correct narrations (0%).

## Live studio (gpt-4.1-mini narrator, gpt-5-mini analyst and critic)

| Measure | Result |
|---|---|
| Moments narrated | 80 over 2 matches (78 by the agent) |
| Narrations rejected by the number check | 2 (2.5%) |
| Reviewed by the critic | 36: 26 approved, 10 flagged |
| Flagged moments rewritten / template after second review | 5 / 1 |
| Reviewed moments with a fan-line issue | 5 of 36 (13.9%) |
| Tactical insights | 16 kept of 22 requested (6 dropped by checks) |
| Latency p50 / p95 | narrator 1089 / 1469 ms · analyst 5206 / 7299 ms · critic 4784 / 6860 ms |

Full-time reports:

- **Okonkwe double secures Kestrel Bay win** (agent). Dario Okonkwe scored 2 as Kestrel Bay FC beat Redmoor Rovers 2-0 at Lantern Park. He was named player of the match. _Checks: Recap writer: wrote headline and 4 reports → Number check: every number traced to computed facts → Verifier critic: approved._
- **Corvel nets four as Rovers win 4-1** (agent). Bastian Corvel scored 4 as Redmoor Rovers beat Kestrel Bay FC 4-1. He was unstoppable and took home player of the match honours. _Checks: Recap writer: wrote headline and 4 reports → Number check: every number traced to computed facts → Verifier critic: approved._

Examples of what the critic flagged:

- BigChanceMissed: insight: Claims that the miss "wastes Redmoor's 64.4% possession and 81.7% pass accuracy" and "stalling their momentum" assert a causal effect the facts do not support. The data show possession (64.4%), pass accuracy (81.7%) and momentum (-18.8) at the time, but do not prove the miss directly wasted those team metrics or caused the momentum figure.
- PassingMove: casual: Claims the 12-pass run happened "before scoring" — the facts do not say this move led to a goal or that the pass sequence preceded the only goal.
- PassingMove: homeFan: Says the 12-pass move "kept the lead" (causal claim). The facts show the score is 1-0 at minute 50 but do not support that this specific move preserved or created the lead.
- KeySave: casual: Says the save "kept Redmoor Rovers ahead" but Redmoor are 0-1 down (the save prevented Kestrel Bay FC from extending their lead).
- KeySave: homeFan: Claims the save "stopped our chance to equalize" — Kestrel Bay FC are leading 1-0, so any successful shot by them would not equalize.
- KeySave: awayFan: States the save "keeping us in the lead" — Redmoor Rovers are trailing 0-1, so the save did not keep them in the lead.
- MomentumShift: insight: Incorrect attribution — momentum -41.9 is negative (meaning the away team, Redmoor Rovers, has the recent threat). The line says momentum "stays -41.9 for Kestrel Bay", which contradicts the facts/meaning.
- MomentumShift: awayFan: Implies the away supporter is 'down 0-1' — but facts show Redmoor Rovers (the away club) are leading 1-0; the fan quote should not present the away club as being behind.
- BigChanceMissed: insight: Claim that Dario Okonkwe "lost a decisive momentum swing" is not supported by the provided evidence — momentum is 50 (previously 51.8 after the goal), which does not clearly indicate a decisive swing away from Kestrel Bay FC.
- MomentumShift: analyst: Incorrect interpretation — momentum is -41.4 (negative = home on top) so it does not support that Redmoor Rovers are generating more overall threat; the facts show momentum favouring Kestrel Bay FC.
- MomentumShift: awayFan: Incorrect claim — saying Redmoor Rovers have "strong momentum" contradicts the momentum metric (−41.4, which by the stated meaning favours the home team, Kestrel).
- KeySave: insight: Claims it "halted Kestrel’s momentum (match momentum 50.3)" are not supported by the facts. The evidence shows a TempoShift for Kestrel Bay at minute 46 (suggesting increased tempo) and a single overall match momentum value (50.3) — there is no clear fact that the save halted Kestrel Bay's momentum.

Examples of tactical insights that passed both checks:

- KeySave (2'): Halvik’s save stopped a 0.156 xG effort (14.3 m, 28.7° at 25.5 m/s), preserving 0–0 and denying Kestrel Bay early control despite their 60.1% possession and 55.2 momentum; it keeps Redmoor competitive. 
- MomentumShift (24'): Redmoor Rovers are dominating: momentum −42.5 (away on top), 62.8% possession, 140 passes (81.4% accuracy) vs Kestrel 79 passes (70.9%), 38 progressive passes to 24 — control and threat despite 0–0 and similar xG.
- KeySave (35'): Halvik's save (xG 0.196 from 9.8 m, 24°, 19.8 m/s) prevents Kestrel Bay taking the lead, preserving Redmoor's control (63.1% possession) and positive momentum (25.3) after their 12-pass possession sequence.
- KeySave (38'): Halvik’s save (xG 0.164, 13.7 m, 28.5 m/s) stops Kestrel converting growing momentum (momentum 11.9) — their 4th on‑target — and preserves Redmoor’s territorial control (possession 62.9%).
- Goal (45'): Okonkwe's 45' goal (xG 0.254) makes it 1–0, rewarding Kestrel Bay's high pressing (93 pressures vs 66) despite 38.2% possession; a two-pass, 15s move created the chance and shifts momentum (+11.8).
- HalfTime (47'): Kestrel Bay lead 1-0 despite 39.1% possession and 71.2% pass accuracy (vs Redmoor 60.9%/80.8%), showing efficient counter-pressing (100 pressures vs 66) and finishing (Okonkwe 0.254 xG) — shifts momentum (-17.2).

## Translation (gpt-4.1-mini translator, code number check)

Checked English lines from 8 moments and one full-time report, translated into each language. A translated line is kept only if it carries exactly the numbers of its English line; otherwise viewers see the English line.

| Language | Lines kept | Latency p50 / p95 |
|---|---|---|
| French | 37 / 37 (100%) | 1514 / 2783 ms |
| Spanish | 37 / 37 (100%) | 1639 / 2741 ms |
| German | 37 / 37 (100%) | 1776 / 2508 ms |
| Portuguese | 37 / 37 (100%) | 1654 / 2933 ms |
| Italian | 37 / 37 (100%) | 1716 / 2596 ms |

Examples:

- French: "Early save by Piotr Halvik keeps the score 0-0 at 2 minutes." → "Une première parade de Piotr Halvik maintient le score à 0-0 après 2 minutes."
- Spanish: "Early save by Piotr Halvik keeps the score 0-0 at 2 minutes." → "Una parada temprana de Piotr Halvik mantiene el marcador 0-0 a los 2 minutos."
- German: "Early save by Piotr Halvik keeps the score 0-0 at 2 minutes." → "Frühe Parade von Piotr Halvik hält den Spielstand nach 2 Minuten bei 0-0."
- Portuguese: "Early save by Piotr Halvik keeps the score 0-0 at 2 minutes." → "Defesa rápida de Piotr Halvik mantém o 0-0 aos 2 minutos."
- Italian: "Early save by Piotr Halvik keeps the score 0-0 at 2 minutes." → "Una parata precoce di Piotr Halvik mantiene il punteggio sullo 0-0 al 2° minuto."

## Reproduce

```bash
# deterministic checks and the code half of the red team (no Azure needed)
dotnet run --project eval/MatchDirector.Eval
# full run against your Foundry project (Azure CLI login)
dotnet run --project eval/MatchDirector.Eval -- --endpoint https://<your-foundry>.cognitiveservices.azure.com/
```

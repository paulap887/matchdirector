# MatchDirector evaluation

Generated 2026-10-15 12:00 UTC by `eval/MatchDirector.Eval`. All matches are synthetic and seeded, so the deterministic results reproduce exactly; model results vary slightly between runs.

## Headlines

- **100% of 1,901 moments cite valid, earlier events** as evidence (50 matches, 96,650 events).
- **100% of fallback narrations pass the number check**, so the safe path never states an uncomputed number.
- **100% of invented statistics are blocked by code** before anything reaches the screen.
- **The verifier critic catches 98.2% of meaning errors** (wrong team, swapped fan lines, wrong score story, invented history) with **0% false alarms** on correct lines.
- **Live studio:** the critic reviewed 34 moments, approved 23 and flagged 11. Flagged lines were rewritten and rechecked (6 rewritten, 2 replaced by the template); flagged insights were dropped. Narrator latency p50 1776 ms, p95 2338 ms.

## Deterministic checks (no model)

| Check | Result |
|---|---|
| Matches simulated | 50 (96,650 events) |
| Moments with valid evidence | 1,901 / 1,901 (100%) |
| Goals with a goal moment | 144 / 144 (100%) |
| Template narrations passing the number check | 1,901 / 1,901 (100%) |
| Stats engine throughput | 113,369 events/second |
| Realism per match | 2.88 goals, 27.5 shots, 78.6% pass accuracy |

Moments per match: HighRegain 11.1, PassingMove 8.4, DifficultPass 5.4, KeySave 2.9, Goal 2.9, MomentumShift 2.6, TempoShift 1.4, BigChanceMissed 1.2, HalfTime 1, FullTime 1.

## Red team: deliberately wrong narration

Each attack edits correct (template) lines for 15 non-routine moments. Code checks numbers first; the critic sees only what passes.

| Attack | What it does | Expected catcher | Caught by code | Caught by critic | Total caught |
|---|---|---|---|---|---|
| Invented statistic | adds a season total that was never computed | code | 15/15 | 0/15 | 100% |
| Wrong team | credits the moment to the other club | critic | 0/10 | 10/10 | 100% |
| Swapped fan lines | home supporters cheer for the away club and vice versa | critic | 0/15 | 14/15 | 93.3% |
| Wrong score story | says the wrong club is ahead | critic | 0/15 | 15/15 | 100% |
| Invented history | adds a historical claim with no number in it | critic | 0/15 | 15/15 | 100% |

False alarms: the critic flagged 0 of 15 correct narrations (0%).

## Live studio (gpt-4.1-mini narrator, gpt-5-mini analyst and critic)

| Measure | Result |
|---|---|
| Moments narrated | 80 over 2 matches (75 by the agent) |
| Narrations rejected by the number check | 5 (6.3%) |
| Reviewed by the critic | 34: 23 approved, 11 flagged |
| Flagged moments rewritten / template after second review | 6 / 2 |
| Tactical insights | 15 kept of 22 requested (7 dropped by checks) |
| Latency p50 / p95 | narrator 1776 / 2338 ms · analyst 6326 / 7708 ms · critic 4945 / 8837 ms |

Examples of what the critic flagged:

- KeySave: analyst: Misstates who took the shot — Piotr Halvik is the goalkeeper who made the save, not the shooter.
- MomentumShift: insight: Claims 'despite overall momentum -42.5' imply the momentum is against Redmoor Rovers, but per the metric (positive = home on top) a momentum of -42.5 actually favors the away team, Redmoor Rovers — so 'despite' is incorrect.
- PassingMove: homeFan: Not written as a supporter of the home club (Kestrel Bay FC). The line speaks in third person about Kestrel Bay rather than from a home-fan perspective ('we/us' for the home team).
- KeySave: insight: States the score as "0–30" which is incorrect — the facts show 0–0 at minute 35.
- KeySave: insight: Says "despite their 63.1% possession" implying Redmoor lacked control; the facts show Redmoor (the team of Halvik) had 63.1% possession, so the phrasing is misleading/wrong.
- BigChanceMissed: insight: Claims Redmoor was "on top" by citing "momentum -18.8" are not supported by the provided evidence. The get_match_stats output shows momentum: -18.8 but does not indicate which team that favors, so stating it as demonstrating Redmoor's momentum (and that the miss "halts Redmoor's ... momentum") is not supported by the facts.
- KeySave: homeFan: written as a supporter praising/crediting Redmoor Rovers' keeper—homeFan must be a supporter of the home club (Kestrel Bay FC) and should not frame 'we/us' as Redmoor or praise the opponent in that way.
- Goal: insight: Incorrect score formatting/claim — reads "makes it 1–30" but the facts show the score is 1-0.
- PassingMove: homeFan: Presents the 10-pass move as belonging to the home side by saying "our 10-pass runs", but the facts show the 10-pass sequence was by Redmoor Rovers (the away team); a homeFan must speak as a supporter of the home club (Kestrel Bay).
- KeySave: homeFan: Attributes the save to keep "us" (the home side) level — Piotr Halvik plays for Redmoor Rovers (the away team), so the line wrongly frames him as keeping Kestrel Bay level.
- KeySave: insight: Claims the save "halted Kestrel's momentum" which is not supported by the provided evidence. The tools show a match momentum value (50.3) but do not document Kestrel Bay building momentum that was stopped by this save, so the causal claim is unsupported.
- KeySave: homeFan: Suggests the save "keeps Kestrel Bay hopeful" — Piotr Halvik is Redmoor Rovers' goalkeeper and his save preserved Redmoor's lead, so this line misstates the effect for the home team.

Examples of tactical insights that passed both checks:

- KeySave (2'): Halvik’s save denied a 0.156 expected-goals chance from 14.3 metres (28.7°), keeping it 0-0 and sustaining Kestrel Bay’s 60.1% possession and +55.2 momentum early — prevents an early lead and preserves pressure.
- HalfTime (47'): Kestrel Bay lead 1-0 despite 39.1% possession vs Redmoor’s 60.9% and similar xG (1.26 vs 1.19). Okonkwe’s 45' finish (xG 0.254) flips state while momentum is -17.2 favoring Redmoor.
- KeySave (63'): Halvik’s 63' save denied a 0.181 xG chance (13.1 m, 31.1°), keeping score 1–0; with Redmoor 57.8% possession versus Kestrel’s 129 pressures and momentum +42.4, Kestrel maintain control and game momentum.
- MomentumShift (72'): At 72', Redmoor's momentum (-41.9) shows sustained pressure after Kestrel's 70' goal: Redmoor have 56.5% possession, 369 passes and 86 progressive passes — they're controlling play and forcing a push to chase a 2-0 deficit.
- BigChanceMissed (72'): Corvel’s 0.58 xG miss at 72' leaves Redmoor trailing 2-0, squandering their best chance to cut the deficit; despite 56.8% possession and Corvel’s 11 shots (xG 1.63), momentum remains -40.3 for Kestrel.
- Goal (8'): 8' Corvel goal (xG 0.143, 15 m, 27°) makes it 0–1 from a 13s, 3-pass move. Kestrel still have 54.7% possession, but momentum is -42.3, shifting control and momentum to Redmoor despite less possession.

## Reproduce

```bash
# deterministic checks and the code half of the red team (no Azure needed)
dotnet run --project eval/MatchDirector.Eval
# full run against your Foundry project (Azure CLI login)
dotnet run --project eval/MatchDirector.Eval -- --endpoint https://<your-foundry>.cognitiveservices.azure.com/
```

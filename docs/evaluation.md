# MatchDirector evaluation

Generated 2026-10-20 17:30 UTC by `eval/MatchDirector.Eval`. All matches are synthetic and seeded, so the deterministic results reproduce exactly; model results vary slightly between runs.

## Headlines

- **100% of 1,901 moments cite valid, earlier events** as evidence (50 matches, 96,650 events).
- **100% of fallback narrations pass the number check**, so the safe path never states an uncomputed number.
- **100% of invented statistics are blocked by code** before anything reaches the screen.
- **The verifier critic catches 98.2% of meaning errors** (wrong team, swapped fan lines, wrong score story, invented history) with **0% false alarms** on correct lines.
- **Live studio:** the critic reviewed 33 moments, approved 22 and flagged 11. Flagged lines were rewritten and rechecked (6 rewritten, 1 replaced by the template); flagged insights were dropped. Narrator latency p50 1739 ms, p95 2353 ms.

## Deterministic checks (no model)

| Check | Result |
|---|---|
| Matches simulated | 50 (96,650 events) |
| Moments with valid evidence | 1,901 / 1,901 (100%) |
| Goals with a goal moment | 144 / 144 (100%) |
| Template narrations passing the number check | 1,901 / 1,901 (100%) |
| Full-time template recaps passing the number check | 50 / 50 (100%) |
| Stats engine throughput | 261,698 events/second |
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
| Reviewed by the critic | 33: 22 approved, 11 flagged |
| Flagged moments rewritten / template after second review | 6 / 1 |
| Reviewed moments with a fan-line issue | 6 of 33 (18.2%) |
| Tactical insights | 14 kept of 21 requested (7 dropped by checks) |
| Latency p50 / p95 | narrator 1739 / 2353 ms · analyst 5936 / 7723 ms · critic 4214 / 10553 ms |

Full-time reports:

- **Okonkwe double seals Kestrel Bay win** (agent). Dario Okonkwe scored both goals as Kestrel Bay FC beat Redmoor Rovers 2-0. Okonkwe was named player of the match. The win came despite Kestrel Bay having less possession. _Checks: Recap writer: wrote headline and 4 reports → Number check: every number traced to computed facts → Verifier critic: approved._
- **Corvel nets four as Rovers rout Kestrel Bay** (agent). Redmoor Rovers beat Kestrel Bay FC 4-1 at Lantern Park. Bastian Corvel scored every Redmoor goal and was named player of the match. _Checks: Recap writer: wrote headline and 4 reports → Number check: every number traced to computed facts → Verifier critic: approved._

Examples of what the critic flagged:

- KeySave: insight: Incorrect score phrasing — "preserving 0-30" is wrong (should be preserving 0-0).
- PassingMove: homeFan: line contradicts the fan's mood — it says both 'we are worried' and 'and celebrate', but facts list mood as 'worried' for the home fan (Kestrel Bay FC).
- BigChanceMissed: insight: Incorrect score reported — text says "0–30 game" but the facts show a 0–0 score.
- KeySave: homeFan: The line is written as if the home fan's team "hasn't conceded" ("still no goals against us"), which misrepresents the facts. The home fan is from Kestrel Bay FC (the attacking/home team in this moment) — they would more naturally refer to "no goal for us"; as written it implies the home fan supports the team that Halvik (Redmoor Rovers) plays for, which contradicts the facts.
- KeySave: awayFan: Claims Redmoor Rovers are "ahead at 1-0" — facts show Redmoor (away) have 0 and Kestrel Bay (home) lead 1-0, so the away fan cannot say "keeping us ahead".
- PassingMove: homeFan: Incorrectly says the move was "for a goal" — the facts describe a passing move but do not state it resulted in a goal.
- MomentumShift: insight: Claims that momentum −41.4 "still favours Kestrel" contradict the metric meaning (momentum is -100..100, positive = home); -41.4 indicates Redmoor Rovers hold the momentum, not Kestrel.
- HalfTime: insight: Claims Redmoor Rovers are "forcing turnovers" are not supported by the provided facts/evidence (no turnover/turnover-forcing metric is given).
- KeySave: homeFan: Claims Halvik is "our keeper" for the home supporters — Piotr Halvik plays for Redmoor Rovers (the away team), so the home fan cannot refer to him as their keeper.
- KeySave: insight: Says the save "halted Kestrel's momentum (match momentum 50.3)" — the provided momentum value is a match-level number (50.3) and there is no evidence that it specifically represented or was "halted" for Kestrel Bay FC; the causal claim about halting Kestrel's momentum is not supported by the facts.
- Goal: insight: Claims the goal 'ends Kestrel Bay's 56.5% possession edge' — the facts show the possession numbers (56.5% v 43.5%) but do not show that the goal 'ended' that edge or changed possession figures; that causal/temporal claim is not supported by the provided evidence.
- PassingMove: awayFan: Claims the 10-pass move led to a 2-1 lead — the facts do not state this move caused the score to become 2-1.

Examples of tactical insights that passed both checks:

- MomentumShift (24'): At 24', Redmoor’s momentum (−42.5) shows they’ve taken control: 62.8% possession, 38 progressive passes (Kestrel 24) and a 23' high regain 17 m from goal — shifting control and raising chance quality in a 0–0 game.
- KeySave (35'): Halvik’s save denied a 0.196 xG chance from 9.8 m at 24° (19.8 m/s), halting Kestrel’s momentum (+25.3) and keeping the 0–0 game despite Kestrel’s 0.85 xG overall.
- Goal (45'): Okonkwe's 45' goal (xG 0.254, 9 m, 44.1°) gives Kestrel a 1-0 lead despite 38.2% possession; equal shots (8) and momentum +11.8 now reward clinical counterplay from a 2-pass, 15s move.
- HalfTime (47'): Kestrel lead 1-0 despite Redmoor’s 60.9% possession and 80.8% pass accuracy; xG is level (1.26 vs 1.19). Kestrel’s intense pressing (100 vs 66 pressures) and a clinical finish (goal xG 0.254) turned control into a lead.
- KeySave (63'): Halvik's minute-63 save denied a 0.181 xG close-range (13.1 m, 31.1°) chance, preserving Kestrel Bay's 1–0 lead; it halts Redmoor's surge despite their 57.8% possession and preserves home momentum 42.4.
- Goal (70'): Okonkwe’s finish (xG 0.107) makes it 2–0, rewards an 8-pass, 32-second move from 15.2 m; shifts momentum (31.6) and highlights Kestrel’s effective pressing (146 pressures) despite only 43.1% possession.

## Reproduce

```bash
# deterministic checks and the code half of the red team (no Azure needed)
dotnet run --project eval/MatchDirector.Eval
# full run against your Foundry project (Azure CLI login)
dotnet run --project eval/MatchDirector.Eval -- --endpoint https://<your-foundry>.cognitiveservices.azure.com/
```

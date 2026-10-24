---
description: Add a new kind of moment end to end, from detection to narration and UI.
---
Add a new moment type called `${input:momentType}` that is detected when ${input:condition}.

Work through the whole pipeline and keep the "numbers come from code" rule:
1. `src/MatchDirector.StatsEngine/Moment.cs`: add the enum value.
2. `src/MatchDirector.StatsEngine/MatchAnalyzer.cs`: detect it, compute its metrics, cite the event IDs it was computed from, give it a priority and a cooldown if it could repeat often.
3. `src/MatchDirector.Agents/Narration/AgentNarrator.cs`: add its plain-language `Meaning` and any new metric `Unit`; add the named player's role in `MomentFacts.PlayerRole`.
4. `src/MatchDirector.Agents/Narration/Narration.cs`: add template lines (casual, analyst and both fan lines) for `TemplateNarrator`.
5. `src/web/src/i18n.ts`: add an `m.<Type>` label in every language (the build fails if one is missing).
6. Tests: a unit test in `tests/MatchDirector.StatsEngine.Tests` for detection, and make sure the existing "every moment cites real earlier events" and "template narration passes the verifier" tests still pass.

Run `dotnet test` and `cd src/web && npm run lint && npm run build` before finishing.

# MatchDirector: instructions for GitHub Copilot

Product name: **Ninetyfold** (use it in anything users see). `MatchDirector` remains the code and repository name.

MatchDirector is an AI broadcast studio over synthetic football events: .NET 10 + Aspire, React/Vite,
Microsoft Foundry and Microsoft Agent Framework.

## Rules
- Numbers come from code (`src/MatchDirector.StatsEngine`); LLMs only narrate. Every claim an agent makes must cite event IDs.
- All clubs, players and venues are fictional. No real league, club or player names, logos or music.
- Public repo: never commit secrets. Use managed identity, Key Vault and user-secrets.
- Commits and PRs go out under the author's name only. No AI attribution trailers or lines.
- Contracts in `src/MatchDirector.Contracts` are the shared schema; change them deliberately and keep new fields optional.

## Architecture
1. **Ingest**: `MatchDirector.Simulator` plays seeded, deterministic matches and publishes `MatchInfo` and `MatchEvent` to Event Hubs.
2. **Interpret**: `MatchDirector.StatsEngine` (`MatchAnalyzer`) turns events into stats and `Moment`s. Each moment carries its metrics and the event IDs (`Evidence`) they came from.
3. **Explain**: `MatchDirector.Agents`. The `Director` (code) orchestrates a fast lane (Narrator agent, then a code number check, then on air) and a review lane (Tactical analyst with code tools, Verifier critic, rewrite and recheck). At full time the Recap writer drafts a report from `RecapFacts`; it is number-checked and critic-reviewed, with `TemplateRecap` as the fallback. Player of the match comes from `PlayerRatings` in the stats engine.
4. **Render**: `MatchDirector.OverlayHub` pushes overlays over SignalR; `src/web` draws them.
5. **Personalise**: viewer modes in `src/web` (casual, analyst, club fan, player focus) select different overlay variants and layouts.

## When generating code
- An agent never computes a statistic. If a line needs a new number, add it to the stats engine as a moment metric or a tool result.
- Any new model output must pass `NarrationVerifier` (no number outside the facts) and have a deterministic fallback in `TemplateNarrator`.
- Prefer small, focused changes that match the surrounding code; add or update tests in `tests/`.
- Determinism matters: the simulator and stats engine must give the same output for the same seed.

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Run everything: `dotnet run --project src/MatchDirector.AppHost` (needs Docker for the Event Hubs emulator)
- Web only: `cd src/web && npm run dev`; lint and build with `npm run lint` and `npm run build`

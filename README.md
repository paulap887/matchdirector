# MatchDirector

**An AI broadcast studio for explainable, personalised match intelligence.**

MatchDirector turns synthetic football events into real-time insights, broadcast-ready overlays and match recaps. A team of AI agents explains *why* each moment matters, and the same intelligence is personalised for each viewer: an analyst, a casual fan, a supporter of one club, or someone following a single player, in their own language.

> **Numbers come from code; words come from AI.** Models never calculate a statistic. They only narrate stats that have already been computed and verified, and every claim cites the events behind it.

*All clubs, players and match data are fictional and generated synthetically.*

> 🚧 Built during the *Inside the Game* developer hackathon (6–27 October 2026). Work in progress.

## Pipeline

| Stage | What happens | Where |
|---|---|---|
| 1. Ingest | Synthetic match simulator streams events in real time | `src/MatchDirector.Simulator` |
| 2. Interpret | Deterministic stats: pass quality, speed, momentum, control vs. chaos | `src/MatchDirector.StatsEngine` |
| 3. Explain | Agent team explains why moments matter, with evidence | `src/MatchDirector.Agents` |
| 4. Render | Timed, machine-readable overlays pushed live | `src/MatchDirector.OverlayHub`, `src/web` |
| 5. Personalise | Analyst / casual / club / player-focus modes, multiple languages | `src/MatchDirector.Agents`, `src/web` |

## Architecture

_Diagram coming soon._

## Repository layout

```
src/
  MatchDirector.AppHost          .NET Aspire orchestration
  MatchDirector.ServiceDefaults  OpenTelemetry, health checks, resilience
  MatchDirector.Contracts        Event and overlay schemas
  MatchDirector.Simulator        Synthetic match engine
  MatchDirector.StatsEngine      Deterministic football statistics
  MatchDirector.Agents           Agent team (Microsoft Agent Framework)
  MatchDirector.OverlayHub       Real-time overlay API (SignalR)
  web                            React + Vite overlay UI
tests/                           Unit tests
infra/                           Azure infrastructure (Bicep, azd)
```

## Run locally

Requirements: .NET 10 SDK, Node.js 22+.

```bash
cd src/web && npm install && cd ../..
dotnet run --project src/MatchDirector.AppHost
```

The Aspire dashboard opens with every service, its logs and its traces.

### Synthetic matches

The simulator plays *Kestrel Bay FC v Redmoor Rovers* (both fictional) as a seeded, deterministic possession model: the same seed always replays the same match. It is calibrated against typical top-flight averages per match: about 2.8 goals, 27 shots, 900 passes and 79% pass accuracy, with a median shot distance of 15 m.

Set `Simulator__Seed` to replay a match and `Simulator__Speed` (1–20) to set the replay speed. To export a full match as a JSON-lines dataset:

```bash
dotnet run --project src/MatchDirector.Simulator -- export 42 match-42.jsonl
```

## License

[MIT](LICENSE)

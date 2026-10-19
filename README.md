# MatchDirector

**An AI broadcast studio for explainable, personalised match intelligence.**

MatchDirector turns synthetic football events into real-time insights, broadcast-ready overlays and match recaps. A team of AI agents explains *why* each moment matters, and the same intelligence is personalised for each viewer: an analyst, a casual fan, a supporter of one club, or someone following a single player, in their own language.

> **Numbers come from code; words come from AI.** Models never calculate a statistic. They only narrate stats that have already been computed and verified, and every claim cites the events behind it.

*All clubs, players and match data are fictional and generated synthetically.*

> 🚧 Built during the *Inside the Game* developer hackathon (6–27 October 2026). Work in progress.

**Live demo:** https://overlayhub.whitemeadow-6a3a991d.swedencentral.azurecontainerapps.io (a simulated match plays continuously at 10× speed)

## Pipeline

| Stage | What happens | Where |
|---|---|---|
| 1. Ingest | Synthetic match simulator streams events in real time | `src/MatchDirector.Simulator` |
| 2. Interpret | Deterministic stats: pass quality, speed, momentum, control vs. chaos | `src/MatchDirector.StatsEngine` |
| 3. Explain | Agent team explains why moments matter, with evidence, and writes a fact-checked full-time report | `src/MatchDirector.Agents` |
| 4. Render | Timed, machine-readable overlays pushed live | `src/MatchDirector.OverlayHub`, `src/web` |
| 5. Personalise | Casual, analyst, club-fan and player-focus views; pin a stat; shareable view links | `src/MatchDirector.Agents`, `src/web` |

## Evaluation

[`eval/MatchDirector.Eval`](eval/MatchDirector.Eval) measures the "numbers come from code" promise and the agent team. Latest results ([full report](docs/evaluation.md)):

| What | Result |
|---|---|
| Moments citing valid, earlier events | **100%** of 1,901 (50 matches, 96,650 events) |
| Fallback narration and full-time reports passing the number check | **100%** |
| Invented statistics blocked by code (red team) | **100%** |
| Meaning errors caught by the verifier critic (red team) | **98.2%**, with **0%** false alarms on correct lines |
| Narrator latency, live models | p50 1.8 s · p95 2.3 s |

The deterministic guarantees run in CI on every push, and the build fails if any of them drops below 100%.

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
eval/                            Evaluation harness (guarantees, live agents, red team)
docs/                            Evaluation report
infra/                           Azure infrastructure (Bicep)
```

## Run locally

Requirements: .NET 10 SDK, Node.js 22+.

```bash
cd src/web && npm install && cd ../..
dotnet run --project src/MatchDirector.AppHost
```

The Aspire dashboard opens with every service, its logs and its traces.

### Azure resources

Event Hubs runs in a local emulator (Docker). Microsoft Foundry is provisioned in Azure on first run: a Foundry resource with a project, `gpt-5-mini` ("reasoning") and `gpt-4.1-mini` ("fast") deployments, Application Insights, and a monthly budget alert ([infra/budget.bicep](infra/budget.bicep)). Local auth is disabled, so services use managed identity or your Azure CLI login, and no keys exist.

Tell Aspire where to provision (user-secrets, never committed):

```bash
cd src/MatchDirector.AppHost
dotnet user-secrets set Azure:SubscriptionId <subscription-id>
dotnet user-secrets set Azure:TenantId <tenant-id>
dotnet user-secrets set Azure:Location swedencentral
dotnet user-secrets set Azure:ResourceGroup rg-matchdirector-dev
dotnet user-secrets set Azure:CredentialSource AzureCli
dotnet user-secrets set Parameters:budgetEmail <you@example.com>
```

### Deploy to Azure

Deploys to Azure Container Apps with real Event Hubs, the Foundry models and managed identity throughout (no keys or connection secrets). The overlay hub serves the built React app.

```bash
aspire deploy --apphost src/MatchDirector.AppHost/MatchDirector.AppHost.csproj
```

User-secrets are not loaded when deploying, so supply the same settings as `Azure__SubscriptionId`, `Azure__TenantId`, `Azure__Location`, `Azure__ResourceGroup` and `Parameters__budgetEmail` environment variables. Simulator, agents and overlay hub each run as one 0.25 vCPU replica; expect roughly USD 1–2 per day while deployed.

### Synthetic matches

The simulator plays *Kestrel Bay FC v Redmoor Rovers* (both fictional) as a seeded, deterministic possession model: the same seed always replays the same match. It is calibrated against typical top-flight averages per match: about 2.8 goals, 27 shots, 900 passes and 79% pass accuracy, with a median shot distance of 15 m.

Set `Simulator__Seed` to replay a match and `Simulator__Speed` (1–20) to set the replay speed. To export a full match as a JSON-lines dataset:

```bash
dotnet run --project src/MatchDirector.Simulator -- export 42 match-42.jsonl
```

## License

[MIT](LICENSE)

# MatchDirector

**An AI broadcast studio for explainable, personalised match intelligence.**

MatchDirector watches a live (synthetic) football match and runs a small studio of AI agents over it. Code computes every statistic; the agents explain *why* each moment matters, check each other's work, and tell the same match differently to a casual fan, an analyst, a supporter of either club, or someone following one player. At full time they write the match report.

> **Numbers come from code; words come from AI.** Models never calculate a statistic. They only word facts that the stats engine has already computed, every number they write is checked against those facts, and every moment cites the events behind it.

**Live demo:** https://overlayhub.whitemeadow-6a3a991d.swedencentral.azurecontainerapps.io. Matches play on demand: opening the page kicks one off within seconds (one match is about 9 minutes at 10× speed).

*All clubs, players, venues and match data are fictional and generated synthetically. Built for the* Inside the Game *developer hackathon, October 2026.*

| Analyst | Club fan |
|---|---|
| ![Analyst view: stats, momentum and chaos meters, metric chips and the agent trace](docs/images/mode-analyst.png) | ![Club-fan view: partisan narration, us-versus-them stats, opponent moments muted](docs/images/mode-club.png) |
| **Casual fan** | **Player focus** |
| ![Casual view: big plain-language moments and a pinned stat](docs/images/mode-casual.png) | ![Player-focus view: live player card and only that player's moments](docs/images/mode-player.png) |

## How it works

```mermaid
flowchart LR
    subgraph Azure["Azure, deployed with .NET Aspire"]
        SIM[Match simulator<br/>seeded, deterministic]
        EH[(Event Hubs)]
        subgraph Agents["Agents service"]
            SE[Stats engine<br/>stats + moments<br/>with evidence]
            DIR{Director<br/>code}
            NAR[Narrator<br/>gpt-4.1-mini]
            CHK[Number check<br/>code]
            ANA[Tactical analyst<br/>gpt-5-mini + tools]
            CRI[Verifier critic<br/>gpt-5-mini]
            REC[Recap writer<br/>gpt-5-mini]
        end
        HUB[Overlay hub<br/>SignalR + React app]
        FOUNDRY[[Microsoft Foundry]]
    end
    UI[Viewer<br/>casual · analyst<br/>club fan · player focus]

    SIM --> EH --> SE --> DIR
    DIR -- fast lane --> NAR --> CHK --> HUB
    DIR -- review lane --> ANA --> CRI --> HUB
    CRI -. flagged lines .-> NAR
    DIR -- full time --> REC --> CHK
    HUB <--> UI
    HUB -. kick-off on demand .-> SIM
    Agents -. managed identity .-> FOUNDRY
```

1. **Simulator** plays a seeded, possession-based match (passes, carries, pressures, tackles, shots, saves, goals with pitch coordinates) and streams it to **Event Hubs**. Matches only play while someone is watching.
2. **Stats engine** (plain C#, no AI) turns events into team and player stats and detects *moments* (goals, big chances, key saves, passing moves, killer passes, high regains, momentum and tempo shifts). Each moment carries its computed metrics and the IDs of the events it was computed from.
3. **Director** (code) runs two lanes per moment:
   - **Fast lane**: the **Narrator** writes four lines (casual, analyst, home fan, away fan); **code checks every number** against the facts; the overlay is on air in about two seconds.
   - **Review lane**: for non-routine moments the **Verifier critic** checks the meaning (right team and player, right side for fan lines, right score story, no invented history). Flagged lines go back to the Narrator, are rechecked, and are replaced by a deterministic template if they fail twice. For important moments the **Tactical analyst** explains why it matters, looking stats up through code tools; its numbers are checked too.
   - **Full time**: the **Recap writer** drafts the match report from code-computed facts. **Player of the match** is chosen by a published formula in the stats engine, not by the AI. The report is number-checked and critic-reviewed.
4. **Overlay hub** pushes overlays, live stats and reports over SignalR. Each overlay carries an **agent trace** (which agent or check ran, on which model, how long it took, what it found), shown under "Why am I seeing this?".
5. **Viewer modes** are different experiences, not just different wording: casual (headline moments, big text), analyst (every metric, evidence open, influential players), club fan (partisan narration, us v them, opponent moments muted) and player focus (live player card, only that player's moments). Any view can pin one stat and be shared as a link, for example `?mode=club&club=RDM` or `?mode=player&player=KBY-09`.

## Challenge stages

| Stage | What MatchDirector does | Where to look |
|---|---|---|
| (a) Ingest live events | Deterministic simulator streaming to Event Hubs; JSON-lines export as a reusable synthetic dataset | [`src/MatchDirector.Simulator`](src/MatchDirector.Simulator) |
| (b) Interpret into stats and patterns | xG from distance and angle, pass difficulty and progression, ball and shot speed, possession by time, momentum, control vs chaos, player ratings | [`src/MatchDirector.StatsEngine`](src/MatchDirector.StatsEngine) |
| (c) Explain why a moment matters | Narrator, Tactical analyst with code tools, Verifier critic, full-time Recap writer, all evidence-backed | [`src/MatchDirector.Agents`](src/MatchDirector.Agents) |
| (d) Timed, machine-readable overlays | `Overlay` contract (show time, duration, priority, metrics, evidence, variants, agent trace) pushed over SignalR | [`src/MatchDirector.Contracts`](src/MatchDirector.Contracts), [`src/MatchDirector.OverlayHub`](src/MatchDirector.OverlayHub) |
| (e) Personalise | Casual, analyst, club fan and player focus modes; pin a stat; shareable links | [`src/web`](src/web) |

## Judging criteria

| Criterion | Evidence |
|---|---|
| **Technological implementation** | .NET 10 + Aspire, Microsoft Agent Framework, Microsoft Foundry, Event Hubs, Container Apps, React 19. 122 unit tests and an [evaluation harness](docs/evaluation.md) with a CI quality gate. |
| **Agentic design and innovation** | A code Director orchestrating four agents across a fast lane and a review lane; a critic-and-rewrite loop with a deterministic backstop; an analyst that looks stats up through tools instead of computing them; a visible agent trace on every overlay. |
| **Real-world impact** | Deployed on Azure with managed identity only (no keys anywhere), self-healing after restarts, on-demand play so idle cost is about USD 1.6 a day, a budget alert, and seeded matches that replay exactly. |
| **UX and presentation** | Four genuinely different viewer experiences, "Why am I seeing this?" on every overlay and report, shareable views, mobile-friendly layout. |
| **Category adherence** | All five required stages, plus suggested features: pass difficulty, ball and shot speed, control vs chaos, synthetic dataset creation and match recaps. |

## Evaluation

[`eval/MatchDirector.Eval`](eval/MatchDirector.Eval) measures the "numbers come from code" promise and the agent team. Latest results ([full report](docs/evaluation.md)):

| What | Result |
|---|---|
| Moments citing valid, earlier events | **100%** of 1,901 (50 matches, 96,650 events) |
| Fallback narration and full-time reports passing the number check | **100%** |
| Invented statistics blocked by code (red team) | **100%** |
| Meaning errors caught by the verifier critic (red team) | **98.2%**, with **0%** false alarms on correct lines |
| Narrator latency, live models | p50 1.8 s · p95 2.3 s |

The deterministic guarantees run in CI on every push, and the build fails if any of them drops below 100%. The evaluation also found and fixed two bugs in our own fallback text that unit tests had missed.

## Design decisions

- **Code computes, AI words.** Agents receive computed facts with units and a plain meaning per moment type. A regex-based number check rejects any number not in the facts, before anything reaches the screen.
- **Two lanes, not one long chain.** Viewers get a checked overlay in about two seconds; the slower review happens in the background and updates the same overlay in place. Each lane drops its oldest work rather than falling behind the live match.
- **A backstop for every model.** Every agent output has a deterministic template fallback that is itself verified by the evaluation, so a model outage or a bad answer never leaves the screen blank or wrong.
- **Judgement stays in code.** Player of the match, moment detection and priorities are formulas in the stats engine, documented and tested, never a model's opinion.
- **Cost-aware by design.** Matches play only when someone is watching, one per kick-off request, and pause after two minutes with no viewers.
- **Synthetic and reproducible.** Fictional clubs and players; the same seed always produces the same match, which makes tests and evaluation exact.

## Repository layout

```
src/
  MatchDirector.AppHost          .NET Aspire orchestration (local run and Azure deployment)
  MatchDirector.ServiceDefaults  OpenTelemetry, health checks, resilience
  MatchDirector.Contracts        Event, overlay and report schemas
  MatchDirector.Simulator        Synthetic match engine
  MatchDirector.StatsEngine      Deterministic football statistics, moments and player ratings
  MatchDirector.Agents           Director and agent studio (Microsoft Agent Framework)
  MatchDirector.OverlayHub       Real-time overlay hub (SignalR), serves the web app
  web                            React + Vite viewer
tests/                           Unit tests
eval/                            Evaluation harness (guarantees, live agents, red team)
docs/                            Evaluation report and screenshots
infra/                           Budget alert (Bicep); other Azure resources are generated by Aspire
.github/                         CI, GitHub Copilot instructions and prompts
```

## Run locally

Requirements: .NET 10 SDK, Node.js 22+, Docker (for the Event Hubs emulator) and an Azure subscription for the Foundry models.

```bash
cd src/web && npm install && cd ../..
dotnet run --project src/MatchDirector.AppHost
```

The Aspire dashboard opens with every service, its logs and its traces. Open the `web` endpoint to kick off a match. Without Foundry settings the studio still runs, using deterministic template narration only.

### Azure resources

Event Hubs runs in a local emulator. Microsoft Foundry is provisioned in Azure on first run: a Foundry resource with a project, `gpt-5-mini` ("reasoning") and `gpt-4.1-mini` ("fast") deployments, and a monthly budget alert ([infra/budget.bicep](infra/budget.bicep)). Local auth is disabled, so services use managed identity or your Azure CLI login, and no keys exist.

Tell Aspire where to provision (user-secrets, never committed):

```bash
cd src/MatchDirector.AppHost
dotnet user-secrets set Azure:SubscriptionId <subscription-id>
dotnet user-secrets set Azure:TenantId <tenant-id>
dotnet user-secrets set Azure:Location swedencentral
dotnet user-secrets set Azure:ResourceGroup <resource-group>
dotnet user-secrets set Azure:CredentialSource AzureCli
dotnet user-secrets set Parameters:budgetEmail <you@example.com>
```

### Deploy to Azure

Deploys to Azure Container Apps with Event Hubs, the Foundry models and managed identity throughout. The overlay hub serves the built React app.

```bash
aspire deploy --apphost src/MatchDirector.AppHost/MatchDirector.AppHost.csproj
```

User-secrets are not loaded when deploying, so supply the same settings as `Azure__SubscriptionId`, `Azure__TenantId`, `Azure__Location`, `Azure__ResourceGroup` and `Parameters__budgetEmail` environment variables. Simulator, agents and overlay hub each run as one 0.25 vCPU replica. Idle cost is about USD 1.6 a day; each watched match adds roughly USD 0.05–0.10 in model calls.

### Evaluation

```bash
dotnet run --project eval/MatchDirector.Eval                                      # deterministic checks and the code half of the red team
dotnet run --project eval/MatchDirector.Eval -- --endpoint <foundry-endpoint>     # full run against the models (Azure CLI login)
```

### Synthetic matches

The simulator plays *Kestrel Bay FC v Redmoor Rovers* (both fictional) as a seeded, deterministic possession model. It is calibrated against typical top-flight averages per match: about 2.8 goals, 27 shots, 900 passes and 79% pass accuracy, with a median shot distance of 15 m. Set `Simulator__Seed` to replay a match, `Simulator__Speed` (1–20) to set the replay speed, and `Simulator__OnDemand=false` to play continuously. To export a full match as a JSON-lines dataset:

```bash
dotnet run --project src/MatchDirector.Simulator -- export 42 match-42.jsonl
```

## Responsible AI and limitations

- The models only word computed facts; numbers are enforced by code, and meaning by a second model whose red-team recall is 98.2%, not 100%. When the critic flags a line twice, a deterministic template goes on air instead.
- The fast narrator sometimes writes a fan line from the wrong club's side; the critic catches these, which means club-fan mode falls back to template lines more often than other modes.
- Reviews take 5–15 seconds, so a corrected or annotated overlay can arrive after the original has been shown; the moments feed always shows the reviewed version.
- All data is synthetic. Real tracking or event data would need a loader into the same `MatchEvent` contract.

## License

[MIT](LICENSE)

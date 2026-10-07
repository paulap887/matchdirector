# MatchDirector

Hackathon entry: AI broadcast studio over synthetic football events (.NET 10 + Aspire, React/Vite, Microsoft Foundry, Agent Framework).

## Rules
- Numbers come from code (StatsEngine); LLMs only narrate. Every claim an agent makes must cite event IDs.
- All clubs, players and venues are fictional. No real league, club or player names, logos or music.
- Public repo: never commit secrets. Use managed identity, Key Vault and user-secrets.
- Commits and PRs go out under the author's name only. No AI attribution trailers or lines.
- Contracts in `src/MatchDirector.Contracts` are the shared schema; change them deliberately.

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Run everything: `dotnet run --project src/MatchDirector.AppHost`
- Web only: `cd src/web && npm run dev`

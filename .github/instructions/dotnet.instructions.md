---
applyTo: "src/**/*.cs,tests/**/*.cs"
---
# C# conventions in MatchDirector

- .NET 10, nullable enabled, file-scoped namespaces, primary constructors for services (`public sealed class Director(INarrator narrator, ...)`).
- Contracts and value types are `sealed record`s. New contract fields go at the end with a default, so existing producers keep compiling.
- Classes are `sealed` unless designed for inheritance. Keep comments for the why, as `/// <summary>` on public types and members.
- Services are wired in the Aspire AppHost (`src/MatchDirector.AppHost/AppHost.cs`); connection details come from references, never from hard-coded endpoints or keys.
- Agents use Microsoft Agent Framework (`AsAIAgent`, `ChatClientAgentOptions`). Their replies are JSON read with `AgentJson.Parse<T>`, and every failure path falls back to deterministic output.
- Pitch coordinates are metres on a 105 x 68 pitch; the home team attacks towards x = 105 in odd periods. Use `Pitch.ToAttackingFrame` rather than flipping coordinates by hand.
- Tests use xUnit with descriptive snake_case names (`Every_moment_cites_real_earlier_events`). Prefer property-style tests over many seeds (`MatchEngine(seed, ...)`) for engine behaviour.

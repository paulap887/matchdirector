using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Recap;
using MatchDirector.Agents.Translation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace MatchDirector.Agents.Studio;

/// <summary>
/// Builds the studio's agents on two model deployments: "fast" for live narration, "reasoning" for review and writing.
/// Every agent and model call is traced with OpenTelemetry (agent spans, model spans and token usage).
/// </summary>
public static class StudioFactory
{
    /// <summary>The ActivitySource and Meter name for the studio's own spans and the agents' model calls.</summary>
    public const string TelemetrySource = "MatchDirector.Agents";

    public static AgentNarrator Narrator(ChatClient fast, ILoggerFactory logs) =>
        new(Instrument(fast.AsAIAgent(instructions: AgentNarrator.Instructions, name: "Narrator", clientFactory: TraceModelCalls)),
            logs.CreateLogger<AgentNarrator>());

    public static StudioTeam Team(ChatClient fast, ChatClient reasoning, string fastModel, string reasoningModel, ILoggerFactory logs) => new(
        new VerifierCritic(Agent(reasoning, "VerifierCritic", VerifierCritic.Instructions), logs.CreateLogger<VerifierCritic>()),
        new TacticalAnalyst(reasoning, logs.CreateLogger<TacticalAnalyst>()),
        new RecapWriter(Agent(reasoning, "RecapWriter", RecapWriter.Instructions), logs.CreateLogger<RecapWriter>()),
        new Translator(Instrument(fast.AsAIAgent(instructions: Translator.Instructions, name: "Translator", clientFactory: TraceModelCalls)),
            logs.CreateLogger<Translator>()),
        fastModel,
        reasoningModel);

    /// <summary>An agent on the reasoning model with low reasoning effort, which keeps reviews to a few seconds.</summary>
    private static AIAgent Agent(ChatClient client, string name, string instructions) => Instrument(client.AsAIAgent(new ChatClientAgentOptions
    {
        Name = name,
        ChatOptions = new ChatOptions
        {
            Instructions = instructions,
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low }
        }
    }, clientFactory: TraceModelCalls));

    /// <summary>Adds an agent span around each run.</summary>
    public static AIAgent Instrument(AIAgent agent) => agent.AsBuilder().UseOpenTelemetry(TelemetrySource).Build();

    /// <summary>Adds a span and token-usage metrics to each model call.</summary>
    public static IChatClient TraceModelCalls(IChatClient client) => client.AsBuilder().UseOpenTelemetry(sourceName: TelemetrySource).Build();
}

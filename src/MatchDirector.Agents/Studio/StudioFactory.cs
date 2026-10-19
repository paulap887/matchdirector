using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Recap;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace MatchDirector.Agents.Studio;

/// <summary>Builds the studio's agents on two model deployments: "fast" for live narration, "reasoning" for review and writing.</summary>
public static class StudioFactory
{
    public static AgentNarrator Narrator(ChatClient fast, ILoggerFactory logs) =>
        new(fast.AsAIAgent(instructions: AgentNarrator.Instructions, name: "Narrator"), logs.CreateLogger<AgentNarrator>());

    public static StudioTeam Team(ChatClient reasoning, string fastModel, string reasoningModel, ILoggerFactory logs) => new(
        new VerifierCritic(Agent(reasoning, "VerifierCritic", VerifierCritic.Instructions), logs.CreateLogger<VerifierCritic>()),
        new TacticalAnalyst(reasoning, logs.CreateLogger<TacticalAnalyst>()),
        new RecapWriter(Agent(reasoning, "RecapWriter", RecapWriter.Instructions), logs.CreateLogger<RecapWriter>()),
        fastModel,
        reasoningModel);

    /// <summary>An agent on the reasoning model with low reasoning effort, which keeps reviews to a few seconds.</summary>
    private static AIAgent Agent(ChatClient client, string name, string instructions) => client.AsAIAgent(new ChatClientAgentOptions
    {
        Name = name,
        ChatOptions = new ChatOptions
        {
            Instructions = instructions,
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low }
        }
    });
}

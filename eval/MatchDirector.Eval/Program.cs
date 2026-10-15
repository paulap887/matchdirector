using System.Globalization;
using Azure.AI.OpenAI;
using Azure.Identity;
using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using MatchDirector.Eval;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Chat;

// MatchDirector evaluation harness.
//   dotnet run --project eval/MatchDirector.Eval -- [--matches 50] [--live-matches 2] [--redteam 15] [--out docs/evaluation]
//   Live agent sections need a Foundry endpoint (--endpoint or MATCHDIRECTOR_FOUNDRY_ENDPOINT) and an Azure CLI login.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture; // stable number formatting in the report
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var options = EvalOptions.Parse(args);
var report = new EvalReport(options);

Console.WriteLine($"Deterministic checks over {options.Matches} simulated matches...");
report.Deterministic = DeterministicEval.Run(options.Matches);

StudioPipeline? pipeline = null;
if (options.Endpoint is { } endpoint)
{
    var client = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential());
    ChatClient Chat(string deployment) => client.GetChatClient(deployment);
    var studio = new StudioTeam(
        new VerifierCritic(
            Chat(options.Reasoning).AsAIAgent(new ChatClientAgentOptions
            {
                Name = "VerifierCritic",
                ChatOptions = new Microsoft.Extensions.AI.ChatOptions
                {
                    Instructions = VerifierCritic.Instructions,
                    Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low }
                }
            }),
            NullLogger<VerifierCritic>.Instance),
        new TacticalAnalyst(Chat(options.Reasoning), NullLogger<TacticalAnalyst>.Instance),
        options.FastModel,
        options.ReasoningModel);
    var narrator = new AgentNarrator(
        Chat(options.Fast).AsAIAgent(instructions: AgentNarrator.Instructions, name: "Narrator"),
        NullLogger<AgentNarrator>.Instance);
    pipeline = new StudioPipeline(narrator, studio, NullLogger<StudioPipeline>.Instance);

    if (options.LiveMatches > 0)
    {
        Console.WriteLine($"Live studio over {options.LiveMatches} match(es) (up to {options.MaxMoments} moments each)...");
        report.Live = await LiveEval.RunAsync(pipeline, options);
    }
}
else
{
    Console.WriteLine("No Foundry endpoint: skipping the live studio and the critic's red-team results.");
}

Console.WriteLine($"Red team: {options.RedTeam} moments x {RedTeam.Attacks.Count} attacks...");
report.RedTeam = await RedTeam.RunAsync(options, pipeline?.Studio.Critic);

await report.WriteAsync();
Console.WriteLine($"Report written to {options.Out}.md and {options.Out}.json");

// Quality gate: the deterministic guarantees must hold exactly, or the run fails (CI runs this on every push).
var failures = report.GuaranteeFailures();
foreach (var failure in failures)
    Console.Error.WriteLine($"GUARANTEE FAILED: {failure}");
return failures.Count == 0 ? 0 : 1;

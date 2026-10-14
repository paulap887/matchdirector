namespace MatchDirector.Agents.Studio;

/// <summary>The review agents available to the director, and the model names shown in each overlay's trace.</summary>
public sealed record StudioTeam(VerifierCritic? Critic, TacticalAnalyst? Analyst, string FastModel, string ReasoningModel)
{
    public static readonly StudioTeam TemplatesOnly = new(null, null, "none", "none");

    public bool HasReviewers => Critic is not null;
}

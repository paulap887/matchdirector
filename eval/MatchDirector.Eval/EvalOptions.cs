namespace MatchDirector.Eval;

public sealed record EvalOptions(
    int Matches, int LiveMatches, int MaxMoments, int RedTeam, int Translate, int FirstSeed, string? Endpoint,
    string Fast, string Reasoning, string FastModel, string ReasoningModel, string Out)
{
    public static EvalOptions Parse(string[] args)
    {
        string? Arg(string name) => args.SkipWhile(a => a != $"--{name}").Skip(1).FirstOrDefault();
        int Int(string name, int fallback) => int.TryParse(Arg(name), out var v) ? v : fallback;

        return new EvalOptions(
            Matches: Int("matches", 50),
            LiveMatches: Int("live-matches", 2),
            MaxMoments: Int("max-moments", 40),
            RedTeam: Int("redteam", 15),
            Translate: Int("translate", 8),
            FirstSeed: Int("seed", 1000),
            Endpoint: Arg("endpoint") ?? Environment.GetEnvironmentVariable("MATCHDIRECTOR_FOUNDRY_ENDPOINT"),
            Fast: Arg("fast") ?? "fast",
            Reasoning: Arg("reasoning") ?? "reasoning",
            FastModel: Arg("fast-model") ?? "gpt-4.1-mini",
            ReasoningModel: Arg("reasoning-model") ?? "gpt-5-mini",
            Out: Arg("out") ?? "docs/evaluation");
    }
}

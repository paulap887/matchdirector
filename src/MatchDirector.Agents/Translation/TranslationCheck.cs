using System.Text.RegularExpressions;
using MatchDirector.Agents.Narration;

namespace MatchDirector.Agents.Translation;

/// <summary>
/// Code check for translations: a translated line must contain exactly the numbers of the English line it came from.
/// The English line already passed the number check, so every number on screen still traces to the stats engine.
/// </summary>
public static partial class TranslationCheck
{
    /// <summary>What differs between the numbers of the two lines; empty when the translation is faithful.</summary>
    public static IReadOnlyList<string> Differences(string english, string translated)
    {
        var expected = NarrationVerifier.NumbersIn(english).ToHashSet();
        var actual = NarrationVerifier.NumbersIn(DecimalComma().Replace(translated, ".")).ToHashSet();
        return actual.Except(expected).Select(n => $"added {n}")
            .Concat(expected.Except(actual).Select(n => $"dropped {n}"))
            .ToList();
    }

    /// <summary>"0,35" is 0.35 in most of the languages served.</summary>
    [GeneratedRegex(@"(?<=\d),(?=\d)")]
    private static partial Regex DecimalComma();
}

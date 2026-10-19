using System.Globalization;

namespace MatchDirector.Agents.Narration;

/// <summary>Every way a writer might reasonably show a computed number, so the number check accepts honest rounding.</summary>
public static class NumberVariants
{
    private static readonly string[] Formats = ["0", "0.#", "0.##"];

    public static HashSet<double> Expand(IEnumerable<double> values)
    {
        var numbers = new HashSet<double>();
        foreach (var value in values)
        {
            numbers.Add(value);
            numbers.Add(Math.Abs(value)); // "-42" is read as "42" by the number matcher
            foreach (var shown in new[] { value, Math.Abs(value), value * 100 }) // 0.34 xG may be said as 34%
            {
                foreach (var format in Formats)
                    numbers.Add(double.Parse(shown.ToString(format, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                numbers.Add(Math.Round(shown));
                numbers.Add(Math.Round(shown, 1));
                numbers.Add(Math.Round(shown, 2));
            }
        }
        return numbers;
    }
}

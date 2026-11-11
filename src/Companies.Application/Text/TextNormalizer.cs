using System.Globalization;
using System.Text;

namespace Companies.Application.Text;

/// <summary>
/// Turns free text into comparable tokens: lower case, no accents, no punctuation.
/// </summary>
internal static class TextNormalizer
{
    /// <summary>
    /// "Café  Münster, S.A." becomes ["cafe", "munster", "sa"].
    /// Runs of single letters are joined so "I.B.M." and "AT&amp;T" become "ibm" and "att".
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var cleaned = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue; // accent that was split off its base letter
            }

            if (char.IsLetterOrDigit(character))
            {
                cleaned.Append(char.ToLowerInvariant(character));
            }
            else if (character is not ('\'' or '’'))
            {
                cleaned.Append(' '); // apostrophes vanish ("McDonald's" -> "mcdonalds"), all else separates
            }
        }

        var rawTokens = cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return JoinSingleLetterRuns(rawTokens);
    }

    public static string Compact(IEnumerable<string> tokens) => string.Concat(tokens);

    private static List<string> JoinSingleLetterRuns(string[] tokens)
    {
        var result = new List<string>(tokens.Length);
        var run = new StringBuilder();

        foreach (var token in tokens)
        {
            if (token.Length == 1)
            {
                run.Append(token);
                continue;
            }

            FlushRun();
            result.Add(token);
        }

        FlushRun();
        return result;

        void FlushRun()
        {
            if (run.Length > 0)
            {
                result.Add(run.ToString());
                run.Clear();
            }
        }
    }
}

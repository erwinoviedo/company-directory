namespace Companies.Application.Text;

internal static class StringSimilarity
{
    /// <summary>
    /// Similarity from 0 (nothing in common) to 1 (identical), based on Levenshtein edit distance:
    /// one minus the number of single-character edits divided by the longer length.
    /// </summary>
    public static double Levenshtein(string first, string second)
    {
        if (first.Length == 0 && second.Length == 0)
        {
            return 1;
        }

        var distance = EditDistance(first, second);
        return 1 - (double)distance / Math.Max(first.Length, second.Length);
    }

    private static int EditDistance(string first, string second)
    {
        var previous = new int[second.Length + 1];
        var current = new int[second.Length + 1];

        for (var column = 0; column <= second.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= first.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= second.Length; column++)
            {
                var substitutionCost = first[row - 1] == second[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[second.Length];
    }
}

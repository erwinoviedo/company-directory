namespace Companies.Application.Text;

/// <summary>
/// A company name broken into the words that identify it.
/// </summary>
/// <param name="Words">All words, minus a leading "the" and trailing legal suffixes. "The Bank of America Corp." -> bank, of, america.</param>
/// <param name="Significant">Words minus filler words. -> bank, america.</param>
internal sealed record CompanyNameTokens(IReadOnlyList<string> Words, IReadOnlyList<string> Significant)
{
    private static readonly HashSet<string> LegalSuffixes =
    [
        "inc", "incorporated", "llc", "llp", "lp", "ltd", "limited", "corp", "corporation",
        "co", "company", "plc", "gmbh", "ag", "sa", "sas", "srl", "bv", "nv", "pty", "holdings", "group",
    ];

    private static readonly HashSet<string> FillerWords = ["the", "and", "of"];

    public static CompanyNameTokens From(string? companyName)
    {
        var words = TextNormalizer.Tokenize(companyName).ToList();

        // "Acme Tools Co., Ltd." -> acme, tools. Never strip the last remaining word.
        while (words.Count > 1 && LegalSuffixes.Contains(words[^1]))
        {
            words.RemoveAt(words.Count - 1);
        }

        // A leading article is never part of the brand: "The Bank of America" -> bank, of, america.
        if (words.Count > 1 && words[0] == "the")
        {
            words.RemoveAt(0);
        }

        var significant = words.Where(word => !FillerWords.Contains(word)).ToList();
        return new CompanyNameTokens(words, significant.Count > 0 ? significant : words);
    }
}

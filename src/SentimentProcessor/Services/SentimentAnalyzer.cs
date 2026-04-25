namespace SentimentProcessor.Services;

// Algoritm simplu bazat pe cuvinte cheie
// Scorul e între -1.0 (negativ) și +1.0 (pozitiv)
public static class SentimentAnalyzer
{
    private static readonly string[] PositiveWords =
    [
        "good",
        "great",
        "excellent",
        "amazing",
        "wonderful",
        "fantastic",
        "love",
        "best",
        "awesome",
        "perfect",
        "beautiful",
        "happy",
        "enjoy",
        "liked",
        "recommend",
        "brilliant",
        "outstanding",
        "superb",
        "helpful",
        "bun",
        "excelent",
        "minunat",
        "frumos",
        "super",
        "fain",
        "misto",
    ];

    private static readonly string[] NegativeWords =
    [
        "bad",
        "terrible",
        "awful",
        "horrible",
        "worst",
        "hate",
        "poor",
        "disappointing",
        "useless",
        "boring",
        "wrong",
        "broken",
        "ugly",
        "stupid",
        "failure",
        "mediocre",
        "rau",
        "groaznic",
        "prost",
        "nasol",
        "dezamagitor",
    ];

    public static double Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.0;
        }

        var words = text.ToLowerInvariant()
            .Split([' ', '.', ',', '!', '?', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        var positiveCount = 0;
        var negativeCount = 0;

        foreach (var word in words)
        {
            if (Array.Exists(PositiveWords, w => w == word))
            {
                positiveCount++;
            }

            if (Array.Exists(NegativeWords, w => w == word))
            {
                negativeCount++;
            }
        }

        var total = positiveCount + negativeCount;
        if (total == 0)
        {
            return 0.0;
        }

        // Normalizare între -1 și +1
        return Math.Round((double)(positiveCount - negativeCount) / total, 2);
    }
}

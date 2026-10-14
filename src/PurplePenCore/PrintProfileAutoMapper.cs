// PrintProfileAutoMapper.cs
//
// Proposes which print-profile colour each source-map colour layer should be
// paired with, so the user only has to review the pairing instead of building it
// by hand. Suggestions combine the preflight's exact matches, colour names (Swedish
// and English, including percentages), CMYK closeness and the ISOM symbol numbers
// that use the colour. Every profile colour is used at most once; layers without a
// convincing match keep their original colour.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PurplePen
{
    /// <summary>Explains where a proposed handling for a source-map colour came from.</summary>
    public enum PrintProfileSuggestionKind
    {
        /// <summary>The preflight or an earlier user choice already identifies this pairing exactly.</summary>
        Confirmed,

        /// <summary>The pairing was proposed from names, CMYK values and symbols.</summary>
        Suggested,

        /// <summary>No profile colour fits well enough; the original colour is kept.</summary>
        NoMatch,
    }

    /// <summary>One proposed handling for a single source-map colour layer.</summary>
    public sealed class PrintProfileMappingSuggestion
    {
        /// <summary>Gets or sets the source-map colour the suggestion is for.</summary>
        public SourceMapColor SourceColor { get; set; } = new SourceMapColor();

        /// <summary>Gets or sets the proposed profile rule, or null to keep the original colour.</summary>
        public PrintProfileColorRule Rule { get; set; }

        /// <summary>Gets or sets how the suggestion was found.</summary>
        public PrintProfileSuggestionKind Kind { get; set; }

        /// <summary>Gets or sets the internal match score (higher is more certain).</summary>
        public int Score { get; set; }
    }

    /// <summary>Creates automatic source-colour to profile-colour pairings.</summary>
    public static class PrintProfileAutoMapper
    {
        /// <summary>Score given to pairings the preflight or the user already identified exactly.</summary>
        private const int ConfirmedScore = 1000;

        /// <summary>Lowest score that is accepted as an automatic suggestion.</summary>
        private const int MinimumScore = 55;

        /// <summary>English and alternative colour words translated to the Swedish words used by the profiles.</summary>
        private static readonly Dictionary<string, string> WordTranslations = new Dictionary<string, string>(StringComparer.Ordinal) {
            { "yellow", "gul" }, { "gult", "gul" }, { "gula", "gul" },
            { "green", "grön" }, { "grönt", "grön" }, { "gröna", "grön" },
            { "blue", "blå" }, { "blått", "blå" }, { "blåa", "blå" },
            { "brown", "brun" }, { "brunt", "brun" }, { "bruna", "brun" },
            { "black", "svart" }, { "svarta", "svart" },
            { "purple", "violett" }, { "violet", "violett" }, { "lila", "violett" }, { "magenta", "violett" },
            { "white", "vit" }, { "vitt", "vit" }, { "vita", "vit" },
            { "over", "över" }, { "for", "för" }, { "road", "väg" }, { "fill", "fyllning" },
            { "contour", "kontur" }, { "contours", "kontur" }, { "höjdkurvor", "kontur" }, { "höjdkurva", "kontur" },
            { "area", "yta" }, { "areas", "yta" }, { "ytor", "yta" },
            { "line", "linje" }, { "lines", "linje" }, { "point", "punkt" }, { "points", "punkt" },
            { "opaque", "täckande" }, { "transparent", "transparent" },
            { "registration", "alla" }, { "all", "alla" }, { "colors", "färger" }, { "colours", "färger" },
        };

        /// <summary>Colour words that identify the hue of a layer.</summary>
        private static readonly HashSet<string> HueWords = new HashSet<string>(StringComparer.Ordinal) {
            "gul", "grön", "blå", "brun", "svart", "violett", "vit",
        };

        /// <summary>ISOM / ISSprOM symbol numbers that typically use the colour of a profile rule.</summary>
        private static readonly Dictionary<string, int[]> SymbolHints = new Dictionary<string, int[]>(StringComparer.Ordinal) {
            { "yellow-100", new int[] { 401, 402 } },
            { "yellow", new int[] { 401, 402 } },
            { "yellow-50", new int[] { 403, 404 } },
            { "green-30", new int[] { 406, 407 } },
            { "green-60", new int[] { 408, 409 } },
            { "green-100", new int[] { 410, 411 } },
            { "green-area", new int[] { 410, 411 } },
            { "brown-contours", new int[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 } },
            { "brown", new int[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 } },
            { "brown-50", new int[] { 529 } },
            { "road-fill", new int[] { 502 } },
            { "blue-area", new int[] { 301 } },
            { "blue-line-point", new int[] { 304, 305, 306, 308, 309, 310, 311, 312, 313 } },
            { "black-35", new int[] { 214 } },
            { "black-50-building", new int[] { 521 } },
        };

        /// <summary>Proposes a handling for every source-map colour.</summary>
        /// <param name="profile">The selected print profile.</param>
        /// <param name="sourceColors">The map's colours, lowest first.</param>
        /// <param name="ruleResults">Preflight results for the profile; exact and user-chosen matches are kept.</param>
        /// <returns>One suggestion per source colour, in the source colours' order.</returns>
        public static List<PrintProfileMappingSuggestion> Suggest(PrintProfile profile, IEnumerable<SourceMapColor> sourceColors,
                                                                   IEnumerable<ColorPreflightRuleResult> ruleResults)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            List<SourceMapColor> sources = (sourceColors ?? Enumerable.Empty<SourceMapColor>()).ToList();
            List<ColorPreflightRuleResult> results = (ruleResults ?? Enumerable.Empty<ColorPreflightRuleResult>()).ToList();
            List<Candidate> candidates = new List<Candidate>();

            for (int sourceIndex = 0; sourceIndex < sources.Count; ++sourceIndex) {
                SourceMapColor source = sources[sourceIndex];
                foreach (PrintProfileColorRule rule in profile.ColorRules) {
                    int score = IsConfirmedPair(results, rule, source) ? ConfirmedScore : Score(profile, rule, source);
                    if (score >= MinimumScore)
                        candidates.Add(new Candidate { SourceIndex = sourceIndex, Rule = rule, Score = score });
                }
            }

            // Greedy assignment: the most certain pairs win, every rule and every layer is used once.
            PrintProfileMappingSuggestion[] suggestions = new PrintProfileMappingSuggestion[sources.Count];
            HashSet<string> usedRules = new HashSet<string>(StringComparer.Ordinal);
            foreach (Candidate candidate in candidates.OrderByDescending(item => item.Score)
                                                      .ThenBy(item => item.SourceIndex)
                                                      .ThenBy(item => item.Rule.RelativeDrawOrder)) {
                if (suggestions[candidate.SourceIndex] != null || usedRules.Contains(candidate.Rule.Id))
                    continue;
                usedRules.Add(candidate.Rule.Id);
                suggestions[candidate.SourceIndex] = new PrintProfileMappingSuggestion {
                    SourceColor = sources[candidate.SourceIndex],
                    Rule = candidate.Rule,
                    Kind = candidate.Score >= ConfirmedScore ? PrintProfileSuggestionKind.Confirmed : PrintProfileSuggestionKind.Suggested,
                    Score = candidate.Score,
                };
            }

            for (int sourceIndex = 0; sourceIndex < sources.Count; ++sourceIndex) {
                if (suggestions[sourceIndex] == null) {
                    suggestions[sourceIndex] = new PrintProfileMappingSuggestion {
                        SourceColor = sources[sourceIndex],
                        Kind = PrintProfileSuggestionKind.NoMatch,
                    };
                }
            }

            return suggestions.ToList();
        }

        /// <summary>Checks whether the preflight already pairs a rule with exactly this source colour.</summary>
        /// <param name="results">Preflight rule results.</param>
        /// <param name="rule">The profile rule.</param>
        /// <param name="source">The source-map colour.</param>
        /// <returns>True for an exact identifier match or an earlier explicit user mapping.</returns>
        private static bool IsConfirmedPair(List<ColorPreflightRuleResult> results, PrintProfileColorRule rule, SourceMapColor source)
        {
            ColorPreflightRuleResult result = results.FirstOrDefault(item => String.Equals(item.Rule.Id, rule.Id, StringComparison.Ordinal));
            if (result == null || result.CandidateColors.Count != 1)
                return false;
            if (result.MatchStatus != ColorPreflightMatchStatus.Matched && !result.UserConfirmed)
                return false;
            SourceMapColor candidate = result.CandidateColors[0];
            return candidate.OcadId == source.OcadId && candidate.DrawOrder == source.DrawOrder
                && String.Equals(candidate.Name, source.Name, StringComparison.Ordinal);
        }

        /// <summary>Scores how well a source colour fits a profile rule.</summary>
        /// <param name="profile">The profile, used to see whether the rule's CMYK is unique.</param>
        /// <param name="rule">The profile rule.</param>
        /// <param name="source">The source-map colour.</param>
        /// <returns>A score; at least <see cref="MinimumScore"/> is a usable suggestion.</returns>
        internal static int Score(PrintProfile profile, PrintProfileColorRule rule, SourceMapColor source)
        {
            NameInfo sourceName = NameInfo.Parse(source.Name);
            List<NameInfo> ruleNames = new List<NameInfo> { NameInfo.Parse(rule.Name) };
            ruleNames.AddRange(rule.Identifier.Names.Select(NameInfo.Parse));

            int nameScore = ruleNames.Max(ruleName => ScoreNames(sourceName, ruleName));
            int score = nameScore;
            if (rule.Identifier.OcadIds.Contains(source.OcadId))
                score += 30;

            float difference = MaxDifference(source.Cmyk, rule.EffectiveCmyk);
            bool exactCmyk = difference <= 0.5F;
            if (exactCmyk) {
                score += 40;
                int sameCmykRules = profile.ColorRules.Count(other => MaxDifference(other.EffectiveCmyk, rule.EffectiveCmyk) <= 0.5F);
                if (sameCmykRules == 1)
                    score += 20;
            }
            else if (difference <= 5F) {
                score += 30;
            }
            else if (difference <= 12F) {
                score += 15;
            }
            else if (difference > 30F) {
                score -= 50;
            }

            score += ScoreSymbols(rule, source);
            return score;
        }

        /// <summary>Scores the colour names of a source colour and a rule.</summary>
        private static int ScoreNames(NameInfo source, NameInfo rule)
        {
            if (source.Tokens.Count == 0 || rule.Tokens.Count == 0)
                return 0;
            if (String.Equals(source.Normalized, rule.Normalized, StringComparison.Ordinal))
                return 100;
            if (source.Tokens.SetEquals(rule.Tokens))
                return 80;

            int score = 0;
            if (source.Hues.Count > 0 && rule.Hues.Count > 0) {
                if (source.Hues.IsSubsetOf(rule.Hues))
                    score += 30;
                else
                    score -= 60;
            }

            if (source.Hues.Count > 0 || source.Percentages.Count > 0) {
                if (source.EffectivePercentages.Overlaps(rule.EffectivePercentages))
                    score += 20;
                else
                    score -= 40;
            }

            List<string> sourceWords = source.Tokens.Where(token => !source.Hues.Contains(token) && !source.Percentages.Contains(token)).ToList();
            List<string> ruleWords = rule.Tokens.Where(token => !rule.Hues.Contains(token) && !rule.Percentages.Contains(token)).ToList();
            int sharedWords = sourceWords.Intersect(ruleWords, StringComparer.Ordinal).Count();
            score += 10 * sharedWords;
            return score;
        }

        /// <summary>Adds points when the colour is used by symbols that normally use the rule's colour.</summary>
        private static int ScoreSymbols(PrintProfileColorRule rule, SourceMapColor source)
        {
            if (!SymbolHints.TryGetValue(rule.Id, out int[] hints) || source.Symbols.Count == 0)
                return 0;
            int matching = source.Symbols.Count(symbol => hints.Contains(SymbolNumber(symbol.SymbolId)));
            if (matching == 0)
                return 0;
            return matching * 2 >= source.Symbols.Count ? 25 : 10;
        }

        /// <summary>Extracts the whole ISOM symbol number from an identifier such as "403.1".</summary>
        /// <param name="symbolId">The symbol identifier.</param>
        /// <returns>The symbol number, or -1 when there is none.</returns>
        private static int SymbolNumber(string symbolId)
        {
            if (String.IsNullOrEmpty(symbolId))
                return -1;
            string whole = symbolId.Split('.', '-', ' ')[0];
            return Int32.TryParse(whole, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : -1;
        }

        /// <summary>Returns the largest difference between two CMYK colours, in percentage points.</summary>
        private static float MaxDifference(PrintProfileCmyk first, PrintProfileCmyk second)
        {
            return Math.Max(Math.Max(Math.Abs(first.Cyan - second.Cyan), Math.Abs(first.Magenta - second.Magenta)),
                            Math.Max(Math.Abs(first.Yellow - second.Yellow), Math.Abs(first.Black - second.Black)));
        }

        /// <summary>A pair that can be suggested, with its score.</summary>
        private sealed class Candidate
        {
            public int SourceIndex;
            public PrintProfileColorRule Rule;
            public int Score;
        }

        /// <summary>A colour name split into normalized words, hues and percentages.</summary>
        private sealed class NameInfo
        {
            public string Normalized = String.Empty;
            public HashSet<string> Tokens = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> Hues = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> Percentages = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>Gets the percentages, treating a name without any as 100 %.</summary>
            public HashSet<string> EffectivePercentages {
                get { return Percentages.Count > 0 ? Percentages : new HashSet<string>(StringComparer.Ordinal) { "100" }; }
            }

            /// <summary>Splits and normalizes a colour name.</summary>
            /// <param name="name">The colour name as written in the map or profile.</param>
            /// <returns>The parsed name.</returns>
            public static NameInfo Parse(string name)
            {
                NameInfo info = new NameInfo();
                if (String.IsNullOrWhiteSpace(name))
                    return info;

                StringBuilder word = new StringBuilder();
                List<string> words = new List<string>();
                foreach (char character in name.ToLowerInvariant()) {
                    if (Char.IsLetterOrDigit(character)) {
                        word.Append(character);
                    }
                    else {
                        AddWord(words, word);
                    }
                }
                AddWord(words, word);

                foreach (string rawWord in words) {
                    string translated = WordTranslations.TryGetValue(rawWord, out string swedish) ? swedish : rawWord;
                    info.Tokens.Add(translated);
                    if (HueWords.Contains(translated))
                        info.Hues.Add(translated);
                }

                foreach (Match match in Regex.Matches(name, @"(\d{1,3})\s*%")) {
                    if (Int32.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 && value <= 100)
                        info.Percentages.Add(value.ToString(CultureInfo.InvariantCulture));
                }

                info.Normalized = String.Join(" ", words.Select(item => WordTranslations.TryGetValue(item, out string swedish) ? swedish : item));
                return info;
            }

            /// <summary>Moves a finished word from the builder into the list.</summary>
            private static void AddWord(List<string> words, StringBuilder word)
            {
                if (word.Length > 0) {
                    words.Add(word.ToString());
                    word.Clear();
                }
            }
        }
    }
}

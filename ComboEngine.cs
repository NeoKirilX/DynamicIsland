using System.Text.RegularExpressions;

namespace DynamicIsland;

static class ComboEngine
{
    static readonly char[] PunctChars = [' ', '.', ',', ';', '!', '?', '-', '—', '–', ':', '"', '\'', '«', '»'];
    static readonly Regex BracketBlockRegex = new(@"\([^)]*\)|\[[^\]]*\]|\{[^}]*\}", RegexOptions.Compiled);
    static readonly Regex NonAlphaNumRegex = new(@"[\s.,!?;:""'—–\-\(\)\[\]\{\}«»]+", RegexOptions.Compiled);
    static readonly Regex PunctSplitRegex = new(@"[,;!?]+\s*", RegexOptions.Compiled);
    static readonly Regex WordSplitRegex = new(@"[\s.,!?;:""'—–\-\(\)\[\]«»]+", RegexOptions.Compiled);

    public static string CleanPhrase(string s, bool stripBrackets = true)
    {
        string trimmed = s.Trim(PunctChars);
        if (stripBrackets && trimmed.Length >= 2)
        {
            char first = trimmed[0], last = trimmed[^1];
            if ((first == '(' && last == ')') || (first == '[' && last == ']') || (first == '{' && last == '}'))
            {
                string inner = trimmed.Substring(1, trimmed.Length - 2).Trim(PunctChars);
                if (inner.Length > 0) return inner;
            }
        }
        return trimmed;
    }

    public static string NormalizePhrase(string s)
    {
        string withoutBrackets = BracketBlockRegex.Replace(s, "");
        string target = string.IsNullOrWhiteSpace(withoutBrackets) ? s : withoutBrackets;
        return NonAlphaNumRegex.Replace(target.ToLowerInvariant(), "");
    }

    public static string FormatBadge(string phrase, int count, ComboCounterStyle style, int total = 0)
    {
        string badge = style switch
        {
            ComboCounterStyle.EnX => $"x{count}",
            ComboCounterStyle.Times => $"×{count}",
            ComboCounterStyle.Brackets => $"({count})",
            ComboCounterStyle.Square => $"[{count}]",
            ComboCounterStyle.Hash => $"#{count}",
            ComboCounterStyle.Progress when total > 0 => $"{count}/{total}",
            _ => $"х{count}"
        };
        return string.IsNullOrEmpty(phrase) ? badge : $"{phrase} {badge}";
    }

    static int MaxComboRun(List<string> parts, int minLen)
    {
        if (parts.Count == 0) return 0;
        string norm0 = NormalizePhrase(parts[0]);
        if (norm0.Length >= minLen && parts.All(p => NormalizePhrase(p) == norm0))
            return parts.Count;

        int maxRun = 0, curRun = 0;
        string prevNorm = "";
        foreach (string part in parts)
        {
            string curNorm = NormalizePhrase(part);
            if (curNorm.Length >= minLen && curNorm == prevNorm)
            {
                curRun++;
            }
            else
            {
                curRun = curNorm.Length >= minLen ? 1 : 0;
                prevNorm = curNorm;
            }
            if (curRun > maxRun) maxRun = curRun;
        }
        return maxRun;
    }

    static (List<string>? Parts, int Repeats) FindNgramCombo(List<string> words, int minRep, int minLen = 1, int maxN = 3)
    {
        for (int n = maxN; n >= 2; n--)
        {
            for (int start = 0; start <= words.Count - n * minRep; start++)
            {
                var chunk = words.GetRange(start, n);
                string norm0 = NormalizePhrase(chunk[0]);
                if (chunk.All(w => NormalizePhrase(w) == norm0)) continue;
                if (norm0.Length < minLen) continue;

                string patternNorm = string.Join(" ", chunk.Select(NormalizePhrase));
                int reps = 1;
                while (start + (reps + 1) * n <= words.Count)
                {
                    var nextChunk = words.GetRange(start + reps * n, n);
                    string nextNorm = string.Join(" ", nextChunk.Select(NormalizePhrase));
                    if (nextNorm != patternNorm) break;
                    reps++;
                }

                if (reps >= minRep)
                {
                    var result = new List<string>();
                    if (start > 0) result.Add(string.Join(" ", words.Take(start)));
                    for (int r = 0; r < reps; r++)
                        result.Add(string.Join(" ", words.GetRange(start + r * n, n)));
                    int tail = start + reps * n;
                    if (tail < words.Count) result.Add(string.Join(" ", words.Skip(tail)));
                    return (result, reps);
                }
            }
        }
        return (null, 0);
    }

    public static List<string> SplitPhraseLine(string text, ComboSplitMode mode, int minRep, int minLen, bool stripBrackets)
    {
        string cleanFull = CleanPhrase(text, stripBrackets);
        if (mode == ComboSplitMode.Lines || string.IsNullOrWhiteSpace(cleanFull))
            return [cleanFull];

        var punctParts = PunctSplitRegex.Split(cleanFull)
            .Select(p => CleanPhrase(p, stripBrackets))
            .Where(p => p.Length > 0)
            .ToList();

        int punctRun = MaxComboRun(punctParts, minLen);
        if (mode == ComboSplitMode.Punct)
            return punctRun >= minRep ? punctParts : [cleanFull];

        if (punctRun >= minRep && mode == ComboSplitMode.All)
            return punctParts;

        var words = WordSplitRegex.Split(cleanFull)
            .Select(w => CleanPhrase(w, stripBrackets))
            .Where(w => w.Length > 0)
            .ToList();

        if (words.Count >= minRep)
        {
            var (ngramParts, ngramReps) = FindNgramCombo(words, minRep, minLen);
            int wordRun = MaxComboRun(words, minLen);

            if (wordRun >= minRep && wordRun >= ngramReps) return words;
            if (ngramParts != null && ngramReps >= minRep) return ngramParts;
            if (wordRun >= minRep) return words;
        }

        return [cleanFull];
    }

    public static LyricsService.Line[] Process(LyricsService.Line[] rawLines, TimeSpan totalDuration)
    {
        if (!Settings.ComboEnabled || rawLines == null || rawLines.Length == 0)
            return rawLines ?? [];

        int minRep = Settings.ComboMinRepeats;
        int minLen = Settings.ComboMinWordLen;
        var style = Settings.ComboCounterStyle;
        var mode = Settings.ComboSplitMode;
        bool ignoreAdlibs = Settings.ComboIgnoreAdlibs;
        bool stripBrackets = Settings.ComboStripBrackets;

        var expanded = new List<(TimeSpan Time, string Text, int OrigLineIndex)>();
        for (int i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i];
            if (string.IsNullOrWhiteSpace(line.Text) || Lyric.IsWordless(line.Text))
            {
                expanded.Add((line.Time, line.Text, i));
                continue;
            }

            TimeSpan nextTime = i + 1 < rawLines.Length ? rawLines[i + 1].Time : totalDuration;
            TimeSpan span = nextTime > line.Time ? nextTime - line.Time : TimeSpan.FromSeconds(2);
            if (span < TimeSpan.FromMilliseconds(500)) span = TimeSpan.FromMilliseconds(500);

            var parts = SplitPhraseLine(line.Text, mode, minRep, minLen, stripBrackets);
            if (parts.Count <= 1)
            {
                expanded.Add((line.Time, line.Text, i));
            }
            else
            {
                TimeSpan step = TimeSpan.FromTicks(span.Ticks / parts.Count);
                for (int k = 0; k < parts.Count; k++)
                    expanded.Add((line.Time + TimeSpan.FromTicks(step.Ticks * k), parts[k], i));
            }
        }

        // Detect combos and runs
        var result = new List<LyricsService.Line>();
        int idx = 0;
        while (idx < expanded.Count)
        {
            var item = expanded[idx];
            string norm = NormalizePhrase(item.Text);

            if (norm.Length < minLen || Lyric.IsWordless(item.Text))
            {
                result.Add(new LyricsService.Line(item.Time, item.Text));
                idx++;
                continue;
            }

            // Find run length with optional adlib skipping
            int run = 1;
            int check = idx + 1;
            int skippedAdlibs = 0;
            while (check < expanded.Count)
            {
                string nextNorm = NormalizePhrase(expanded[check].Text);
                if (nextNorm == norm)
                {
                    run++;
                    check++;
                }
                else if (ignoreAdlibs && check + 1 < expanded.Count && NormalizePhrase(expanded[check + 1].Text) == norm)
                {
                    string candidate = CleanPhrase(expanded[check].Text, stripBrackets);
                    if (candidate.Length <= 4 || (candidate.StartsWith("(") && candidate.EndsWith(")")))
                    {
                        skippedAdlibs++;
                        run++;
                        check++;
                    }
                    else break;
                }
                else break;
            }

            if (run >= minRep)
            {
                int count = 1;
                for (int r = 0; r < run; r++)
                {
                    var cur = expanded[idx + r];
                    result.Add(new LyricsService.Line(cur.Time, FormatBadge(cur.Text, count++, style, run)));
                }
                idx += run;
            }
            else
            {
                result.Add(new LyricsService.Line(item.Time, item.Text));
                idx++;
            }
        }

        return result.ToArray();
    }
}

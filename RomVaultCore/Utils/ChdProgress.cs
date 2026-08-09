using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RomVaultCore.Utils
{
    internal static class ChdProgress
    {
        private static readonly Regex PercentPattern = new Regex(
            @"(?<![\d.])(?<percent>\d{1,3}(?:\.\d+)?)\s*%",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static int ParsePercent(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return -1;

            Match match = PercentPattern.Match(line);
            if (!match.Success ||
                !double.TryParse(match.Groups["percent"].Value, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out double percent) ||
                percent < 0 || percent > 100)
                return -1;

            return (int)Math.Floor(percent);
        }

        internal static string DescribePhase(string arguments)
        {
            string command = (arguments ?? "").TrimStart();
            int separator = command.IndexOfAny(new[] { ' ', '\t' });
            if (separator >= 0)
                command = command.Substring(0, separator);

            switch (command.ToLowerInvariant())
            {
                case "createcd":
                case "createdvd":
                case "createraw":
                case "createhd":
                case "createld":
                    return "encoding";
                case "copy":
                    return Regex.IsMatch(arguments ?? "", @"(?:^|\s)-c\s+none(?:\s|$)", RegexOptions.IgnoreCase)
                        ? "staging"
                        : "compressing";
                case "verify":
                    return "verifying";
                case "extractcd":
                case "extractdvd":
                case "extractraw":
                case "extracthd":
                case "extractld":
                    return "extracting";
                case "addmeta":
                    return "writing metadata";
                default:
                    return "processing";
            }
        }

        internal static bool RunSelfTest(out string error)
        {
            error = "";
            try
            {
                string[] lines =
                {
                    "Compressing, 13.3% complete",
                    "Compressing, 18.8% complete",
                    "Compressing, 23.3% complete",
                    "Compressing, 28.4% complete",
                    "Compressing, 37.0% complete"
                };
                int[] expected = { 13, 18, 23, 28, 37 };
                for (int i = 0; i < lines.Length; i++)
                {
                    if (ParsePercent(lines[i]) != expected[i])
                        throw new InvalidOperationException("Decimal chdman progress was parsed incorrectly.");
                }

                ChdProgressTracker tracker = new ChdProgressTracker("createcd -i disc.cue -o disc.chd");
                int[] observed = new int[3];
                int observedCount = 0;
                foreach (string line in new[]
                {
                    "Compressing, 13.3% complete",
                    "Compressing, 18.8% complete",
                    "Compressing, 17.2% complete",
                    "Compressing, 23.3% complete"
                })
                {
                    if (tracker.TryAdvance(line, out int percent))
                        observed[observedCount++] = percent;
                }
                if (observedCount != 3 || observed[0] != 13 || observed[1] != 18 || observed[2] != 23)
                    throw new InvalidOperationException("CHD progress was not monotonic.");
                if (tracker.Phase != "encoding" || DescribePhase("copy -i a -o b -c none") != "staging" ||
                    DescribePhase("copy -i a -o b -c zstd") != "compressing" || DescribePhase("verify -i a") != "verifying")
                    throw new InvalidOperationException("CHD progress phases were classified incorrectly.");

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    internal sealed class ChdProgressTracker
    {
        private int _lastPercent = -1;

        internal ChdProgressTracker(string arguments)
        {
            Phase = ChdProgress.DescribePhase(arguments);
        }

        internal string Phase { get; }

        internal bool TryAdvance(string line, out int percent)
        {
            percent = ChdProgress.ParsePercent(line);
            if (percent < 0 || percent <= _lastPercent)
                return false;

            _lastPercent = percent;
            return true;
        }
    }
}

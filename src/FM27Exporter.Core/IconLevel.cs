using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FM27Exporter.Core
{
    public static class IconLevel
    {
        private static readonly Regex Icon = new Regex(
            @"^scaling-icons-(?<body>[a-z0-9-]+?)(?:---white)?-size-",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex Number = new Regex(@"-(?<n>\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly string[] States = { "slightly-injured", "injured", "recurring" };

        public static string? Value(IEnumerable<string> classes)
        {
            foreach (var c in classes)
            {
                if (c == null)
                    continue;
                var m = Icon.Match(c);
                if (!m.Success)
                    continue;
                var body = m.Groups["body"].Value;
                var n = Number.Match(body);
                if (n.Success)
                    return int.Parse(n.Groups["n"].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                foreach (var s in States)
                    if (body.EndsWith("-" + s, System.StringComparison.Ordinal))
                        return Title(s);
            }
            return null;
        }

        private static string Title(string state)
        {
            var parts = state.Split('-');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FM27Exporter.Core
{
    public static class StarRating
    {
        public const string StarClass = "fm-star-rating-star";
        public const string StarClassNoMargin = "fm-star-rating-star-no-margin";

        private static readonly Regex State = new Regex(
            @"^ability-(max|half|minimum)-potential-level-(fullfilled|full|half|none)-youth-(true|false)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool IsStar(IEnumerable<string> classes)
        {
            foreach (var c in classes)
                if (c == StarClass || c == StarClassNoMargin)
                    return true;
            return false;
        }

        public static double? StarValue(IEnumerable<string> classes, bool withRange)
        {
            foreach (var c in classes)
            {
                if (c == null)
                    continue;
                var m = State.Match(c);
                if (m.Success)
                    return withRange ? Math.Max(Part(m.Groups[1].Value), Part(m.Groups[2].Value)) : Part(m.Groups[1].Value);
            }
            return null;
        }

        public static double? StarValue(IEnumerable<string> classes)
        {
            foreach (var c in classes)
            {
                if (c == null)
                    continue;
                var m = State.Match(c);
                if (m.Success)
                    return Math.Max(Part(m.Groups[1].Value), Part(m.Groups[2].Value));
            }
            return null;
        }

        public static string Format(double total) => total.ToString("0.0", CultureInfo.InvariantCulture);

        public static string Format(double low, double high) =>
            high - low < 0.25 ? Format(high) : Format(low) + "-" + Format(high);

        private static double Part(string level) => level switch
        {
            "max" or "fullfilled" or "full" => 1.0,
            "half" => 0.5,
            _ => 0.0,
        };
    }
}

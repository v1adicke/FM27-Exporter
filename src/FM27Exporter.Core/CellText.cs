using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FM27Exporter.Core
{
    public static class CellText
    {
        private static readonly Regex Tag = new Regex(
            @"</?(?:align|allcaps|alpha|b|br|color|cspace|font|font-weight|gradient|i|indent|line-height|line-indent|link|lowercase|margin|mark|material|mspace|nobr|noparse|page|pos|rotate|s|size|smallcaps|space|sprite|strikethrough|style|sub|sup|u|underline|uppercase|voffset|width)(?:[=\s][^<>]*)?/?>|<#[0-9a-fA-F]{3,8}>",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Space = new Regex(@"[ \t\r\n\f\v]+", RegexOptions.Compiled);

        public static string Clean(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var plain = text!.IndexOf('<') >= 0 ? Tag.Replace(text, string.Empty) : text;
            if (plain.IndexOf('\u00AD') >= 0 || plain.IndexOf('\u200B') >= 0 || plain.IndexOf('\u2060') >= 0)
                plain = plain.Replace("\u00AD", string.Empty).Replace("\u200B", string.Empty).Replace("\u2060", string.Empty);
            return Space.Replace(plain, " ").Trim();
        }

        public static string Join(IEnumerable<string?> parts)
        {
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                var clean = Clean(part);
                if (clean.Length == 0)
                    continue;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(clean);
            }
            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FM27Exporter.Core
{
    public static class ExportNames
    {
        public static string BaseName(string screen, DateTime when, bool timestamp = true)
        {
            var name = Sanitize(string.IsNullOrWhiteSpace(screen) ? "Table" : screen.Trim());
            return timestamp ? name + " " + when.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) : name;
        }

        public static string Unique(string folder, string baseName, IEnumerable<string> extensions)
        {
            var exts = new List<string>(extensions);
            for (var i = 1; ; i++)
            {
                var candidate = i == 1 ? baseName : $"{baseName} ({i})";
                var taken = false;
                foreach (var ext in exts)
                    if (File.Exists(Path.Combine(folder, candidate + ext)))
                        taken = true;
                if (!taken)
                    return candidate;
            }
        }

        public static string Sanitize(string name)
        {
            var invalid = new HashSet<char>(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' });
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(ch < 32 || invalid.Contains(ch) ? '_' : ch);
            var result = sb.ToString().Trim().TrimEnd('.');
            if (result.Length > 80)
                result = result.Substring(0, 80).TrimEnd();
            return result.Length == 0 ? "Table" : result;
        }

        public static string GuessScreen(IEnumerable<string?> namesInnermostFirst)
        {
            string? fallback = null;
            foreach (var raw in namesInnermostFirst)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                var name = raw!.Trim();
                if (!char.IsUpper(name[0]) || name.IndexOf(' ') >= 0 || IsGeneric(name))
                    continue;
                foreach (var suffix in ScreenSuffixes)
                {
                    if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                        return Words(name.Substring(0, name.Length - suffix.Length));
                }
                if (fallback == null)
                    fallback = Words(name);
            }
            return fallback ?? "Table";
        }

        private static readonly string[] ScreenSuffixes = { "MainContentBlock", "ContentBlock", "Screen", "Panel", "View", "Page" };

        private static readonly string[] GenericPrefixes =
        {
            "Bindable", "Binding", "VisualElement", "Grid", "ScalableBindable", "MultiGrid", "Template", "SpeakTo",
            "MainUIDocument", "DefaultLayer", "MenuBody", "Unity",
        };

        private static bool IsGeneric(string name)
        {
            foreach (var p in GenericPrefixes)
                if (name.StartsWith(p, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static string Words(string name)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if (ch == '_' || ch == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                        sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(ch) && sb.Length > 0 && sb[sb.Length - 1] != ' '
                    && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    sb.Append(' ');
                sb.Append(ch);
            }
            return sb.ToString().Trim();
        }
    }
}

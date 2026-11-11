using System;
using System.IO;
using System.Text;
using FM27Exporter.Core;
using UnityEngine.UIElements;

namespace FM27Exporter
{
    internal static class Diagnostics
    {
        private const int MaxElements = 4000;

        public static string Write(Settings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Plugin.Name} {Plugin.Version} diagnostics {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Game {UnityEngine.Application.version}, Unity {UnityEngine.Application.unityVersion}");
            sb.AppendLine("To report a problem: https://github.com/v1adicke/fm27-exporter/issues — attach this file and BepInEx/LogOutput.log.");
            var roots = Ui.PanelRoots();
            sb.AppendLine($"Panels: {roots.Count}");
            var tables = TableView.FindAll(out var notes);
            foreach (var n in notes)
                sb.AppendLine("note: " + n);
            sb.AppendLine($"Usable tables: {tables.Count}");
            for (var t = 0; t < tables.Count; t++)
            {
                var view = tables[t];
                var cols = view.ReadColumns();
                var rows = view.VisibleRows();
                sb.AppendLine();
                sb.AppendLine($"== Table {t}: '{view.ScreenName()}' #{view.Table.name} area={Ui.Area(view.Table):0} " +
                              $"ItemCount={view.ItemCount?.ToString() ?? "?"} scroll={view.Rows.scrollOffset} " +
                              $"viewport={view.Rows.contentViewport.layout.height:0} content={view.Rows.contentContainer.layout.height:0}");
                sb.AppendLine($"   path: {Path(view.Table)}");
                for (var i = 0; i < cols.Count; i++)
                    sb.AppendLine($"   col {i,3}: '{cols[i].Header}'{(cols[i].Hidden ? " (hidden)" : string.Empty)}");
                sb.AppendLine($"   visible rows: {rows.Count}, cells in first row: {(rows.Count > 0 ? rows[0].Cells.Count : 0)}");
                for (var r = 0; r < Math.Min(3, rows.Count); r++)
                {
                    var cells = new StringBuilder();
                    foreach (var c in rows[r].Cells)
                        cells.Append(TableView.ReadCell(c)).Append(" | ");
                    sb.AppendLine($"   row y={rows[r].ContentY:0} h={rows[r].Height:0}{(rows[r].Selected ? " [selected]" : "")}: {cells}");
                }
                for (var r = 0; r < rows.Count; r++)
                {
                    var line = new StringBuilder();
                    for (var c = 0; c < rows[r].Cells.Count; c++)
                    {
                        var stars = Ui.FindAll(rows[r].Cells[c], e => e.ClassListContains(StarRating.StarClass) || e.ClassListContains(StarRating.StarClassNoMargin));
                        if (stars.Count == 0)
                            continue;
                        line.Append($" c{c}={TableView.ReadCell(rows[r].Cells[c])}[");
                        foreach (var st in stars)
                        {
                            var state = "?";
                            foreach (var cls in Ui.Classes(st))
                                if (cls.StartsWith("ability-", StringComparison.Ordinal))
                                    state = cls.Replace("ability-", "").Replace("-potential-level-", "/").Replace("-youth-false", "").Replace("-youth-true", "/y");
                            line.Append(state).Append(Ui.IsHidden(st) ? "(h)" : "").Append(' ');
                        }
                        line.Append(']');
                    }
                    if (line.Length > 0)
                        sb.AppendLine($"   stars row {r}:{line}");
                }
                if (rows.Count > 0)
                {
                    sb.AppendLine("   first row tree:");
                    Dump(sb, rows[0].Cells.Count > 0 ? rows[0].Cells[0].parent : view.Rows, 3, new int[1]);
                }
            }
            if (tables.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("No usable table. Element tree of the panels (bounded):");
                var budget = new int[1];
                foreach (var root in roots)
                    Dump(sb, root, 0, budget);
            }

            var folder = System.IO.Path.Combine(settings.OutputFolder(), "diagnostics");
            Directory.CreateDirectory(folder);
            var file = System.IO.Path.Combine(folder, ExportNames.BaseName("diagnostics", DateTime.Now) + ".txt");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            return file;
        }

        internal static string Path(VisualElement e)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var x = e; x != null && parts.Count < 16; x = x.parent)
                if (!string.IsNullOrEmpty(x.name))
                    parts.Add("#" + x.name);
            parts.Reverse();
            return string.Join(" > ", parts);
        }

        private static void Dump(StringBuilder sb, VisualElement e, int depth, int[] count)
        {
            if (count[0]++ > MaxElements || depth > 60)
                return;
            var text = Ui.AsText(e)?.text;
            var classes = string.Join(" .", Ui.Classes(e));
            sb.Append(' ', depth * 2).Append(Ui.TypeName(e));
            if (!string.IsNullOrEmpty(e.name)) sb.Append(" #").Append(e.name);
            if (classes.Length > 0) sb.Append(" .").Append(classes);
            if (Ui.IsHidden(e)) sb.Append(" [hidden]");
            if (!string.IsNullOrEmpty(text)) sb.Append(" \"").Append(text!.Length > 80 ? text.Substring(0, 80) + "…" : text).Append('"');
            sb.AppendLine();
            for (var i = 0; i < e.childCount; i++)
                Dump(sb, e[i], depth + 1, count);
        }
    }
}

using System;
using System.Collections.Generic;
using FM27Exporter.Core;
using UnityEngine.UIElements;

namespace FM27Exporter
{
    internal sealed class TableView
    {
        public const string TableClass = "SI.Bindable.StreamedTable";
        public const string HeadersName = "column-headers";
        public const string RowClass = "streamed-table__row";
        public const string CellClass = "streamed-table__cell";

        private TableView(VisualElement table, VisualElement headers, ScrollView rows)
        {
            Table = table;
            Headers = headers;
            Rows = rows;
        }

        public VisualElement Table { get; }
        public VisualElement Headers { get; }
        public ScrollView Rows { get; }

        public int? ItemCount
        {
            get
            {
                try { return Ui.InvokeInt(Table, "get_ItemCount"); }
                catch (Exception ex) { Plugin.Logger.LogDebug($"ItemCount: {ex.Message}"); return null; }
            }
        }

        public List<int>? SelectedIndices()
        {
            try
            {
                var list = Ui.InvokePtr(Table.Pointer, "get_ListElement");
                var selection = Ui.InvokePtr(list, "get_Selection");
                var count = Ui.UnboxInt(Ui.InvokePtr(selection, "get_Count"));
                if (count == null)
                    return null;
                var result = new List<int>(count.Value);
                for (var i = 0; i < count.Value; i++)
                {
                    var index = Ui.UnboxInt(Ui.InvokePtr(selection, "get_Item", i));
                    if (index == null)
                        return null;
                    result.Add(index.Value);
                }
                return result;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Selection not readable: {ex.Message}");
                return null;
            }
        }

        public bool IsPaginated
        {
            get
            {
                try { return Ui.InvokeBool(Table, "get_IsPaginated") ?? false; }
                catch (Exception ex) { Plugin.Logger.LogDebug($"IsPaginated: {ex.Message}"); return false; }
            }
        }

        public static List<TableView> FindAll(out List<string> notes)
        {
            notes = new List<string>();
            var found = new List<(TableView view, float area)>();
            foreach (var root in Ui.PanelRoots())
            {
                foreach (var table in Ui.FindAll(root, e => Ui.TypeName(e) == TableClass, prune: Ui.IsHidden))
                {
                    if (Ui.IsHidden(table))
                        continue;
                    var area = Ui.Area(table);
                    if (area < 1f)
                        continue;
                    var headers = Ui.FindFirst(table, e => e.name == HeadersName);
                    var rows = RowScrollView(table);
                    if (headers == null || rows == null)
                    {
                        notes.Add($"StreamedTable '{table.name}' without {(headers == null ? "#column-headers" : "row list")}");
                        continue;
                    }
                    found.Add((new TableView(table, headers, rows), area));
                }
            }
            found.Sort((a, b) => b.area.CompareTo(a.area));
            var result = new List<TableView>(found.Count);
            foreach (var f in found)
                result.Add(f.view);
            return result;
        }

        public static TableView? UnderPointer(List<TableView> tables)
        {
            try
            {
                if (!Pointer.TryGetClientPosition(out var x, out var y, out var clientWidth))
                    return null;
                TableView? best = null;
                var bestArea = float.MaxValue;
                var picks = new Dictionary<IntPtr, VisualElement?>();
                foreach (var t in tables)
                {
                    var panel = t.Table.panel;
                    if (panel == null)
                        continue;
                    var root = panel.visualTree;
                    var rootWidth = root?.worldBound.width ?? 0f;
                    var scale = rootWidth > 1f && clientWidth > 0 ? clientWidth / rootWidth : 1f;
                    var p = new UnityEngine.Vector2(x / scale, y / scale);
                    var key = root?.Pointer ?? IntPtr.Zero;
                    if (!picks.TryGetValue(key, out var picked))
                    {
                        picks[key] = picked = panel.Pick(p);
                        Plugin.Logger.LogInfo($"Under the cursor ({x},{y} → {p.x:0},{p.y:0}, panel {rootWidth:0} wide): " +
                                              (picked == null ? "nothing" : $"{Ui.TypeName(picked)} {Diagnostics.Path(picked)}"));
                    }
                    var area = Ui.Area(t.Table);
                    if (picked != null && Ui.IsDescendant(picked, t.Table) && area < bestArea)
                    {
                        best = t;
                        bestArea = area;
                    }
                }
                return best;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"UnderPointer: {ex.Message}");
            }
            return null;
        }

        private static ScrollView? RowScrollView(VisualElement table)
        {
            try
            {
                var ptr = Ui.Invoke(table, "get_ScrollViewToScroll");
                if (ptr != IntPtr.Zero)
                {
                    var sv = new ScrollView(ptr);
                    if (Ui.IsDescendant(sv, table) && Ui.FindFirst(sv, x => x.ClassListContains(RowClass)) != null)
                        return sv;
                    Plugin.Logger.LogDebug("ScrollViewToScroll is not this table's row list; searching.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"ScrollViewToScroll: {ex.Message}");
            }
            foreach (var e in Ui.FindAll(table, x => x.TryCast<ScrollView>() != null))
            {
                var sv = e.TryCast<ScrollView>()!;
                if (Ui.FindFirst(sv, x => x.ClassListContains(RowClass)) != null)
                    return sv;
            }
            return null;
        }

        public string ScreenName()
        {
            if ((Table.name ?? string.Empty).IndexOf("shortlist", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Shortlist";
            var names = new List<string?>();
            for (var e = Table.parent; e != null; e = e.parent)
                names.Add(e.name);
            return ExportNames.GuessScreen(names);
        }

        public sealed class Column
        {
            public Column(int index, string header, bool hidden)
            {
                Index = index;
                Header = header;
                Hidden = hidden;
            }

            public int Index { get; }
            public string Header { get; }
            public bool Hidden { get; }
        }

        public List<Column> ReadColumns()
        {
            var columns = new List<Column>();
            for (var i = 0; i < Headers.childCount; i++)
            {
                var pane = Headers[i];
                columns.Add(new Column(i, CellText.Join(Ui.Texts(pane)), Ui.IsHidden(pane)));
            }
            return columns;
        }

        public sealed class VisibleRow
        {
            public VisibleRow(float contentY, float height, List<VisualElement> cells, bool selected)
            {
                ContentY = contentY;
                Height = height;
                Cells = cells;
                Selected = selected;
            }

            public const string SelectedClass = "virtualised-list__item--selected";
            public bool Selected { get; }

            public float ContentY { get; }
            public float Height { get; }
            public List<VisualElement> Cells { get; }
        }

        public List<VisibleRow> VisibleRows()
        {
            var viewportTop = Rows.contentViewport.worldBound.y;
            var offset = Rows.scrollOffset.y;
            var rows = new List<VisibleRow>();
            foreach (var row in Ui.FindAll(Rows.contentContainer, e => e.ClassListContains(RowClass), prune: Ui.IsHidden))
            {
                if (Ui.IsHidden(row))
                    continue;
                var cells = Ui.FindAll(row, e => e.ClassListContains(CellClass));
                if (cells.Count == 0)
                    continue;
                var b = row.worldBound;
                var selected = false;
                for (var x = row; x != null && x.Pointer != Rows.contentContainer.Pointer; x = x.parent)
                    if (x.ClassListContains(VisibleRow.SelectedClass)) { selected = true; break; }
                rows.Add(new VisibleRow(b.y - viewportTop + offset, b.height, cells, selected));
            }
            rows.Sort((a, b) => a.ContentY.CompareTo(b.ContentY));
            return rows;
        }

        public static string ReadCell(VisualElement cell)
        {
            var texts = new List<string>();
            var icons = new List<string>();
            var stars = 0;
            var low = 0.0;
            var high = 0.0;
            Ui.Walk(cell, e =>
            {
                if (Ui.IsHidden(e))
                    return false;
                var text = Ui.AsText(e)?.text;
                if (!string.IsNullOrEmpty(text))
                {
                    texts.Add(text!);
                    return false; // a label's children are its own parts (e.g. inline images)
                }
                if (e.ClassListContains(StarRating.StarClass) || e.ClassListContains(StarRating.StarClassNoMargin))
                {
                    stars++;
                    var classes = Ui.Classes(e);
                    low += StarRating.StarValue(classes, false) ?? 0.0;
                    high += StarRating.StarValue(classes, true) ?? 0.0;
                    return false;
                }
                if (e.ClassListContains(Gauge.StepperClass))
                {
                    var steps = new List<IReadOnlyCollection<string>>();
                    for (var i = 0; i < e.childCount; i++)
                        steps.Add(Ui.Classes(e[i]));
                    var gauge = Gauge.Value(steps);
                    if (gauge != null)
                        icons.Add(gauge);
                    return false;
                }
                var icon = IconLevel.Value(Ui.Classes(e));
                if (icon != null)
                    icons.Add(icon);
                return true;
            });
            var value = CellText.Join(texts);
            if (value.Length == 0 && stars > 0)
                value = StarRating.Format(low, high);
            if (value.Length == 0 && icons.Count > 0)
                value = string.Join(" ", icons);
            return value;
        }
    }
}

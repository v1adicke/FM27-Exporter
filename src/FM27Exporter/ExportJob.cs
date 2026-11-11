using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using FM27Exporter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27Exporter
{
    internal sealed class ExportJob
    {
        private readonly Settings _settings;
        private readonly TableView _view;
        private readonly List<TableView.Column> _columns;
        private readonly ScrollCollector _collector;
        private readonly Vector2 _originalOffset;
        private readonly FrameRate? _frameRate;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly int? _expected;
        private readonly bool _paginated;

        public string Status { get; private set; }
        public bool Finished { get; private set; }
        public bool Succeeded { get; private set; }

        private ExportJob(Settings settings, TableView view, List<TableView.Column> columns)
        {
            _settings = settings;
            _view = view;
            _columns = columns;
            _originalOffset = view.Rows.scrollOffset;
            _expected = view.ItemCount;
            var options = new ScrollCollectorOptions
            {
                StableFrames = settings.StableFrames,
                MaxSettleFrames = settings.MaxSettleFrames,
                MaxRows = settings.MaxRows,
                MaxSettleSeconds = settings.MaxSettleSeconds,
            };
            List<int>? targets = null;
            if (settings.SelectedRowsMinimum > 0)
            {
                var selection = view.SelectedIndices();
                if (selection != null && selection.Count >= settings.SelectedRowsMinimum)
                    targets = selection;
                Plugin.Logger.LogInfo($"Selected in the game: {selection?.Count.ToString() ?? "unknown"} row(s)" +
                                      (targets != null ? $" → exporting only those (indices {string.Join(",", targets.Take(20))}{(targets.Count > 20 ? ",…" : "")})." : "."));
            }
            _collector = new ScrollCollector(new GameList(view), columns.Count, CellMapping, _expected, options,
                m => Plugin.Logger.LogInfo(m), targets: targets);
            _frameRate = settings.UnlockFrameRate ? FrameRate.Unlock() : null;
            _paginated = view.IsPaginated;
            if (_paginated)
                Plugin.Logger.LogWarning("This list is paginated: only the page on screen is exported.");
            Status = "Exporting… (press the export key again to cancel)";
        }

        public static ExportJob? Start(Settings settings, out string message)
        {
            var tables = TableView.FindAll(out var notes);
            foreach (var n in notes)
                Plugin.Logger.LogInfo(n);
            if (tables.Count == 0)
            {
                message = "No table on screen. Open a squad, search, shortlist or any other list and try again.";
                return null;
            }
            var picked = TableView.UnderPointer(tables);
            var view = picked ?? tables[0];
            Plugin.Logger.LogInfo(picked != null ? "Table chosen: the one under the mouse cursor." : "Table chosen: the biggest on screen (none under the cursor).");
            var columns = view.ReadColumns();
            if (columns.Count == 0)
            {
                message = "The table has no columns yet — wait until it has loaded.";
                return null;
            }
            Plugin.Logger.LogInfo($"Exporting '{view.ScreenName()}' ({tables.Count} table(s) on screen): " +
                                  $"{columns.Count} columns, {view.ItemCount?.ToString() ?? "?"} rows.");
            Plugin.Logger.LogInfo($"Table path: {Diagnostics.Path(view.Table)}");
            message = string.Empty;
            return new ExportJob(settings, view, columns);
        }

        public void Cancel()
        {
            if (Finished)
                return;
            Restore();
            Status = $"Export cancelled after {_collector.Count} rows.";
            Plugin.Logger.LogInfo(Status);
            Finished = true;
        }

        public void Tick()
        {
            if (Finished)
                return;
            try
            {
                if (_view.Table.panel == null)
                {
                    Abort("Export stopped: the table was closed.");
                    return;
                }
                var now = _view.ItemCount;
                if (_expected.HasValue && now.HasValue && now.Value != _expected.Value)
                {
                    Abort($"Export stopped: the list changed while exporting ({_expected} → {now} rows). Try again.");
                    return;
                }
                _collector.Tick();
                if (_collector.Targets is { } wanted)
                    Status = $"Exporting the {wanted.Count} selected rows… {wanted.Count(_collector.Rows.Has)}/{wanted.Count} " +
                             "(press the export key again to cancel)";
                else
                {
                    var total = _collector.Expected.HasValue ? $"/{_collector.Expected}" : string.Empty;
                    Status = $"Exporting… {_collector.Count}{total} rows (press the export key again to cancel)";
                }
                if (_collector.Finished)
                    Finish();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Export failed: {ex}");
                Restore();
                Status = "Export failed: " + ex.Message + " (see BepInEx/LogOutput.log)";
                Finished = true;
            }
        }

        private void Abort(string message)
        {
            Restore();
            Status = message;
            Plugin.Logger.LogWarning(message);
            Finished = true;
        }

        private int[] CellMapping(int cellsPerRow)
        {
            var map = new int[_columns.Count];
            var visible = 0;
            foreach (var c in _columns)
                if (!c.Hidden)
                    visible++;
            var byVisible = cellsPerRow != _columns.Count && cellsPerRow == visible;
            var v = 0;
            for (var c = 0; c < _columns.Count; c++)
                map[c] = byVisible ? (_columns[c].Hidden ? -1 : v++) : _columns[c].Index;
            if (cellsPerRow != _columns.Count)
                Plugin.Logger.LogWarning($"Rows have {cellsPerRow} cells for {_columns.Count} header panes ({visible} visible); " +
                                         (byVisible ? "mapping by visible position." : "mapping by position, extra cells ignored."));
            return map;
        }

        private void Finish()
        {
            Restore();
            Finished = true;
            var expected = _paginated ? null : _collector.Expected; // ItemCount may count every page
            var targets = _collector.Targets;
            var missing = targets != null
                ? targets.Where(t => !_collector.Rows.Has(t)).ToList()
                : _collector.Rows.MissingIndices(expected ?? -1);
            var headers = new List<string>();
            var keep = new List<int>();
            for (var c = 0; c < _columns.Count; c++)
            {
                if (_columns[c].Hidden)
                    continue;
                keep.Add(c);
                headers.Add(_columns[c].Header);
            }
            var highlighted = _collector.SelectedIndices;
            var onlySelected = targets != null
                || (_settings.SelectedRowsMinimum > 0 && highlighted.Count >= _settings.SelectedRowsMinimum);
            var rows = new List<IReadOnlyList<string>>();
            foreach (var r in _collector.Rows.Rows(targets ?? (onlySelected ? highlighted : null)))
            {
                var cells = new string[keep.Count];
                for (var i = 0; i < keep.Count; i++)
                    cells[i] = r[keep[i]];
                rows.Add(cells);
            }
            var screen = _view.ScreenName();
            var table = new ExportTable(screen, headers, rows);
            if (_settings.DropEmptyColumns)
                table = table.WithoutEmptyColumns();

            var files = Write(table, screen);
            var secs = _clock.Elapsed.TotalSeconds;
            var notes = missing.Count > 0 ? $" WARNING: {missing.Count} row(s) could not be read." : string.Empty;
            if (_paginated)
                notes += " The list is paginated: only the page on screen was exported.";
            if (_collector.UnsettledPages > 0)
                notes += $" {_collector.UnsettledPages} scroll step(s) were read before the rows had settled — check the values.";
            if (_collector.EndReason == "max rows")
                notes += $" Stopped at Export.MaxRows = {_settings.MaxRows}.";
            var hidden = _columns.Count - keep.Count;
            if (hidden > 0)
            {
                notes += $" {hidden} column(s) hidden by the game for lack of width were left out — widen the window to include them.";
                Plugin.Logger.LogWarning($"{hidden} hidden column(s) not exported.");
            }
            if (targets != null)
            {
                var unmarked = targets.Where(t => _collector.Rows.Has(t) && !highlighted.Contains(t)).ToList();
                if (unmarked.Count > 0)
                {
                    notes += $" WARNING: {unmarked.Count} exported row(s) did not look selected on screen — check the file.";
                    Plugin.Logger.LogWarning($"Selection indices not highlighted on screen (first 20): {string.Join(",", unmarked.Take(20))}");
                }
            }
            var count = onlySelected
                ? $"{table.Rows.Count} selected rows (of {expected?.ToString() ?? _collector.Count.ToString()})"
                : expected.HasValue && expected.Value != table.Rows.Count
                    ? $"{table.Rows.Count} of {expected} rows" : $"{table.Rows.Count} rows";
            Status = $"Exported {count} × {table.ColumnCount} columns in {secs:0.0}s → {files}{notes}";
            Plugin.Logger.LogInfo(Status);
            if (missing.Count > 0)
                Plugin.Logger.LogWarning($"Missing row indices (first 20): {string.Join(",", missing.GetRange(0, Math.Min(20, missing.Count)))}");
            Succeeded = true;
        }

        private string Write(ExportTable table, string screen)
        {
            var folder = _settings.OutputFolder();
            Directory.CreateDirectory(folder);
            var formats = _settings.Formats;
            var exts = new List<string>();
            foreach (var f in new[] { "html", "rtf", "csv" })
                if (formats.Contains(f))
                    exts.Add("." + f);
            var name = ExportNames.BaseName(screen, DateTime.Now, _settings.Timestamp);
            name = ExportNames.Unique(folder, name, exts);
            var basePath = Path.Combine(folder, name);
            if (formats.Contains("html")) Fm24HtmlWriter.Write(table, basePath + ".html");
            if (formats.Contains("rtf")) Fm24TextWriter.Write(table, basePath + ".rtf");
            if (formats.Contains("csv")) CsvWriter.Write(table, basePath + ".csv", _settings.CsvDelimiter);
            return basePath + "." + string.Join("/", exts).Replace(".", string.Empty);
        }

        private void Restore()
        {
            try { _view.Rows.scrollOffset = _originalOffset; }
            catch (Exception ex) { Plugin.Logger.LogWarning($"Could not restore scroll position: {ex.Message}"); }
            _frameRate?.Restore();
        }

        private sealed class GameList : IScrollList
        {
            private readonly TableView _view;

            public GameList(TableView view) => _view = view;

            public float ScrollY => _view.Rows.scrollOffset.y;
            public float ViewportHeight => _view.Rows.contentViewport.layout.height;
            public float ContentHeight => _view.Rows.contentContainer.layout.height;

            public float MaxScroll
            {
                get
                {
                    var scroll = _view.Rows;
                    var max = scroll.contentContainer.layout.height - scroll.contentViewport.layout.height;
                    try
                    {
                        var high = scroll.verticalScroller.highValue;
                        if (!float.IsNaN(high) && high > max)
                            max = high;
                    }
                    catch
                    {
                    }
                    return float.IsNaN(max) ? 0f : Math.Max(0f, max);
                }
            }

            public void ScrollTo(float y)
            {
                var current = _view.Rows.scrollOffset;
                _view.Rows.scrollOffset = new Vector2(current.x, y);
            }

            public IReadOnlyList<IVisibleRow> VisibleRows()
            {
                var rows = _view.VisibleRows();
                var result = new List<IVisibleRow>(rows.Count);
                foreach (var r in rows)
                    result.Add(new Row(r));
                return result;
            }

            private sealed class Row : IVisibleRow
            {
                private readonly TableView.VisibleRow _row;
                private readonly string?[] _texts;

                public Row(TableView.VisibleRow row)
                {
                    _row = row;
                    _texts = new string?[row.Cells.Count];
                }

                public float ContentY => _row.ContentY;
                public float Height => _row.Height;
                public int CellCount => _row.Cells.Count;
                public string Cell(int index) => _texts[index] ??= TableView.ReadCell(_row.Cells[index]);
                public bool Selected => _row.Selected;
            }
        }
    }

    internal sealed class FrameRate
    {
        private readonly int _target;
        private bool _restored;

        private FrameRate(int target) => _target = target;

        public static FrameRate? Unlock()
        {
            try
            {
                var target = Application.targetFrameRate;
                if (target <= 0 || target >= 60)
                    return null;
                var saved = new FrameRate(target);
                Application.targetFrameRate = 60;
                return saved;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Frame rate not unlocked: {ex.Message}");
                return null;
            }
        }

        public void Restore()
        {
            if (_restored)
                return;
            _restored = true;
            try
            {
                Application.targetFrameRate = _target;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Frame rate not restored: {ex.Message}");
            }
        }
    }
}

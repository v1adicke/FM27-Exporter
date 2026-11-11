using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FM27Exporter.Core
{
    public interface IScrollList
    {
        float ScrollY { get; }
        float MaxScroll { get; }
        float ViewportHeight { get; }
        float ContentHeight { get; }
        void ScrollTo(float y);

        IReadOnlyList<IVisibleRow> VisibleRows();
    }

    public interface IVisibleRow
    {
        float ContentY { get; }
        float Height { get; }
        int CellCount { get; }
        string Cell(int index);
        bool Selected => false;
    }

    public sealed class ScrollCollectorOptions
    {
        public int StableFrames { get; set; } = 3;
        public int MaxSettleFrames { get; set; } = 90;

        public double MaxSettleSeconds { get; set; } = 2.0;

        public int MaxRetries { get; set; } = 3;
        public int MaxRows { get; set; } = 25000;
        public int MaxSteps { get; set; } = 20000;
        public float PageFraction { get; set; } = 0.9f;
    }

    public sealed class ScrollCollector
    {
        private readonly IScrollList _list;
        private readonly int _columns;
        private readonly Func<int, int[]> _cellMapping;
        private readonly ScrollCollectorOptions _options;
        private readonly Action<string>? _log;
        private readonly Func<double> _clock;
        private readonly RowCollector _rows;
        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly Dictionary<string, int> _waits = new Dictionary<string, int>();

        private int[]? _cellOf;
        private List<(float y, int index)> _previousPage = new List<(float, int)>();
        private float _previousOffset;
        private string? _lastFingerprint;
        private int _stableFor, _settleFrames, _stuckSteps, _retries;
        private double _settleStart;
        private float _lastOffsetY = float.NaN;
        private bool _started;
        private readonly SortedSet<int>? _targets;
        private bool _exactPitch, _jumped;
        private float _originY;
        private readonly HashSet<int> _revisited = new HashSet<int>();

        public ScrollCollector(IScrollList list, int columns, Func<int, int[]> cellMapping, int? expected,
            ScrollCollectorOptions? options = null, Action<string>? log = null, Func<double>? clock = null,
            IEnumerable<int>? targets = null)
        {
            if (targets != null)
                _targets = new SortedSet<int>(targets.Where(t => t >= 0));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _columns = columns;
            _cellMapping = cellMapping ?? throw new ArgumentNullException(nameof(cellMapping));
            Expected = expected > 0 ? expected : null;
            _options = options ?? new ScrollCollectorOptions();
            _log = log;
            if (clock == null)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                clock = () => sw.Elapsed.TotalSeconds;
            }
            _clock = clock;
            _rows = new RowCollector(columns);
        }

        public int? Expected { get; }
        public bool Finished { get; private set; }
        public int Steps { get; private set; }
        public int Frames { get; private set; }
        public float Pitch { get; private set; }
        public int Count => _rows.Count;
        public RowCollector Rows => _rows;
        public IReadOnlyCollection<int> SelectedIndices => _selected;
        public IReadOnlyCollection<int>? Targets => _targets;
        public int Jumps { get; private set; }

        public int UnsettledPages { get; private set; }

        public string EndReason { get; private set; } = string.Empty;

        public void Tick()
        {
            if (Finished)
                return;
            Frames++;
            if (!_started)
            {
                _started = true;
                Scroll(0f);
                return;
            }

            _settleFrames++;
            var rows = Sorted(_list.VisibleRows());
            var fingerprint = Fingerprint(rows);
            if (fingerprint == _lastFingerprint)
                _stableFor++;
            else
            {
                _stableFor = 0;
                _lastFingerprint = fingerprint;
            }

            var why = _stableFor < _options.StableFrames ? "changing"
                : AnyBlank(rows) ? "blank rows"
                : rows.Count == 0 ? (StillLoading() ? "loading" : null)
                : !Covers(rows) ? "rows not at scroll position"
                : null;
            if (why != null)
            {
                _waits[why] = _waits.TryGetValue(why, out var n) ? n + 1 : 1;
                var timedOut = _settleFrames >= _options.MaxSettleFrames && _clock() - _settleStart >= _options.MaxSettleSeconds;
                if (!timedOut)
                    return;
                UnsettledPages++;
                _log?.Invoke($"Page at {_list.ScrollY:0} read before it settled ({why}).");
            }

            if (!Collect(rows, allowGap: _retries >= _options.MaxRetries))
            {
                _retries++;
                _log?.Invoke($"Page at {_list.ScrollY:0} does not overlap the previous page (at {_previousOffset:0}); retry {_retries}.");
                Scroll(Math.Min(Math.Max(0f, _list.MaxScroll), _previousOffset + _list.ViewportHeight * 0.45f));
                return;
            }
            _retries = 0;
            _jumped = false;
            _previousOffset = _list.ScrollY;
            Steps++;

            var offsetY = _list.ScrollY;
            var maxY = _list.MaxScroll;
            var viewport = _list.ViewportHeight;
            if (float.IsNaN(offsetY) || float.IsNaN(maxY) || float.IsNaN(viewport) || float.IsInfinity(maxY)) { End("no layout"); return; }
            maxY = Math.Max(0f, maxY);
            _stuckSteps = Math.Abs(offsetY - _lastOffsetY) < 0.5f ? _stuckSteps + 1 : 0;
            _lastOffsetY = offsetY;

            if (rows.Count == 0 && offsetY <= 0.5f) { End("empty"); return; }
            if (_targets != null && _targets.All(_rows.Has)) { End("selected rows"); return; }
            if (Expected.HasValue && _rows.Count >= Expected.Value) { End("all rows"); return; }
            if (_rows.Count >= _options.MaxRows) { End("max rows"); return; }
            if (Steps >= _options.MaxSteps) { End("max steps"); return; }
            if (offsetY >= maxY - 0.5f) { End("end"); return; }
            if (_stuckSteps >= 2) { End("stuck"); return; }

            var page = Math.Max(Pitch, viewport * _options.PageFraction);
            if (_targets != null && _exactPitch)
            {
                var wanted = _targets.Where(t => !_rows.Has(t)).DefaultIfEmpty(-1).First();
                if (wanted >= 0)
                {
                    var y = _originY + wanted * Pitch;
                    var ahead = y > offsetY + viewport + page;           // further than the next page
                    var behind = y < offsetY && _revisited.Add(wanted);  // passed without reading it: go back once
                    if (ahead || behind)
                    {
                        _jumped = true;
                        Jumps++;
                        Scroll(Math.Min(maxY, Math.Max(0f, y - Pitch)));
                        return;
                    }
                }
            }
            Scroll(Math.Min(maxY, offsetY + Math.Max(1f, page)));
        }

        private void End(string reason)
        {
            EndReason = reason;
            Finished = true;
            _log?.Invoke($"Scroll collection ended ({reason}) after {Steps} steps / {Frames} frames: {_rows.Count} rows" +
                         (UnsettledPages > 0 ? $", {UnsettledPages} page(s) read before they settled." : ".") +
                         (_waits.Count > 0 ? " Frames waited: " + string.Join(", ", _waits.Select(kv => $"{kv.Key} {kv.Value}")) + "." : string.Empty));
        }

        private void Scroll(float y)
        {
            _list.ScrollTo(y);
            _settleFrames = 0;
            _stableFor = 0;
            _lastFingerprint = null;
            _settleStart = _clock();
        }

        private bool StillLoading() => Expected.HasValue || _list.ContentHeight > 4f;

        private bool Covers(List<IVisibleRow> rows)
        {
            var top = _list.ScrollY;
            var viewport = _list.ViewportHeight;
            var bottom = Math.Min(top + viewport, _list.ContentHeight);
            var pitch = Pitch > 0f ? Pitch : MeasuredPitch(rows);
            var slack = Math.Max(1.5f * pitch, 0.25f * viewport);
            var first = rows[0];
            var last = rows[rows.Count - 1];
            if (first.ContentY > top + slack)
                return false;
            if (Expected is int n && rows.Count >= n)
                return true;
            return last.ContentY + last.Height >= bottom - slack;
        }

        private bool Collect(List<IVisibleRow> rows, bool allowGap)
        {
            if (rows.Count == 0)
                return true;
            if (Pitch <= 0f)
            {
                Pitch = InitialPitch(rows);
                _originY = rows[0].ContentY;
                if (_list.ScrollY > 0.5f)
                    _log?.Invoke($"The list did not scroll to the top (offset {_list.ScrollY:0}); the first rows may be missing.");
            }
            _cellOf ??= _cellMapping(rows[0].CellCount);

            if (!TryAnchor(rows, out var anchorY, out var anchorIndex))
            {
                if (!allowGap)
                    return false;
                var last = _previousPage[_previousPage.Count - 1];
                (anchorY, anchorIndex) = last;
                _log?.Invoke("No overlapping row between pages after retries; numbering by distance (rows may be missing).");
            }
            var page = new List<(float, int)>(rows.Count);
            foreach (var row in rows)
            {
                var index = anchorIndex + (int)Math.Round((row.ContentY - anchorY) / Pitch, MidpointRounding.AwayFromZero);
                var cells = new string[_columns];
                for (var c = 0; c < _columns; c++)
                {
                    var i = c < _cellOf.Length ? _cellOf[c] : -1;
                    cells[c] = i >= 0 && i < row.CellCount ? row.Cell(i) ?? string.Empty : string.Empty;
                }
                _rows.Add(index, cells);
                if (row.Selected)
                    _selected.Add(index);
                else
                    _selected.Remove(index);
                page.Add((row.ContentY, index));
            }
            _previousPage = page;
            return true;
        }

        private bool TryAnchor(List<IVisibleRow> rows, out float y, out int index)
        {
            if (_previousPage.Count == 0)
            {
                (y, index) = (rows[0].ContentY, 0); // first page, scrolled to the top: the topmost row is row 0
                return true;
            }
            if (_jumped)
            {
                (y, index) = (_originY, 0); // after a jump: number rows from the list top
                return true;
            }
            var tolerance = Pitch * 0.3f;
            foreach (var row in rows)
            {
                foreach (var (py, pi) in _previousPage)
                {
                    if (Math.Abs(row.ContentY - py) <= tolerance)
                    {
                        (y, index) = (py, pi);
                        return true;
                    }
                }
            }
            (y, index) = (0f, 0);
            return false;
        }

        private float InitialPitch(List<IVisibleRow> rows)
        {
            var measured = MeasuredPitch(rows);
            if (Expected is int n && n > 0)
            {
                var exact = _list.ContentHeight / n;
                if (!float.IsNaN(exact) && exact > 1f && Math.Abs(exact - measured) <= 0.02f * measured)
                {
                    _exactPitch = true;
                    return exact;
                }
                _log?.Invoke($"Row pitch: measured {measured:0.###}, content/ItemCount {exact:0.###} — using the measurement.");
            }
            return measured;
        }

        public static float MeasuredPitch(IReadOnlyList<IVisibleRow> sortedRows)
        {
            var gaps = new List<float>();
            for (var i = 1; i < sortedRows.Count; i++)
            {
                var d = sortedRows[i].ContentY - sortedRows[i - 1].ContentY;
                if (d > 1f)
                    gaps.Add(d);
            }
            if (gaps.Count == 0)
                return Math.Max(1f, sortedRows[0].Height);
            gaps.Sort();
            var median = gaps[gaps.Count / 2];
            float sum = 0f;
            var n = 0;
            foreach (var g in gaps)
            {
                if (g < median * 0.5f || g > median * 1.5f)
                    continue;
                sum += g;
                n++;
            }
            return sum / n;
        }

        private bool AnyBlank(List<IVisibleRow> rows)
        {
            foreach (var row in rows)
            {
                var blank = true;
                if (_cellOf != null)
                {
                    foreach (var i in _cellOf)
                        if (i >= 0 && i < row.CellCount && !string.IsNullOrEmpty(row.Cell(i))) { blank = false; break; }
                }
                else
                {
                    for (var i = 0; i < row.CellCount; i++)
                        if (!string.IsNullOrEmpty(row.Cell(i))) { blank = false; break; }
                }
                if (blank)
                    return true;
            }
            return false;
        }

        private static List<IVisibleRow> Sorted(IReadOnlyList<IVisibleRow> rows)
        {
            var list = new List<IVisibleRow>(rows);
            list.Sort((a, b) => a.ContentY.CompareTo(b.ContentY));
            return list;
        }

        private static string Fingerprint(List<IVisibleRow> rows)
        {
            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                sb.Append((int)Math.Round(r.ContentY)).Append(':');
                for (var i = 0; i < r.CellCount; i++)
                    sb.Append(r.Cell(i)).Append('\u001f');
                sb.Append('\u001e');
            }
            return sb.ToString();
        }
    }
}

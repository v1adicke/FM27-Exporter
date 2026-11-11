using System;
using System.Collections.Generic;
using System.Linq;
using FM27Exporter.Core;
using Xunit;

namespace FM27Exporter.Tests
{
    internal sealed class FakeList : IScrollList
    {
        private readonly string[][] _items;
        private readonly float _rowHeight, _viewport, _topPadding;
        private readonly int _overprovision, _fillDelay;
        private readonly bool _snapToPixels, _ignoreScroll, _nanViewport;
        private readonly int _scrollDelay, _loadDelay, _cellStagger;
        private int _overshoots;
        private int _pendingSince;
        private readonly Dictionary<int, int> _boundAt = new Dictionary<int, int>();
        private float _scrollY, _layoutY, _pendingY = float.NaN;
        private int _frame;
        private readonly HashSet<int> _selected;
        private readonly float _bottomPadding;

        public FakeList(int count, int columns = 3, float rowHeight = 24f, float viewport = 500f, int overprovision = 2,
            int fillDelay = 4, bool snapToPixels = false, float topPadding = 0f, bool ignoreScroll = false, float startY = 0f,
            int scrollDelay = 0, int overshoots = 0, int loadDelay = 0, int cellStagger = 0, bool nanViewport = false,
            IEnumerable<int>? selected = null, float bottomPadding = 0f)
        {
            _bottomPadding = bottomPadding;
            _selected = new HashSet<int>(selected ?? Enumerable.Empty<int>());
            _scrollDelay = scrollDelay;
            _overshoots = overshoots;
            _loadDelay = loadDelay;
            _cellStagger = cellStagger;
            _nanViewport = nanViewport;
            _items = Enumerable.Range(0, count)
                .Select(i => new[] { "" }.Concat(Enumerable.Range(0, columns).Select(c => $"r{i}c{c}")).ToArray())
                .ToArray();
            _rowHeight = rowHeight;
            _viewport = viewport;
            _overprovision = overprovision;
            _fillDelay = fillDelay;
            _snapToPixels = snapToPixels;
            _topPadding = topPadding;
            _ignoreScroll = ignoreScroll;
            _scrollY = _layoutY = Math.Min(startY, MaxScroll);
        }

        public int ScrollCalls { get; private set; }
        public float ScrollY => _scrollY;
        public float ContentHeight => _topPadding + _items.Length * _rowHeight + _bottomPadding;
        public float MaxScroll => Math.Max(0f, ContentHeight - _viewport);
        public float ViewportHeight => _nanViewport ? float.NaN : _viewport;

        public void ScrollTo(float y)
        {
            ScrollCalls++;
            if (_ignoreScroll)
                return;
            if (_overshoots > 0 && y > _scrollY)
            {
                _overshoots--;
                y = _scrollY + (y - _scrollY) * 1.8f; // e.g. a scroll animation that went too far
            }
            _scrollY = _pendingY = Math.Clamp(y, 0f, MaxScroll);
            _pendingSince = _frame;
        }

        public void Frame()
        {
            _frame++;
            if (!float.IsNaN(_pendingY) && _frame - _pendingSince > _scrollDelay)
            {
                _layoutY = _pendingY;
                _pendingY = float.NaN;
            }
            var (first, last) = Range();
            foreach (var i in _boundAt.Keys.ToList())
                if (i < first || i > last)
                    _boundAt.Remove(i); // element recycled
            for (var i = first; i <= last; i++)
                if (!_boundAt.ContainsKey(i))
                    _boundAt[i] = _frame;
        }

        private (int, int) Range()
        {
            if (_items.Length == 0)
                return (0, -1);
            var first = (int)Math.Floor((_layoutY - _topPadding) / _rowHeight) - _overprovision;
            var last = (int)Math.Ceiling((_layoutY + _viewport - _topPadding) / _rowHeight) + _overprovision;
            return (Math.Max(0, first), Math.Min(_items.Length - 1, last));
        }

        public IReadOnlyList<IVisibleRow> VisibleRows()
        {
            var rows = new List<IVisibleRow>();
            if (_frame < _loadDelay)
                return rows; // list not populated yet
            foreach (var (index, bound) in _boundAt.OrderByDescending(kv => kv.Key).Select(kv => (kv.Key, kv.Value)))
            {
                var y = _topPadding + index * _rowHeight;
                if (_snapToPixels)
                    y = (float)Math.Round(y - _layoutY) + (float)Math.Round(_layoutY); // world-space snapping
                var age = _frame - bound;
                var cells = new string[_items[index].Length];
                for (var c = 0; c < cells.Length; c++)
                    if (age >= _fillDelay + c * _cellStagger) // with a stagger, cells fill one after another
                        cells[c] = _items[index][c];
                rows.Add(new Row(y, _rowHeight, cells, _selected.Contains(index)));
            }
            return rows;
        }

        private sealed class Row : IVisibleRow
        {
            private readonly string[] _cells;
            public Row(float y, float h, string[] cells, bool selected) { ContentY = y; Height = h; _cells = cells; Selected = selected; }
            public bool Selected { get; }
            public float ContentY { get; }
            public float Height { get; }
            public int CellCount => _cells.Length;
            public string Cell(int index) => _cells[index] ?? string.Empty;
        }
    }

    public class ScrollCollectorTests
    {
        private static int[] SkipSelection(int cellsPerRow) => Enumerable.Range(1, cellsPerRow - 1).ToArray();

        private static ScrollCollector Run(FakeList list, int columns, int? expected, ScrollCollectorOptions? options = null, int maxFrames = 200000,
            IEnumerable<int>? targets = null)
        {
            var log = new List<string>();
            var frame = 0;
            var c = new ScrollCollector(list, columns, SkipSelection, expected, options, log.Add, () => frame / 60.0, targets);
            for (var f = 0; f < maxFrames && !c.Finished; f++, frame++)
            {
                list.Frame();
                c.Tick();
            }
            Assert.True(c.Finished, "collector did not finish");
            return c;
        }

        private static void AssertComplete(ScrollCollector c, int count, int columns)
        {
            Assert.Equal(count, c.Count);
            Assert.Empty(c.Rows.MissingIndices(count));
            var rows = c.Rows.Rows();
            for (var i = 0; i < count; i++)
                for (var col = 0; col < columns; col++)
                    Assert.Equal($"r{i}c{col}", rows[i][col]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Collects_every_row_of_a_long_list_in_order(bool itemCountKnown)
        {
            var list = new FakeList(1000);
            var c = Run(list, 3, itemCountKnown ? 1000 : (int?)null);
            AssertComplete(c, 1000, 3);
            Assert.Equal(itemCountKnown ? "all rows" : "end", c.EndReason);
        }

        [Fact]
        public void Short_list_that_fits_on_one_page()
        {
            var c = Run(new FakeList(5), 3, 5);
            AssertComplete(c, 5, 3);
            Assert.Equal(1, c.Steps);
        }

        [Fact]
        public void Empty_list_finishes()
        {
            var c = Run(new FakeList(0), 3, 0);
            Assert.Equal(0, c.Count);
            var d = Run(new FakeList(0), 3, null);
            Assert.Equal(0, d.Count);
            Assert.Equal("empty", d.EndReason);
        }

        [Theory]
        [InlineData(23.7f, true)]
        [InlineData(23.7f, false)]
        [InlineData(31.33f, false)]
        public void No_drift_on_ten_thousand_fractional_pixel_snapped_rows(float rowHeight, bool itemCountKnown)
        {
            var list = new FakeList(10000, columns: 2, rowHeight: rowHeight, viewport: 731f, snapToPixels: true);
            var c = Run(list, 2, itemCountKnown ? 10000 : (int?)null, new ScrollCollectorOptions { StableFrames = 2 });
            AssertComplete(c, 10000, 2);
        }

        [Fact]
        public void Content_padding_above_the_first_row()
        {
            var c = Run(new FakeList(300, topPadding: 7f), 3, 300);
            AssertComplete(c, 300, 3);
        }

        [Fact]
        public void Starts_from_the_top_even_if_the_list_was_scrolled()
        {
            var c = Run(new FakeList(400, startY: 3000f), 3, 400);
            AssertComplete(c, 400, 3);
        }

        [Fact]
        public void Slow_filling_rows_are_waited_for()
        {
            var c = Run(new FakeList(200, fillDelay: 30), 3, 200);
            AssertComplete(c, 200, 3);
        }

        [Fact]
        public void Max_rows_limits_the_export()
        {
            var c = Run(new FakeList(1000), 3, 1000, new ScrollCollectorOptions { MaxRows = 100 });
            Assert.Equal("max rows", c.EndReason);
            Assert.InRange(c.Count, 100, 140);
            Assert.Empty(c.Rows.MissingIndices(100));
        }

        [Fact]
        public void A_list_that_does_not_scroll_ends_instead_of_hanging()
        {
            var list = new FakeList(1000, ignoreScroll: true);
            var c = Run(list, 3, 1000, maxFrames: 5000);
            Assert.Equal("stuck", c.EndReason);
            Assert.True(c.Count > 0 && c.Count < 1000);
            Assert.True(list.ScrollCalls < 10);
        }

        [Fact]
        public void Rows_that_never_fill_are_read_after_the_settle_limit()
        {
            var c = Run(new FakeList(50, fillDelay: int.MaxValue), 3, 50, new ScrollCollectorOptions { MaxSettleFrames = 10 });
            Assert.Equal(50, c.Count);
            Assert.All(c.Rows.Rows(), r => Assert.All(r, cell => Assert.Equal("", cell)));
        }

        [Fact]
        public void Measured_pitch_averages_pixel_snapping()
        {
            var list = new FakeList(100, rowHeight: 23.7f, snapToPixels: true, fillDelay: 0);
            list.Frame();
            var rows = list.VisibleRows().OrderBy(r => r.ContentY).ToList();
            Assert.InRange(ScrollCollector.MeasuredPitch(rows), 23.5f, 23.9f);
        }

        [Fact]
        public void Stale_rows_from_the_old_scroll_position_are_not_taken_as_the_top()
        {
            var list = new FakeList(800, startY: 9000f, scrollDelay: 20);
            var c = Run(list, 3, 800);
            AssertComplete(c, 800, 3);
        }

        [Fact]
        public void A_scroll_that_overshoots_is_retried_instead_of_skipping_rows()
        {
            var list = new FakeList(1000, overshoots: 3);
            var c = Run(list, 3, null);
            AssertComplete(c, 1000, 3);
        }

        [Fact]
        public void A_list_that_is_still_loading_is_waited_for()
        {
            var c = Run(new FakeList(120, loadDelay: 40), 3, 120);
            AssertComplete(c, 120, 3);
        }

        [Fact]
        public void ItemCount_zero_counts_as_unknown()
        {
            var c = Run(new FakeList(500), 3, 0);
            Assert.Null(c.Expected);
            AssertComplete(c, 500, 3);
        }

        [Fact]
        public void Cells_that_fill_one_after_another_are_all_captured()
        {
            var c = Run(new FakeList(300, columns: 6, fillDelay: 2, cellStagger: 3), 6, 300);
            AssertComplete(c, 300, 6);
        }

        [Fact]
        public void Unusable_layout_ends_instead_of_running_forever()
        {
            var c = Run(new FakeList(1000, nanViewport: true), 3, null, maxFrames: 20000);
            Assert.Equal("no layout", c.EndReason);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Selected_rows_are_found_anywhere_in_a_long_list(bool snap)
        {
            var picked = new[] { 0, 7, 500, 2999, 9999 };
            var list = new FakeList(10000, rowHeight: 30.4f, snapToPixels: snap, selected: picked);
            var c = Run(list, 3, 10000);
            AssertComplete(c, 10000, 3);
            Assert.Equal(picked, c.SelectedIndices.OrderBy(i => i).ToArray());
            var rows = c.Rows.Rows(c.SelectedIndices);
            Assert.Equal(picked.Select(i => $"r{i}c0").ToArray(), rows.Select(r => r[0]).ToArray());
        }

        [Fact]
        public void No_selection_means_no_selected_indices()
        {
            var c = Run(new FakeList(300), 3, 300);
            Assert.Empty(c.SelectedIndices);
            Assert.Equal(300, c.Rows.Rows(null).Count);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 0)]
        [InlineData(true, 2)]   // deferred scrolling
        public void Targeted_mode_jumps_to_the_selected_rows_only(bool snap, int scrollDelay)
        {
            var picked = new[] { 3, 4, 1200, 1201, 5000, 9998, 9999 };
            var list = new FakeList(10000, rowHeight: 30.0001f, snapToPixels: snap, scrollDelay: scrollDelay, selected: picked);
            var c = Run(list, 3, 10000, targets: picked);
            Assert.Equal("selected rows", c.EndReason);
            foreach (var t in picked)
                Assert.Equal($"r{t}c0", c.Rows.Rows(new[] { t })[0][0]);
            Assert.Equal(picked, c.SelectedIndices.Where(picked.Contains).OrderBy(i => i).ToArray()); // highlighted = model
            Assert.True(c.Jumps >= 3, $"jumps {c.Jumps}");
            Assert.True(c.Steps < 30, $"steps {c.Steps} (a full pass takes ~700)");
        }

        [Fact]
        public void Targeted_mode_without_ItemCount_scans_but_stops_after_the_last_target()
        {
            var picked = new[] { 10, 400 };
            var list = new FakeList(10000, selected: picked);
            var c = Run(list, 3, null, targets: picked);
            Assert.Equal("selected rows", c.EndReason);
            Assert.Equal(new[] { "r10c0", "r400c0" }, c.Rows.Rows(picked).Select(r => r[0]).ToArray());
            Assert.True(c.Count < 1000);
        }

        [Fact]
        public void Short_list_with_empty_space_below_the_rows_settles_without_waiting()
        {
            var list = new FakeList(5, rowHeight: 30f, viewport: 600f, bottomPadding: 265f);
            var c = Run(list, 3, 5);
            AssertComplete(c, 5, 3);
            Assert.Equal(0, c.UnsettledPages);
            Assert.True(c.Frames < 30, $"frames {c.Frames}");
        }
    }
}

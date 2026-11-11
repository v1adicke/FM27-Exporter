using System;
using System.Collections.Generic;

namespace FM27Exporter.Core
{
    public sealed class RowCollector
    {
        private readonly SortedDictionary<int, string[]> _rows = new SortedDictionary<int, string[]>();
        private readonly int _columns;

        public RowCollector(int columns)
        {
            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns));
            _columns = columns;
        }

        public int Count => _rows.Count;

        public bool Has(int index) => _rows.ContainsKey(index);

        public int MaxIndex { get; private set; } = -1;

        public static int IndexOf(float contentY, float rowHeight)
        {
            if (rowHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(rowHeight));
            return (int)Math.Round(contentY / rowHeight, MidpointRounding.AwayFromZero);
        }

        public bool Add(int index, IReadOnlyList<string> cells)
        {
            if (index < 0)
                return false;
            var row = new string[_columns];
            for (var c = 0; c < _columns; c++)
                row[c] = c < cells.Count ? cells[c] ?? string.Empty : string.Empty;

            if (_rows.TryGetValue(index, out var existing))
            {
                if (Filled(row) >= Filled(existing))
                    _rows[index] = row;
                return false;
            }
            _rows[index] = row;
            if (index > MaxIndex)
                MaxIndex = index;
            return true;
        }

        public List<int> MissingIndices(int expectedCount = -1)
        {
            var last = expectedCount > 0 ? expectedCount - 1 : MaxIndex;
            var missing = new List<int>();
            for (var i = 0; i <= last; i++)
                if (!_rows.ContainsKey(i))
                    missing.Add(i);
            return missing;
        }

        public List<IReadOnlyList<string>> Rows(IReadOnlyCollection<int>? only = null)
        {
            var list = new List<IReadOnlyList<string>>(only?.Count ?? _rows.Count);
            var set = only == null ? null : new HashSet<int>(only);
            foreach (var kv in _rows)
                if (set == null || set.Contains(kv.Key))
                    list.Add(kv.Value);
            return list;
        }

        private static int Filled(string[] row)
        {
            var n = 0;
            foreach (var cell in row)
                if (!string.IsNullOrEmpty(cell))
                    n++;
            return n;
        }
    }
}

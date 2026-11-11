using System;
using System.Collections.Generic;

namespace FM27Exporter.Core
{
    public sealed class ExportTable
    {
        public ExportTable(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            Title = title ?? string.Empty;
            Headers = headers ?? throw new ArgumentNullException(nameof(headers));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        public string Title { get; }

        public IReadOnlyList<string> Headers { get; }

        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

        public int ColumnCount => Headers.Count;

        public string Cell(int row, int column)
        {
            var r = Rows[row];
            return column < r.Count ? r[column] ?? string.Empty : string.Empty;
        }

        public ExportTable WithoutEmptyColumns()
        {
            var keep = new List<int>();
            for (var c = 0; c < ColumnCount; c++)
            {
                if (Headers[c].Length > 0)
                {
                    keep.Add(c);
                    continue;
                }
                for (var r = 0; r < Rows.Count; r++)
                {
                    if (Cell(r, c).Length > 0)
                    {
                        keep.Add(c);
                        break;
                    }
                }
            }
            if (keep.Count == ColumnCount)
                return this;
            var headers = new List<string>(keep.Count);
            foreach (var c in keep)
                headers.Add(Headers[c]);
            var rows = new List<IReadOnlyList<string>>(Rows.Count);
            for (var r = 0; r < Rows.Count; r++)
            {
                var row = new string[keep.Count];
                for (var i = 0; i < keep.Count; i++)
                    row[i] = Cell(r, keep[i]);
                rows.Add(row);
            }
            return new ExportTable(Title, headers, rows);
        }
    }
}

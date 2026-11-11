using System;
using System.Collections.Generic;
using System.Linq;
using SI.Bindable;

namespace FM27Exporter
{
    internal static class ViewEditor
    {
        public static readonly string[] AttributeColumns =
        {
            "attribute-corners", "attribute-crossing", "attribute-dribbling", "attribute-finishing", "attribute-first-touch",
            "attribute-free-kicks", "attribute-heading", "attribute-long-shots", "attribute-long-throws", "attribute-marking",
            "attribute-passing", "attribute-penalty-taking", "attribute-tackling", "attribute-technique",
            "attribute-aggression", "attribute-anticipation", "attribute-bravery", "attribute-composure", "attribute-concentration",
            "attribute-decisions", "attribute-determination", "attribute-flair", "attribute-leadership", "attribute-movement",
            "attribute-positioning", "attribute-teamwork", "attribute-vision", "attribute-work-rate",
            "attribute-acceleration", "attribute-agility", "attribute-balance", "attribute-jumping-reach", "attribute-natural-fitness",
            "attribute-pace", "attribute-stamina", "attribute-strength",
            "attribute-aerial-reach", "attribute-command-of-area", "attribute-communication", "attribute-eccentricity",
            "attribute-handling", "attribute-kicking", "attribute-one-on-ones", "attribute-punching", "attribute-reflexes",
            "attribute-rushing-out", "attribute-throwing",
        };

        public static string AddAttributeColumns()
        {
            var tables = TableView.FindAll(out _);
            if (tables.Count == 0)
                return "No table on screen.";
            var view = TableView.UnderPointer(tables) ?? tables[0];
            var table = view.Table.TryCast<StreamedTable>();
            var tv = table?.m_streamedTableView;
            if (table == null || tv == null)
                return "This table has no editable view.";
            if (!tv.IsCustomView)
                return "This is one of the game's built-in views. Open the view menu → \"Create Copy Of Current View…\", then press the key again.";

            var have = new List<string>();
            var config = tv.TableConfig;
            for (var i = 0; i < config.Count; i++)
                have.Add(config[i].ColumnID);
            Plugin.Logger.LogInfo($"View '{tv.SerialisedName}' columns before: {string.Join(", ", have)}");

            if (!have.Any(id => AttributeColumns.Contains(id)))
                return "Add any one attribute column first (header right-click → Insert Column → Attributes), then press the key again. Nothing changed.";

            var added = 0;
            foreach (var id in AttributeColumns)
            {
                if (have.Contains(id))
                    continue;
                tv.AddColumn(id, -1);
                added++;
            }
            if (added > 0)
                table.SaveAndApplyCustomView();
            var after = new List<string>();
            for (var i = 0; i < tv.TableConfig.Count; i++)
                after.Add(tv.TableConfig[i].ColumnID);
            Plugin.Logger.LogInfo($"Added {added} attribute column(s); view now has {after.Count}: {string.Join(", ", after)}");
            return added == 0
                ? "All attribute columns are already in this view."
                : $"Added {added} attribute columns to '{tv.SerialisedName}'. Use the view menu → \"Export Current View…\" to save it as a file.";
        }
    }
}

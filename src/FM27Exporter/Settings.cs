using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FM27Exporter.Core;

namespace FM27Exporter
{
    internal sealed class Settings
    {
        private const string DefaultExportKey = "F8";
        private const string DefaultDiagnosticsKey = "Shift+F8";
        private const string DefaultAddAttributesKey = "Ctrl+F8";

        private readonly ConfigEntry<string> _addAttributesKey;
        private readonly ConfigEntry<string> _exportKey, _diagnosticsKey, _formats, _folder, _csvDelimiter;
        private readonly ConfigEntry<bool> _timestamp, _dropEmptyColumns, _overlay, _unlockFrameRate;
        private readonly ConfigEntry<int> _maxRows, _stableFrames, _maxSettleFrames, _selectedMin;
        private readonly ConfigEntry<float> _maxSettleSeconds;
        private Hotkey _export, _diagnostics, _addAttributes;

        public Settings(ConfigFile config)
        {
            _exportKey = config.Bind("Keys", "Export", DefaultExportKey,
                "Key that exports the table on screen (FM24 used Ctrl+P, which FM27 uses for Preferences). Examples: F8, Ctrl+F9, Alt+E.");
            _diagnosticsKey = config.Bind("Keys", "Diagnostics", DefaultDiagnosticsKey,
                "Writes a description of the UI on screen to <output>/diagnostics — attach it to a bug report if an export goes wrong.");
            _addAttributesKey = config.Bind("Keys", "AddAttributeColumns", DefaultAddAttributesKey,
                "Adds every player attribute column to the current custom view of the table under the mouse (as the game's " +
                "header menu \"Insert Column\" would, one by one).");
            _formats = config.Bind("Output", "Formats", "html,rtf,csv",
                "Comma-separated: html = FM24 'Web Page', rtf = FM24 'Text File', csv = spreadsheet CSV.");
            _folder = config.Bind("Output", "Folder", "",
                "Output folder. Empty = Documents/Sports Interactive/Football Manager 27/exports.");
            _csvDelimiter = config.Bind("Output", "CsvDelimiter", ",",
                "CSV field separator: ',' or ';' (for Excel with a decimal comma) or 'tab'.");
            _timestamp = config.Bind("Output", "Timestamp", true, "Add date and time to file names (else an existing file gets ' (2)').");
            _dropEmptyColumns = config.Bind("Output", "DropEmptyColumns", true,
                "Leave out columns without header and without values (e.g. the selection checkbox column).");
            _overlay = config.Bind("Display", "ShowStatus", true, "Show export progress and result at the top of the screen.");
            _unlockFrameRate = config.Bind("Export", "UnlockFrameRate", false,
                "Raise a frame-rate cap below 60 fps to 60 while exporting; restored afterwards. Can cause instability on some Linux/Intel setups.");
            _maxRows = config.Bind("Export", "MaxRows", 25000, "Stop after this many rows.");
            _selectedMin = config.Bind("Export", "SelectedRowsMinimum", 2,
                "Export only the selected rows when at least this many are selected. 0 = always export all rows.");
            _stableFrames = config.Bind("Export", "StableFrames", 3,
                "After each scroll step, wait until the visible rows stay unchanged for this many frames.");
            _maxSettleFrames = config.Bind("Export", "MaxSettleFrames", 90,
                "…but read a step anyway once this many frames AND MaxSettleSeconds have passed.");
            _maxSettleSeconds = config.Bind("Export", "MaxSettleSeconds", 2.0f,
                "Time part of that limit, in seconds.");

            _export = Parse(_exportKey.Value, DefaultExportKey);
            _diagnostics = Parse(_diagnosticsKey.Value, DefaultDiagnosticsKey);
            _exportKey.SettingChanged += (_, _) => _export = Parse(_exportKey.Value, DefaultExportKey);
            _diagnosticsKey.SettingChanged += (_, _) => _diagnostics = Parse(_diagnosticsKey.Value, DefaultDiagnosticsKey);
            _addAttributes = Parse(_addAttributesKey.Value, DefaultAddAttributesKey);
            _addAttributesKey.SettingChanged += (_, _) => _addAttributes = Parse(_addAttributesKey.Value, DefaultAddAttributesKey);
        }

        public Hotkey ExportKey => _export;
        public Hotkey DiagnosticsKey => _diagnostics;
        public Hotkey AddAttributesKey => _addAttributes;
        public double MaxSettleSeconds => Math.Max(0.1, _maxSettleSeconds.Value);
        public bool Timestamp => _timestamp.Value;
        public bool DropEmptyColumns => _dropEmptyColumns.Value;
        public bool ShowOverlay => _overlay.Value;
        public bool UnlockFrameRate => _unlockFrameRate.Value;
        public int MaxRows => Math.Max(1, _maxRows.Value);
        public int SelectedRowsMinimum => Math.Max(0, _selectedMin.Value);
        public int StableFrames => Math.Max(1, _stableFrames.Value);
        public int MaxSettleFrames => Math.Max(StableFrames + 1, _maxSettleFrames.Value);

        public char CsvDelimiter => _csvDelimiter.Value.Trim().ToLowerInvariant() switch
        {
            ";" => ';',
            "tab" or "\\t" => '\t',
            _ => ',',
        };

        public HashSet<string> Formats
        {
            get
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in _formats.Value.Split(',', ';', ' '))
                {
                    var f = part.Trim().TrimStart('.').ToLowerInvariant();
                    if (f == "txt" || f == "text") f = "rtf";
                    if (f == "htm" || f == "web") f = "html";
                    if (f == "html" || f == "rtf" || f == "csv")
                        set.Add(f);
                }
                if (set.Count == 0)
                    set.Add("html");
                return set;
            }
        }

        public string OutputFolder()
        {
            if (!string.IsNullOrWhiteSpace(_folder.Value))
                return Environment.ExpandEnvironmentVariables(_folder.Value.Trim());
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Sports Interactive", "Football Manager 27", "exports");
        }

        private static Hotkey Parse(string text, string fallback)
        {
            if (Hotkey.TryParse(text, out var key))
                return key;
            Plugin.Logger.LogWarning($"Key '{text}' not understood, using {fallback}.");
            Hotkey.TryParse(fallback, out key);
            return key;
        }
    }
}

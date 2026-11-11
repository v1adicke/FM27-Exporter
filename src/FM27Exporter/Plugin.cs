using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace FM27Exporter
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "local.fm27exporter";
        public const string Name = "FM27 Exporter";
        public const string Version = "1.0.0";

        internal static ManualLogSource Logger { get; private set; } = null!;
        internal static Settings Settings { get; private set; } = null!;

        public override void Load()
        {
            Logger = Log;
            Settings = new Settings(Config);
            Logger.LogInfo($"{Name} {Version} loaded. Game: {Application.productName} {Application.version}, Unity {Application.unityVersion}.");
            Logger.LogInfo($"Export key: {Settings.ExportKey}, diagnostics key: {Settings.DiagnosticsKey}, output: {Settings.OutputFolder()}");
            AddComponent<ExporterBehaviour>();
        }
    }
}

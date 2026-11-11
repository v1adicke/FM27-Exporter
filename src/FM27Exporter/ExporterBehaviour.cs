using System;
using UnityEngine;

namespace FM27Exporter
{
    public class ExporterBehaviour : MonoBehaviour
    {
        private const float MessageSeconds = 8f;

        private KeyWatcher? _exportKey, _diagnosticsKey, _addAttributesKey;
        private ExportJob? _job;
        private string _message = string.Empty;
        private float _messageUntil;

        public ExporterBehaviour(IntPtr pointer) : base(pointer)
        {
        }

        public void Start()
        {
            var s = Plugin.Settings;
            _exportKey = new KeyWatcher(() => s.ExportKey);
            _diagnosticsKey = new KeyWatcher(() => s.DiagnosticsKey);
            _addAttributesKey = new KeyWatcher(() => s.AddAttributesKey);
        }

        public void Update()
        {
            try
            {
                if (_job != null)
                {
                    if (_exportKey != null && _exportKey.Pressed())
                        _job.Cancel();
                    else
                        _job.Tick();
                    Show(_job.Status, _job.Finished ? MessageSeconds : 1f);
                    if (_job.Finished)
                        _job = null;
                    return;
                }
                if (_diagnosticsKey != null && _diagnosticsKey.Pressed())
                {
                    var file = Diagnostics.Write(Plugin.Settings);
                    Plugin.Logger.LogInfo($"Diagnostics written to {file}");
                    Show("Diagnostics written to " + file, MessageSeconds);
                }
                if (_addAttributesKey != null && _addAttributesKey.Pressed())
                {
                    var result = ViewEditor.AddAttributeColumns();
                    Plugin.Logger.LogInfo(result);
                    Show(result, MessageSeconds);
                }
                if (_exportKey != null && _exportKey.Pressed())
                {
                    _job = ExportJob.Start(Plugin.Settings, out var reason);
                    if (_job == null)
                    {
                        Plugin.Logger.LogWarning(reason);
                        Show(reason, MessageSeconds);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"{Plugin.Name}: {ex}");
                try { _job?.Cancel(); } catch { /* restore failed: already logged by the job */ }
                _job = null;
                Show("FM27 Exporter error: " + ex.Message, MessageSeconds);
            }
        }

        public void OnGUI()
        {
            if (!Plugin.Settings.ShowOverlay || _message.Length == 0 || Time.unscaledTime > _messageUntil)
                return;
            try
            {
                var box = GUI.skin.box;
                int size = box.fontSize; bool wrap = box.wordWrap; var align = box.alignment;
                box.fontSize = 16; box.wordWrap = true; box.alignment = TextAnchor.MiddleCenter;
                var width = Math.Min(Screen.width - 40, 900);
                GUI.Box(new Rect((Screen.width - width) / 2f, 20, width, 56), _message, box);
                box.fontSize = size; box.wordWrap = wrap; box.alignment = align;
            }
            catch
            {
            }
        }

        private void Show(string message, float seconds)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + seconds;
        }
    }
}

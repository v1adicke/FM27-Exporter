using System;
using System.Collections.Generic;

namespace FM27Exporter.Core
{
    public readonly struct Hotkey
    {
        public const int VkShift = 0x10, VkControl = 0x11, VkAlt = 0x12;

        public Hotkey(int virtualKey, bool ctrl, bool shift, bool alt)
        {
            VirtualKey = virtualKey;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public int VirtualKey { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            parts.Add(KeyName(VirtualKey));
            return string.Join("+", parts);
        }

        public static bool TryParse(string? text, out Hotkey hotkey)
        {
            hotkey = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            bool ctrl = false, shift = false, alt = false;
            int? key = null;
            foreach (var raw in text!.Split('+'))
            {
                var part = raw.Trim().ToUpperInvariant();
                switch (part)
                {
                    case "CTRL": case "CONTROL": ctrl = true; continue;
                    case "SHIFT": shift = true; continue;
                    case "ALT": alt = true; continue;
                }
                if (key != null)
                    return false;
                key = KeyCode(part);
                if (key == null)
                    return false;
            }
            if (key == null)
                return false;
            hotkey = new Hotkey(key.Value, ctrl, shift, alt);
            return true;
        }

        private static int? KeyCode(string part)
        {
            if (part.Length == 1 && part[0] >= 'A' && part[0] <= 'Z') return part[0];
            if (part.Length == 1 && part[0] >= '0' && part[0] <= '9') return part[0];
            if (part.Length >= 2 && part[0] == 'F' && int.TryParse(part.Substring(1), out var f) && f >= 1 && f <= 24)
                return 0x70 + f - 1;
            return part switch
            {
                "INSERT" or "INS" => 0x2D,
                "HOME" => 0x24,
                "END" => 0x23,
                "PAGEUP" or "PGUP" => 0x21,
                "PAGEDOWN" or "PGDN" => 0x22,
                "PAUSE" => 0x13,
                "SCROLLLOCK" => 0x91,
                _ => null,
            };
        }

        private static string KeyName(int vk)
        {
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1);
            if ((vk >= 'A' && vk <= 'Z') || (vk >= '0' && vk <= '9')) return ((char)vk).ToString();
            return vk switch
            {
                0x2D => "Insert", 0x24 => "Home", 0x23 => "End", 0x21 => "PageUp", 0x22 => "PageDown",
                0x13 => "Pause", 0x91 => "ScrollLock",
                _ => "0x" + vk.ToString("X2"),
            };
        }
    }
}

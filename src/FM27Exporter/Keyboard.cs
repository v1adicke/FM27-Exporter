using System;
using System.Runtime.InteropServices;
using FM27Exporter.Core;

namespace FM27Exporter
{
    internal sealed class KeyWatcher
    {
        private readonly Func<Hotkey> _key;
        private bool _wasDown;

        public KeyWatcher(Func<Hotkey> key) => _key = key;

        public bool Pressed()
        {
            var k = _key();
            var down = IsDown(k.VirtualKey)
                       && IsDown(Hotkey.VkControl) == k.Ctrl
                       && IsDown(Hotkey.VkShift) == k.Shift
                       && IsDown(Hotkey.VkAlt) == k.Alt;
            var pressed = down && !_wasDown && GameHasFocus();
            _wasDown = down;
            return pressed;
        }

        private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private static bool GameHasFocus()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return false;
            GetWindowThreadProcessId(window, out var pid);
            return pid == (uint)Environment.ProcessId;
        }

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }

    internal static class Pointer
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public static bool TryGetClientPosition(out float x, out float y, out float clientWidth)
        {
            x = y = clientWidth = 0;
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero || !GetCursorPos(out var p) || !ScreenToClient(window, ref p))
                return false;
            if (GetClientRect(window, out var rect))
                clientWidth = rect.Right - rect.Left;
            GetWindowThreadProcessId(window, out var pid);
            if (pid != (uint)Environment.ProcessId)
                return false;
            x = p.X;
            y = p.Y;
            return true;
        }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}

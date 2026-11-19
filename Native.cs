using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopSnow
{
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77,
                         SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO
        { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
        [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT
        { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

        public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        public delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr data);
        public delegate IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWL32(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWL64(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWL32(IntPtr h, int i, int v);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWL64(IntPtr h, int i, IntPtr v);

        public static long GetExStyle(IntPtr h)
        { return IntPtr.Size == 8 ? GetWL64(h, GWL_EXSTYLE).ToInt64() : GetWL32(h, GWL_EXSTYLE); }

        public static void SetExStyle(IntPtr h, long v)
        { if (IntPtr.Size == 8) SetWL64(h, GWL_EXSTYLE, new IntPtr(v)); else SetWL32(h, GWL_EXSTYLE, (int)v); }

        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, MouseProc cb, IntPtr mod, uint tid);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);

        public static string ClassOf(IntPtr h)
        { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }

        public static bool Bounds(IntPtr h, out RECT r)
        {
            if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf<RECT>()) == 0 && r.Right > r.Left) return true;
            return GetWindowRect(h, out r);
        }

        public static bool Cloaked(IntPtr h)
        { return DwmGetWindowAttribute(h, 14, out int v, 4) == 0 && v != 0; }
    }
}
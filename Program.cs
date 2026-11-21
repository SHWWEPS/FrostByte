using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace FrostByte
{
    internal static class Program
    {
        static Native.MouseProc _proc;      // держим ссылку, чтобы делегат не собрал GC
        static IntPtr _hook = IntPtr.Zero;
        static SnowEngine _engine;
        static OverlayWindow _overlay;
        static SettingsWindow _win;
        static Settings _s;
        static WF.NotifyIcon _tray;
        static IntPtr _iconHandle;
        static readonly Stopwatch Sw = new Stopwatch();
        static double _last, _nextFrame;
        static int _busyCount;

        static System.Threading.Mutex _mutex;

        [STAThread]
        static void Main()
        {
            bool first;
            _mutex = new System.Threading.Mutex(true, "FrostByte_SingleInstance", out first);
            if (!first)
            {
                System.Windows.MessageBox.Show("FrostByte уже запущен :J. Ищи снежинку в трее, возле часов (стрелка в верх ^).", "Desktop Snow");
                return;
            }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };            _s = Settings.Load();
            _engine = new SnowEngine(_s);
            _overlay = new OverlayWindow(_engine);
            _overlay.Show();
            _win = new SettingsWindow(_s, _engine);

            _tray = new WF.NotifyIcon { Text = "Desktop Snow", Icon = MakeIcon(), Visible = true };
            var menu = new WF.ContextMenuStrip();
            menu.Items.Add("Настройки", null, (a, b) => ShowSettings());
            var pause = new WF.ToolStripMenuItem("Пауза");
            pause.Click += (a, b) => { _s.Paused = !_s.Paused; _s.Touch(); pause.Checked = _s.Paused; };
            menu.Items.Add(pause);
            menu.Items.Add("Вызвать снегоуборочную машину", null, (a, b) => _engine.CallPlow());
            menu.Items.Add("Очистить весь снег", null, (a, b) => _engine.ClearAllSnow());
            menu.Items.Add("Создать ярлык на рабочем столе", null, (a, b) => Shortcut.Create());
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add("Выход", null, (a, b) => app.Shutdown());
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (a, b) => ShowSettings();

            _proc = HookCallback;
            try { _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0); }
            catch { _hook = IntPtr.Zero; }
            if (_hook == IntPtr.Zero)
                _tray.ShowBalloonTip(4000, "Desktop Snow", "Не удалось включить сбор снежков (хук мыши недоступен). Снег и гирлянда работают.", WF.ToolTipIcon.Info);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (a, b) => Housekeep();
            timer.Start();

            Sw.Start();
            CompositionTarget.Rendering += OnFrame;
            app.Run();

            CompositionTarget.Rendering -= OnFrame;
            timer.Stop();
            if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
            _s.Flush();
            _tray.Visible = false; _tray.Dispose();
            if (_iconHandle != IntPtr.Zero) Native.DestroyIcon(_iconHandle);
            _overlay.Close();
        }

        static void ShowSettings()
        { _win.Show(); _win.Activate(); }

        static void OnFrame(object sender, EventArgs e)
        {
            double now = Sw.Elapsed.TotalSeconds;
            if (_last <= 0) _last = now;
            if (now < _nextFrame) return;
            double dt = Math.Min(0.05, now - _last);
            _last = now;
            _nextFrame = now + 1.0 / Math.Max(15, _s.Fps) - 0.002;
            if (_overlay.Visibility != Visibility.Visible) return;
            try { _engine.Update(dt); _overlay.Canvas.InvalidateVisual(); } catch { }
        }

        static void Housekeep()
        {
            UpdateVisibility();
            _busyCount++;
            if (_busyCount % 20 == 0)
            {
                _s.Flush();
                if (_overlay.Visibility == Visibility.Visible) _overlay.Housekeep();
            }
        }

        static void UpdateVisibility()
        {
            bool show = !_s.Paused;
            if (show && _s.PauseFullscreen) show = !ForeignFullscreen();
            if (show && _s.PauseOnBattery && WF.SystemInformation.PowerStatus.PowerLineStatus == WF.PowerLineStatus.Offline) show = false;

            if (show && _overlay.Visibility != Visibility.Visible)
            {
                _overlay.Visibility = Visibility.Visible;
                _overlay.Housekeep();
                _last = 0;
            }
            else if (!show && _overlay.Visibility == Visibility.Visible)
            {
                _overlay.Visibility = Visibility.Hidden;
            }
        }

        static bool ForeignFullscreen()
        {
            bool found = false;
            int pid = Environment.ProcessId;
            bool maxToo = _s.PauseOnMaximized;

            Native.EnumWindows((h, l) =>
            {
                if (!Native.IsWindowVisible(h)) return true;
                if (Native.IsIconic(h)) return true;
                if (Native.Cloaked(h)) return true;

                Native.GetWindowThreadProcessId(h, out uint p);
                if (p == (uint)pid) return true;

                string cls = Native.ClassOf(h);
                if (cls == "Progman") return true;
                if (cls == "WorkerW") return true;
                if (cls == "Shell_TrayWnd") return true;
                if (cls == "Shell_SecondaryTrayWnd") return true;
                if ((Native.GetExStyle(h) & Native.WS_EX_TOOLWINDOW) != 0) return true;

                if (!Native.Bounds(h, out Native.RECT r)) return true;
                double fw = r.Right - r.Left;
                double fh = r.Bottom - r.Top;

                IntPtr mon = Native.MonitorFromWindow(h, 2);
                Native.MONITORINFO mi = new Native.MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
                if (!Native.GetMonitorInfo(mon, ref mi)) return true;

                double mw = mi.rcMonitor.Right - mi.rcMonitor.Left;
                double mh = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

                // мелкое окно не в счёт — смотрим, что под ним
                if (fw < mw * 0.6) return true;
                if (fh < mh * 0.6) return true;

                // настоящее полноэкранное: весь монитор
                bool full = fw >= mw * 0.99 && fh >= mh * 0.99
                         && Math.Abs(r.Left - mi.rcMonitor.Left) <= 2
                         && Math.Abs(r.Top - mi.rcMonitor.Top) <= 2;

                // плюс развёрнутые окна, если включена галочка
                if (!full && maxToo)
                {
                    double ww = mi.rcWork.Right - mi.rcWork.Left;
                    double wh = mi.rcWork.Bottom - mi.rcWork.Top;
                    full = fw >= ww * 0.98 && fh >= wh * 0.98;
                }

                found = full;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_RBUTTONDOWN || msg == Native.WM_LBUTTONUP)
                {
                    try
                    {
                        var d = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                        _engine.CursorX = d.pt.X - _engine.OX;
                        _engine.CursorY = d.pt.Y - _engine.OY;
                        if (msg == Native.WM_LBUTTONUP) _engine.OnMouseUp();
                        else _engine.OnMouseDown(msg == Native.WM_LBUTTONDOWN);
                    }
                    catch { }
                }
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);   // клик ВСЕГДА идёт дальше
        }

        static SD.Icon MakeIcon()
        {
            var bmp = new SD.Bitmap(32, 32);
            using (var g = SD.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SD.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(SD.Color.Transparent);
                for (int pass = 0; pass < 2; pass++)
                {
                    using var pen = new SD.Pen(pass == 0 ? SD.Color.FromArgb(255, 20, 60, 100) : SD.Color.FromArgb(255, 225, 245, 255), pass == 0 ? 5f : 2.4f)
                    { StartCap = SD.Drawing2D.LineCap.Round, EndCap = SD.Drawing2D.LineCap.Round };
                    for (int i = 0; i < 3; i++)
                    {
                        double a = i * Math.PI / 3;
                        float dx = (float)Math.Cos(a) * 12, dy = (float)Math.Sin(a) * 12;
                        g.DrawLine(pen, 16 - dx, 16 - dy, 16 + dx, 16 + dy);
                    }
                }
            }
            _iconHandle = bmp.GetHicon();
            return SD.Icon.FromHandle(_iconHandle);
        }
    }
}
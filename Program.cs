using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace DesktopSnow
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
            _mutex = new System.Threading.Mutex(true, "DesktopSnow_SingleInstance", out first);
            if (!first)
            {
                System.Windows.MessageBox.Show("Снегопад уже запущен :J. Ищи снежинку в трее, возле часов (стрелка в верх ^).", "Desktop Snow");
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

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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
            _s.Flush();

            bool busy = _s.PauseFullscreen && ForeignFullscreen();
            if (_s.PauseOnBattery && WF.SystemInformation.PowerStatus.PowerLineStatus == WF.PowerLineStatus.Offline) busy = true;

            // гасим только если чужое полноэкранное окно держится 2 проверки подряд
            if (busy) _busyCount++; else _busyCount = 0;

            bool show = !_s.Paused && _busyCount < 2;
            if (show && _overlay.Visibility != Visibility.Visible) { _overlay.Visibility = Visibility.Visible; _last = 0; }
            else if (!show && _overlay.Visibility == Visibility.Visible) _overlay.Visibility = Visibility.Hidden;
            if (show) _overlay.Housekeep();
        }

        // Полноэкранное окно ЧУЖОГО процесса. Свои окна игнорируем —
        // иначе оверлей во весь экран принимал сам себя за игру и прятался каждую секунду.
        static bool ForeignFullscreen()
        {
            IntPtr fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == _overlay.Hwnd) return false;

            Native.GetWindowThreadProcessId(fg, out uint pid);
            if (pid == (uint)Environment.ProcessId) return false;

            string cls = Native.ClassOf(fg);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;

            if (!Native.Bounds(fg, out Native.RECT r)) return false;
            double fw = r.Right - r.Left, fh = r.Bottom - r.Top;
            if (fw < 200 || fh < 200) return false;

            IntPtr mon = Native.MonitorFromWindow(fg, Native.MONITOR_DEFAULTTONEAREST);
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
            if (!Native.GetMonitorInfo(mon, ref mi)) return false;

            // rcWork — монитор без панели задач. Обычное развёрнутое окно
            // шире рабочей области не станет, а настоящая игра/видео — станет.
            double mw = mi.rcWork.Right - mi.rcWork.Left;
            double mh = mi.rcWork.Bottom - mi.rcWork.Top;
            return fw >= mw * 0.99 && fh >= mh * 0.99;
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
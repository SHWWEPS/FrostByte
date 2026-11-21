using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace FrostByte
{
    internal class SnowCanvas : FrameworkElement
    {
        readonly SnowEngine _e;
        public SnowCanvas(SnowEngine e) { _e = e; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext dc)
        {
            try { _e.Draw(dc); } catch { }
        }
    }

    internal class OverlayWindow : Window
    {
        public SnowCanvas Canvas;
        int _w, _h; double _dpi;

        public OverlayWindow(SnowEngine e)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 0; Top = 0; Width = 200; Height = 120;

            var host = new Canvas { IsHitTestVisible = false };
            Canvas = new SnowCanvas(e) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            host.Children.Add(Canvas);
            Content = host;

            SourceInitialized += (a, b) => { ApplyStyles(); Reposition(); };
        }

        public IntPtr Hwnd => new WindowInteropHelper(this).Handle;

        public void ApplyStyles()
        {
            var h = Hwnd; if (h == IntPtr.Zero) return;
            long ex = Native.GetExStyle(h);
            long want = ex | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            if (want != ex) Native.SetExStyle(h, want);
        }

        public void Reposition()
        {
            var h = Hwnd; if (h == IntPtr.Zero) return;
            int x = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
            int y = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
            int w = Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN);
            int hh = Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN);
            if (w < 1 || hh < 1) return;

            double dpi = 96;
            try { dpi = VisualTreeHelper.GetDpi(this).DpiScaleX * 96.0; } catch { }
            if (dpi < 48) dpi = 96;
            double sc = dpi / 96.0;

            Left = x / sc; Top = y / sc; Width = w / sc; Height = hh / sc;
            Canvas.Width = w; Canvas.Height = hh;
            Canvas.RenderTransform = new ScaleTransform(1 / sc, 1 / sc);
            Native.SetWindowPos(h, Native.HWND_TOPMOST, x, y, w, hh, Native.SWP_NOACTIVATE);
            _w = w; _h = hh; _dpi = dpi;
        }

        public void Housekeep()
        {
            var h = Hwnd; if (h == IntPtr.Zero) return;
            ApplyStyles();
            int w = Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN);
            int hh = Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN);
            double dpi = _dpi;
            try { dpi = VisualTreeHelper.GetDpi(this).DpiScaleX * 96.0; } catch { }
            if (w != _w || hh != _h || Math.Abs(dpi - _dpi) > 0.5) Reposition();
            else Native.SetWindowPos(h, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
    }
}
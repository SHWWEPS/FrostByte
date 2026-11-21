using System;
using System.Windows;
using System.Windows.Media;

namespace FrostByte
{
    internal partial class SnowEngine
    {
        SolidColorBrush[] flakeB;
        LinearGradientBrush bankB;
        RadialGradientBrush ballB;
        SolidColorBrush coal, carrot, hatRed, hatWhite, hatGreen, gold, shadow, speck,
                          plowBody, plowDark, plowGlass, plowBlade, beacon, exhaust;
        Pen stickPen, wirePen, plowPen;
        RadialGradientBrush[,] glow;
        SolidColorBrush[,] core;
        static readonly Color[] Palette =
        {
            Color.FromRgb(255,70,70), Color.FromRgb(255,190,60), Color.FromRgb(80,220,110),
            Color.FromRgb(80,150,255), Color.FromRgb(230,90,220), Color.FromRgb(80,230,235),
            Color.FromRgb(255,235,190)
        };

        static SolidColorBrush Solid(byte a, byte r, byte g, byte b)
        { var x = new SolidColorBrush(Color.FromArgb(a, r, g, b)); x.Freeze(); return x; }

        void InitBrushes()
        {
            flakeB = new SolidColorBrush[8];
            for (int i = 0; i < 8; i++) flakeB[i] = Solid((byte)(90 + i * 23), 255, 255, 255);

            var lg = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(214, 232, 248), 90);
            lg.Freeze(); bankB = lg;

            var rg = new RadialGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(216, 233, 247));
            rg.GradientOrigin = new Point(0.35, 0.3); rg.Freeze(); ballB = rg;

            coal = Solid(255, 42, 42, 52); carrot = Solid(255, 240, 138, 44);
            hatRed = Solid(255, 214, 40, 57); hatWhite = Solid(255, 252, 252, 255);
            hatGreen = Solid(255, 30, 142, 74); gold = Solid(255, 242, 193, 78);
            shadow = Solid(45, 90, 120, 150); speck = Solid(70, 150, 175, 200);
            plowBody = Solid(255, 217, 59, 43); plowDark = Solid(255, 43, 43, 52);
            plowGlass = Solid(255, 191, 230, 255); plowBlade = Solid(255, 242, 193, 78);
            beacon = Solid(255, 255, 165, 31); exhaust = Solid(90, 200, 205, 215);

            stickPen = new Pen(Solid(255, 122, 74, 34), 2.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            stickPen.Freeze();
            wirePen = new Pen(Solid(255, 31, 58, 36), 1.4); wirePen.Freeze();
            plowPen = new Pen(plowDark, 1.2); plowPen.Freeze();

            glow = new RadialGradientBrush[7, 8];
            core = new SolidColorBrush[7, 8];
            for (int c = 0; c < 7; c++)
                for (int l = 0; l < 8; l++)
                {
                    double f = (l + 1) / 8.0;
                    var col = Palette[c];
                    var g = new RadialGradientBrush(Color.FromArgb((byte)(130 * f), col.R, col.G, col.B),
                                                    Color.FromArgb(0, col.R, col.G, col.B));
                    g.Freeze(); glow[c, l] = g;
                    double k = 0.3 + 0.7 * f;
                    core[c, l] = Solid(255, (byte)(col.R * k + 255 * (1 - k) * 0.35),
                                            (byte)(col.G * k + 255 * (1 - k) * 0.35),
                                            (byte)(col.B * k + 255 * (1 - k) * 0.35));
                }
        }

        public void Draw(DrawingContext dc)
        {
            foreach (var s in Surfaces) DrawBank(dc, s);
            if (S.GarlandEnabled)
                foreach (var s in Surfaces)
                {
                    if (s.Bottom || s.W < 160) continue;
                    if (s.Taskbar ? S.GarlandTaskbar : S.GarlandWindows) DrawGarland(dc, s);
                }
            foreach (var b in Balls) DrawBall(dc, b.X, b.Y, b.R, b.Roll);
            foreach (var m in Men) DrawMan(dc, m);
            foreach (var s in Surfaces) if (s.PlowState == 1) DrawPlow(dc, s);
            foreach (var f in Flakes) if (!f.Front) dc.DrawEllipse(flakeB[f.Shade], null, new Point(f.X, f.Y), f.Size, f.Size);
            foreach (var f in Flakes) if (f.Front) dc.DrawEllipse(flakeB[f.Shade], null, new Point(f.X, f.Y), f.Size, f.Size);
            foreach (var p in Sparks)
            {
                int a = Math.Max(0, Math.Min(7, (int)(p.Life / p.Max * 7)));
                dc.DrawEllipse(p.Kind == 1 ? exhaust : flakeB[a], null, new Point(p.X, p.Y), p.Size, p.Size);
            }
        }

        static double HAt(Surface s, int k)
        {
            if (k < 0 || k >= s.Cells) return 0;
            double a = k > 0 ? s.Snow[k - 1] : s.Snow[k];
            double b = s.Snow[k];
            double c = k < s.Cells - 1 ? s.Snow[k + 1] : s.Snow[k];
            return (a + 2 * b + c) * 0.25;
        }

        void DrawBank(DrawingContext dc, Surface s)
        {
            int i = 0;
            while (i < s.Cells)
            {
                if (s.Blocked[i] || s.Snow[i] < 0.4) { i++; continue; }
                int j = i;
                while (j < s.Cells && !s.Blocked[j] && s.Snow[j] >= 0.4) j++;

                double left = Math.Max(s.X, s.X + i * Cell - Cell * 1.5);
                double right = Math.Min(s.X + s.W, s.X + j * Cell + Cell * 1.5);
                double h0 = HAt(s, i), h1 = HAt(s, j - 1);

                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(left, s.Y + 3), true, true);
                    ctx.LineTo(new Point(left, s.Y), true, false);
                    ctx.QuadraticBezierTo(new Point(left + Cell * 0.7, s.Y - h0 * 0.55),
                                          new Point(s.X + (i + 0.5) * Cell, s.Y - h0), true, false);
                    for (int k = i; k < j - 1; k++)
                    {
                        double cx = s.X + (k + 0.5) * Cell, cy = s.Y - HAt(s, k);
                        double nx = s.X + (k + 1.5) * Cell, ny = s.Y - HAt(s, k + 1);
                        ctx.QuadraticBezierTo(new Point(cx, cy), new Point((cx + nx) / 2, (cy + ny) / 2), true, false);
                    }
                    ctx.LineTo(new Point(s.X + (j - 0.5) * Cell, s.Y - h1), true, false);
                    ctx.QuadraticBezierTo(new Point(right - Cell * 0.7, s.Y - h1 * 0.55),
                                          new Point(right, s.Y), true, false);
                    ctx.LineTo(new Point(right, s.Y + 3), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(bankB, null, g);
                i = j;
            }
        }

        double Brightness(int idx)
        {
            double t = Time * S.GarlandSpeed, v;
            switch (S.GarlandMode)
            {
                case 1: v = 0.5 + 0.5 * Math.Sin(t * 1.7 + gPhase[idx & 2047]); break;
                case 2: v = ((idx * 7 + (int)(t * 3.0) * 13) % 11) < 5 ? 0.12 : 1.0; break;
                case 3: v = 1.0; break;
                default: v = 0.5 + 0.5 * Math.Sin(t * 2.2 - idx * 0.45); break;
            }
            return Math.Max(0, Math.Min(1, v * S.GarlandBrightness));
        }

        void DrawGarland(DrawingContext dc, Surface s)
        {
            double y = s.Y + 3, sag = 13;
            int idx = Math.Abs(s.Hwnd.ToInt32() & 1023);
            int i = 0;
            while (i < s.Cells)
            {
                if (s.Blocked[i]) { i++; continue; }
                int j = i;
                while (j < s.Cells && !s.Blocked[j]) j++;

                double xa = s.X + i * Cell;
                double xb = Math.Min(s.X + s.W, s.X + j * Cell);
                double len = xb - xa;
                if (len > 20)
                {
                    int parts = Math.Max(1, (int)Math.Round(len / 110.0));
                    double st = len / parts;

                    var wire = new StreamGeometry();
                    using (var ctx = wire.Open())
                    {
                        ctx.BeginFigure(new Point(xa, y), false, false);
                        for (int k = 0; k < parts; k++)
                        {
                            double x0 = xa + k * st, x1 = x0 + st;
                            ctx.QuadraticBezierTo(new Point((x0 + x1) / 2, y + sag * 2), new Point(x1, y), true, false);
                        }
                    }
                    wire.Freeze();
                    dc.DrawGeometry(null, wirePen, wire);

                    for (int k = 0; k < parts; k++)
                    {
                        double x0 = xa + k * st, x1 = x0 + st, mid = (x0 + x1) / 2;
                        int n = Math.Max(2, (int)(st / Math.Max(10, S.BulbSpacing)));
                        for (int q = 0; q < n; q++)
                        {
                            idx++;
                            double t = (q + 0.5) / n, mt = 1 - t;
                            double px = mt * mt * x0 + 2 * mt * t * mid + t * t * x1;
                            double py = mt * mt * y + 2 * mt * t * (y + sag * 2) + t * t * y + 3;
                            double br = Brightness(idx);
                            int lvl = Math.Max(0, Math.Min(7, (int)(br * 7.99)));
                            int col = idx % 7;
                            double gr = 6 + 5 * br;
                            dc.DrawEllipse(glow[col, lvl], null, new Point(px, py), gr, gr);
                            dc.DrawEllipse(core[col, lvl], null, new Point(px, py), 2.6, 3.4);
                        }
                    }
                }
                i = j;
            }
        }

        void DrawBall(DrawingContext dc, double x, double y, double r, double roll)
        {
            dc.DrawEllipse(shadow, null, new Point(x, y + r - 1), r * 0.85, 3.5);
            dc.DrawEllipse(ballB, null, new Point(x, y), r, r);
            for (int i = 0; i < 3; i++)
            {
                double a = roll + i * 2.094;
                dc.DrawEllipse(speck, null, new Point(x + Math.Cos(a) * r * 0.5, y + Math.Sin(a) * r * 0.5), r * 0.12, r * 0.12);
            }
            dc.DrawEllipse(flakeB[7], null, new Point(x - r * 0.32, y - r * 0.36), r * 0.2, r * 0.15);
        }

        void DrawMan(DrawingContext dc, Snowman m)
        {
            double bx = ManPos(m, out double by, out double ty);
            double r1 = m.R1, r2 = m.R2;

            if (!m.OnRoof && m.Resting)
            {
                dc.DrawEllipse(bankB, null, new Point(bx, by + r1 - 1), r1 * 1.6, 6.5);
                dc.DrawEllipse(bankB, null, new Point(bx, by + r1 - 4), r1 * 1.15, 5);
            }
            dc.DrawEllipse(shadow, null, new Point(bx, by + r1 - 1), r1 * 0.95, 4);

            double wave = Time < m.WaveUntil ? Math.Sin(Time * 11) * 0.4 : 0;
            for (int side = -1; side <= 1; side += 2)
            {
                double sx = bx + side * r1 * 0.82, sy = by - r1 * 0.15;
                double ex = bx + side * (r1 + 26), ey = by - r1 * 0.55 + (side > 0 ? wave * 22 : 0);
                dc.DrawLine(stickPen, new Point(sx, sy), new Point(ex, ey));
                dc.DrawLine(stickPen, new Point(ex - side * 7, ey + 2), new Point(ex - side * 12, ey - 7));
                dc.DrawLine(stickPen, new Point(ex - side * 3, ey + 1), new Point(ex - side * 6, ey + 8));
            }

            dc.DrawEllipse(ballB, null, new Point(bx, by), r1, r1);
            for (int i = -1; i <= 1; i++)
                dc.DrawEllipse(coal, null, new Point(bx, by + i * r1 * 0.45), 2.8, 2.8);

            dc.DrawEllipse(ballB, null, new Point(bx, ty), r2, r2);

            bool blink = Time < m.BlinkUntil;
            for (int side = -1; side <= 1; side += 2)
                dc.DrawEllipse(coal, null, new Point(bx + side * r2 * 0.33, ty - r2 * 0.18), 2.1, blink ? 0.6 : 2.1);

            var nose = new StreamGeometry();
            using (var ctx = nose.Open())
            {
                ctx.BeginFigure(new Point(bx - 1, ty + 1), true, true);
                ctx.LineTo(new Point(bx + r2 * 0.85, ty + 3.5), true, false);
                ctx.LineTo(new Point(bx - 1, ty + 5.5), true, false);
            }
            nose.Freeze();
            dc.DrawGeometry(carrot, null, nose);

            for (int i = 0; i < 5; i++)
            {
                double a = Math.PI * (0.18 + 0.64 * i / 4.0);
                dc.DrawEllipse(coal, null, new Point(bx - Math.Cos(a) * r2 * 0.52, ty + r2 * 0.3 + Math.Sin(a) * r2 * 0.2), 1.3, 1.3);
            }

            double sway = Math.Sin(Time * 1.4 + m.Phase) * 0.06;
            dc.PushTransform(new RotateTransform(-12 + sway * 30, bx, ty - r2 * 0.6));
            if (m.Hat == 0)
            {
                var cone = new StreamGeometry();
                using (var ctx = cone.Open())
                {
                    ctx.BeginFigure(new Point(bx - r2 * 0.82, ty - r2 * 0.7), true, true);
                    ctx.QuadraticBezierTo(new Point(bx - r2 * 0.4, ty - r2 * 2.0), new Point(bx + r2 * 1.05, ty - r2 * 1.95), true, false);
                    ctx.QuadraticBezierTo(new Point(bx + r2 * 0.5, ty - r2 * 1.2), new Point(bx + r2 * 0.82, ty - r2 * 0.7), true, false);
                }
                cone.Freeze();
                dc.DrawGeometry(hatRed, null, cone);
                dc.DrawEllipse(hatWhite, null, new Point(bx, ty - r2 * 0.72), r2 * 0.95, r2 * 0.26);
                dc.DrawEllipse(hatWhite, null, new Point(bx + r2 * 1.08, ty - r2 * 1.98), r2 * 0.24, r2 * 0.24);
            }
            else
            {
                var cone = new StreamGeometry();
                using (var ctx = cone.Open())
                {
                    ctx.BeginFigure(new Point(bx - r2 * 0.8, ty - r2 * 0.7), true, true);
                    ctx.QuadraticBezierTo(new Point(bx - r2 * 0.2, ty - r2 * 2.2), new Point(bx + r2 * 0.9, ty - r2 * 2.1), true, false);
                    ctx.QuadraticBezierTo(new Point(bx + r2 * 0.4, ty - r2 * 1.2), new Point(bx + r2 * 0.8, ty - r2 * 0.7), true, false);
                }
                cone.Freeze();
                dc.DrawGeometry(hatGreen, null, cone);
                dc.DrawEllipse(hatRed, null, new Point(bx, ty - r2 * 0.72), r2 * 0.92, r2 * 0.22);
                dc.DrawEllipse(gold, null, new Point(bx + r2 * 0.93, ty - r2 * 2.12), r2 * 0.22, r2 * 0.22);
            }
            dc.Pop();
        }

        void DrawPlow(DrawingContext dc, Surface s)
        {
            double x = s.PlowX, y = s.Y + 1;
            if (s.PlowDir < 0) dc.PushTransform(new ScaleTransform(-1, 1, x, y));

            dc.DrawEllipse(shadow, null, new Point(x, y), 40, 5);
            dc.DrawRoundedRectangle(plowBody, plowPen, new Rect(x - 34, y - 34, 64, 22), 5, 5);
            dc.DrawRoundedRectangle(plowBody, plowPen, new Rect(x - 10, y - 52, 32, 20), 4, 4);
            dc.DrawRoundedRectangle(plowGlass, null, new Rect(x - 6, y - 48, 24, 12), 3, 3);
            dc.DrawRectangle(plowDark, null, new Rect(x - 30, y - 40, 8, 8));

            double pulse = 0.45 + 0.55 * Math.Abs(Math.Sin(Time * 6));
            int lv = Math.Max(0, Math.Min(7, (int)(pulse * 7.99)));
            dc.DrawEllipse(glow[1, lv], null, new Point(x + 6, y - 56), 13, 13);
            dc.DrawEllipse(beacon, null, new Point(x + 6, y - 56), 4, 4);

            var blade = new StreamGeometry();
            using (var ctx = blade.Open())
            {
                ctx.BeginFigure(new Point(x + 30, y - 1), true, true);
                ctx.LineTo(new Point(x + 47, y - 5), true, false);
                ctx.LineTo(new Point(x + 44, y - 32), true, false);
                ctx.LineTo(new Point(x + 30, y - 27), true, false);
            }
            blade.Freeze();
            dc.DrawGeometry(plowBlade, plowPen, blade);
            dc.DrawLine(plowPen, new Point(x + 22, y - 18), new Point(x + 33, y - 18));

            double rot = x / 9.0;
            for (int k = 0; k < 2; k++)
            {
                double wx = x + (k == 0 ? -22 : 16), wy = y - 9;
                dc.DrawEllipse(plowDark, null, new Point(wx, wy), 9, 9);
                dc.DrawEllipse(Solid(255, 120, 130, 145), null, new Point(wx, wy), 3.6, 3.6);
                for (int a = 0; a < 3; a++)
                {
                    double ang = rot + a * Math.PI / 3;
                    dc.DrawLine(plowPen, new Point(wx - Math.Cos(ang) * 7, wy - Math.Sin(ang) * 7),
                                         new Point(wx + Math.Cos(ang) * 7, wy + Math.Sin(ang) * 7));
                }
            }
            if (s.PlowDir < 0) dc.Pop();
        }
    }
}
using System;
using System.Collections.Generic;

namespace DesktopSnow
{
    internal partial class SnowEngine
    {
        public const double Cell = 6.0;
        public Settings S;
        public double W = 1920, H = 1080, OX = 0, OY = 0;
        public double Time, Wind, CursorX, CursorY;

        public readonly List<Surface> Surfaces = new List<Surface>();
        public readonly List<Flake> Flakes = new List<Flake>();
        public readonly List<Ball> Balls = new List<Ball>();
        public readonly List<Snowman> Men = new List<Snowman>();
        public readonly List<Spark> Sparks = new List<Spark>();
        public readonly Random Rnd = new Random();

        double refreshAcc;
        readonly double[] gPhase = new double[2048];

        static readonly HashSet<string> Skip = new HashSet<string>
        { "Progman","WorkerW","Windows.UI.Core.CoreWindow","tooltips_class32","MSCTFIME UI","IME",
          "NotifyIconOverflowWindow","Shell_SecondaryTrayWnd_X","XamlExplorerHostIslandWindow" };

        public SnowEngine(Settings s)
        {
            S = s;
            for (int i = 0; i < gPhase.Length; i++) gPhase[i] = Rnd.NextDouble() * 6.283;
            InitBrushes();
            RefreshVirtual();
            RefreshSurfaces();
        }

        public void RefreshVirtual()
        {
            OX = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
            OY = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
            W = Math.Max(320, Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN));
            H = Math.Max(240, Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN));
        }

        // ---------- окна и панель задач ----------
        public void RefreshSurfaces()
        {
            RefreshVirtual();
            var seen = new HashSet<IntPtr>();
            int z = 0, pid = Environment.ProcessId;

            Native.EnumWindows((h, l) =>
            {
                z++;
                if (!Native.IsWindowVisible(h)) return true;
                Native.GetWindowThreadProcessId(h, out uint p);
                if (p == (uint)pid) return true;
                string cls = Native.ClassOf(h);
                bool tb = cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
                if (!tb)
                {
                    if (Native.IsIconic(h) || Native.Cloaked(h) || Skip.Contains(cls)) return true;
                    if ((Native.GetExStyle(h) & Native.WS_EX_TOOLWINDOW) != 0) return true;
                    if (Native.GetWindowTextLength(h) == 0) return true;
                }
                if (!Native.Bounds(h, out Native.RECT r)) return true;
                double x = r.Left - OX, y = r.Top - OY, w = r.Right - r.Left, hh = r.Bottom - r.Top;
                if (tb) { if (w < 40 || hh < 6) return true; }
                else if (w < 140 || hh < 90) return true;
                if (x + w < 0 || y + hh < 0 || x > W || y > H) return true;

                var s = Find(h);
                if (s == null) { s = new Surface { Hwnd = h }; Surfaces.Add(s); }
                s.Taskbar = tb; s.Bottom = false; s.Z = z; s.X = x; s.Y = y; s.W = w; s.H = hh;
                Resize(s, w);
                seen.Add(h);
                return true;
            }, IntPtr.Zero);

            int mon = 0;
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hm, IntPtr hdc, ref Native.RECT rr, IntPtr d) =>
            {
                mon++;
                var key = new IntPtr(-1000 - mon);
                double x = rr.Left - OX, y = rr.Bottom - OY, w = rr.Right - rr.Left;
                var s = Find(key);
                if (s == null) { s = new Surface { Hwnd = key }; Surfaces.Add(s); }
                s.Bottom = true; s.Taskbar = false; s.Z = int.MaxValue;
                s.X = x; s.Y = y; s.W = w; s.H = 0;
                Resize(s, w);
                seen.Add(key);
                return true;
            }, IntPtr.Zero);

            for (int i = Surfaces.Count - 1; i >= 0; i--)
                if (!seen.Contains(Surfaces[i].Hwnd)) { Crumble(Surfaces[i]); Surfaces.RemoveAt(i); }

            foreach (var s in Surfaces)
            {
                Array.Clear(s.Blocked, 0, s.Blocked.Length);
                if (s.Bottom && !S.BottomSnow) { Array.Clear(s.Snow, 0, s.Snow.Length); continue; }
                foreach (var o in Surfaces)
                {
                    if (o == s || o.Bottom || o.Z >= s.Z) continue;
                    if (o.Y <= s.Y && o.Y + o.H >= s.Y)
                    {
                        int c0 = Math.Max(0, (int)Math.Floor((o.X - s.X) / Cell));
                        int c1 = Math.Min(s.Cells, (int)Math.Ceiling((o.X + o.W - s.X) / Cell));
                        for (int c = c0; c < c1; c++) s.Blocked[c] = true;
                    }
                }
            }
            Surfaces.Sort((a, b) => a.Y.CompareTo(b.Y));
        }

        Surface Find(IntPtr h) { foreach (var s in Surfaces) if (s.Hwnd == h) return s; return null; }

        void Resize(Surface s, double w)
        {
            int n = Math.Max(1, (int)Math.Ceiling(w / Cell));
            if (n == s.Cells) return;
            var ns = new double[n];
            Array.Copy(s.Snow, ns, Math.Min(s.Snow.Length, n));
            s.Snow = ns; s.Blocked = new bool[n]; s.Cells = n;
        }

        static int CellOf(Surface s, double x)
        { return Math.Max(0, Math.Min(s.Cells - 1, (int)((x - s.X) / Cell))); }

        double MaxOf(Surface s) { return s.Taskbar ? S.MaxTaskbarSnow : (s.Bottom ? 40 : S.MaxWindowSnow); }

        // ---------- главный цикл ----------
        public void Update(double dt)
        {
            Time += dt;
            refreshAcc += dt;
            double poll = 1.0 / Math.Max(2, Math.Min(60, S.WindowPollHz));
            if (refreshAcc >= poll) { refreshAcc = 0; RefreshSurfaces(); }

            Native.GetCursorPos(out Native.POINT cp);
            CursorX = cp.X - OX; CursorY = cp.Y - OY;

            Wind = S.WindStrength * (0.55 + 0.45 * Math.Sin(Time * 0.17))
                 * (S.Gusts ? 1 + 0.5 * Math.Sin(Time * 0.61 + 1.3) : 1);

            EnsureFlakes();
            UpdateFlakes(dt);
            foreach (var s in Surfaces) { Relax(s); if (S.Melt) Melt(s, dt); UpdatePlow(s, dt); }
            UpdateBalls(dt);
            UpdateMen(dt);
            UpdateSparks(dt);
        }

        void EnsureFlakes()
        {
            int target = S.SnowEnabled ? Math.Max(0, Math.Min(3000, S.FlakeCount)) : 0;
            while (Flakes.Count < target) Flakes.Add(NewFlake(true));
            while (Flakes.Count > target) Flakes.RemoveAt(Flakes.Count - 1);
        }

        Flake NewFlake(bool spread)
        {
            var f = new Flake();
            Reset(f);
            if (spread) f.Y = Rnd.NextDouble() * H;
            return f;
        }

        void Reset(Flake f)
        {
            f.Front = Rnd.NextDouble() < 0.35;
            f.Size = (f.Front ? 2.6 + Rnd.NextDouble() * 2.6 : 1.3 + Rnd.NextDouble() * 2.0) * S.FlakeScale;
            f.Vy = f.Front ? 90 + Rnd.NextDouble() * 55 : 35 + Rnd.NextDouble() * 50;
            f.X = Rnd.NextDouble() * W;
            f.Y = -Rnd.Next(5, 140);
            f.Phase = Rnd.NextDouble() * 6.283;
            f.Sway = 0.5 + Rnd.NextDouble() * 1.4;
            f.Shade = Math.Max(0, Math.Min(7, (int)((f.Front ? 0.72 : 0.38) * 7 + Rnd.NextDouble() * 2)));
        }

        void UpdateFlakes(double dt)
        {
            foreach (var f in Flakes)
            {
                double prev = f.Y;
                f.Y += f.Vy * S.FallSpeed * dt;
                f.X += (Wind * (0.35 + f.Size * 0.16) + Math.Sin(Time * f.Sway + f.Phase) * 14) * dt;
                if (f.X < -25) f.X += W + 50; else if (f.X > W + 25) f.X -= W + 50;

                if (S.Accumulate && FindLanding(f.X, f.Y, prev, f.Front, 8, out _) is Surface s)
                { Deposit(s, f.X, (0.55 + f.Size * 0.42) * S.AccumRate); Reset(f); continue; }
                if (f.Y > H + 25) Reset(f);
            }
        }

        Surface FindLanding(double x, double bottom, double prevBottom, bool front, double tol, out double landY, bool objects = false)
        {
            landY = 0;
            foreach (var s in Surfaces)
            {
                if (x < s.X || x > s.X + s.W) continue;
                if (front && !s.Taskbar && !s.Bottom) continue;
                if (s.Bottom && !S.BottomSnow && !objects) continue;
                int c = CellOf(s, x);
                if (s.Blocked[c]) continue;
                double land = s.Y - s.Snow[c];
                if (bottom >= land && prevBottom <= land + tol) { landY = land; return s; }
            }
            return null;
        }

        void Deposit(Surface s, double x, double amount)
        {
            int c = CellOf(s, x); double max = MaxOf(s);
            s.Snow[c] = Math.Min(max, s.Snow[c] + amount);
            if (c > 0) s.Snow[c - 1] = Math.Min(max, s.Snow[c - 1] + amount * 0.35);
            if (c < s.Cells - 1) s.Snow[c + 1] = Math.Min(max, s.Snow[c + 1] + amount * 0.35);
        }

        void Relax(Surface s)
        {
            int n = s.Cells; if (n < 2) return;
            int start = Rnd.Next(0, n - 1), count = Math.Min(n - 1, 48);
            for (int k = 0; k < count; k++)
            {
                int i = (start + k) % (n - 1);
                double d = s.Snow[i] - s.Snow[i + 1];
                if (d > 2.5) { double t = (d - 2.5) * 0.25; s.Snow[i] -= t; s.Snow[i + 1] += t; }
                else if (d < -2.5) { double t = (-d - 2.5) * 0.25; s.Snow[i] += t; s.Snow[i + 1] -= t; }
            }
        }

        void Melt(Surface s, double dt)
        { double k = 1 - 0.02 * dt; for (int i = 0; i < s.Cells; i++) s.Snow[i] *= k; }

        // ---------- снегоуборочная машина ----------
        public void CallPlow()
        { foreach (var s in Surfaces) if (s.Taskbar) { s.PlowForce = true; s.PlowCool = 0; } }

        void UpdatePlow(Surface s, double dt)
        {
            if (!s.Taskbar) return;
            if (s.PlowState == 0)
            {
                if (!S.PlowEnabled && !s.PlowForce) return;
                if (Time < s.PlowCool && !s.PlowForce) return;
                double sum = 0; for (int i = 0; i < s.Cells; i++) sum += s.Snow[i];
                double avg = sum / s.Cells;
                if (avg >= S.PlowThreshold || s.PlowForce)
                {
                    s.PlowWait += dt;
                    if (s.PlowWait > 3 || s.PlowForce)
                    {
                        s.PlowForce = false; s.PlowWait = 0; s.PlowState = 1;
                        s.PlowDir = S.PlowFromLeft ? 1 : -1;
                        s.PlowX = s.PlowDir > 0 ? s.X - 100 : s.X + s.W + 100;
                    }
                }
                else s.PlowWait = 0;
                return;
            }

            double speed = Math.Max(200, s.W / 5.0) * S.PlowSpeed;
            s.PlowX += s.PlowDir * speed * dt;
            double blade = s.PlowX + s.PlowDir * 36;
            for (int c = 0; c < s.Cells; c++)
            {
                if (s.Snow[c] <= 0) continue;
                double cx = s.X + (c + 0.5) * Cell;
                bool behind = s.PlowDir > 0 ? cx < blade : cx > blade;
                if (!behind) continue;
                if (Sparks.Count < 380 && s.Snow[c] > 2)
                    for (int k = 0; k < 2; k++)
                        Sparks.Add(new Spark
                        {
                            X = blade, Y = s.Y - 6, Vx = s.PlowDir * (70 + Rnd.NextDouble() * 190),
                            Vy = -(130 + Rnd.NextDouble() * 220), Life = 0.5 + Rnd.NextDouble() * 0.7,
                            Max = 1.2, Size = 1.2 + Rnd.NextDouble() * 2.2, Kind = 0
                        });
                s.Snow[c] = 0;
            }
            if (Sparks.Count < 380 && Rnd.NextDouble() < 0.25)
                Sparks.Add(new Spark { X = s.PlowX - s.PlowDir * 20, Y = s.Y - 54, Vx = -s.PlowDir * 15, Vy = -25, Life = 0.9, Max = 0.9, Size = 3.5, Kind = 1 });

            if ((s.PlowDir > 0 && s.PlowX > s.X + s.W + 110) || (s.PlowDir < 0 && s.PlowX < s.X - 110))
            { Array.Clear(s.Snow, 0, s.Cells); s.PlowState = 0; s.PlowCool = Time + S.PlowCooldown; }
        }

        // ---------- частицы ----------
        void Puff(double x, double y, int n)
        {
            for (int i = 0; i < n && Sparks.Count < 400; i++)
                Sparks.Add(new Spark
                {
                    X = x, Y = y, Vx = (Rnd.NextDouble() - 0.5) * 160, Vy = -(40 + Rnd.NextDouble() * 150),
                    Life = 0.4 + Rnd.NextDouble() * 0.6, Max = 1.0, Size = 1 + Rnd.NextDouble() * 2.4, Kind = 0
                });
        }

        void Crumble(Surface s)
        {
            for (int c = 0; c < s.Cells; c += 6)
                if (s.Snow[c] > 3) Puff(s.X + c * Cell, s.Y - s.Snow[c], 2);
        }

        void UpdateSparks(double dt)
        {
            for (int i = Sparks.Count - 1; i >= 0; i--)
            {
                var p = Sparks[i];
                p.Vy += (p.Kind == 1 ? -20 : 520) * dt;
                p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Life -= dt;
                if (p.Life <= 0 || p.Y > H + 60) Sparks.RemoveAt(i);
            }
        }

        public void ClearAllSnow()
        {
            foreach (var s in Surfaces) { Crumble(s); Array.Clear(s.Snow, 0, s.Cells); s.PlowState = 0; s.PlowCool = Time + S.PlowCooldown; }
        }
    }
}
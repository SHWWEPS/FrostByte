using System;

namespace FrostByte
{
    internal partial class SnowEngine
    {
        const double StartBallR = 7.5;
        const double MaxBallR = 28;

        bool ModOk()
        {
            int vk = S.BallModifier switch { 1 => 0x12, 2 => 0x11, 3 => 0x10, _ => 0 };
            return vk == 0 || (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        static bool ShiftDown()
        {
            return (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
        }

        Surface SurfaceAt(double x, double y)
        {
            Surface best = null;
            foreach (var s in Surfaces)
            {
                if (s.Bottom) continue;
                if (x < s.X || x > s.X + s.W) continue;
                if (y < s.Y || y > s.Y + s.H) continue;
                if (best == null || s.Z < best.Z) best = s;
            }
            return best;
        }

        Surface BottomNear(double x, double y)
        {
            foreach (var s in Surfaces)
                if (s.Bottom && x >= s.X && x <= s.X + s.W && y > s.Y - 70) return s;
            return null;
        }

        Ball BallUnderCursor()
        {
            for (int i = Balls.Count - 1; i >= 0; i--)
            {
                var b = Balls[i];
                if (b.Held) continue;
                double dx = CursorX - b.X, dy = CursorY - b.Y, r = b.R * 1.3;
                if (dx * dx + dy * dy <= r * r) return b;
            }
            return null;
        }

        Snowman ManUnderCursor()
        {
            for (int i = Men.Count - 1; i >= 0; i--)
            {
                var m = Men[i];
                double x = ManPos(m, out double by, out double ty);
                double dx = CursorX - x, dy = CursorY - by, r = m.R1 * 1.2;
                if (dx * dx + dy * dy <= r * r) return m;
                dy = CursorY - ty; r = m.R2 * 1.25;
                if (dx * dx + dy * dy <= r * r) return m;
                if (Math.Abs(dx) < m.R2 * 1.4 && CursorY > ty - m.R2 * 2.6 && CursorY < ty - m.R2 * 0.4) return m;
            }
            return null;
        }

        double ManPos(Snowman m, out double bottomY, out double topY)
        {
            double x, by;
            if (m.Resting && m.Ref != null)
            {
                x = m.Ref.X + m.Dx;
                double line = m.Ref.Y + m.BaseDy;
                if (m.OnRoof) line -= m.Ref.Snow[CellOf(m.Ref, x)];
                by = line - m.R1;
            }
            else
            {
                x = m.X;
                by = m.Y;
            }
            bottomY = by;
            topY = by - m.R1 - m.R2 + 5 + Math.Sin(Time * 1.4 + m.Phase) * 1.2;
            return x;
        }

        public void OnMouseDown(bool left)
        {
            if (!left) { RightClick(); return; }
            if (!S.BallsEnabled || !ModOk()) return;

            var m = ManUnderCursor();
            if (m != null)
            {
                double mx = ManPos(m, out double mby, out _);
                m.X = mx; m.Y = mby;
                m.GrabDX = mx - CursorX;
                m.GrabDY = mby - CursorY;
                m.Held = true; m.Resting = false; m.Ref = null; m.Vy = 0;
                return;
            }

            var pick = BallUnderCursor();
            if (pick != null)
            {
                pick.GrabDX = pick.X - CursorX;
                pick.GrabDY = pick.Y - CursorY;
                pick.Held = true; pick.Resting = false; pick.Ref = null; pick.Vy = 0;
                pick.LastX = pick.X;
                return;
            }

            Surface s = SurfaceAt(CursorX, CursorY);
            if (s == null) s = BottomNear(CursorX, CursorY);
            if (s == null) return;
            int c = CellOf(s, CursorX);
            if (s.Snow[c] < S.MinSnowForBall) return;

            while (Balls.Count >= S.MaxBalls) { Puff(Balls[0].X, Balls[0].Y, 8); Balls.RemoveAt(0); }

            double line0 = s.Y - s.Snow[c];
            var b = new Ball
            {
                R = StartBallR,
                X = CursorX,
                Y = line0 - StartBallR,
                Held = true,
                Born = Time,
                GrabDX = 0,
                LastX = CursorX
            };
            b.GrabDY = b.Y - CursorY;
            Balls.Add(b);
            Scrape(s, b, 0.25);
            b.Y = s.Y - s.Snow[c] - b.R;
            Puff(CursorX, s.Y - s.Snow[c], 5);
        }

        void RightClick()
        {
            var m = ManUnderCursor();
            if (m != null)
            {
                double x = ManPos(m, out double by, out double ty);
                bool destroy = ShiftDown();
                Men.Remove(m);
                if (destroy) { Puff(x, by, 26); return; }
                double off = (m.R1 + m.R2) * 0.62;
                AddBall(x - off, by, m.R1);
                AddBall(x + off, ty, m.R2);
                Puff(x, by - m.R1, 10);
                return;
            }
            var b = BallUnderCursor();
            if (b != null) { Puff(b.X, b.Y, 12); Balls.Remove(b); }
        }

        void AddBall(double x, double y, double r)
        {
            while (Balls.Count >= S.MaxBalls) { Puff(Balls[0].X, Balls[0].Y, 8); Balls.RemoveAt(0); }
            Balls.Add(new Ball { X = x, Y = y, R = r, Vy = 0, Born = Time, LastX = x });
        }

        Surface RollingSurface(double x, double bottom)
        {
            Surface best = null;
            double bestD = 1e9;
            foreach (var s in Surfaces)
            {
                if (x < s.X || x > s.X + s.W) continue;
                if (s.Bottom && !S.BottomSnow) continue;
                int c = CellOf(s, x);
                if (s.Blocked[c]) continue;
                if (s.Snow[c] < 0.8) continue;
                double line = s.Y - s.Snow[c];
                if (bottom < line - 18 || bottom > line + 30) continue;
                double d = Math.Abs(line - bottom);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        double Scrape(Surface s, Ball b, double k)
        {
            double rad = Math.Max(9, b.R * 1.15);
            int c0 = Math.Max(0, (int)((b.X - rad - s.X) / Cell));
            int c1 = Math.Min(s.Cells - 1, (int)((b.X + rad - s.X) / Cell));
            double sum = 0;
            for (int i = c0; i <= c1; i++)
            {
                double cx = s.X + (i + 0.5) * Cell;
                double w = 1 - Math.Abs(cx - b.X) / rad;
                if (w <= 0) continue;
                double take = s.Snow[i] * w * k;
                s.Snow[i] -= take;
                sum += take;
            }
            if (sum > 0) b.R = Math.Min(MaxBallR, Math.Sqrt(b.R * b.R + sum * 1.1));
            return sum;
        }

        public void OnMouseUp()
        {
            for (int i = Men.Count - 1; i >= 0; i--)
            {
                var m = Men[i];
                if (!m.Held) continue;
                m.Held = false;
                m.Vy = 0;
                var s = SurfaceAt(m.X, CursorY);
                if (s != null) PlaceMan(m, s, CursorY);
            }

            for (int i = Balls.Count - 1; i >= 0; i--)
            {
                if (i >= Balls.Count) continue;
                var b = Balls[i];
                if (!b.Held) continue;
                b.Held = false;
                b.Vy = 0;
                b.Rolling = false;

                Ball hit = null;
                if (S.AutoSnowman)
                    foreach (var o in Balls)
                    {
                        if (o == b || !o.Resting) continue;
                        if (Math.Abs(b.X - o.X) < 0.45 * o.R && b.Y + b.R < o.Y - o.R * 0.15 && o.Y - b.Y < 280) { hit = o; break; }
                    }
                if (hit != null)
                {
                    MakeMan(hit);
                    Balls.Remove(b);
                    Balls.Remove(hit);
                    if (i > Balls.Count) i = Balls.Count;
                    continue;
                }

                var s = SurfaceAt(b.X, CursorY);
                if (s != null)
                {
                    b.Resting = true;
                    b.Ref = s;
                    b.Dx = Math.Max(b.R * 0.5, Math.Min(s.W - b.R * 0.5, b.X - s.X));
                    b.X = s.X + b.Dx;
                    b.Y = s.Y - s.Snow[CellOf(s, b.X)] - b.R;
                    Puff(b.X, b.Y + b.R, 6);
                }
            }
        }

        void PlaceMan(Snowman m, Surface s, double dropY)
        {
            m.Resting = true;
            m.Ref = s;
            m.Held = false;
            m.Vy = 0;
            m.Dx = Math.Max(m.R1 * 0.6, Math.Min(s.W - m.R1 * 0.6, m.X - s.X));
            m.X = s.X + m.Dx;

            double need = m.R1 * 4.4 + 6;
            if (s.Y >= need)
            {
                m.OnRoof = true;
                m.BaseDy = 0;
                m.Y = s.Y - s.Snow[CellOf(s, m.X)] - m.R1;
            }
            else
            {
                m.OnRoof = false;
                double baseLine = Math.Max(s.Y + need, Math.Min(s.Y + s.H - 4, dropY + m.R1));
                m.BaseDy = baseLine - s.Y;
                m.Y = baseLine - m.R1;
            }
            Puff(m.X, m.Y + m.R1, 8);
        }

        void UpdateBalls(double dt)
        {
            for (int i = Balls.Count - 1; i >= 0; i--)
            {
                if (i >= Balls.Count) continue;
                var b = Balls[i];

                if (b.Held)
                {
                    b.X = CursorX + b.GrabDX;
                    double want = CursorY + b.GrabDY;
                    var rs = RollingSurface(b.X, want + b.R);
                    if (rs != null)
                    {
                        int c = CellOf(rs, b.X);
                        b.Y = rs.Y - rs.Snow[c] - b.R;
                        double moved = b.X - b.LastX;
                        b.Roll += moved / Math.Max(6, b.R);
                        if (Math.Abs(moved) > 0.4)
                        {
                            double k = 0.10 * Math.Min(1.0, Math.Abs(moved) / 5.0);
                            double got = Scrape(rs, b, k);
                            b.Y = rs.Y - rs.Snow[c] - b.R;
                            if (got > 1.2 && Sparks.Count < 340 && Rnd.NextDouble() < 0.4)
                                Puff(b.X - Math.Sign(moved) * b.R * 0.7, rs.Y - rs.Snow[c], 1);
                        }
                        b.Rolling = true;
                    }
                    else
                    {
                        b.Y = want;
                        b.Rolling = false;
                    }
                    b.LastX = b.X;
                    continue;
                }

                if (b.Resting)
                {
                    if (b.Ref == null || !Surfaces.Contains(b.Ref)) { b.Resting = false; b.Ref = null; b.Vy = 0; }
                    else
                    {
                        b.X = b.Ref.X + b.Dx;
                        b.Y = b.Ref.Y - b.Ref.Snow[CellOf(b.Ref, b.X)] - b.R;
                        continue;
                    }
                }

                double prevBottom = b.Y + b.R;
                b.Vy += 900 * dt;
                b.Y += b.Vy * dt;
                b.Roll += b.Vy * dt / Math.Max(6, b.R) * 0.3;

                if (S.AutoSnowman)
                {
                    Ball hit = null;
                    foreach (var o in Balls)
                    {
                        if (o == b || !o.Resting) continue;
                        if (Math.Abs(b.X - o.X) < 0.45 * o.R && b.Y + b.R >= o.Y - o.R - 2 && b.Y < o.Y) { hit = o; break; }
                    }
                    if (hit != null)
                    {
                        MakeMan(hit);
                        Balls.Remove(b);
                        Balls.Remove(hit);
                        if (i > Balls.Count) i = Balls.Count;
                        continue;
                    }
                }

                var s = FindLanding(b.X, b.Y + b.R, prevBottom, false, 18, out double land, true);
                if (s != null)
                { b.Y = land - b.R; b.Resting = true; b.Ref = s; b.Dx = b.X - s.X; b.Vy = 0; Puff(b.X, land, 6); }
                else if (b.Y - b.R > H + 80) Balls.RemoveAt(i);
            }
        }

        void MakeMan(Ball lower)
        {
            if (lower.Ref == null) return;
            while (Men.Count >= 10)
            {
                var old = Men[0];
                double ox = ManPos(old, out double oby, out _);
                Puff(ox, oby, 16);
                Men.RemoveAt(0);
            }
            var m = new Snowman
            {
                Ref = lower.Ref,
                Dx = lower.X - lower.Ref.X,
                R1 = Math.Max(18, lower.R),
                Phase = Rnd.NextDouble() * 6.283,
                Hat = S.HatStyle == 2 ? Rnd.Next(0, 2) : S.HatStyle,
                BlinkAt = Time + 2 + Rnd.NextDouble() * 4,
                WaveAt = Time + 4 + Rnd.NextDouble() * 8,
                Born = Time,
                Resting = true,
                OnRoof = true,
                BaseDy = 0,
                X = lower.X
            };
            m.R2 = m.R1 * 0.72;
            m.Y = lower.Ref.Y - lower.Ref.Snow[CellOf(lower.Ref, lower.X)] - m.R1;
            Men.Add(m);
            Puff(lower.X, lower.Y, 16);
        }

        void UpdateMen(double dt)
        {
            for (int i = Men.Count - 1; i >= 0; i--)
            {
                var m = Men[i];
                if (m.Held)
                {
                    m.X = CursorX + m.GrabDX;
                    m.Y = CursorY + m.GrabDY;
                }
                else
                {
                    if (m.Resting)
                    {
                        if (m.Ref == null || !Surfaces.Contains(m.Ref)) { m.Resting = false; m.Ref = null; m.Vy = 0; }
                        else
                        {
                            m.X = m.Ref.X + m.Dx;
                            double line = m.Ref.Y + m.BaseDy;
                            if (m.OnRoof) line -= m.Ref.Snow[CellOf(m.Ref, m.X)];
                            m.Y = line - m.R1;
                        }
                    }
                    if (!m.Resting)
                    {
                        double prev = m.Y + m.R1;
                        m.Vy += 900 * dt;
                        m.Y += m.Vy * dt;
                        var s = FindLanding(m.X, m.Y + m.R1, prev, false, 22, out double land, true);
                        if (s != null)
                        {
                            m.Y = land - m.R1;
                            m.Resting = true;
                            m.OnRoof = true;
                            m.BaseDy = 0;
                            m.Ref = s;
                            m.Dx = m.X - s.X;
                            m.Vy = 0;
                            Puff(m.X, land, 8);
                        }
                        else if (m.Y - m.R1 > H + 150) { Men.RemoveAt(i); continue; }
                    }
                }
                if (Time > m.BlinkAt) { m.BlinkUntil = Time + 0.13; m.BlinkAt = Time + 2.5 + Rnd.NextDouble() * 4; }
                if (Time > m.WaveAt) { m.WaveUntil = Time + 1.3; m.WaveAt = Time + 8 + Rnd.NextDouble() * 9; }
            }
        }
    }
}
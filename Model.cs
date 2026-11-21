using System;

namespace FrostByte
{
    internal class Surface
    {
        public IntPtr Hwnd;
        public double X, Y, W, H;
        public bool Taskbar, Bottom;
        public int Z;
        public int Cells;
        public double[] Snow = Array.Empty<double>();
        public bool[] Blocked = Array.Empty<bool>();
        public int PlowState;
        public double PlowX, PlowDir, PlowWait, PlowCool;
        public bool PlowForce;
    }

    internal class Flake
    {
        public double X, Y, Vy, Size, Phase, Sway;
        public bool Front;
        public int Shade;
    }

    internal class Ball
    {
        public double X, Y, Vy, R, Dx, Born;
        public double GrabDX, GrabDY;
        public double Roll, LastX;
        public bool Held, Resting, Rolling;
        public Surface Ref;
    }

    internal class Snowman
    {
        public Surface Ref;
        public double Dx, R1, R2, Phase, Born;
        public double X, Y, Vy, GrabDX, GrabDY;
        public double BaseDy;
        public bool OnRoof;
        public bool Held, Resting;
        public int Hat;
        public double BlinkAt, BlinkUntil, WaveAt, WaveUntil;
    }

    internal class Spark
    {
        public double X, Y, Vx, Vy, Life, Max, Size;
        public int Kind;
    }
}
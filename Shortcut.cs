using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace FrostByte
{
    internal static class Shortcut
    {
        public static void Create()
        {
            try
            {
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    WF.MessageBox.Show("Не вижу exe-файл. Собери готовый exe командой dotnet publish и запусти его.", "Desktop Snow");
                    return;
                }

                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FrostByte");
                Directory.CreateDirectory(dir);
                string icoPath = Path.Combine(dir, "snow.ico");
                SaveIcon(icoPath);

                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FrostByte.lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    WF.MessageBox.Show("Windows не разрешает создать ярлык автоматически. Сделай вручную: правый клик по FrostByte.exe → Отправить → Рабочий стол (создать ярлык).", "Desktop Snow");
                    return;
                }

                object shell = Activator.CreateInstance(shellType);
                object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type t = sc.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { exe });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(exe) });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { icoPath + ",0" });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Снегопад, снеговики и гирлянда на рабочем столе" });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                Marshal.ReleaseComObject(sc);
                Marshal.ReleaseComObject(shell);

                WF.MessageBox.Show("Ярлык «Снегопад» появился на рабочем столе.", "Desktop Snow");
            }
            catch (Exception ex)
            {
                WF.MessageBox.Show("Не получилось: " + ex.Message + "\nМожно вручную: правый клик по exe → Отправить → Рабочий стол (создать ярлык).", "Desktop Snow");
            }
        }

        static void SaveIcon(string path)
        {
            using (var bmp = new SD.Bitmap(64, 64))
            {
                using (var g = SD.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SD.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(SD.Color.Transparent);
                    float cx = 32, cy = 32, r = 25;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        var col = pass == 0 ? SD.Color.FromArgb(255, 16, 48, 84) : SD.Color.FromArgb(255, 235, 248, 255);
                        using (var pen = new SD.Pen(col, pass == 0 ? 9f : 4.5f))
                        {
                            pen.StartCap = SD.Drawing2D.LineCap.Round;
                            pen.EndCap = SD.Drawing2D.LineCap.Round;
                            for (int i = 0; i < 6; i++)
                            {
                                double a = i * Math.PI / 3;
                                float dx = (float)(Math.Cos(a) * r), dy = (float)(Math.Sin(a) * r);
                                g.DrawLine(pen, cx - dx, cy - dy, cx + dx, cy + dy);
                                float bx = cx + dx * 0.6f, by = cy + dy * 0.6f;
                                for (int s = -1; s <= 1; s += 2)
                                {
                                    double a2 = a + s * 0.75;
                                    g.DrawLine(pen, bx, by, bx + (float)(Math.Cos(a2) * r * 0.3), by + (float)(Math.Sin(a2) * r * 0.3));
                                }
                            }
                        }
                    }
                }
                IntPtr h = bmp.GetHicon();
                using (var ic = SD.Icon.FromHandle(h))
                using (var fs = File.Create(path))
                    ic.Save(fs);
                Native.DestroyIcon(h);
            }
        }
    }
}
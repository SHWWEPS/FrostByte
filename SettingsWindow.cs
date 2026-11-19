using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace DesktopSnow
{
    internal class SettingsWindow : Window
    {
        readonly Settings S; readonly SnowEngine E;
        static readonly Brush Fg = new SolidColorBrush(Color.FromRgb(232, 238, 247));
        static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(30, 34, 42));

        public SettingsWindow(Settings s, SnowEngine e)
        {
            S = s; E = e;
            Title = "Desktop Snow — настройки";
            Width = 540; Height = 680;
            Background = Bg; Foreground = Fg;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontSize = 13;

            var tabs = new TabControl { Background = Bg, BorderThickness = new Thickness(0), Margin = new Thickness(6) };
            tabs.Items.Add(Tab("Снег", SnowPage()));
            tabs.Items.Add(Tab("Машина", PlowPage()));
            tabs.Items.Add(Tab("Снежки", BallPage()));
            tabs.Items.Add(Tab("Гирлянда", GarlandPage()));
            tabs.Items.Add(Tab("Общие", GeneralPage()));
            Content = tabs;
        }

        TabItem Tab(string title, UIElement body)
        {
            var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body };
            return new TabItem { Header = title, Content = sc };
        }

        StackPanel Page() { return new StackPanel { Margin = new Thickness(14) }; }

        void Check(StackPanel p, string text, Func<bool> get, Action<bool> set)
        {
            var cb = new CheckBox { Content = text, IsChecked = get(), Foreground = Fg, Margin = new Thickness(0, 6, 0, 6) };
            cb.Checked += (a, b) => { set(true); S.Touch(); };
            cb.Unchecked += (a, b) => { set(false); S.Touch(); };
            p.Children.Add(cb);
        }

        void Slide(StackPanel p, string text, double min, double max, double val, double tick, Action<double> set, string unit = "")
        {
            var lbl = new TextBlock { Foreground = Fg, Margin = new Thickness(0, 10, 0, 2), Text = text + ": " + Math.Round(val, 2) + unit };
            var sl = new Slider { Minimum = min, Maximum = max, Value = val, IsSnapToTickEnabled = tick > 0, TickFrequency = tick > 0 ? tick : 1 };
            sl.ValueChanged += (a, b) => { set(sl.Value); lbl.Text = text + ": " + Math.Round(sl.Value, 2) + unit; S.Touch(); };
            p.Children.Add(lbl); p.Children.Add(sl);
        }

        void Combo(StackPanel p, string text, string[] items, int idx, Action<int> set)
        {
            p.Children.Add(new TextBlock { Text = text, Foreground = Fg, Margin = new Thickness(0, 10, 0, 2) });
            var cb = new ComboBox { SelectedIndex = Math.Max(0, Math.Min(items.Length - 1, idx)) };
            foreach (var i in items) cb.Items.Add(i);
            cb.SelectionChanged += (a, b) => { set(cb.SelectedIndex); S.Touch(); };
            p.Children.Add(cb);
        }

        void Btn(StackPanel p, string text, Action act)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(10, 6, 10, 6) };
            b.Click += (a, e2) => act();
            p.Children.Add(b);
        }

        UIElement SnowPage()
        {
            var p = Page();
            Check(p, "Снегопад включён", () => S.SnowEnabled, v => S.SnowEnabled = v);
            Slide(p, "Количество снежинок", 0, 2000, S.FlakeCount, 50, v => S.FlakeCount = (int)v);
            Slide(p, "Размер снежинок", 0.5, 2.5, S.FlakeScale, 0.1, v => S.FlakeScale = v, "x");
            Slide(p, "Скорость падения", 0.2, 3, S.FallSpeed, 0.1, v => S.FallSpeed = v, "x");
            Slide(p, "Сила ветра", -80, 80, S.WindStrength, 5, v => S.WindStrength = v);
            Check(p, "Порывы ветра", () => S.Gusts, v => S.Gusts = v);
            Check(p, "Снег накапливается", () => S.Accumulate, v => S.Accumulate = v);
            Slide(p, "Скорость накопления", 0.2, 5, S.AccumRate, 0.1, v => S.AccumRate = v, "x");
            Slide(p, "Макс. сугроб на окнах", 5, 80, S.MaxWindowSnow, 1, v => S.MaxWindowSnow = v, " px");
            Slide(p, "Макс. сугроб на панели задач", 10, 120, S.MaxTaskbarSnow, 1, v => S.MaxTaskbarSnow = v, " px");
            Check(p, "Снег на нижней кромке экрана", () => S.BottomSnow, v => S.BottomSnow = v);
            Check(p, "Медленное таяние", () => S.Melt, v => S.Melt = v);
            Btn(p, "Очистить весь снег", () => E.ClearAllSnow());
            return p;
        }

        UIElement PlowPage()
        {
            var p = Page();
            Check(p, "Снегоуборочная машина включена", () => S.PlowEnabled, v => S.PlowEnabled = v);
            Slide(p, "Порог срабатывания (средняя высота снега)", 5, 80, S.PlowThreshold, 1, v => S.PlowThreshold = v, " px");
            Slide(p, "Пауза между заездами", 10, 600, S.PlowCooldown, 10, v => S.PlowCooldown = v, " c");
            Slide(p, "Скорость машины", 0.3, 3, S.PlowSpeed, 0.1, v => S.PlowSpeed = v, "x");
            Check(p, "Заезжает слева (иначе справа)", () => S.PlowFromLeft, v => S.PlowFromLeft = v);
            Btn(p, "Вызвать машину сейчас", () => E.CallPlow());
            p.Children.Add(new TextBlock { Foreground = Fg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0),
                Text = "Машина приедет, когда средний сугроб на панели задач продержится выше порога 3 секунды." });
            return p;
        }

        UIElement BallPage()
        {
            var p = Page();
            Check(p, "Собирать снежок: навести на окно + ЛКМ", () => S.BallsEnabled, v => S.BallsEnabled = v);
            Slide(p, "Минимум снега для снежка", 2, 30, S.MinSnowForBall, 1, v => S.MinSnowForBall = v, " px");
            Combo(p, "Дополнительная клавиша (чтобы не мешать кликам)", new[] { "не нужна", "Alt", "Ctrl", "Shift" }, S.BallModifier, v => S.BallModifier = v);
            Check(p, "Тащить снежок, пока держу ЛКМ", () => S.BallDrag, v => S.BallDrag = v);
            Slide(p, "Максимум снежков", 5, 80, S.MaxBalls, 1, v => S.MaxBalls = (int)v);
            Check(p, "Автоматически лепить снеговика из 2 снежков", () => S.AutoSnowman, v => S.AutoSnowman = v);
            Combo(p, "Шапка снеговика", new[] { "Санта", "Эльф", "Случайная" }, S.HatStyle, v => S.HatStyle = v);
            p.Children.Add(new TextBlock { Foreground = Fg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0),
                Text = "Клик всегда проходит в приложение — программа его не перехватывает. Отпусти снежок над другим снежком, чтобы получился снеговик. ПКМ по снеговику — разобрать." });
            return p;
        }

        UIElement GarlandPage()
        {
            var p = Page();
            Check(p, "Гирлянда включена", () => S.GarlandEnabled, v => S.GarlandEnabled = v);
            Check(p, "На окнах", () => S.GarlandWindows, v => S.GarlandWindows = v);
            Check(p, "На панели задач", () => S.GarlandTaskbar, v => S.GarlandTaskbar = v);
            Slide(p, "Шаг лампочек", 12, 60, S.BulbSpacing, 1, v => S.BulbSpacing = v, " px");
            Combo(p, "Режим", new[] { "Бегущая волна", "Плавное мерцание", "Случайное мигание", "Статично" }, S.GarlandMode, v => S.GarlandMode = v);
            Slide(p, "Скорость анимации", 0.1, 4, S.GarlandSpeed, 0.1, v => S.GarlandSpeed = v, "x");
            Slide(p, "Яркость", 0.2, 1.5, S.GarlandBrightness, 0.05, v => S.GarlandBrightness = v, "x");
            return p;
        }

        UIElement GeneralPage()
        {
            var p = Page();
            Slide(p, "Частота кадров — плавность снега, гирлянды, машины", 15, 144, S.Fps, 1, v => S.Fps = (int)v, " FPS");
            Slide(p, "Слежение за окнами — как быстро сугроб едет за окном", 2, 60, S.WindowPollHz, 1, v => S.WindowPollHz = (int)v, " раз/с");
            Check(p, "Пауза в полноэкранных приложениях и играх", () => S.PauseFullscreen, v => S.PauseFullscreen = v);
            Check(p, "Пауза при работе от батареи", () => S.PauseOnBattery, v => S.PauseOnBattery = v);
            Check(p, "Запускать вместе с Windows", () => S.Autostart, v => { S.Autostart = v; SetAutostart(v); });
            Btn(p, "Показать окна, на которых лежит снег", () =>
            {
                var sb = new System.Text.StringBuilder();
                foreach (var w in E.Surfaces)
                {
                    if (w.Bottom) continue;
                    var t = new System.Text.StringBuilder(256);
                    Native.GetWindowText(w.Hwnd, t, 256);
                    sb.AppendLine((w.Taskbar ? "[ПАНЕЛЬ] " : "") + t + " | " + Native.ClassOf(w.Hwnd) + " | " + (int)w.W + "x" + (int)w.H);
                }
                System.Windows.MessageBox.Show(sb.ToString(), "Окна со снегом");
            });
            Btn(p, "Сбросить все настройки", () =>
            {
                S.Reset(); Hide();
                System.Windows.MessageBox.Show("Настройки сброшены. Открой окно настроек заново, чтобы увидеть новые значения.", "Desktop Snow");
            });
            p.Children.Add(new TextBlock
            {
                Foreground = Fg,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 14, 0, 0),
                Text = "Частота кадров выше частоты монитора не прыгнет: на 60 Гц экране максимум ~60 FPS. Слежение за окнами — отдельная нагрузка: 30 подходит почти всем, 60 если таскаешь окна и хочешь идеальную точность. Окно настроек закрывается крестиком, программа остаётся в трее."
            });
            return p;
        }

        static void SetAutostart(bool on)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (k == null) return;
                if (on) k.SetValue("DesktopSnow", "\"" + Environment.ProcessPath + "\"");
                else k.DeleteValue("DesktopSnow", false);
            }
            catch { }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        { e.Cancel = true; Hide(); S.Flush(); }
    }
}
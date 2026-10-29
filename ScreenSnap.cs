using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    internal static class Program
    {
        private static System.Threading.Mutex _single;

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                ScreenGrab.Log("UNHANDLED " + e.ExceptionObject);
            };
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                ScreenGrab.Log("THREAD " + e.Exception);
            };
            ScreenGrab.Log("start pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + " args=" + string.Join(",", args));

            bool firstInstance;
            _single = new System.Threading.Mutex(true, "ScreenSnap.SingleInstance.v1", out firstInstance);
            if (!firstInstance)
            {
                ScreenGrab.Log("another instance is already running, exiting");
                return;
            }

            try { NativeMethods.SetProcessDPIAware(); } catch { }

            if (args.Length > 0 && (args[0] == "--full" || args[0] == "--shot"))
            {
                ScreenGrab.Log("cli start args=" + string.Join(",", args));
                string p = ScreenGrab.FullScreen();
                ScreenGrab.Log("cli done p=" + (p ?? "null"));
                Environment.Exit(p == null ? 2 : 0);
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext());
            }
            catch (Exception ex)
            {
                ScreenGrab.Log("FATAL " + ex);
                throw;
            }
        }
    }

    internal static class ScreenGrab
    {
        private static string _folder;

        internal static string Folder
        {
            get
            {
                if (_folder != null) return _folder;

                System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string>();
                string custom = Environment.GetEnvironmentVariable("SCREENSNAP_DIR");
                if (!string.IsNullOrEmpty(custom)) candidates.Add(custom);
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ScreenSnap"));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ScreenSnap"));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenSnap"));

                foreach (string c in candidates)
                {
                    try
                    {
                        Directory.CreateDirectory(c);
                        _folder = c;
                        Log("dir ok " + c);
                        return c;
                    }
                    catch (Exception ex)
                    {
                        Log("dir fail " + c + " :: " + ex.GetType().Name);
                    }
                }
                return candidates[candidates.Count - 1];
            }
        }

        internal static readonly string LogFile = Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath), "snap-log.txt");

        internal static void Log(string msg)
        {
            try { File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine); }
            catch { }
        }

        internal static Bitmap Grab()
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            Bitmap bmp = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(vs.Location, Point.Empty, vs.Size, CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }

        internal static string Save(Bitmap bmp)
        {
            string dir = Folder;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, "ScreenSnap_" + stamp + ".png");
            int n = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(dir, "ScreenSnap_" + stamp + "_" + n + ".png");
                n++;
            }
            bmp.Save(path, ImageFormat.Png);
            return path;
        }

        internal static string FullScreen()
        {
            try
            {
                using (Bitmap b = Grab())
                {
                    Log("grab ok " + b.Width + "x" + b.Height);
                    string p = Save(b);
                    Log("saved " + p);
                    return p;
                }
            }
            catch (Exception ex)
            {
                Log("FAIL " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly HotkeyWindow _hotkeys;

        internal TrayContext()
        {
            Icon icon = null;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            if (icon == null) icon = SystemIcons.Application;

            _tray = new NotifyIcon();
            _tray.Icon = icon;
            _tray.Text = "ScreenSnap 截图";
            _tray.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("框选截图   Ctrl+Alt+S", null, delegate { RegionShot(); });
            menu.Items.Add("全屏截图   Ctrl+Alt+F", null, delegate { FullShot(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("打开截图文件夹", null, delegate { OpenFolder(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Quit(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { RegionShot(); };

            _hotkeys = new HotkeyWindow();
            _hotkeys.HotkeyPressed += delegate (int id)
            {
                ScreenGrab.Log("dispatch id=" + id);
                if (id == 1) RegionShot(); else FullShot();
            };

            ScreenGrab.Log("tray ready: " + _hotkeys.Describe());
            try { _tray.ShowBalloonTip(5000, "ScreenSnap 已就绪", _hotkeys.Describe(), ToolTipIcon.Info); } catch { }
        }

        private void FullShot()
        {
            try
            {
                using (Bitmap b = ScreenGrab.Grab())
                {
                    ScreenGrab.Log("grab ok " + b.Width + "x" + b.Height);
                    string p = ScreenGrab.Save(b);
                    ScreenGrab.Log("saved " + p);
                    try { Clipboard.SetImage(b); } catch { }
                    Notify(p);
                }
            }
            catch (Exception ex)
            {
                ScreenGrab.Log("FAIL " + ex.GetType().Name + ": " + ex.Message);
                Notify(null);
            }
        }

        private void RegionShot()
        {
            try
            {
                using (RegionForm f = new RegionForm())
                {
                    ScreenGrab.Log("region overlay open");
                    DialogResult dr = f.ShowDialog();
                    ScreenGrab.Log("region closed result=" + dr);
                    if (dr == DialogResult.OK && f.Result != null)
                    {
                        string p = ScreenGrab.Save(f.Result);
                        try { Clipboard.SetImage(f.Result); } catch { }
                        Notify(p);
                    }
                }
            }
            catch { }
        }

        private void Notify(string path)
        {
            if (path == null) return;
            try { _tray.ShowBalloonTip(2500, "截图已保存", path, ToolTipIcon.Info); }
            catch { }
        }

        private void OpenFolder()
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + ScreenGrab.Folder + "\""); }
            catch { }
        }

        private void Quit()
        {
            _tray.Visible = false;
            _tray.Dispose();
            _hotkeys.Dispose();
            ExitThread();
        }
    }

    internal sealed class HotkeyWindow : Form
    {
        internal event Action<int> HotkeyPressed;
        private bool _registered;
        private bool _tried;

        internal HotkeyWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-3000, -3000, 1, 1);
            Opacity = 0;
            CreateHandle();
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
            if (!IsHandleCreated)
            {
                CreateHandle();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeys();
        }

        internal string RegionName { get; private set; }
        internal string FullName { get; private set; }

        internal string Describe()
        {
            return "框选 " + (RegionName ?? "未注册") + "  |  全屏 " + (FullName ?? "未注册");
        }

        private void RegisterHotkeys()
        {
            if (_tried)
            {
                return;
            }
            _tried = true;

            const uint ALT = 0x0001u, CTRL = 0x0002u, SHIFT = 0x0004u, NOREPEAT = 0x4000u;

            uint[][] region =
            {
                new uint[] { CTRL | ALT | NOREPEAT, 0x53u },
                new uint[] { CTRL | SHIFT | NOREPEAT, 0x53u },
                new uint[] { ALT | SHIFT | NOREPEAT, 0x53u }
            };
            string[] regionNames = { "Ctrl+Alt+S", "Ctrl+Shift+S", "Alt+Shift+S" };

            uint[][] full =
            {
                new uint[] { CTRL | ALT | NOREPEAT, 0x46u },
                new uint[] { CTRL | SHIFT | NOREPEAT, 0x46u },
                new uint[] { ALT | SHIFT | NOREPEAT, 0x46u }
            };
            string[] fullNames = { "Ctrl+Alt+F", "Ctrl+Shift+F", "Alt+Shift+F" };

            RegionName = TryRegister(1, region, regionNames);
            FullName = TryRegister(2, full, fullNames);
            _registered = RegionName != null || FullName != null;
            ScreenGrab.Log("hotkeys region=" + (RegionName ?? "FAILED") + " full=" + (FullName ?? "FAILED"));
        }

        private string TryRegister(int id, uint[][] combos, string[] names)
        {
            for (int i = 0; i < combos.Length; i++)
            {
                bool ok = NativeMethods.RegisterHotKey(Handle, id, combos[i][0], combos[i][1]);
                int err = Marshal.GetLastWin32Error();
                ScreenGrab.Log("register id=" + id + " " + names[i] + " ok=" + ok + " err=" + err);
                if (ok) return names[i];
            }
            return null;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)
            {
                ScreenGrab.Log("hotkey fired id=" + m.WParam.ToInt32());
                Action<int> handler = HotkeyPressed;
                if (handler != null) handler(m.WParam.ToInt32());
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(Handle, 1);
                NativeMethods.UnregisterHotKey(Handle, 2);
            }
            base.OnFormClosing(e);
        }
    }

    internal sealed class RegionForm : Form
    {
        private readonly Bitmap _bright;
        private readonly Bitmap _dim;
        private Point _anchor;
        private bool _dragging;
        private Rectangle _sel;

        internal Bitmap Result;

        internal RegionForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            DoubleBuffered = true;

            _bright = ScreenGrab.Grab();
            _dim = new Bitmap(_bright.Width, _bright.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_dim))
            {
                g.DrawImageUnscaled(_bright, 0, 0);
                using (SolidBrush shade = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                {
                    g.FillRectangle(shade, 0, 0, _dim.Width, _dim.Height);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(_dim, 0, 0);

            if (_sel.Width > 0 && _sel.Height > 0)
            {
                e.Graphics.DrawImage(_bright, _sel, _sel, GraphicsUnit.Pixel);
                using (Pen pen = new Pen(Color.FromArgb(77, 107, 254), 1))
                {
                    e.Graphics.DrawRectangle(pen, _sel.X, _sel.Y, _sel.Width - 1, _sel.Height - 1);
                }

                string label = _sel.Width + " x " + _sel.Height;
                using (Font f = new Font("Segoe UI", 9f))
                {
                    SizeF sz = e.Graphics.MeasureString(label, f);
                    float x = _sel.X + 6;
                    float y = _sel.Y + 6;
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                    {
                        e.Graphics.FillRectangle(bg, x, y, sz.Width + 8, sz.Height + 2);
                    }
                    e.Graphics.DrawString(label, f, Brushes.White, x + 4, y + 1);
                }
            }

            base.OnPaint(e);
        }

        private static Rectangle MakeRect(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            return new Rectangle(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { Close(); return; }
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _anchor = e.Location;
                _sel = new Rectangle(e.Location, Size.Empty);
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _sel = MakeRect(_anchor, e.Location);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging && e.Button == MouseButtons.Left)
            {
                _dragging = false;
                Rectangle r = MakeRect(_anchor, e.Location);
                r.Intersect(new Rectangle(0, 0, _bright.Width, _bright.Height));

                if (r.Width >= 2 && r.Height >= 2)
                {
                    Result = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(Result))
                    {
                        g.DrawImage(_bright, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
                    }
                    DialogResult = DialogResult.OK;
                }
                Close();
            }
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); return; }
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_bright != null) _bright.Dispose();
                if (_dim != null) _dim.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace ScreenInk
{
    internal static class Win32
    {
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int SWP_NOMOVE = 0x0002;
        public const int SWP_NOSIZE = 0x0001;
        public const int SWP_NOACTIVATE = 0x0010;
        public const int SWP_FRAMECHANGED = 0x0020;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINTAPI { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINTAPI ptMinPosition;
            public POINTAPI ptMaxPosition;
            public RECT rcNormalPosition;
        }


        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        public const int SW_RESTORE = 9;
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, int flags);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int value);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
        public const uint GW_OWNER = 4;
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int key);
        public const int VK_MENU = 0x12;
        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr instance, int cursor);
        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr cursor);
        public const int IDC_ARROW = 32512;
        public const int IDC_CROSS = 32515;
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        public const uint MOD_ALT = 1;
        public const uint MOD_CONTROL = 2;
    }

    internal class WindowInfo
    {
        public IntPtr Handle;
        public string Title;
        public string ProcessName;
        public Rectangle Bounds;
        public bool Minimized;
        public string Display { get { return Title + "  [" + ProcessName + "]" + (Minimized ? "（最小化）" : ""); } }
        public override string ToString() { return Display; }
    }

    internal static class WindowList
    {
        public static List<WindowInfo> Scan()
        {
            List<WindowInfo> list = new List<WindowInfo>();
            int self = Process.GetCurrentProcess().Id;
            Win32.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                try
                {
                    if (!Win32.IsWindowVisible(h)) return true;
                    if (Win32.GetWindowTextLength(h) <= 0) return true;
                    uint pid;
                    Win32.GetWindowThreadProcessId(h, out pid);
                    if ((int)pid == self) return true;
                    StringBuilder sb = new StringBuilder(512);
                    Win32.GetWindowText(h, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (title.Length == 0) return true;
                    if (title == "Program Manager") return true;
                    string pname = "";
                    try { using (Process p = Process.GetProcessById((int)pid)) pname = p.ProcessName; } catch { }
                    string[] skip = { "TextInputHost", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "LockApp", "Progman", "WorkerW" };
                    foreach (string s in skip) if (pname.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;
                    Rectangle rect;
                    bool minimized = Win32.IsIconic(h);
                    if (minimized)
                    {
                        Win32.WINDOWPLACEMENT wp = new Win32.WINDOWPLACEMENT();
                        wp.length = Marshal.SizeOf(typeof(Win32.WINDOWPLACEMENT));
                        if (!Win32.GetWindowPlacement(h, ref wp)) return true;
                        rect = new Rectangle(wp.rcNormalPosition.Left, wp.rcNormalPosition.Top,
                            wp.rcNormalPosition.Right - wp.rcNormalPosition.Left,
                            wp.rcNormalPosition.Bottom - wp.rcNormalPosition.Top);
                    }
                    else
                    {
                        Win32.RECT r;
                        if (!Win32.GetWindowRect(h, out r)) return true;
                        rect = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                    }
                    if (rect.Width < 40 || rect.Height < 30) return true;
                    WindowInfo wi = new WindowInfo();
                    wi.Handle = h;
                    wi.Title = title;
                    wi.ProcessName = pname;
                    wi.Minimized = minimized;
                    wi.Bounds = rect;
                    list.Add(wi);
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            list.Sort(delegate(WindowInfo a, WindowInfo b) { return string.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase); });
            return list;
        }

        public static Rectangle BoundsOf(IntPtr hwnd)
        {
            Win32.RECT r;
            if (Win32.GetWindowRect(hwnd, out r)) return new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            return Rectangle.Empty;
        }


    }

    internal class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private IntPtr _hook = IntPtr.Zero;
        private LowLevelMouseProc _proc;
        public Func<int, Point, bool> Handler;

        public bool Install()
        {
            if (_hook != IntPtr.Zero) return true;
            _proc = Callback;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        public void Uninstall()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && Handler != null)
                {
                    MSLLHOOKSTRUCT data = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    if (Handler(wParam.ToInt32(), new Point(data.pt.x, data.pt.y))) return new IntPtr(1);
                }
            }
            catch { }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    internal class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;
        public Func<int, int, bool> Handler;

        public bool Install()
        {
            if (_hook != IntPtr.Zero) return true;
            _proc = Callback;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        public void Uninstall()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && Handler != null)
                {
                    KBDLLHOOKSTRUCT data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    if (Handler(wParam.ToInt32(), (int)data.vkCode)) return new IntPtr(1);
                }
            }
            catch { }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    internal class Shape
    {
        public string Id = "";
        public string Tool = "line";
        public string Text = "";
        public string CreatedAt = "";
        public bool Relative;
        public List<Point> Points = new List<Point>();
        public Color Color = Color.FromArgb(239, 68, 68);
    }

    internal class OverlayForm : Form
    {
        private readonly List<Shape> _shapes = new List<Shape>();
        private Shape _current;
        private Shape _selected;
        private int _selectedHandle = -1;
        private Point _selectStart;
        private List<Point> _selectOriginal;
        private bool _selectChanged;
        private bool _down;
        private Point _hover;
        private bool _hasHover;
        private string _tool = "line";
        private Color _inkColor = Color.FromArgb(239, 68, 68);
        private WindowInfo _target;
        private bool _targetMinimized;
        private System.Windows.Forms.Timer _tracker;
        private System.Windows.Forms.Timer _cursorTimer;
        private readonly Action<string> _status;
        private readonly MouseHook _hook = new MouseHook();
        private readonly KeyboardHook _keyboardHook = new KeyboardHook();
        private InlineEditorForm _editor;
        private bool _editorOpen;
        private bool _clickThrough = true;
        private System.Windows.Forms.Timer _paintTimer;
        private System.Windows.Forms.Timer _inputTimer;
        private Bitmap _buffer;
        private Point _pendingScreen;
        private bool _pendingDown;
        private bool _pendingMove;
        private bool _pendingUp;
        private bool _pendingFinish;
        private Rectangle _dirty;
        private bool _dirtyPending;
        private volatile bool _altDown;
        private bool _altReady;
        private DateTime _altDownAt = DateTime.MinValue;
        private bool _keyboardHookOk;
        private static readonly IntPtr ArrowCursor = Win32.LoadCursor(IntPtr.Zero, Win32.IDC_ARROW);
        private static readonly IntPtr CrossCursor = Win32.LoadCursor(IntPtr.Zero, Win32.IDC_CROSS);

        public OverlayForm(Action<string> status)
        {
            _status = status;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            _paintTimer = new System.Windows.Forms.Timer();
            _paintTimer.Interval = 15;
            _paintTimer.Tick += delegate
            {
                _paintTimer.Stop();
                if (!_dirtyPending) return;
                Rectangle d = _dirty;
                _dirty = Rectangle.Empty;
                _dirtyPending = false;
                RenderBuffer(d);
                Invalidate(d);
            };
            Text = "ScreenInk Overlay";
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        public Func<Point, bool> IsExempt { get; set; }

        private void SetClickThrough(bool on)
        {
            if (_clickThrough == on) return;
            _clickThrough = on;
            if (!IsHandleCreated) return;
            int ex = Win32.GetWindowLong(this.Handle, Win32.GWL_EXSTYLE);
            if (on) ex |= Win32.WS_EX_TRANSPARENT;
            else ex &= ~Win32.WS_EX_TRANSPARENT;
            Win32.SetWindowLong(this.Handle, Win32.GWL_EXSTYLE, ex);
            Win32.SetWindowPos(this.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
            this.Cursor = on ? Cursors.Default : Cursors.Cross;
        }

        public string Tool
        {
            get { return _tool; }
            set { _tool = value; }
        }

        public Color InkColor
        {
            get { return _inkColor; }
            set { _inkColor = value; }
        }

        public bool PassThrough { get; set; }
        public bool IsActive { get { return this.Visible; } }
        public bool ModifierDown { get { return DrawModifierDown(); } }
        public WindowInfo CurrentTarget { get { return _target; } }
        public string CurrentTool { get { return _tool; } }
        public Rectangle CurrentBounds { get { return this.Bounds; } }
        public int ShapeCount { get { return _shapes.Count + (_current != null ? 1 : 0); } }

        public void Begin(Rectangle bounds, WindowInfo target)
        {
            _target = target;
            _targetMinimized = false;
            if (_tracker != null) { _tracker.Stop(); _tracker.Dispose(); _tracker = null; }
            this.Bounds = bounds;
            this.Show();
            _shapes.Clear();
            List<Shape> saved = LocalStore.LoadForTarget(target);
            if (saved.Count > 0)
            {
                _shapes.AddRange(saved);
                _status("已恢复 " + saved.Count + " 条历史标注");
            }
            Invalidate();
            SetClickThrough(true);
            Win32.SetWindowPos(this.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            _hook.Handler = OnGlobalMouse;
            if (!_hook.Install()) _status("全局鼠标钩子安装失败，可能无法画线");
            _keyboardHook.Handler = OnGlobalKey;
            _keyboardHookOk = _keyboardHook.Install();
            if (!_keyboardHookOk) _status("键盘修饰键钩子安装失败，可能只能普通鼠标操作");
            if (_cursorTimer != null) { _cursorTimer.Stop(); _cursorTimer.Dispose(); }
            if (_inputTimer != null) { _inputTimer.Stop(); _inputTimer.Dispose(); }
            _inputTimer = new System.Windows.Forms.Timer();
            _inputTimer.Interval = 12;
            _inputTimer.Tick += delegate { ProcessInput(); };
            _inputTimer.Start();
            _cursorTimer = new System.Windows.Forms.Timer();
            _cursorTimer.Interval = 50;
            _cursorTimer.Tick += delegate
            {
                SetClickThrough(PassThrough || _editorOpen || !DrawModifierDown());
                if (_editorOpen) { HideHover(); return; }
                if (_altDown && !_altReady && (DateTime.UtcNow - _altDownAt).TotalMilliseconds >= 250) _altReady = true;
                if (PassThrough || !DrawModifierDown())
                {
                    if (_down && _hasHover) FinishAt(_hover);
                    HideHover();
                    return;
                }
                Point sp = Cursor.Position;
                if (IsExempt != null && IsExempt(sp)) { HideHover(); return; }
                Point cp = PointToClient(sp);
                bool inside = cp.X >= 0 && cp.Y >= 0 && cp.X < Width && cp.Y < Height;
                if (inside) { _hover = cp; _hasHover = true; }
                else { HideHover(); }
            };
            _cursorTimer.Start();
            if (target != null)
            {
                    _tracker = new System.Windows.Forms.Timer();
                    _tracker.Interval = 150;
                _tracker.Tick += delegate
                {
                    if (_target == null) return;
                    if (Win32.IsIconic(_target.Handle))
                    {
                        if (!_targetMinimized)
                        {
                            _targetMinimized = true;
                            this.Hide();
                            _status("目标窗口已最小化，覆膜隐藏");
                        }
                        return;
                    }
                    if (_targetMinimized)
                    {
                        _targetMinimized = false;
                        this.Show();
                        Invalidate();
                        _status("目标窗口已恢复，覆膜跟随");
                    }
                    Rectangle r = WindowList.BoundsOf(_target.Handle);
                    if (r.IsEmpty) return;
                    if (r != this.Bounds) { this.Bounds = r; ResetBuffer(); Invalidate(); }
                };
                _tracker.Start();
            }
            _status(target == null ? "全屏标注：可在系统任意位置画线" : "已锁定：" + target.Display);
        }

        public void End()
        {
            _hook.Uninstall();
            _keyboardHook.Uninstall();
            if (_paintTimer != null) _paintTimer.Stop();
            if (_inputTimer != null) { _inputTimer.Stop(); _inputTimer.Dispose(); _inputTimer = null; }
            _pendingDown = false;
            _pendingMove = false;
            _pendingUp = false;
            _dirtyPending = false;
            _dirty = Rectangle.Empty;
            _altDown = false;
            Win32.SetCursor(ArrowCursor);
            if (_cursorTimer != null) { _cursorTimer.Stop(); _cursorTimer.Dispose(); _cursorTimer = null; }
            if (_tracker != null) { _tracker.Stop(); _tracker.Dispose(); _tracker = null; }
            _down = false;
            _current = null;
            _hasHover = false;
            _targetMinimized = false;
            if (_editor != null) { _editorOpen = false; try { _editor.Close(); } catch { } _editor = null; }
            if (_buffer != null) { _buffer.Dispose(); _buffer = null; }
            this.Hide();
        }

        public void ClearShapes()
        {
            _shapes.Clear();
            _current = null;
            ResetBuffer();
            Invalidate();
            _status("已清空当前画线");
        }

        public string SaveOverlay(string path)
        {
            return LocalStore.SaveOverlayFile(path, _shapes, _target, this.Bounds);
        }

        public string LoadOverlay(string path)
        {
            List<Shape> list = LocalStore.LoadOverlayFile(path);
            if (list.Count == 0) return "覆膜文件为空或无法读取";
            _shapes.Clear();
            _shapes.AddRange(list);
            ResetBuffer();
            Invalidate();
            return "已加载 " + list.Count + " 条覆膜";
        }

        public string ExportPng(string outputPath = null)
        {
            try
            {
                Rectangle b = this.Bounds;
                if (b.Width <= 0 || b.Height <= 0) return null;
                using (Bitmap bmp = new Bitmap(b.Width, b.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        bool wasVisible = this.Visible;
                        if (wasVisible) this.Hide();
                        g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                        if (wasVisible) this.Show();
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        foreach (Shape s in _shapes)
                        {
                            DrawShape(g, s, false);
                            if (!String.IsNullOrEmpty(s.Text))
                            {
                                Point p = MapPoint(s, s.Points[s.Points.Count - 1]);
                                using (Font f = new Font("Microsoft YaHei UI", 10f))
                                using (SolidBrush bg = new SolidBrush(Color.FromArgb(210, 15, 23, 42)))
                                using (SolidBrush fg = new SolidBrush(Color.White))
                                {
                                    SizeF sz = g.MeasureString(s.Text, f);
                                    RectangleF box = new RectangleF(p.X + 8, p.Y + 8, sz.Width + 12, sz.Height + 8);
                                    g.FillRectangle(bg, box);
                                    g.DrawString(s.Text, f, fg, box.X + 6, box.Y + 4);
                                }
                            }
                        }
                    }
                    string path = outputPath;
                    if (String.IsNullOrEmpty(path))
                    {
                        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ScreenInk");
                        Directory.CreateDirectory(dir);
                        path = Path.Combine(dir, "ScreenInk-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                    }
                    else
                    {
                        string dir = Path.GetDirectoryName(path);
                        if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    return path;
                }
            }
            catch (Exception e)
            {
                _status("导出失败：" + e.Message);
                return null;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_buffer == null || _buffer.Width != Width || _buffer.Height != Height) ResetBuffer();
            if (_buffer != null) e.Graphics.DrawImage(_buffer, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel);
        }

        private void ResetBuffer()
        {
            if (_buffer != null) { _buffer.Dispose(); _buffer = null; }
            if (Width <= 0 || Height <= 0) return;
            _buffer = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(_buffer)) g.Clear(BackColor);
            RenderBuffer(new Rectangle(0, 0, Width, Height));
        }

        private void RenderBuffer(Rectangle dirty)
        {
            if (Width <= 0 || Height <= 0) return;
            if (_buffer == null || _buffer.Width != Width || _buffer.Height != Height) ResetBuffer();
            if (_buffer == null) return;
            Rectangle r = Rectangle.Intersect(dirty, new Rectangle(0, 0, Width, Height));
            if (r.Width <= 0 || r.Height <= 0) return;
            using (Graphics g = Graphics.FromImage(_buffer))
            using (SolidBrush bg = new SolidBrush(BackColor))
            {
                g.SetClip(r);
                g.FillRectangle(bg, r);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (Shape s in _shapes) DrawShape(g, s, false);
                if (_current != null) DrawShape(g, _current, true);
                DrawSelection(g);
            }
        }

        private void RequestPaint(Rectangle r)
        {
            if (!Visible || r.Width <= 0 || r.Height <= 0) return;
            r.Inflate(6, 6);
            if (_dirtyPending) _dirty = Rectangle.Union(_dirty, r);
            else { _dirty = r; _dirtyPending = true; }
            if (_paintTimer != null && !_paintTimer.Enabled) _paintTimer.Start();
        }

        private static Rectangle CrossRect(Point p)
        {
            return new Rectangle(p.X - 14, p.Y - 14, 28, 28);
        }

        private void HideHover()
        {
            if (!_hasHover) return;
            RequestPaint(CrossRect(_hover));
            _hasHover = false;
        }

        private Point MapPoint(Shape s, Point p)
        {
            if (s == null || !s.Relative) return p;
            return new Point((int)Math.Round(p.X * (double)Width / 10000.0), (int)Math.Round(p.Y * (double)Height / 10000.0));
        }

        private static Rectangle ShapeBounds(Shape s, int pad)
        {
            if (s == null || s.Points == null || s.Points.Count == 0) return Rectangle.Empty;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Point p in s.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return Rectangle.FromLTRB(minX - pad, minY - pad, maxX + pad, maxY + pad);
        }

        private void DrawSelection(Graphics g)
        {
            if (_selected == null || _selected.Points.Count == 0) return;
            using (Pen pen = new Pen(Color.FromArgb(56, 189, 248), 1.5f))
            using (SolidBrush fill = new SolidBrush(Color.White))
            {
                foreach (Point p in _selected.Points)
                {
                    Point q = MapPoint(_selected, p);
                    g.DrawRectangle(pen, q.X - 4, q.Y - 4, 8, 8);
                    g.FillRectangle(fill, q.X - 2, q.Y - 2, 4, 4);
                }
            }
        }

        private void DrawShape(Graphics g, Shape s, bool preview)
        {
            if (s == null || s.Points == null || s.Points.Count == 0) return;
            using (Pen pen = new Pen(s.Color, 3f))
            using (SolidBrush brush = new SolidBrush(s.Color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                Point a = MapPoint(s, s.Points[0]);
                Point b = MapPoint(s, s.Points[s.Points.Count - 1]);
                if (s.Tool == "freehand" || s.Tool == "polyline")
                {
                    if (s.Points.Count > 1)
                    {
                        Point[] pts = new Point[s.Points.Count];
                        for (int i = 0; i < s.Points.Count; i++) pts[i] = MapPoint(s, s.Points[i]);
                        g.DrawLines(pen, pts);
                    }
                }
                else if (s.Tool == "box")
                {
                    Rectangle r = Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                    g.DrawRectangle(pen, r);
                }
                else if (s.Tool == "arrow")
                {
                    g.DrawLine(pen, a, b);
                    double ang = Math.Atan2(b.Y - a.Y, b.X - a.X);
                    float len = 13f;
                    PointF p1 = new PointF((float)(b.X - len * Math.Cos(ang - Math.PI / 6)), (float)(b.Y - len * Math.Sin(ang - Math.PI / 6)));
                    PointF p2 = new PointF((float)(b.X - len * Math.Cos(ang + Math.PI / 6)), (float)(b.Y - len * Math.Sin(ang + Math.PI / 6)));
                    g.FillPolygon(brush, new PointF[] { new PointF(b.X, b.Y), p1, p2 });
                }
                else
                {
                    g.DrawLine(pen, a, b);
                }
            }
        }

        private bool DrawModifierDown()
        {
            if (_keyboardHookOk) return _altReady;
            return (Win32.GetAsyncKeyState(Win32.VK_MENU) & 0x8000) != 0;
        }

        private bool OnGlobalKey(int msg, int vk)
        {
            if (vk == 0x12 || vk == 0xA4 || vk == 0xA5)
            {
                if (msg == 0x0100 || msg == 0x0104)
                {
                    if (!_altDown) { _altDown = true; _altDownAt = DateTime.UtcNow; _altReady = false; }
                }
                else if (msg == 0x0101 || msg == 0x0105)
                {
                    _altDown = false;
                    _altReady = false;
                    if (_down) _pendingUp = true;
                    _hasHover = false;
                    Win32.SetCursor(ArrowCursor);
                }
                return true;
            }
            return false;
        }

        private bool OnGlobalMouse(int msg, Point screen)
        {
            if (!Visible || _editorOpen) return false;
            if (IsExempt != null && IsExempt(screen))
            {
                if (_down) _pendingUp = true;
                return false;
            }
            if (PassThrough)
            {
                if (_down) _pendingUp = true;
                return false;
            }
            if (!DrawModifierDown())
            {
                if (_down) _pendingUp = true;
                return false;
            }
            if ((msg == 0x0204 || msg == 0x0205) && _current != null && _current.Tool == "polyline")
            {
                if (msg == 0x0204) _pendingFinish = true;
                return true;
            }
            _pendingScreen = screen;
            if (msg == 0x0201)
            {
                _pendingDown = true;
                return true;
            }
            if (msg == 0x0200)
            {
                _pendingMove = true;
                return false;
            }
            if (msg == 0x0202)
            {
                _pendingUp = true;
                return true;
            }
            return false;
        }

        private void ProcessInput()
        {
            if (!Visible || _editorOpen) return;
            if (!DrawModifierDown() && _down) _pendingUp = true;
            Point p = PointToClient(_pendingScreen);
            bool inside = p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;
            if (_tool == "select")
            {
                if (_pendingDown) { _pendingDown = false; if (inside) BeginSelect(p); }
                if (_pendingMove) { _pendingMove = false; if (_down) UpdateSelect(p); }
                if (_pendingUp) { _pendingUp = false; if (_down) EndSelect(); _down = false; }
                _pendingFinish = false;
                return;
            }
            bool polyline = _tool == "polyline" || (_current != null && _current.Tool == "polyline");
            if (_pendingDown)
            {
                _pendingDown = false;
                if (polyline)
                {
                    if (inside) AddPolylinePoint(p);
                }
                else if (!_down && inside) StartAt(p);
            }
            if (_pendingMove)
            {
                _pendingMove = false;
                if (_down && !polyline && inside) MoveTo(p);
            }
            if (_pendingUp)
            {
                _pendingUp = false;
                if (_down && !polyline) FinishAt(p);
                _down = false;
            }
            if (_pendingFinish)
            {
                _pendingFinish = false;
                FinishPolyline();
            }
        }

        private void AddPolylinePoint(Point p)
        {
            if (_current == null)
            {
                _current = new Shape();
                _current.Id = Guid.NewGuid().ToString("N");
                _current.Tool = "polyline";
                _current.Color = _inkColor;
                _current.Points.Add(p);
            }
            else
            {
                Point last = _current.Points[_current.Points.Count - 1];
                if (Math.Abs(last.X - p.X) + Math.Abs(last.Y - p.Y) < 2) return;
                _current.Points.Add(p);
            }
            _down = false;
            RequestPaint(ShapeBounds(_current, 8));
        }

        private void FinishPolyline()
        {
            if (_current == null || _current.Tool != "polyline") return;
            if (_current.Points.Count < 2) { _current = null; return; }
            Shape finished = _current;
            Rectangle region = ShapeBounds(finished, 8);
            _current = null;
            finished.CreatedAt = DateTime.UtcNow.ToString("o");
            if (Width > 0 && Height > 0)
            {
                for (int i = 0; i < finished.Points.Count; i++)
                {
                    Point q = finished.Points[i];
                    finished.Points[i] = new Point(q.X * 10000 / Width, q.Y * 10000 / Height);
                }
            }
            finished.Relative = true;
            _shapes.Add(finished);
            RequestPaint(region);
            ShowEditor(finished, this.Bounds, _target);
        }


        private static double DistToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            if (Math.Abs(dx) + Math.Abs(dy) < 0.001) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            double x = a.X + t * dx, y = a.Y + t * dy;
            return Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y));
        }

        private bool HitShape(Shape s, Point p, out int handle)
        {
            handle = -1;
            if (s == null || s.Points.Count == 0) return false;
            Point[] pts = new Point[s.Points.Count];
            for (int i = 0; i < s.Points.Count; i++) pts[i] = MapPoint(s, s.Points[i]);
            for (int i = 0; i < pts.Length; i++)
            {
                if (Math.Abs(pts[i].X - p.X) <= 9 && Math.Abs(pts[i].Y - p.Y) <= 9) { handle = i; return true; }
            }
            for (int i = 1; i < pts.Length; i++)
            {
                if (DistToSegment(p, pts[i - 1], pts[i]) <= 7) return true;
            }
            return false;
        }

        private bool BeginSelect(Point p)
        {
            _selected = null;
            _selectedHandle = -1;
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                int h;
                if (HitShape(_shapes[i], p, out h))
                {
                    _selected = _shapes[i];
                    _selectedHandle = h;
                    break;
                }
            }
            if (_selected == null)
            {
                _down = false;
                RequestPaint(new Rectangle(0, 0, Width, Height));
                return false;
            }
            _selectStart = p;
            _selectOriginal = new List<Point>(_selected.Points);
            _selectChanged = false;
            _down = true;
            RequestPaint(new Rectangle(0, 0, Width, Height));
            return true;
        }

        private void UpdateSelect(Point p)
        {
            if (_selected == null || _selectOriginal == null || Width <= 0 || Height <= 0) return;
            if (_selectedHandle >= 0 && _selectedHandle < _selected.Points.Count)
            {
                _selected.Points[_selectedHandle] = new Point(
                    Math.Max(0, Math.Min(10000, p.X * 10000 / Width)),
                    Math.Max(0, Math.Min(10000, p.Y * 10000 / Height)));
            }
            else
            {
                int dx = p.X - _selectStart.X;
                int dy = p.Y - _selectStart.Y;
                for (int i = 0; i < _selected.Points.Count && i < _selectOriginal.Count; i++)
                {
                    _selected.Points[i] = new Point(
                        Math.Max(0, Math.Min(10000, _selectOriginal[i].X + dx * 10000 / Width)),
                        Math.Max(0, Math.Min(10000, _selectOriginal[i].Y + dy * 10000 / Height)));
                }
            }
            _selectChanged = true;
            RequestPaint(new Rectangle(0, 0, Width, Height));
        }

        private void EndSelect()
        {
            if (_selected != null && _selectChanged)
            {
                string err = LocalStore.Save(_selected, _target, this.Bounds);
                _status(err == null ? "已更新标注 " + DateTime.Now.ToString("HH:mm:ss") : err);
                ActivityLog.Add("annotation_updated", _selected.Tool);
            }
            _down = false;
            _selectChanged = false;
            _selectOriginal = null;
            RequestPaint(new Rectangle(0, 0, Width, Height));
        }

        public void DeleteSelected()
        {
            if (_selected == null) { _status("没有选中标注"); return; }
            _shapes.Remove(_selected);
            _selected = null;
            _selectedHandle = -1;
            ResetBuffer();
            Invalidate();
            _status("已删除选中标注");
            ActivityLog.Add("annotation_deleted", "");
        }

        public string EditSelectedText()
        {
            if (_selected == null) return "没有选中标注";
            Shape s = _selected;
            Point at = MapPoint(s, s.Points[s.Points.Count - 1]);
            _editorOpen = true;
            SetClickThrough(true);
            HideHover();
            _editor = new InlineEditorForm(this.PointToScreen(at), s.Text,
                delegate(string text)
                {
                    _editorOpen = false;
                    _editor = null;
                    s.Text = text;
                    LocalStore.Save(s, _target, this.Bounds);
                    ActivityLog.Add("annotation_text", text);
                    ResetBuffer();
                    Invalidate();
                    _status("已更新备注");
                },
                delegate { _editorOpen = false; _editor = null; });
            _editor.Show();
            _editor.Activate();
            return "编辑备注";
        }

        private void StartAt(Point p)
        {
            _down = true;
            _current = new Shape();
            _current.Id = Guid.NewGuid().ToString("N");
            _current.Tool = _tool;
            _current.Color = _inkColor;
            _current.Points.Add(p);
            RequestPaint(new Rectangle(p.X - 4, p.Y - 4, 8, 8));
        }

        private void MoveTo(Point p)
        {
            if (!_down || _current == null) return;
            Point prev = _current.Points[_current.Points.Count - 1];
            if (_current.Tool == "freehand")
            {
                _current.Points.Add(p);
                RequestPaint(Rectangle.FromLTRB(
                    Math.Min(prev.X, p.X) - 6, Math.Min(prev.Y, p.Y) - 6,
                    Math.Max(prev.X, p.X) + 6, Math.Max(prev.Y, p.Y) + 6));
            }
            else
            {
                if (_current.Points.Count == 1) _current.Points.Add(p);
                else _current.Points[_current.Points.Count - 1] = p;
                RequestPaint(ShapeBounds(_current, 8));
            }
        }

        private void FinishAt(Point p)
        {
            if (!_down || _current == null) return;
            _down = false;
            if (_current.Tool != "freehand")
            {
                if (_current.Points.Count == 1) _current.Points.Add(p);
                else _current.Points[_current.Points.Count - 1] = p;
            }
            Point a = _current.Points[0];
            Point b = _current.Points[_current.Points.Count - 1];
            if (_current.Tool != "freehand" && Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < 4)
            {
                Rectangle tiny = ShapeBounds(_current, 8);
                _current = null;
                RequestPaint(tiny);
                return;
            }
            Shape finished = _current;
            Rectangle region = ShapeBounds(finished, 8);
            _current = null;
            finished.CreatedAt = DateTime.UtcNow.ToString("o");
            if (Width > 0 && Height > 0)
            {
                for (int i = 0; i < finished.Points.Count; i++)
                {
                    Point q = finished.Points[i];
                    finished.Points[i] = new Point(q.X * 10000 / Width, q.Y * 10000 / Height);
                }
            }
            finished.Relative = true;
            _shapes.Add(finished);
            RequestPaint(region);
            Rectangle bounds = this.Bounds;
            WindowInfo target = _target;
            ShowEditor(finished, bounds, target);
        }

        private void ShowEditor(Shape shape, Rectangle bounds, WindowInfo target)
        {
            if (shape == null || shape.Points.Count == 0) return;
            if (_editor != null) { try { _editor.Close(); } catch { } _editor = null; }
            Point end = MapPoint(shape, shape.Points[shape.Points.Count - 1]);
            if (shape.Tool == "box" && shape.Points.Count > 1)
            {
                Point first = MapPoint(shape, shape.Points[0]);
                end = new Point((first.X + end.X) / 2, (first.Y + end.Y) / 2);
            }
            Point screenPoint = this.PointToScreen(end);
            _editorOpen = true;
            SetClickThrough(true);
            HideHover();
            _editor = new InlineEditorForm(screenPoint, shape.Text,
                delegate(string text)
                {
                    _editorOpen = false;
                    _editor = null;
                    shape.Text = text;
                    Invalidate();
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        string err = LocalStore.Save(shape, target, bounds);
                        if (_status != null) _status(err == null ? "已保存标注 " + DateTime.Now.ToString("HH:mm:ss") : err);
                    });
                },
                delegate
                {
                    _editorOpen = false;
                    _editor = null;
                    _shapes.Remove(shape);
                    Invalidate();
                });
            _editor.Show();
            _editor.Activate();
        }

        private void OnDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) StartAt(e.Location);
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            MoveTo(e.Location);
        }

        private void OnUp(object sender, MouseEventArgs e)
        {
            FinishAt(e.Location);
        }
    }

    internal static class LocalStore
    {
        private static string Esc(string v)
        {
            if (v == null) v = "";
            return v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Hex(Color c) { return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenInk"); } }
        public static string FilePath { get { return Path.Combine(Dir, "annotations.jsonl"); } }

        public static List<object> Recent(int max)
        {
            List<object> list = new List<object>();
            try
            {
                if (!File.Exists(FilePath)) return list;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string[] lines = File.ReadAllLines(FilePath);
                for (int i = lines.Length - 1; i >= 0 && list.Count < max; i--)
                {
                    if (String.IsNullOrWhiteSpace(lines[i])) continue;
                    try { list.Add(ser.Deserialize<Dictionary<string, object>>(lines[i])); } catch { }
                }
            }
            catch { }
            return list;
        }

        public static string Save(Shape s, WindowInfo target, Rectangle bounds)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder points = new StringBuilder();
                StringBuilder relative = new StringBuilder();
                for (int i = 0; i < s.Points.Count; i++)
                {
                    Point p = s.Points[i];
                    int px = bounds.Left + (s.Relative && bounds.Width > 0 ? p.X * bounds.Width / 10000 : p.X);
                    int py = bounds.Top + (s.Relative && bounds.Height > 0 ? p.Y * bounds.Height / 10000 : p.Y);
                    if (i > 0) { points.Append(","); relative.Append(","); }
                    points.Append("{\"x\":").Append(px).Append(",\"y\":").Append(py).Append("}");
                    relative.Append("{\"u\":").Append((p.X / 10000.0).ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture))
                        .Append(",\"v\":").Append((p.Y / 10000.0).ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)).Append("}");
                }
                string app = target == null ? "desktop" : (target.ProcessName ?? "");
                string title = target == null ? "全屏" : (target.Title ?? "");
                string hwnd = target == null ? "0" : target.Handle.ToInt64().ToString();
                string now = DateTime.UtcNow.ToString("o");
                string json = "{\"id\":\"" + Esc(s.Id) + "\",\"source\":{\"type\":\"screen\",\"app\":\"" + Esc(app) + "\",\"title\":\"" + Esc(title) + "\",\"hwnd\":\"" + Esc(hwnd) + "\"},"
                    + "\"frame\":\"screen\",\"tool\":\"" + Esc(s.Tool) + "\",\"color\":\"" + Hex(s.Color) + "\","
                    + "\"anchor\":{\"screen\":{\"x\":" + bounds.Left + ",\"y\":" + bounds.Top + ",\"w\":" + bounds.Width + ",\"h\":" + bounds.Height + "},"
                    + "\"target\":{\"title\":\"" + Esc(title) + "\",\"app\":\"" + Esc(app) + "\",\"hwnd\":\"" + Esc(hwnd) + "\"}},"
                    + "\"createdAt\":\"" + Esc(s.CreatedAt) + "\",\"points\":[" + points.ToString() + "],"
                    + "\"relativePoints\":[" + relative.ToString() + "],\"coordinateSpace\":\"target-relative\",\"text\":\"" + Esc(s.Text) + "\"}";
                File.AppendAllText(FilePath, json + "\r\n", new UTF8Encoding(false));
                ActivityLog.Add("annotation_saved", s.Tool + (String.IsNullOrEmpty(s.Text) ? "" : "：" + s.Text));
                return null;
            }
            catch (Exception e) { return "保存失败：" + e.Message; }
        }

        public static List<Shape> LoadForTarget(WindowInfo target)
        {
            List<Shape> result = new List<Shape>();
            try
            {
                if (!File.Exists(FilePath)) return result;
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string wantApp = target == null ? "desktop" : (target.ProcessName ?? "");
                string wantTitle = target == null ? "全屏" : (target.Title ?? "");
                string wantHwnd = target == null ? "0" : target.Handle.ToInt64().ToString();
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    if (String.IsNullOrWhiteSpace(line)) continue;
                    Dictionary<string, object> obj;
                    try { obj = serializer.Deserialize<Dictionary<string, object>>(line); } catch { continue; }
                    if (obj == null) continue;
                    Dictionary<string, object> source;
                    if (!(obj.ContainsKey("source") && obj["source"] is Dictionary<string, object>)) continue;
                    source = (Dictionary<string, object>)obj["source"];
                    string app = source.ContainsKey("app") ? Convert.ToString(source["app"]) : "";
                    string title = source.ContainsKey("title") ? Convert.ToString(source["title"]) : "";
                    string hwnd = source.ContainsKey("hwnd") ? Convert.ToString(source["hwnd"]) : "";
                    bool matches = target == null
                        ? app == wantApp
                        : (hwnd == wantHwnd || (app == wantApp && title == wantTitle));
                    if (!matches) continue;
                    if (!(obj.ContainsKey("relativePoints") && obj["relativePoints"] is object[])) continue;
                    object[] rp = (object[])obj["relativePoints"];
                    Shape s = new Shape();
                    s.Id = obj.ContainsKey("id") ? Convert.ToString(obj["id"]) : "";
                    s.Tool = obj.ContainsKey("tool") ? Convert.ToString(obj["tool"]) : "line";
                    s.Text = obj.ContainsKey("text") ? Convert.ToString(obj["text"]) : "";
                    s.CreatedAt = obj.ContainsKey("createdAt") ? Convert.ToString(obj["createdAt"]) : "";
                    s.Relative = true;
                    s.Color = Color.FromArgb(239, 68, 68);
                    string hex = obj.ContainsKey("color") ? Convert.ToString(obj["color"]) : "";
                    try
                    {
                        if (hex.StartsWith("#") && hex.Length >= 7)
                            s.Color = Color.FromArgb(Convert.ToInt32(hex.Substring(1, 2), 16), Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
                    }
                    catch { }
                    foreach (object item in rp)
                    {
                        Dictionary<string, object> d = item as Dictionary<string, object>;
                        if (d == null || !d.ContainsKey("u") || !d.ContainsKey("v")) continue;
                        double u = Convert.ToDouble(d["u"]);
                        double v = Convert.ToDouble(d["v"]);
                        s.Points.Add(new Point((int)Math.Round(u * 10000), (int)Math.Round(v * 10000)));
                    }
                    if (s.Points.Count > 0) result.Add(s);
                }
                Dictionary<string, Shape> unique = new Dictionary<string, Shape>();
                int unnamed = 0;
                foreach (Shape s in result)
                {
                    string key = String.IsNullOrEmpty(s.Id) ? "__" + (unnamed++) : s.Id;
                    unique[key] = s;
                }
                result = new List<Shape>(unique.Values);
            }
            catch { }
            return result;
        }

        private static Shape ShapeFromRecord(Dictionary<string, object> obj)
        {
            if (obj == null) return null;
            object[] rp = obj.ContainsKey("relativePoints") && obj["relativePoints"] is object[] ? (object[])obj["relativePoints"] : null;
            if (rp == null) return null;
            Shape s = new Shape();
            s.Id = obj.ContainsKey("id") ? Convert.ToString(obj["id"]) : "";
            s.Tool = obj.ContainsKey("tool") ? Convert.ToString(obj["tool"]) : "line";
            s.Text = obj.ContainsKey("text") ? Convert.ToString(obj["text"]) : "";
            s.CreatedAt = obj.ContainsKey("createdAt") ? Convert.ToString(obj["createdAt"]) : "";
            s.Relative = true;
            s.Color = Color.FromArgb(239, 68, 68);
            string hex = obj.ContainsKey("color") ? Convert.ToString(obj["color"]) : "";
            try
            {
                if (hex.StartsWith("#") && hex.Length >= 7)
                    s.Color = Color.FromArgb(Convert.ToInt32(hex.Substring(1, 2), 16), Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
            }
            catch { }
            foreach (object item in rp)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d == null || !d.ContainsKey("u") || !d.ContainsKey("v")) continue;
                s.Points.Add(new Point((int)Math.Round(Convert.ToDouble(d["u"]) * 10000), (int)Math.Round(Convert.ToDouble(d["v"]) * 10000)));
            }
            return s.Points.Count > 0 ? s : null;
        }

        public static string SaveOverlayFile(string path, List<Shape> shapes, WindowInfo target, Rectangle bounds)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
                foreach (Shape s in shapes)
                {
                    if (s == null) continue;
                    Dictionary<string, object> o = new Dictionary<string, object>();
                    o["id"] = s.Id;
                    o["tool"] = s.Tool;
                    o["color"] = Hex(s.Color);
                    o["text"] = s.Text;
                    o["createdAt"] = s.CreatedAt;
                    List<Dictionary<string, object>> rp = new List<Dictionary<string, object>>();
                    foreach (Point p in s.Points) rp.Add(new Dictionary<string, object> { { "u", p.X / 10000.0 }, { "v", p.Y / 10000.0 } });
                    o["relativePoints"] = rp;
                    list.Add(o);
                }
                Dictionary<string, object> root = new Dictionary<string, object>();
                root["version"] = 1;
                root["target"] = target == null ? "desktop" : (target.ProcessName + " | " + target.Title);
                root["screen"] = new Dictionary<string, object> { { "x", bounds.Left }, { "y", bounds.Top }, { "w", bounds.Width }, { "h", bounds.Height } };
                root["shapes"] = list;
                string dir = Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, serializer.Serialize(root), new UTF8Encoding(false));
                return null;
            }
            catch (Exception e) { return "保存覆膜失败：" + e.Message; }
        }

        public static List<Shape> LoadOverlayFile(string path)
        {
            List<Shape> list = new List<Shape>();
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
                if (root == null || !root.ContainsKey("shapes")) return list;
                object[] arr = root["shapes"] as object[];
                if (arr == null) return list;
                foreach (object item in arr)
                {
                    Shape s = ShapeFromRecord(item as Dictionary<string, object>);
                    if (s != null) list.Add(s);
                }
            }
            catch { }
            return list;
        }
    }

    internal class ToolbarForm : Form
    {
        private readonly ComboBox _windows = new ComboBox();
        private readonly ComboBox _tool = new ComboBox();
        private readonly ComboBox _color = new ComboBox();
        private readonly CheckBox _lock = new CheckBox();
        private readonly Button _start = new Button();
        private readonly Button _stop = new Button();
        private readonly Button _clear = new Button();
        private readonly Button _export = new Button();
        private readonly Button _saveOverlay = new Button();
        private readonly Button _openOverlay = new Button();
        private readonly Button _browse = new Button();
        private readonly Button _editText = new Button();
        private readonly Button _delete = new Button();
        private readonly Button _refresh = new Button();
        private readonly Button _close = new Button();
        private readonly Label _status = new Label();
        private string _lastOverlayPath;
        private AgentBridge _bridge;
        private OverlayForm _overlay;

        public ToolbarForm()
        {
            Text = "ScreenInk 屏幕墨层";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = true;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(29, 33, 41);
            ForeColor = Color.FromArgb(223, 228, 236);
            Width = 600;
            Height = 114;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 24, wa.Top + 24);
            ClientSize = new Size(640, 140);
            Font = new Font("Microsoft YaHei UI", 9f);

            _windows.DropDownStyle = ComboBoxStyle.DropDownList;
            _windows.Location = new Point(12, 10);
            _windows.Size = new Size(500, 25);
            Controls.Add(_windows);

            _refresh.Text = "刷新";
            _refresh.Location = new Point(518, 9);
            _refresh.Size = new Size(52, 27);
            _refresh.FlatStyle = FlatStyle.Flat;
            _refresh.Click += delegate { RefreshWindows(); };
            Controls.Add(_refresh);

            _lock.Text = "锁定选中窗口";
            _lock.ForeColor = ForeColor;
            _lock.Location = new Point(12, 44);
            _lock.Size = new Size(108, 24);
            _lock.Checked = false;
            Controls.Add(_lock);
            _windows.SelectedIndexChanged += delegate
            {
                WindowInfo w = _windows.SelectedItem as WindowInfo;
                _lock.Checked = w != null && w.Handle != IntPtr.Zero;
            };

            _tool.DropDownStyle = ComboBoxStyle.DropDownList;
            _tool.Items.AddRange(new object[] { "线", "箭头", "手绘", "框", "折线(多点)", "选择/移动" });
            _tool.SelectedIndex = 0;
            _tool.Location = new Point(124, 43);
            _tool.Size = new Size(116, 25);
            _tool.SelectedIndexChanged += delegate
            {
                if (_overlay != null) _overlay.Tool = ToolValue();
                if (ToolValue() == "polyline") SetStatus("折线：按住 Alt 左键逐点添加，右键完成；Esc/结束可退出");
                else if (ToolValue() == "select") SetStatus("选择/移动：按住 Alt 点击标注；拖白点改端点，拖线体移动");
            };
            Controls.Add(_tool);

            _color.DropDownStyle = ComboBoxStyle.DropDownList;
            _color.Items.AddRange(new object[] { "红", "橙", "绿", "蓝" });
            _color.SelectedIndex = 0;
            _color.Location = new Point(244, 43);
            _color.Size = new Size(56, 25);
            _color.SelectedIndexChanged += delegate
            {
                if (_overlay == null) return;
                if (_color.SelectedIndex == 1) _overlay.InkColor = Color.FromArgb(249, 115, 22);
                else if (_color.SelectedIndex == 2) _overlay.InkColor = Color.FromArgb(34, 197, 94);
                else if (_color.SelectedIndex == 3) _overlay.InkColor = Color.FromArgb(59, 130, 246);
                else _overlay.InkColor = Color.FromArgb(239, 68, 68);
            };
            Controls.Add(_color);

            _start.Text = "开始标注";
            _start.Location = new Point(306, 42);
            _start.Size = new Size(84, 27);
            _start.FlatStyle = FlatStyle.Flat;
            _start.BackColor = Color.FromArgb(15, 118, 110);
            _start.ForeColor = Color.White;
            _start.Click += delegate
            {
                if (_overlay != null && _overlay.IsActive) StopAnnotation();
                else StartAnnotation();
            };
            Controls.Add(_start);

            _stop.Text = "结束";
            _stop.Location = new Point(396, 42);
            _stop.Size = new Size(58, 27);
            _stop.FlatStyle = FlatStyle.Flat;
            _stop.BackColor = Color.FromArgb(71, 85, 105);
            _stop.ForeColor = Color.White;
            _stop.Enabled = true;
            _stop.Click += delegate { StopAnnotation(); };
            _stop.Visible = false;
            Controls.Add(_stop);

            _clear.Text = "清空";
            _clear.Location = new Point(460, 42);
            _clear.Size = new Size(58, 27);
            _clear.FlatStyle = FlatStyle.Flat;
            _clear.Click += delegate { if (_overlay != null) _overlay.ClearShapes(); };
            Controls.Add(_clear);

            _export.Text = "导出PNG";
            _export.Location = new Point(524, 42);
            _export.Size = new Size(72, 27);
            _export.FlatStyle = FlatStyle.Flat;
            _export.Click += delegate
            {
                if (_overlay == null) { SetStatus("请先开始标注"); return; }
                string path = _overlay.ExportPng();
                ActivityLog.Add("export_png", path == null ? "failed" : path);
                SetStatus(path == null ? "导出失败" : "已导出：" + path);
            };
            _export.Visible = false;
            Controls.Add(_export);

            _saveOverlay.Text = "保存/导出";
            _saveOverlay.Location = new Point(12, 76);
            _saveOverlay.Size = new Size(96, 27);
            _saveOverlay.FlatStyle = FlatStyle.Flat;
            _saveOverlay.Click += delegate
            {
                if (_overlay == null) { SetStatus("请先开始标注"); return; }
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ScreenInk");
                    Directory.CreateDirectory(dir);
                    dlg.InitialDirectory = dir;
                    dlg.FileName = "ScreenInk-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png";
                    dlg.Filter = "PNG 图片 (*.png)|*.png|ScreenInk 覆膜 (*.screenink.json)|*.screenink.json|JSON (*.json)|*.json";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".png")
                    {
                        string path = _overlay.ExportPng(dlg.FileName);
                        _lastOverlayPath = path;
                        ActivityLog.Add("export_png", path == null ? "failed" : path);
                        SetStatus(path == null ? "导出失败" : "已导出：" + path);
                    }
                    else
                    {
                        string err = _overlay.SaveOverlay(dlg.FileName);
                        if (err == null) _lastOverlayPath = dlg.FileName;
                        ActivityLog.Add("save_overlay", dlg.FileName);
                        SetStatus(err == null ? "已保存覆膜：" + dlg.FileName : err);
                    }
                }
            };
            Controls.Add(_saveOverlay);

            _openOverlay.Text = "打开覆膜";
            _openOverlay.Location = new Point(114, 76);
            _openOverlay.Size = new Size(90, 27);
            _openOverlay.FlatStyle = FlatStyle.Flat;
            _openOverlay.Click += delegate
            {
                if (_overlay == null) { SetStatus("请先开始标注"); return; }
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Filter = "ScreenInk overlay (*.screenink.json)|*.screenink.json|JSON (*.json)|*.json";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string msg = _overlay.LoadOverlay(dlg.FileName);
                    ActivityLog.Add("open_overlay", dlg.FileName);
                    SetStatus(msg);
                }
            };
            Controls.Add(_openOverlay);

            _browse.Text = "浏览路径";
            _browse.Location = new Point(210, 76);
            _browse.Size = new Size(90, 27);
            _browse.FlatStyle = FlatStyle.Flat;
            _browse.Click += delegate
            {
                string dir = !String.IsNullOrEmpty(_lastOverlayPath) && Directory.Exists(Path.GetDirectoryName(_lastOverlayPath))
                    ? Path.GetDirectoryName(_lastOverlayPath)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ScreenInk");
                Directory.CreateDirectory(dir);
                try { Process.Start("explorer.exe", dir); } catch { }
            };
            Controls.Add(_browse);

            _editText.Text = "改备注";
            _editText.Location = new Point(306, 76);
            _editText.Size = new Size(78, 27);
            _editText.FlatStyle = FlatStyle.Flat;
            _editText.Click += delegate
            {
                if (_overlay == null) { SetStatus("请先开始标注"); return; }
                SetStatus(_overlay.EditSelectedText());
            };
            Controls.Add(_editText);

            _delete.Text = "删除选中";
            _delete.Location = new Point(390, 76);
            _delete.Size = new Size(82, 27);
            _delete.FlatStyle = FlatStyle.Flat;
            _delete.Click += delegate
            {
                if (_overlay == null) { SetStatus("请先开始标注"); return; }
                _overlay.DeleteSelected();
            };
            Controls.Add(_delete);

            _close.Text = "×";
            _close.Location = new Point(600, 4);
            _close.Size = new Size(30, 26);
            _close.FlatStyle = FlatStyle.Flat;
            _close.ForeColor = Color.White;
            _close.Click += delegate { Close(); };
            _close.Visible = false;
            Controls.Add(_close);

            _status.Text = "选择窗口后开始；不勾选锁定则全屏标注";
            _status.ForeColor = Color.FromArgb(148, 163, 184);
            _status.Location = new Point(12, 108);
            _status.Size = new Size(610, 16);
            Controls.Add(_status);

            RefreshWindows();
            _bridge = new AgentBridge(BuildContext);
            string bridgeError;
            if (_bridge.Start(out bridgeError)) SetStatus("智能体上下文：http://127.0.0.1:" + _bridge.Port + "/context");
            else SetStatus("智能体桥接未启动：" + bridgeError);
            FormClosing += delegate { if (_bridge != null) _bridge.Stop(); if (_overlay != null) _overlay.End(); };
        }

        private string ToolValue()
        {
            if (_tool.SelectedIndex == 1) return "arrow";
            if (_tool.SelectedIndex == 2) return "freehand";
            if (_tool.SelectedIndex == 3) return "box";
            if (_tool.SelectedIndex == 4) return "polyline";
            if (_tool.SelectedIndex == 5) return "select";
            return "line";
        }

        private Dictionary<string, object> BuildContext()
        {
            WindowInfo target = _overlay != null ? _overlay.CurrentTarget : null;
            bool active = _overlay != null && _overlay.IsActive;
            string tool = _overlay != null ? _overlay.CurrentTool : "line";
            string title = target == null ? "全屏" : (target.Title ?? "");
            string app = target == null ? "desktop" : (target.ProcessName ?? "");
            List<object> recent = LocalStore.Recent(20);
            Dictionary<string, object> ctx = new Dictionary<string, object>();
            ctx["app"] = "ScreenInk";
            ctx["version"] = "0.1.0-alpha";
            ctx["active"] = active;
            ctx["tool"] = tool;
            ctx["shapeCount"] = _overlay != null ? _overlay.ShapeCount : 0;
            Dictionary<string, object> targetInfo = new Dictionary<string, object>();
            targetInfo["title"] = title;
            targetInfo["app"] = app;
            targetInfo["hwnd"] = target == null ? "0" : target.Handle.ToInt64().ToString();
            targetInfo["screen"] = _overlay == null ? new Dictionary<string, object>() : new Dictionary<string, object> {
                { "x", _overlay.CurrentBounds.Left }, { "y", _overlay.CurrentBounds.Top },
                { "w", _overlay.CurrentBounds.Width }, { "h", _overlay.CurrentBounds.Height }
            };
            ctx["target"] = targetInfo;
            ctx["recentAnnotations"] = recent;
            ctx["events"] = ActivityLog.Recent(20);
            ctx["intent"] = IntentEngine.Infer(title, app, tool, active, recent);
            ctx["suggestedPrompts"] = IntentEngine.SuggestedPrompts();
            ctx["dataPath"] = LocalStore.FilePath;
            return ctx;
        }

        private void SetStatus(string s)
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired) { try { this.BeginInvoke((MethodInvoker)delegate { _status.Text = s; }); } catch { } }
            else _status.Text = s;
        }

        private void RefreshWindows()
        {
            IntPtr keep = IntPtr.Zero;
            if (_windows.SelectedItem is WindowInfo) keep = ((WindowInfo)_windows.SelectedItem).Handle;
            List<WindowInfo> list = WindowList.Scan();
            _windows.BeginUpdate();
            _windows.Items.Clear();
            WindowInfo all = new WindowInfo();
            all.Handle = IntPtr.Zero;
            all.Title = "全屏（不锁定）";
            all.ProcessName = "任意窗口";
            _windows.Items.Add(all);
            foreach (WindowInfo w in list) _windows.Items.Add(w);
            int selected = 0;
            for (int i = 0; i < _windows.Items.Count; i++)
            {
                WindowInfo w = _windows.Items[i] as WindowInfo;
                if (w != null && w.Handle == keep && keep != IntPtr.Zero) { selected = i; break; }
            }
            _windows.SelectedIndex = selected;
            _windows.EndUpdate();
        }

        private void StartAnnotation()
        {
            if (_overlay == null) _overlay = new OverlayForm(SetStatus);
            WindowInfo selected = _windows.SelectedItem as WindowInfo;
            WindowInfo target = null;
            if (_lock.Checked && selected != null && selected.Handle != IntPtr.Zero)
            {
                Rectangle b = WindowList.BoundsOf(selected.Handle);
                if (b.IsEmpty || b.Width < 20 || b.Height < 20)
                {
                    SetStatus("目标窗口不可用，请刷新后重选");
                    return;
                }
                selected.Bounds = b;
                target = selected;
                try { Win32.SetForegroundWindow(selected.Handle); } catch { }
            }
            Rectangle bounds = target == null ? SystemInformation.VirtualScreen : target.Bounds;
            _overlay.Tool = ToolValue();
            if (_color.SelectedIndex == 1) _overlay.InkColor = Color.FromArgb(249, 115, 22);
            else if (_color.SelectedIndex == 2) _overlay.InkColor = Color.FromArgb(34, 197, 94);
            else if (_color.SelectedIndex == 3) _overlay.InkColor = Color.FromArgb(59, 130, 246);
            else _overlay.InkColor = Color.FromArgb(239, 68, 68);
            _overlay.IsExempt = delegate(Point p) { return this.Bounds.Contains(p); };
            _overlay.Begin(bounds, target);
            ActivityLog.Add("session_start", target == null ? "全屏" : (target.ProcessName + " | " + target.Title));
            this.TopMost = true;
            Win32.SetWindowPos(this.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            this.Activate();
            _start.Enabled = true;
            _start.Text = "结束标注";
            _start.BackColor = Color.FromArgb(185, 28, 28);
            _stop.Visible = false;
            SetStatus(target == null ? "全屏标注：直接在其他窗口上画线" : "已锁定窗口：" + target.Title);
        }

        private void StopAnnotation()
        {
            if (_overlay != null) _overlay.End();
            ActivityLog.Add("session_stop", "");
            _start.Enabled = true;
            _start.Text = "开始标注";
            _start.BackColor = Color.FromArgb(15, 118, 110);
            _stop.Visible = false;
            SetStatus("已结束；可重新选择窗口开始");
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }
    }

    internal class InlineEditorForm : Form
    {
        private readonly TextBox _text;
        private readonly Action<string> _onSave;
        private readonly Action _onCancel;
        private bool _done;

        public InlineEditorForm(Point screenPoint, string initial, Action<string> onSave, Action onCancel)
        {
            _onSave = onSave;
            _onCancel = onCancel;
            Text = "ScreenInk Note";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(29, 33, 41);
            ForeColor = Color.FromArgb(223, 228, 236);
            Width = 300;
            Height = 108;
            Font = new Font("Microsoft YaHei UI", 9f);
            Rectangle wa = Screen.FromPoint(screenPoint).WorkingArea;
            int x = Math.Max(wa.Left + 8, Math.Min(screenPoint.X + 12, wa.Right - Width - 8));
            int y = Math.Max(wa.Top + 8, Math.Min(screenPoint.Y + 12, wa.Bottom - Height - 8));
            Location = new Point(x, y);

            Label title = new Label();
            title.Text = "在这里写标注";
            title.ForeColor = Color.White;
            title.Location = new Point(10, 7);
            title.Size = new Size(200, 18);
            Controls.Add(title);

            _text = new TextBox();
            _text.Text = initial ?? "";
            _text.Location = new Point(10, 29);
            _text.Size = new Size(280, 24);
            _text.BackColor = Color.FromArgb(15, 23, 42);
            _text.ForeColor = Color.White;
            _text.BorderStyle = BorderStyle.FixedSingle;
            _text.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Save(); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Cancel(); }
            };
            Controls.Add(_text);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Location = new Point(154, 66);
            cancel.Size = new Size(60, 27);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.Click += delegate { Cancel(); };
            Controls.Add(cancel);

            Button save = new Button();
            save.Text = "保存标注";
            save.Location = new Point(220, 66);
            save.Size = new Size(70, 27);
            save.FlatStyle = FlatStyle.Flat;
            save.BackColor = Color.FromArgb(15, 118, 110);
            save.ForeColor = Color.White;
            save.Click += delegate { Save(); };
            Controls.Add(save);

            Shown += delegate { _text.Focus(); _text.SelectAll(); };
        }

        private void Save()
        {
            if (_done) return;
            _done = true;
            if (_onSave != null) _onSave(_text.Text.Trim());
            Close();
        }

        private void Cancel()
        {
            if (_done) return;
            _done = true;
            if (_onCancel != null) _onCancel();
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_done) Cancel();
            base.OnFormClosing(e);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && Array.IndexOf(args, "--mcp") >= 0)
            {
                McpServer.Run();
                return;
            }
            try { Win32.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch { try { Win32.SetProcessDpiAwareness(2); } catch { try { Win32.SetProcessDPIAware(); } catch { } } }
            try
            {
                bool created;
                using (Mutex mutex = new Mutex(true, "ScreenInk.SingleInstance", out created))
                {
                    if (!created) return;
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new ToolbarForm());
                }
            }
            catch (Exception e)
            {
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "b-annot-overlay-error.txt"), e.ToString(), Encoding.UTF8); } catch { }
            }
        }
    }
}

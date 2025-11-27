// Program.cs — версия v24 (New About Dialog, Refined Dark Theme)
#pragma warning disable CS1998
#nullable disable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Collections.Generic;

namespace ModInstaller
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "PLS3DCH_installer_crash.log");
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                try { File.AppendAllText(logPath, $"[UI EX] {DateTime.Now:O} {e.Exception}\r\n"); } catch { }
                MessageBox.Show($"Unhandled UI exception: {e.Exception.Message}\r\nSee {logPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { File.AppendAllText(logPath, $"[DOMAIN EX] {DateTime.Now:O} {(e.ExceptionObject as Exception)}\r\n"); } catch { }
            };
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm(args));
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(logPath, $"[MAIN EX] {DateTime.Now:O} {ex}\r\n"); } catch { }
                MessageBox.Show($"Fatal error during startup: {ex.Message}\r\nSee {logPath}", "Startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // CustomComboBox
    public class CustomComboBox : ComboBox
    {
        public Color ClosedBack { get; set; } = Color.White;
        public Color ClosedFore { get; set; } = Color.Black;
        public Color ArrowBack { get; set; } = Color.FromArgb(230, 230, 230);
        public Color ArrowColor { get; set; } = Color.Black;

        public CustomComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = sel ? SystemColors.Highlight : BackColor;
            Color fore = sel ? SystemColors.HighlightText : ForeColor;
            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = GetItemText(Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            e.DrawFocusRectangle();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_PAINT = 0x000F;
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT)
            {
                using (var g = Graphics.FromHwnd(Handle))
                {
                    Rectangle r = ClientRectangle;
                    using (var b = new SolidBrush(ClosedBack)) g.FillRectangle(b, r);
                    string text = Text;
                    if (string.IsNullOrEmpty(text) && Items.Count > 0 && SelectedIndex >= 0) text = GetItemText(Items[SelectedIndex]);
                    var textRect = new Rectangle(r.Left + 6, r.Top + 2, r.Width - 24, r.Height - 4);
                    TextRenderer.DrawText(g, text, Font, textRect, ClosedFore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    Rectangle arrowRect = new Rectangle(r.Right - 20, r.Top + 4, 16, r.Height - 8);
                    using (var bb = new SolidBrush(ArrowBack)) g.FillRectangle(bb, arrowRect);
                    PointF p1 = new PointF(arrowRect.Left + 4, arrowRect.Top + arrowRect.Height / 2f - 2);
                    PointF p2 = new PointF(arrowRect.Left + arrowRect.Width / 2f, arrowRect.Top + arrowRect.Height / 2f + 3);
                    PointF p3 = new PointF(arrowRect.Right - 4, arrowRect.Top + arrowRect.Height / 2f - 2);
                    using (var pen = new Pen(ArrowColor, 2f) { EndCap = System.Drawing.Drawing2D.LineCap.Round, StartCap = System.Drawing.Drawing2D.LineCap.Round })
                    {
                        g.DrawLine(pen, p1, p2);
                        g.DrawLine(pen, p2, p3);
                    }
                    using (var p = new Pen(Color.FromArgb(80, 80, 80))) g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
                }
            }
        }
    }

    class ThemedColorTable : ProfessionalColorTable
    {
        public Color MenuBack { get; set; }
        public Color MenuFore { get; set; }
        public Color MenuItemSelectedBack { get; set; }
        public Color MenuItemSelectedFore { get; set; }
        public Color DropDownBack { get; set; }
        public override Color ToolStripGradientBegin => MenuBack;
        public override Color ToolStripGradientMiddle => MenuBack;
        public override Color ToolStripGradientEnd => MenuBack;
        public override Color MenuItemSelected => MenuItemSelectedBack;
        public override Color MenuItemSelectedGradientBegin => MenuItemSelectedBack;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelectedBack;
        public override Color MenuItemPressedGradientBegin => MenuItemSelectedBack;
        public override Color MenuItemPressedGradientEnd => MenuItemSelectedBack;
        public override Color ToolStripDropDownBackground => DropDownBack;
        public override Color ImageMarginGradientBegin => DropDownBack;
        public override Color ImageMarginGradientMiddle => DropDownBack;
        public override Color ImageMarginGradientEnd => DropDownBack;
    }

    public class ThinScrollBar : Control
    {
        public enum ScrollOrientation { Vertical, Horizontal }
        public ScrollOrientation Orientation { get; set; } = ScrollOrientation.Vertical;
        int _maximum = 100;
        public int Maximum { get => _maximum; set { _maximum = Math.Max(0, value); Invalidate(); } }
        int _largeChange = 50;
        public int LargeChange { get => _largeChange; set { _largeChange = Math.Max(1, value); Invalidate(); } }
        int _value = 0;
        public int Value { get => _value; set { int v = Math.Max(0, Math.Min(Maximum, value)); if (_value != v) { _value = v; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } } }
        bool dragging = false; int dragOffset = 0;
        Color thumbColor = Color.FromArgb(120, 120, 120);
        Color thumbHover = Color.FromArgb(160, 160, 160);
        bool isHover = false;
        public event EventHandler ValueChanged;
        public ThinScrollBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Width = 10; Height = 10; Cursor = Cursors.Hand;
            BackColor = SystemColors.Control;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(BackColor);
            if (Maximum <= 0) return;
            float ratio = Math.Min(1f, (float)LargeChange / (Maximum + LargeChange));
            int thumbSize = (Orientation == ScrollOrientation.Vertical) ? Math.Max(20, (int)(Height * ratio)) : Math.Max(20, (int)(Width * ratio));
            int range = Maximum;
            int pos = (range == 0) ? 0 : (int)((float)Value / range * ((Orientation == ScrollOrientation.Vertical ? Height : Width) - thumbSize));
            Rectangle thumbRect = (Orientation == ScrollOrientation.Vertical) ? new Rectangle(1, pos, Width - 2, thumbSize) : new Rectangle(pos, 1, thumbSize, Height - 2);
            using (var b = new SolidBrush(isHover ? thumbHover : thumbColor)) g.FillRectangle(b, thumbRect);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); isHover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); isHover = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Maximum <= 0) return;
            int trackLen = (Orientation == ScrollOrientation.Vertical) ? Height : Width;
            float ratio = Math.Min(1f, (float)LargeChange / (Maximum + LargeChange));
            int thumbSize = Math.Max(20, (int)(trackLen * ratio));
            int range = Maximum;
            int pos = (range == 0) ? 0 : (int)((float)Value / range * (trackLen - thumbSize));
            Rectangle thumbRect = (Orientation == ScrollOrientation.Vertical) ? new Rectangle(1, pos, Width - 2, thumbSize) : new Rectangle(pos, 1, thumbSize, Height - 2);
            if (thumbRect.Contains(e.Location)) { dragging = true; dragOffset = (Orientation == ScrollOrientation.Vertical) ? (e.Y - thumbRect.Y) : (e.X - thumbRect.X); Capture = true; }
            else
            {
                if (Orientation == ScrollOrientation.Vertical) { if (e.Y < thumbRect.Y) Value = Math.Max(0, Value - LargeChange); else Value = Math.Min(Maximum, Value + LargeChange); }
                else { if (e.X < thumbRect.X) Value = Math.Max(0, Value - LargeChange); else Value = Math.Min(Maximum, Value + LargeChange); }
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            int trackLen = (Orientation == ScrollOrientation.Vertical) ? Height : Width;
            float ratio = Math.Min(1f, (float)LargeChange / (Maximum + LargeChange));
            int thumbSize = Math.Max(20, (int)(trackLen * ratio));
            int maxPos = trackLen - thumbSize;
            int mousePos = (Orientation == ScrollOrientation.Vertical) ? e.Y : e.X;
            int newPos = mousePos - dragOffset;
            newPos = Math.Max(0, Math.Min(maxPos, newPos));
            int newValue = (Maximum == 0) ? 0 : (int)((float)newPos / maxPos * Maximum);
            Value = newValue;
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
    }

    // SimpleProgressBar
    public class SimpleProgressBar : Control
    {
        public ProgressBarStyle Style { get; set; } = ProgressBarStyle.Blocks;
        int _maximum = 100;
        public int Maximum { get => _maximum; set { _maximum = Math.Max(1, value); Invalidate(); } }
        int _value = 0;
        public int Value { get => _value; set { _value = Math.Max(0, Math.Min(Maximum, value)); Invalidate(); } }
        Color fillColor = Color.FromArgb(0, 160, 0);
        Color backColorCustom = Color.FromArgb(200, 200, 200);
        System.Windows.Forms.Timer marqueeTimer;
        int marqueeOffset = 0;
        public SimpleProgressBar()
        {
            Height = 18;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            marqueeTimer = new System.Windows.Forms.Timer { Interval = 30 };
            marqueeTimer.Tick += (_, __) => { marqueeOffset = (marqueeOffset + 8) % (Width + 200); Invalidate(); };
        }
        public void SetThemeColors(Color back, Color fill)
        {
            backColorCustom = back;
            fillColor = fill;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? SystemColors.Control);
            using (var b = new SolidBrush(backColorCustom)) g.FillRectangle(b, 0, 0, Width, Height);

            if (Style == ProgressBarStyle.Blocks)
            {
                float ratio = (Maximum <= 0) ? 0f : (float)Value / Maximum;
                int w = (int)(Width * ratio);
                using (var b = new SolidBrush(fillColor)) g.FillRectangle(b, 0, 0, w, Height);
            }
            else
            {
                int bandW = Math.Max(Width / 3, 60);
                int x = marqueeOffset - bandW;
                using (var b = new SolidBrush(fillColor))
                {
                    var rect = new Rectangle(x, 2, bandW, Height - 4);
                    g.FillRectangle(b, rect);
                }
            }
            using (var p = new Pen(Color.FromArgb(80, 80, 80))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
        public void StartMarquee() { marqueeTimer.Start(); Style = ProgressBarStyle.Marquee; Invalidate(); }
        public void StopMarquee() { marqueeTimer.Stop(); Style = ProgressBarStyle.Blocks; Invalidate(); }
    }

    public class MainForm : Form
    {
        // Win32
        [DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        const int SB_HORZ = 0;
        const int SB_VERT = 1;
        const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        const int EM_LINESCROLL = 0x00B6;
		const int EM_GETLINECOUNT = 0x00BA;

        MenuStrip menu;
        ToolStripMenuItem menuOptions, menuTheme, themeLight, themeDark, menuAbout;
        
        // Tab Controls
        Panel tabStrip;
        Button btnTabBase, btnTabAdv;
        bool isAdvancedMode = false;

        // Rows (to toggle visibility)
        Control rowModUrl, rowArchive, rowJson, rowTarget, rowSaveToDL;
        Button btnCheckUpdate, btnUpdate, btnUninstall; 

        TextBox txtUrl, txtArchive, txtTarget, txtVersionsUrl;
        RichTextBox rtbLog;
        CustomComboBox cbLanguage, cbVersion, cbConfig;
        Button btnDownload, btnBrowseArchive, btnBrowseTarget, btnDetect;
        Label lblStatus;
        SimpleProgressBar progress;
        ToolTip tip;
        CheckBox chkSaveToDownloads;
        TableLayoutPanel mainPanel;
        ThinScrollBar thinV;
        PictureBox pbLargeIcon;
        readonly Font uiFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        readonly string defaultVersionsJson = "https://raw.githubusercontent.com/denwerdan-arch/PLS3DCH/refs/heads/main/version.json";
        string lastDownloadedOriginalName = null;
        string[] startupArgs;
        
        // --- COLORS: REFINED DARK THEME ---
        // Light Theme
        Color lightBack = Color.White;
        Color lightFore = Color.FromArgb(28, 28, 28);
        Color controlLight = Color.FromArgb(240, 240, 240);
        
        // Dark Theme (Soft/Modern Graphite)
        Color darkBack = Color.FromArgb(32, 32, 32);      // Main background (Comfortable dark gray)
        Color darkFore = Color.FromArgb(220, 220, 220);   // Text (Off-white, less eye strain)
        Color controlDark = Color.FromArgb(45, 45, 48);   // Panels/Controls (Slightly lighter gray)
        
        bool isDark = false;
        ThemedColorTable menuColorTable = new ThemedColorTable();
        bool internalScrollUpdate = false;

        System.Windows.Forms.Timer logScrollTimer;
        int lastFirstVisibleLine = 0;

        int reservedBottom;
        int largeIconSize;

        readonly int minLogHeight = 120;
        Panel logWrapper;

        public MainForm(string[] args)
        {
            startupArgs = args;
            Text = "PLS3DCH installer";
            Width = 1200; Height = 600; StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi; MinimumSize = new Size(880, 400);
            tip = new ToolTip();

            InitBottomStripDimensions();

            int standardHeight = TextRenderer.MeasureText("Ag", uiFont).Height + 14;

            TryLoadEmbeddedIcon();

            // 1. Menu
            menu = new MenuStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Visible, RenderMode = ToolStripRenderMode.Professional };
            menuOptions = new ToolStripMenuItem("Options");
            menuTheme = new ToolStripMenuItem("Theme");
            themeLight = new ToolStripMenuItem("Light") { Checked = true, CheckOnClick = true };
            themeDark = new ToolStripMenuItem("Dark") { Checked = false, CheckOnClick = true };
            themeLight.Click += (s, e) => { SetTheme(false); themeLight.Checked = true; themeDark.Checked = false; };
            themeDark.Click += (s, e) => { SetTheme(true); themeDark.Checked = true; themeLight.Checked = false; };
            menuTheme.DropDownItems.AddRange(new ToolStripItem[] { themeLight, themeDark });
            menuOptions.DropDownItems.Add(menuTheme);
            menuAbout = new ToolStripMenuItem("About"); menuAbout.Click += (_, __) => ShowAboutDialog();
            menu.Items.AddRange(new ToolStripItem[] { menuOptions, menuAbout });
            Controls.Add(menu);

            // 2. Tab Strip
            tabStrip = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(0, 0, 0, 0) };
            btnTabBase = CreateTabButton("Base");
            btnTabAdv = CreateTabButton("Advanced");
            
            btnTabBase.Click += (_, __) => SwitchTab(false);
            btnTabAdv.Click += (_, __) => SwitchTab(true);

            tabStrip.Controls.Add(btnTabAdv);
            tabStrip.Controls.Add(btnTabBase);
            Controls.Add(tabStrip);

            // 3. Main Panel
            mainPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = false, Padding = new Padding(12), AutoScroll = true, GrowStyle = TableLayoutPanelGrowStyle.AddRows, AutoSizeMode = AutoSizeMode.GrowOnly };
            Controls.Add(mainPanel);

            menu.BringToFront();
            tabStrip.BringToFront();

            mainPanel.Padding = new Padding(mainPanel.Padding.Left, menu.Height + tabStrip.Height + 12, mainPanel.Padding.Right, reservedBottom);

            // Thin Scrollbar
            thinV = new ThinScrollBar { Orientation = ThinScrollBar.ScrollOrientation.Vertical, Width = 10 };
            thinV.ValueChanged += (_, __) =>
            {
                if (internalScrollUpdate) return;
                try
                {
                    internalScrollUpdate = true;
                    int curFirst = GetFirstVisibleLogLine();
                    int target = thinV.Value;
                    int delta = target - curFirst;
                    if (delta != 0) SendMessage(rtbLog.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(delta));
                }
                finally { internalScrollUpdate = false; }
            };

            // --- ROWS ---

            // Row 2: Mod URL (Hidden in Base)
            rowModUrl = CreateRowWithIconButton("Mod URL:", out txtUrl, out btnDownload, "Download", CreateDownloadIcon(20, Color.FromArgb(0, 160, 0)), standardHeight);
            btnDownload.Click += async (_, __) => await DownloadMod();
            tip.SetToolTip(btnDownload, "Download archive from URL");
            mainPanel.Controls.Add(rowModUrl);

            // Row 3: Archive (Hidden in Base)
            rowArchive = CreateRowWithIconButton("Or select archive:", out txtArchive, out btnBrowseArchive, "Browse", GetFolderIconBitmap(), standardHeight);
            btnBrowseArchive.Click += (_, __) => BrowseArchive();
            tip.SetToolTip(btnBrowseArchive, "Browse for archive (ZIP or RAR)");
            mainPanel.Controls.Add(rowArchive);

            // Row 4: JSON URL (Hidden in Base)
            rowJson = CreateRowSimple("Version JSON URL:", out txtVersionsUrl, standardHeight);
            txtVersionsUrl.Text = defaultVersionsJson;
            tip.SetToolTip(txtVersionsUrl, "URL to versions.json");
            mainPanel.Controls.Add(rowJson);

            // Row 5: Options (Visible in Both)
            var row5 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0), WrapContents = false, Anchor = AnchorStyles.Left | AnchorStyles.Top };

            var lblLang = new Label { Text = "Language:", AutoSize = true, Font = uiFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 8, 0) };
            cbLanguage = new CustomComboBox { Width = 80, Height = standardHeight, Font = uiFont, Margin = new Padding(0, 2, 12, 0) };
            cbLanguage.Items.AddRange(new[] { "ru", "en" }); cbLanguage.SelectedIndex = 0;

            var lblVer = new Label { Text = "Plasticity version:", AutoSize = true, Font = uiFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 8, 0) };
            cbVersion = new CustomComboBox { Width = 100, Height = standardHeight, Font = uiFont, Margin = new Padding(0, 2, 12, 0) };
            cbVersion.Items.AddRange(new[] { "Beta", "Stable" }); cbVersion.SelectedIndex = 0;

            var lblConfig = new Label { Text = "Config:", AutoSize = true, Font = uiFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 8, 0) };
            cbConfig = new CustomComboBox { Width = 240, Height = standardHeight, Font = uiFont, Margin = new Padding(0, 2, 12, 0) };
            cbConfig.Items.AddRange(new[] { "Don't install", "Theme", "Theme and shortcuts" }); cbConfig.SelectedIndex = 0;
            
            cbLanguage.DrawItem += ComboBox_DrawItem;
            cbVersion.DrawItem += ComboBox_DrawItem;
            cbConfig.DrawItem += ComboBox_DrawItem;

            btnDetect = new Button
            {
                Text = "Auto-detect path",
                Font = uiFont,
                AutoSize = false,
                Width = 200,
                Height = standardHeight,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = false,
                FlatStyle = FlatStyle.Flat,
                UseMnemonic = false
            };
            btnDetect.Click += (_, __) => AutoDetect();

            cbLanguage.SelectedIndexChanged += (_, __) => { AutoDetect(); };
            cbVersion.SelectedIndexChanged += (_, __) => { AutoDetect(); };

            row5.Controls.Add(lblLang); row5.Controls.Add(cbLanguage); 
            row5.Controls.Add(lblVer); row5.Controls.Add(cbVersion);
            row5.Controls.Add(lblConfig); row5.Controls.Add(cbConfig);
            row5.Controls.Add(btnDetect);
            mainPanel.Controls.Add(row5);

            // Row 6: Target Folder (Hidden in Base)
            rowTarget = CreateRowWithIconButton("Target folder:", out txtTarget, out btnBrowseTarget, "Browse", GetFolderIconBitmap(), standardHeight);
            btnBrowseTarget.Click += (_, __) => BrowseFolder();
            tip.SetToolTip(btnBrowseTarget, "Browse for target folder");
            mainPanel.Controls.Add(rowTarget);

            // Row 7: Save to Downloads (Hidden in Base)
            chkSaveToDownloads = new CheckBox { Text = "Save a copy to Downloads", AutoSize = true, Font = uiFont, Padding = new Padding(0, 6, 0, 0), Anchor = AnchorStyles.Left };
            tip.SetToolTip(chkSaveToDownloads, "If enabled, a copy of downloaded archive will be saved to %USERPROFILE%\\Downloads");
            rowSaveToDL = new Panel { AutoSize = true, Dock = DockStyle.Top }; // wrapper to hide easier
            rowSaveToDL.Controls.Add(chkSaveToDownloads);
            mainPanel.Controls.Add(rowSaveToDL);

            // Row 8: Action Buttons
            var row8 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0), Anchor = AnchorStyles.Left };
            
            // Auto button (moved from top)
            btnCheckUpdate = MakeButton("Update && Install", width: 180, height: standardHeight);
            btnCheckUpdate.Click += async (_, __) => await CheckUpdatesAsync();

            // Manual Update (renamed)
            btnUpdate = MakeButton("Manual Update && Install", width: 220, height: standardHeight);
            btnUpdate.Click += async (_, __) => await InstallOrUpdate(true);
            
            // Uninstall
            btnUninstall = MakeButton("Uninstall", width: 120, height: standardHeight);
            btnUninstall.Click += async (_, __) => await Uninstall();
            
            row8.Controls.AddRange(new Control[] { btnCheckUpdate, btnUpdate, btnUninstall });
            mainPanel.Controls.Add(row8);

            // Status + Progress
            var statusPanel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = false, Height = standardHeight + 8, Padding = new Padding(0, 8, 0, 0), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblStatus = new Label { Text = "Status: idle", AutoSize = false, AutoEllipsis = true, Font = uiFont, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left, Padding = new Padding(6, 0, 6, 0), MinimumSize = new Size(200, standardHeight), Height = standardHeight };
            tip.SetToolTip(lblStatus, lblStatus.Text);
            progress = new SimpleProgressBar { Width = 320, Height = Math.Max(16, standardHeight - 6) };
            statusPanel.Controls.Add(lblStatus, 0, 0); statusPanel.Controls.Add(progress, 1, 0);
            mainPanel.Controls.Add(statusPanel);

            // Log wrapper
            logWrapper = new Panel { Dock = DockStyle.Fill, Height = 260, Padding = new Padding(0) };
            rtbLog = new RichTextBox { Multiline = true, ReadOnly = true, ScrollBars = RichTextBoxScrollBars.None, Font = new Font("Consolas", 10f), Dock = DockStyle.Fill };
            logWrapper.Controls.Add(rtbLog);
            thinV.Dock = DockStyle.Right;
            logWrapper.Controls.Add(thinV);
            logWrapper.MinimumSize = new Size(0, minLogHeight);
            mainPanel.Controls.Add(logWrapper);

            // FIX: Enable scroll wheel on RichTextBox
            rtbLog.MouseWheel += (s, e) => 
            {
                if (!thinV.Visible) return;
                // Scroll ~3 lines per notch
                int lines = (e.Delta > 0) ? -3 : 3; 
                thinV.Value += lines;
            };

            // Large Icon
            pbLargeIcon = new PictureBox
            {
                Size = new Size(largeIconSize, largeIconSize),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            if (this.Icon != null) pbLargeIcon.Image = this.Icon.ToBitmap();
            else pbLargeIcon.Image = CreateDownloadIcon(Math.Max(40, largeIconSize), Color.FromArgb(0, 160, 0));
            Controls.Add(pbLargeIcon);
            pbLargeIcon.BringToFront();

            // Log scroll timer
            logScrollTimer = new System.Windows.Forms.Timer { Interval = 120 };
            logScrollTimer.Tick += (_, __) =>
            {
                try
                {
                    if (!thinV.Visible) return;
                    int first = GetFirstVisibleLogLine();
                    if (first != lastFirstVisibleLine)
                    {
                        lastFirstVisibleLine = first;
                        try { internalScrollUpdate = true; thinV.Value = Math.Min(thinV.Maximum, Math.Max(0, first)); } finally { internalScrollUpdate = false; }
                    }
                }
                catch { }
            };
            logScrollTimer.Start();

            // Initial state
            SetTheme(false);
            SwitchTab(false); // Default to Base

            // Layout hooks
            this.Layout += (_, __) => { UpdateThinScrollbars(); AdjustLogWrapperHeight(); PositionLargeIcon(); };
            this.Resize += (_, __) => { ResetHorizontalScroll(); AdjustStatusWidth(); UpdateThinScrollbars(); AdjustLogWrapperHeight(); PositionLargeIcon(); };

            mainPanel.ControlAdded += (_, __) => UpdateThinScrollbars();
            mainPanel.ControlRemoved += (_, __) => UpdateThinScrollbars();
            rtbLog.TextChanged += (_, __) => UpdateThinScrollbars();
            rtbLog.Resize += (_, __) => UpdateThinScrollbars();
            rtbLog.MouseWheel += (_, __) => UpdateThinScrollbars();
            this.Shown += (_, __) => 
            { 
                try 
                { 
                    AutoDetect(); 
                    ResetHorizontalScroll(); 
                    AdjustStatusWidth(); 
                    UpdateThinScrollbars(); 
                    AdjustLogWrapperHeight(); 
                    PositionLargeIcon(); 
                    // FIX: Force TitleBar update on Show
                    UpdateTitleBarColor(this.Handle, isDark);
                } catch { } 
            };

            // minimum size guard
            int minTotalHeight = menu.Height + tabStrip.Height + 12 + 800 + reservedBottom;
            this.MinimumSize = new Size(1080, Math.Max(500, minTotalHeight));
        }

        Button CreateTabButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Left,
                Width = 120,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font(uiFont, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            return b;
        }

        void SwitchTab(bool advanced)
        {
            isAdvancedMode = advanced;
            
            // Visuals
            // In Dark Mode: Active tab is slightly lighter than background (45 vs 32). Inactive is background (32).
            Color activeBack = isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(220, 220, 220);
            Color inactiveBack = isDark ? darkBack : controlLight;

            btnTabBase.BackColor = !advanced ? activeBack : inactiveBack;
            btnTabAdv.BackColor = advanced ? activeBack : inactiveBack;

            // Visibility
            rowModUrl.Visible = advanced;
            rowArchive.Visible = advanced;
            rowJson.Visible = advanced;
            rowTarget.Visible = advanced;
            rowSaveToDL.Visible = advanced;
            
            // Buttons Visibility
            btnCheckUpdate.Visible = true; 
            btnUpdate.Visible = advanced;
            btnUninstall.Visible = true;

            // Force layout update
            UpdateThinScrollbars();
            AdjustLogWrapperHeight();
        }

        void InitBottomStripDimensions()
        {
            try
            {
                using var g = CreateGraphics();
                float dpiY = g.DpiY;
                int px = (int)Math.Round(dpiY * 0.5f); // ~2.0 cm
                reservedBottom = Math.Max(40, Math.Min(px, 120));
                int rawIconSize = Math.Max(40, Math.Min((int)(reservedBottom * 0.7f), 96));
                largeIconSize = (int)(rawIconSize * 0.8f);
            }
            catch
            {
                reservedBottom = 84;
                largeIconSize = (int)(60 * 0.8f);
            }
        }

        void AdjustLogWrapperHeight()
        {
            try
            {
                int totalClientH = this.ClientSize.Height;
                int topUsed = menu.Height + tabStrip.Height + 12;
                int usedByOtherControls = 0;
                foreach (Control c in mainPanel.Controls)
                {
                    if (c == logWrapper) continue;
                    if (!c.Visible) continue;
                    int h = c.Height;
                    if (h <= 0) h = c.PreferredSize.Height;
                    usedByOtherControls += h;
                }

                int paddingTop = mainPanel.Padding.Top;
                int paddingBottom = mainPanel.Padding.Bottom;
                int available = totalClientH - reservedBottom - usedByOtherControls - paddingTop - paddingBottom - 40;
                int newLogH = Math.Max(minLogHeight, available);
                newLogH = Math.Min(newLogH, Math.Max(120, totalClientH - reservedBottom - 80));
                if (logWrapper.Height != newLogH) logWrapper.Height = newLogH;
            }
            catch { }
        }

        void PositionLargeIcon()
        {
            try
            {
                int rightPadding = 12;
                int x = Math.Max(0, this.ClientSize.Width - rightPadding - pbLargeIcon.Width);
                int yStripTop = this.ClientSize.Height - reservedBottom;
                int y = yStripTop + Math.Max(0, (reservedBottom - pbLargeIcon.Height) / 2);
                pbLargeIcon.Location = new Point(x, y);
                pbLargeIcon.BringToFront();
            }
            catch { }
        }

        void TryLoadEmbeddedIcon()
        {
            try
            {
                var asm = typeof(MainForm).Assembly;
                var names = asm.GetManifestResourceNames();
                var candidate = names.FirstOrDefault(n => n.EndsWith(".app.ico", StringComparison.OrdinalIgnoreCase)) ?? names.FirstOrDefault(n => n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));
                if (candidate != null)
                {
                    using var s = asm.GetManifestResourceStream(candidate);
                    if (s != null) this.Icon = new Icon(s);
                }
            }
            catch { }
        }

        void UpdateThinScrollbars()
        {
            try
            {
                if (!mainPanel.IsHandleCreated || !rtbLog.IsHandleCreated) return;

                // Скрываем нативные скроллбары контейнера (на всякий случай)
                ShowScrollBar(mainPanel.Handle, SB_VERT, false);
                ShowScrollBar(mainPanel.Handle, SB_HORZ, false);

                // 1. Получаем реальное количество визуальных строк (с учетом переносов)
                int totalVisualLines = SendMessage(rtbLog.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero);
                if (totalVisualLines == 0) totalVisualLines = 1;

                // 2. Считаем высоту одной строки
                int lineHeight = TextRenderer.MeasureText("Ag", rtbLog.Font).Height;
                
                // 3. Считаем, сколько строк помещается на экране
                // (Вычитаем пару пикселей для надежности, чтобы не переоценить видимость)
                int visibleLines = Math.Max(1, (rtbLog.ClientSize.Height - 4) / Math.Max(1, lineHeight));

                // 4. Рассчитываем максимум для скроллбара
                // Добавляем +1 или +2 к totalVisualLines, чтобы дать небольшой "запас" внизу
                int max = Math.Max(0, totalVisualLines - visibleLines + 1);

                if (max > 0)
                {
                    thinV.Visible = true;
                    thinV.Maximum = max;
                    thinV.LargeChange = Math.Max(1, visibleLines);

                    // Синхронизируем позицию ползунка
                    int firstVisible = GetFirstVisibleLogLine();
                    
                    try 
                    { 
                        internalScrollUpdate = true; 
                        // Ограничиваем значение, чтобы не вылетало
                        thinV.Value = Math.Min(thinV.Maximum, Math.Max(0, firstVisible)); 
                    } 
                    finally { internalScrollUpdate = false; }
                }
                else 
                {
                    thinV.Visible = false;
                    thinV.Value = 0; // Сброс при очистке
                }
            }
            catch { }
        }

        int GetFirstVisibleLogLine()
        {
            try
            {
                if (!rtbLog.IsHandleCreated) return 0;
                int first = SendMessage(rtbLog.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
                return Math.Max(0, first);
            }
            catch { return 0; }
        }

        void ApplyTheme(Control ctl, Color back, Color fore, Color ctrlBack)
        {
            try
            {
                if (ctl is MenuStrip) { }
                else if (ctl == tabStrip) { ctl.BackColor = back; } // Tab strip matches background for cleaner look
                else if (ctl is TableLayoutPanel || ctl is FlowLayoutPanel || ctl is Panel)
                {
                    ctl.BackColor = ctrlBack; ctl.ForeColor = fore;
                }
                else if (ctl is TextBox || ctl is RichTextBox)
                {
                    // Input fields are slightly offset from background in dark mode
                    ctl.BackColor = isDark ? Color.FromArgb(40, 40, 40) : Color.White; 
                    ctl.ForeColor = fore;
                    if (ctl is TextBox tb) tb.BorderStyle = BorderStyle.None;
                }
                else if (ctl is ComboBox cb)
                {
                    cb.BackColor = isDark ? Color.FromArgb(40, 40, 40) : Color.White; 
                    cb.ForeColor = fore; 
                    cb.FlatStyle = FlatStyle.Flat;
                }
                else if (ctl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat; 
                    btn.FlatAppearance.BorderSize = 0;
                    // Improved Button Colors
                    btn.BackColor = isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(225, 225, 225);
                    btn.ForeColor = fore;
                }
                else if (ctl is Label lbl)
                {
                    lbl.BackColor = Color.Transparent; lbl.ForeColor = fore;
                }
                else if (ctl is LinkLabel ll)
                {
                    ll.BackColor = Color.Transparent; 
                    // Softer link colors
                    ll.LinkColor = isDark ? Color.FromArgb(86, 156, 214) : Color.FromArgb(0, 102, 204);
                }
                else
                {
                    ctl.BackColor = ctrlBack; ctl.ForeColor = fore;
                }

                foreach (Control c in ctl.Controls) ApplyTheme(c, back, fore, ctrlBack);
            }
            catch { }
        }

        void SetTheme(bool dark)
        {
            isDark = dark;
            Color back = dark ? darkBack : lightBack;
            Color fore = dark ? darkFore : lightFore;
            Color ctrl = dark ? controlDark : controlLight;

            this.BackColor = back;
            mainPanel.BackColor = back;

            ApplyTheme(this, back, fore, ctrl);

            menuColorTable.MenuBack = ctrl;
            menuColorTable.MenuFore = fore;
            menuColorTable.MenuItemSelectedBack = dark ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
            menuColorTable.MenuItemSelectedFore = fore;
            menuColorTable.DropDownBack = ctrl;
            var renderer = new ToolStripProfessionalRenderer(menuColorTable);
            menu.Renderer = renderer;
            ToolStripManager.Renderer = renderer;
            menu.BackColor = ctrl; menu.ForeColor = fore;
            foreach (ToolStripItem item in menu.Items) ApplyToolStripItemColors(item, ctrl, fore);

            if (cbLanguage != null)
            {
                cbLanguage.ClosedBack = isDark ? Color.FromArgb(40, 40, 40) : Color.White;
                cbLanguage.ClosedFore = isDark ? darkFore : lightFore;
                cbLanguage.ArrowBack = isDark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(230, 230, 230);
                cbLanguage.ArrowColor = isDark ? Color.White : Color.Black;
            }
            if (cbVersion != null)
            {
                cbVersion.ClosedBack = isDark ? Color.FromArgb(40, 40, 40) : Color.White;
                cbVersion.ClosedFore = isDark ? darkFore : lightFore;
                cbVersion.ArrowBack = isDark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(230, 230, 230);
                cbVersion.ArrowColor = isDark ? Color.White : Color.Black;
            }
            if (cbConfig != null)
            {
                cbConfig.ClosedBack = isDark ? Color.FromArgb(40, 40, 40) : Color.White;
                cbConfig.ClosedFore = isDark ? darkFore : lightFore;
                cbConfig.ArrowBack = isDark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(230, 230, 230);
                cbConfig.ArrowColor = isDark ? Color.White : Color.Black;
            }

            cbLanguage?.Invalidate(); cbVersion?.Invalidate(); cbConfig?.Invalidate();

            rtbLog.BackColor = isDark ? darkBack : Color.White; 
            rtbLog.ForeColor = fore;

            RecolorUnderlinesAndTextBoxes(isDark);

            // Softer progress bar colors in dark mode (less neon)
            progress.SetThemeColors(isDark ? Color.FromArgb(45, 45, 45) : Color.FromArgb(220, 220, 220), 
                                  isDark ? Color.FromArgb(60, 160, 60) : Color.FromArgb(0, 120, 0));
            if (progress.Style == ProgressBarStyle.Marquee) progress.StartMarquee(); else progress.StopMarquee();

            pbLargeIcon.BackColor = Color.Transparent;
            pbLargeIcon.Invalidate();

            SwitchTab(isAdvancedMode); // Re-apply tab button colors

            menu.Invalidate(); this.Invalidate();
			UpdateTitleBarColor(this.Handle, dark); 
        }

        void RecolorUnderlinesAndTextBoxes(bool dark)
        {
            Color underline = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(200, 200, 200);
            Color tbBack = dark ? Color.FromArgb(40, 40, 40) : Color.White;
            Color tbFore = dark ? darkFore : lightFore;

            foreach (Control c in mainPanel.Controls)
            {
                if (c is TableLayoutPanel tl)
                {
                    foreach (Control cell in tl.Controls)
                    {
                        if (cell is Panel p)
                        {
                            foreach (Control inner in p.Controls)
                            {
                                if (inner is Panel up && up.Tag as string == "underline")
                                {
                                    up.BackColor = underline;
                                }
                                if (inner is TextBox tb)
                                {
                                    tb.BackColor = tbBack;
                                    tb.ForeColor = tbFore;
                                    tb.BorderStyle = BorderStyle.None;
                                }
                                if (inner is Panel container)
                                {
                                    var u = container.Controls.OfType<Panel>().FirstOrDefault(x => x.Tag as string == "underline");
                                    var t = container.Controls.OfType<TextBox>().FirstOrDefault();
                                    if (u != null) u.BackColor = underline;
                                    if (t != null) { t.BackColor = tbBack; t.ForeColor = tbFore; t.BorderStyle = BorderStyle.None; }
                                }
                            }
                        }
                    }
                }
            }
        }

        void ApplyToolStripItemColors(ToolStripItem item, Color back, Color fore)
        {
            try
            {
                item.BackColor = back; item.ForeColor = fore;
                if (item is ToolStripMenuItem mi)
                {
                    if (mi.DropDown != null) mi.DropDown.BackColor = back;
                    foreach (ToolStripItem sub in mi.DropDownItems) ApplyToolStripItemColors(sub, back, fore);
                }
            }
            catch { }
        }

        private void ComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var cb = sender as ComboBox; if (cb == null) return;
            e.DrawBackground();
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = isDark ? Color.FromArgb(40, 40, 40) : Color.White;
            Color fore = isDark ? darkFore : lightFore;
            Color selBack = isDark ? Color.FromArgb(60, 60, 60) : SystemColors.Highlight;
            Color selFore = isDark ? darkFore : SystemColors.HighlightText;
            using (var b = new SolidBrush(selected ? selBack : back)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = (e.Index >= 0 && e.Index < cb.Items.Count) ? cb.GetItemText(cb.Items[e.Index]) : cb.Text;
            TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, selected ? selFore : fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            e.DrawFocusRectangle();
        }

        void ShowAboutDialog()
        {
            var dlg = new Form
            {
                Text = "About PLS3DCH Installer",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(680, 520),
                MinimumSize = new Size(600, 400),
                Font = uiFont,
                BackColor = isDark ? darkBack : lightBack,
                FormBorderStyle = FormBorderStyle.Sizable,
                MaximizeBox = false,
                MinimizeBox = false
            };
			dlg.Load += (s, e) => UpdateTitleBarColor(dlg.Handle, isDark);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3, 
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); // Icon column
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // Content column
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Main text area
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Credits
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Close button
            dlg.Controls.Add(root);

            // Icon
            var iconBox = new PictureBox
            {
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Margin = new Padding(0, 8, 0, 0)
            };
            if (this.Icon != null) iconBox.Image = this.Icon.ToBitmap();
            else iconBox.Image = CreateDownloadIcon(48, Color.Gray);
            root.Controls.Add(iconBox, 0, 0);

            // --- TEXT AREA WITH CUSTOM SCROLLBAR ---
            
            // 1. Container Panel
            var textContainer = new Panel 
            { 
                Dock = DockStyle.Fill, 
                Margin = new Padding(4, 0, 0, 10),
                BackColor = isDark ? Color.FromArgb(40, 40, 40) : Color.White // Background for container
            };
            
            // 2. Custom ScrollBar
            var sb = new ThinScrollBar { Dock = DockStyle.Right, Width = 12 };
            sb.BackColor = textContainer.BackColor; // Match background
            
            // 3. RichTextBox
            var aboutBox = new RichTextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = textContainer.BackColor,
                ForeColor = isDark ? darkFore : lightFore,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.None, // Hide native scrollbar
            };

            aboutBox.Text =
                "PLS3DCH Installer\n\n" +
                "BASE MODE \n" +
                "Рекомендуемый режим для большинства пользователей.\n" +
                "•Update & Install: Автоматический поиск обновлений, скачивание и установка последней версии мода в один клик.\n" +
                "•Auto-detect path: Автоматическое определение папки с установленным Plasticity.\n" +
                "•Config: Выбор конфигурации (Don't install / Theme / Theme and shortcuts).\n" +
                "•Uninstall: Полное удаление мода и восстановление оригинальных файлов (включая конфиги).\n\n" +
                "Recommended mode for most users.\n" +
                "•Update & Install: Automatically checks for updates, downloads, and installs the latest version in one click.\n" +
                "•Auto-detect path: Automatically finds the Plasticity installation folder.\n" +
                "•Config: Select configuration variant (Don't install / Theme / Theme and shortcuts).\n" +
                "•Uninstall: Completely removes the mod and restores original files (including configs).\n\n" +
                "ADVANCED MODE \n" +
                "Расширенный режим для полного контроля.\n" +
                "•Manual Update & Install: Установка из выбранного локального архива (с предварительной очисткой старых файлов).\n" +
                "•Mod URL / Download: Загрузка архива по прямой ссылке.\n" +
                "•Browse Archive: Ручной выбор ZIP или RAR архива.\n\n" +
                "•Advanced mode for full control.\n" +
                "•Manual Update & Install: Install from a selected local archive (cleans up old files first).\n" +
                "•Mod URL / Download: Download archive via direct link.\n" +
                "•Browse Archive: Manually select a ZIP or RAR archive.\n\n" +
                "CREDITS \n" +
                "Special thanks to Vadim Danilkov (https://t.me/Dead_wall) for testing, advice, and developing the PLS3DCH mod itself.\n" +
                "Отдельное спасибо Вадиму Данилкову (https://t.me/Dead_wall) за тесты, советы и разработку самого мода PLS3DCH.\n\n" +
                "LICENSE \n" +
                "Copying and modifying code is allowed only with attribution to the original author and only for non-commercial use.\n" +
                "Копирование и изменение кода разрешено только с указанием оригинального автора и только для некоммерческого использования.";

            // Style headers
            void BoldText(string txt) {
                int index = aboutBox.Text.IndexOf(txt);
                if (index >= 0) {
                    aboutBox.Select(index, txt.Length);
                    aboutBox.SelectionFont = new Font(aboutBox.Font, FontStyle.Bold);
                }
            }
            BoldText("PLS3DCH Installer");
            BoldText("BASE MODE ");
            BoldText("ADVANCED MODE ");
            BoldText("CREDITS ");
            BoldText("LICENSE ");

            // FIX: Сброс выделения в начало
            aboutBox.Select(0, 0);
            aboutBox.ScrollToCaret();

            // --- SCROLLBAR LOGIC ---
            bool ignoreScroll = false;

            void UpdateScrollParams()
            {
                int totalLines = aboutBox.GetLineFromCharIndex(aboutBox.TextLength) + 1;
                int visibleLines = aboutBox.ClientSize.Height / aboutBox.Font.Height;
                int max = Math.Max(0, totalLines - visibleLines);
                
                if (max > 0) {
                    sb.Visible = true;
                    sb.Maximum = max;
                    sb.LargeChange = Math.Max(1, visibleLines);
                } else {
                    sb.Visible = false;
                }
            }

            sb.ValueChanged += (s, e) => {
                if (ignoreScroll) return;
                int currentLine = SendMessage(aboutBox.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
                int targetLine = sb.Value;
                int delta = targetLine - currentLine;
                if (delta != 0) SendMessage(aboutBox.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(delta));
            };

            aboutBox.MouseWheel += (s, e) => {
                if (!sb.Visible) return;
                int scrollLines = SystemInformation.MouseWheelScrollLines;
                int deltaLines = (e.Delta > 0) ? -scrollLines : scrollLines;
                sb.Value += deltaLines; // This triggers ValueChanged -> updates text
            };

            // Hook resize to update scrollbar size
            aboutBox.Resize += (s, e) => UpdateScrollParams();
            
            // Initial update once handle is created
            aboutBox.HandleCreated += (s, e) => {
                BeginInvoke(new Action(UpdateScrollParams));
            };

            // Add controls to container
            textContainer.Controls.Add(aboutBox);
            textContainer.Controls.Add(sb);
            root.Controls.Add(textContainer, 1, 0);

            // Credits
            var creditsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Padding = new Padding(4, 0, 0, 4)
            };

            var lblAuthor = new Label { Text = "Author: Kolesan", AutoSize = true, Font = new Font(uiFont, FontStyle.Bold), Margin = new Padding(0, 0, 0, 4) };
            var link1 = new LinkLabel { Text = "https://t.me/zero_dimension", AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
            var link2 = new LinkLabel { Text = "https://t.me/kolesan", AutoSize = true, Margin = new Padding(0, 0, 0, 2) };

            void OnLink(object s, LinkLabelLinkClickedEventArgs e) 
            {
                try { Process.Start(new ProcessStartInfo(((LinkLabel)s).Text) { UseShellExecute = true }); } catch { }
            }
            link1.LinkClicked += OnLink;
            link2.LinkClicked += OnLink;

            lblAuthor.ForeColor = isDark ? darkFore : lightFore;
            link1.LinkColor = isDark ? Color.FromArgb(86, 156, 214) : Color.FromArgb(0, 102, 204);
            link2.LinkColor = isDark ? Color.FromArgb(86, 156, 214) : Color.FromArgb(0, 102, 204);

            creditsPanel.Controls.Add(lblAuthor);
            creditsPanel.Controls.Add(link1);
            creditsPanel.Controls.Add(link2);
            
            root.Controls.Add(creditsPanel, 1, 1);

            // Close Button
            var btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 34,
                Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(225, 225, 225),
                ForeColor = isDark ? darkFore : lightFore,
                Margin = new Padding(0, 10, 0, 4), // Внешний отступ
                
                // ФИНАЛЬНЫЙ ФИКС:
                // 1. Стандартное центрирование
                TextAlign = ContentAlignment.MiddleCenter, 
                // 2. Включаем GDI+ рендеринг. Это автоматически "поднимает" текст 
                // на нужный уровень и гарантирует, что он не будет обрезан снизу.
                UseCompatibleTextRendering = true,
                // 3. Убираем внутренние отступы, чтобы не мешать отрисовке
                Padding = new Padding(0) 
            };
            btnClose.FlatAppearance.BorderSize = 0;

            btnClose.Click += (_, __) => dlg.Close();
            root.Controls.Add(btnClose, 1, 2);

            dlg.CancelButton = btnClose;
            dlg.ShowDialog(this);
        }

        Panel CreateBorderedTextBox(out TextBox tb, int height)
        {
            var container = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), Padding = new Padding(0), Height = height, Tag = "border" };
            var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = uiFont,
                BackColor = isDark ? Color.FromArgb(30, 30, 30) : Color.White,
                ForeColor = isDark ? darkFore : lightFore,
                Dock = DockStyle.Fill
            };

            int textH = TextRenderer.MeasureText("Ag", uiFont).Height;
            int topMargin = Math.Max(0, (height - textH) / 2 - 1);
            tb.Margin = new Padding(6, topMargin, 6, 0);

            inner.Controls.Add(tb);
            var underline = new Panel { Height = 2, Dock = DockStyle.Bottom, BackColor = isDark ? Color.FromArgb(90, 90, 90) : Color.FromArgb(200, 200, 200), Tag = "underline" };

            container.Controls.Add(inner);
            container.Controls.Add(underline);
            return container;
        }

        Button MakeButton(string text, int? width = null, int? height = null)
        {
            var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, Font = uiFont, Margin = new Padding(0, 2, 8, 2), Padding = new Padding(10, 6, 10, 6), UseVisualStyleBackColor = true };
            int textHeight = TextRenderer.MeasureText("Ag", uiFont).Height;
            b.Height = (height ?? (textHeight + 14)); b.MinimumSize = new Size(90, b.Height);
            if (width.HasValue) b.MinimumSize = new Size(width.Value, b.MinimumSize.Height);
            b.Anchor = AnchorStyles.Left | AnchorStyles.Top; return b;
        }

        TableLayoutPanel CreateRowSimple(string labelText, out TextBox textBox, int standardHeight)
        {
            var panel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = false, Height = standardHeight + 8, Padding = new Padding(0, 2, 0, 0), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var label = new Label { Text = labelText, AutoSize = true, Font = uiFont, Padding = new Padding(0, 6, 8, 0), Anchor = AnchorStyles.Left };
            var border = CreateBorderedTextBox(out textBox, standardHeight);
            panel.Controls.Add(label, 0, 0); panel.Controls.Add(border, 1, 0);
            return panel;
        }

        TableLayoutPanel CreateRowWithIconButton(string labelText, out TextBox textBox, out Button button, string buttonText, Image icon, int standardHeight)
        {
            var panel = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Top, AutoSize = false, Height = standardHeight + 8, Padding = new Padding(0, 2, 0, 0), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); int btnFixedWidth = 44; panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, btnFixedWidth));
            var label = new Label { Text = labelText, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = uiFont, Padding = new Padding(0, 6, 8, 0), Anchor = AnchorStyles.Left };
            var border = CreateBorderedTextBox(out textBox, standardHeight);

            button = new Button
            {
                Text = "",
                AutoSize = false,
                Font = uiFont,
                Margin = new Padding(6, 0, 0, 0),
                Padding = new Padding(6),
                UseVisualStyleBackColor = true,
                Width = btnFixedWidth - 6,
                Height = Math.Max(standardHeight - 6, TextRenderer.MeasureText("Ag", uiFont).Height + 6),
                ImageAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };

            int vertOffset = Math.Max(0, (standardHeight - button.Height) / 2);
            button.Margin = new Padding(6, vertOffset, 0, 0);

            if (icon != null)
            {
                var bmp = new Bitmap(icon);
                var targetSize = Math.Max(12, Math.Min(20, button.Height - 6));
                var scaled = new Bitmap(bmp, new Size(targetSize, targetSize));
                button.Image = scaled;
            }
            button.AccessibleName = buttonText;
            panel.Controls.Add(label, 0, 0); panel.Controls.Add(border, 1, 0); panel.Controls.Add(button, 2, 0);
            return panel;
        }

        void AdjustStatusWidth() { try { int padding = 40; int available = Math.Max(160, this.ClientSize.Width - progress.Width - padding); lblStatus.Width = available; } catch { } }
        void ResetHorizontalScroll() { try { int y = Math.Abs(mainPanel.AutoScrollPosition.Y); mainPanel.AutoScrollPosition = new Point(0, y); } catch { } }
        void Log(string msg)
        {
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            
            // 1. Заставляем текстовое поле прокрутиться в самый низ
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.ScrollToCaret();

            try { lblStatus.Text = msg; tip.SetToolTip(lblStatus, msg); } catch { }

            // 2. Пересчитываем размеры скроллбара
            UpdateThinScrollbars();

            // 3. Если скроллбар активен, принудительно опускаем ползунок вниз
            if (thinV.Visible)
            {
                try 
                { 
                    internalScrollUpdate = true;
                    thinV.Value = thinV.Maximum; 
                }
                finally { internalScrollUpdate = false; }
            }
        }
        void LogDetect(string msg) => Log("[detect] " + msg);

        async Task DownloadMod()
        {
            string url = txtUrl.Text.Trim(); if (string.IsNullOrEmpty(url)) { Log("No URL provided."); return; }
            try
            {
                progress.Style = ProgressBarStyle.Marquee;
                progress.StartMarquee();
                using var http = new HttpClient();
                var data = await http.GetByteArrayAsync(url);
                string ext = GuessExtensionFromUrlOrPath(url);
                string tempPath = Path.Combine(Path.GetTempPath(), "mod_" + Guid.NewGuid() + ext);
                await File.WriteAllBytesAsync(tempPath, data);
                txtArchive.Text = tempPath;
                lastDownloadedOriginalName = Path.GetFileName(new Uri(url).AbsolutePath);
                Log("Downloaded to temp: " + tempPath);
                if (chkSaveToDownloads.Checked)
                {
                    try
                    {
                        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                        Directory.CreateDirectory(downloads);
                        string name = lastDownloadedOriginalName;
                        if (string.IsNullOrWhiteSpace(name)) name = "PLS3DCH_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
                        string dest = Path.Combine(downloads, name);
                        File.Copy(tempPath, dest, overwrite: true);
                        Log("Saved a copy to Downloads: " + dest);
                    }
                    catch (Exception ex) { Log("Failed to save to Downloads: " + ex.Message); }
                }
            }
            catch (Exception ex) { Log("Download error: " + ex.Message); }
            finally { progress.StopMarquee(); progress.Style = ProgressBarStyle.Blocks; }
        }

        string GuessExtensionFromUrlOrPath(string s) { try { var lower = s.ToLowerInvariant(); if (lower.Contains(".rar")) return ".rar"; if (lower.Contains(".zip")) return ".zip"; } catch { } return ".zip"; }
        void BrowseArchive() { using var dlg = new OpenFileDialog { Filter = "Archives|*.zip;*.rar|ZIP|*.zip|RAR|*.rar" }; if (dlg.ShowDialog() == DialogResult.OK) txtArchive.Text = dlg.FileName; }
        void BrowseFolder() { using var dlg = new FolderBrowserDialog(); if (dlg.ShowDialog() == DialogResult.OK) txtTarget.Text = dlg.SelectedPath; }

        async Task ExtractArchiveAsync(string archivePath, string destDir)
        {
            Directory.CreateDirectory(destDir);
            string ext = Path.GetExtension(archivePath).ToLowerInvariant();
            if (ext == ".zip") { ZipFile.ExtractToDirectory(archivePath, destDir); return; }
            if (ext == ".rar")
            {
                if (FindSevenZip() == null) { bool ok = await EnsureSevenZipAsync(); if (!ok) throw new InvalidOperationException("7-Zip required to extract RAR."); }
                string sevenZip = FindSevenZip(); if (sevenZip == null) throw new InvalidOperationException("7-Zip (7z.exe) not found.");
                await RunProcessAsync(sevenZip, $"x -y \"{archivePath}\" -o\"{destDir}\""); return;
            }
            throw new NotSupportedException($"Unsupported archive type: {ext}");
        }

        void AutoDetect()
        {
            try
            {
                LogDetect("Running auto-detect...");
                int sel = cbVersion.SelectedIndex; string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (sel == 0)
                {
                    string betaBase = Path.Combine(user, "AppData", "Local", "plasticity-beta");
                    var betaCandidate = FindAppCandidate(betaBase, "app_window");
                    if (betaCandidate != null) { txtTarget.Text = betaCandidate.Path; LogDetect($"Detected Beta: {betaCandidate.Path}"); } else LogDetect("Beta installation not found.");
                    return;
                }
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string stableProgramFilesBase = Path.Combine(programFiles, "Plasticity");
                string stableUserBase = Path.Combine(user, "AppData", "Local", "Plasticity");
                var stableA = FindAppCandidate(stableProgramFilesBase, "app_window");
                var stableB = FindAppCandidate(stableUserBase, "main_window");
                AppCandidate bestStable = null;
                if (stableA != null) bestStable = stableA;
                if (stableB != null) { if (bestStable == null) bestStable = stableB; else if (CompareCandidates(stableB, bestStable) > 0) bestStable = stableB; }
                if (bestStable != null) { txtTarget.Text = bestStable.Path; LogDetect($"Detected Stable: {bestStable.Path}"); } else LogDetect("Stable installation not found.");
            }
            catch (Exception ex) { LogDetect("Auto-detect error: " + ex.Message); }
        }

        AppCandidate FindAppCandidate(string baseDir, string rendererSubFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir)) return null;
                var appDirs = Directory.GetDirectories(baseDir, "app-*");
                if (appDirs == null || appDirs.Length == 0) return null;
                AppCandidate best = null;
                foreach (var d in appDirs)
                {
                    string name = Path.GetFileName(d);
                    Version ver = ParseVersionFromFolderName(name);
                    DateTime created = Directory.GetCreationTimeUtc(d);
                    string candidatePath = Path.Combine(d, "resources", "app", ".webpack", "renderer", rendererSubFolder);
                    if (!Directory.Exists(candidatePath)) continue;
                    var cand = new AppCandidate { Path = candidatePath, Version = ver, CreatedUtc = created };
                    if (best == null) { best = cand; continue; }
                    if (best.Version != null && cand.Version != null) { if (CompareVersions(cand.Version, best.Version) > 0) best = cand; }
                    else if (cand.Version != null && best.Version == null) best = cand;
                    else if (cand.Version == null && best.Version == null) { if (cand.CreatedUtc > best.CreatedUtc) best = cand; }
                }
                return best;
            }
            catch { return null; }
        }

        int CompareCandidates(AppCandidate a, AppCandidate b)
        {
            if (a.Version != null && b.Version != null) return CompareVersions(a.Version, b.Version);
            if (a.Version != null && b.Version == null) return 1;
            if (a.Version == null && b.Version != null) return -1;
            return a.CreatedUtc.CompareTo(b.CreatedUtc);
        }

        Version ParseVersionFromFolderName(string folderName)
        {
            try
            {
                if (string.IsNullOrEmpty(folderName)) return null;
                int idx = folderName.IndexOf("app-", StringComparison.OrdinalIgnoreCase);
                string part = folderName;
                if (idx >= 0) part = folderName.Substring(idx + 4);
                var digits = new string(part.Where(c => char.IsDigit(c) || c == '.').ToArray());
                if (Version.TryParse(digits, out var v)) return v;
            }
            catch { }
            return null;
        }

        int CompareVersions(Version a, Version b) => a.CompareTo(b);
        class AppCandidate { public string Path { get; set; } = ""; public Version Version { get; set; } public DateTime CreatedUtc { get; set; } }

        async Task InstallOrUpdate(bool isUpdate)
        {
            string archive = txtArchive.Text.Trim(); 
            string target = txtTarget.Text.Trim();
            
            if (!File.Exists(archive)) { Log("Archive not found."); return; }
            if (!Directory.Exists(target)) { Log("Target folder not found."); return; }
            
            // Config paths
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string plasticRootDir = Path.Combine(userProfile, ".plasticity");       
            string targetV2 = Path.Combine(plasticRootDir, "config", "v2");         
            string backupV2 = Path.Combine(plasticRootDir, "config", "v2.orig");
            string startupFile = Path.Combine(plasticRootDir, "startup.plasticity");
            string backupStartupFile = Path.Combine(plasticRootDir, "startup.plasticity.orig");

            // --- ЛОГИКА ВЫБОРА КОНФИГА ---
            // Теперь мы не смотрим на текст, а смотрим на номер строки (0, 1, 2)
            // 0 = Don't install
            // 1 = Theme (.plasticity_no_keymap)
            // 2 = Theme and shortcuts (.plasticity)
            string configFolderName = null;
            switch (cbConfig.SelectedIndex)
            {
                case 1: configFolderName = ".plasticity_no_keymap"; break;
                case 2: configFolderName = ".plasticity"; break;
                default: configFolderName = null; break; // 0 или -1
            }

            try
            {
                progress.Style = ProgressBarStyle.Marquee; progress.StartMarquee();
                string tmp = Path.Combine(Path.GetTempPath(), "mod_unpack_" + Guid.NewGuid());
                await ExtractArchiveAsync(archive, tmp);
                
                // --- 1. Install Main Mod (index.html + mods folder) ---
                string srcIndex = Directory.GetFiles(tmp, "index.html", SearchOption.AllDirectories).FirstOrDefault() ?? Path.Combine(tmp, "index.html");
                string srcMods = Directory.GetDirectories(tmp, "mods", SearchOption.AllDirectories).FirstOrDefault() ?? Path.Combine(tmp, "mods");
                
                if (!File.Exists(srcIndex)) { Log("index.html missing in archive."); return; }
                
                string dstIndex = Path.Combine(target, "index.html"); 
                string dstMods = Path.Combine(target, "mods"); 
                string bak = Path.Combine(target, "index.html.orig");
                
                // Бекап index.html (копирование)
                try { if (!File.Exists(bak) && File.Exists(dstIndex)) { File.Copy(dstIndex, bak); Log("Backed up original index.html."); } } catch (Exception ex) { Log("Backup error: " + ex.Message); }
                
                // Очистка старых модов перед установкой
                try { if (isUpdate && Directory.Exists(dstMods)) { Directory.Delete(dstMods, true); Log("Removed old mods."); } } catch (Exception ex) { Log("Error removing old mods: " + ex.Message); return; }
                
                // Установка
                try { File.Copy(srcIndex, dstIndex, true); Log("Installed index.html."); } catch (Exception ex) { Log("Error copying index.html: " + ex.Message); return; }
                try { if (Directory.Exists(srcMods)) { CopyDirectory(srcMods, dstMods); Log("Installed mods."); } } catch (Exception ex) { Log("Error copying mods: " + ex.Message); return; }

                // --- 2. Install Config (Merge Logic) ---
                if (configFolderName != null)
                {
                    try
                    {
                        string configSrc = Path.Combine(tmp, configFolderName);
                        
                        // Поиск папки в архиве (если она лежит глубже)
                        if (!Directory.Exists(configSrc))
                        {
                            var potential = Directory.GetDirectories(tmp, configFolderName, SearchOption.AllDirectories).FirstOrDefault();
                            if (potential != null) configSrc = potential;
                        }

                        if (Directory.Exists(configSrc))
                        {
                            Directory.CreateDirectory(plasticRootDir);

                            // А. Бекап папки v2
                            if (Directory.Exists(targetV2) && !Directory.Exists(backupV2))
                            {
                                CopyDirectory(targetV2, backupV2);
                                Log($"Created backup copy of config v2 at {backupV2}");
                            }

                            // Б. Бекап файла startup.plasticity
                            if (File.Exists(startupFile) && !File.Exists(backupStartupFile))
                            {
                                File.Copy(startupFile, backupStartupFile, true);
                                Log($"Created backup copy of startup.plasticity at {backupStartupFile}");
                            }

                            // В. Установка конфигов
                            CopyDirectory(configSrc, plasticRootDir);
                            Log($"Merged config ({configFolderName}) into {plasticRootDir}");
                        }
                        else
                        {
                            Log($"Warning: Config folder '{configFolderName}' not found in archive.");
                        }
                    }
                    catch (Exception ex) { Log("Config install error: " + ex.Message); }
                }

                // 3. Write version info
                try
                {
                    string lang = (cbLanguage.SelectedItem?.ToString() ?? "ru").Trim().ToLowerInvariant();
                    string detectedVersion = DetectVersionFromArchiveName(Path.GetFileName(archive)) ?? (lastDownloadedOriginalName != null ? DetectVersionFromArchiveName(lastDownloadedOriginalName) : null) ?? "r0.000";
                    string installedVerPath = Path.Combine(dstMods, "installed_version.txt"); 
                    Directory.CreateDirectory(dstMods);
                    
                    // Записываем имя папки конфига, а не текст из UI, чтобы было технически точно
                    string configInfo = configFolderName != null ? $"+{configFolderName}" : "";
                    
                    await File.WriteAllTextAsync(installedVerPath, $"{lang} {detectedVersion} {configInfo}");
                    Log($"Updated installed version: {lang} {detectedVersion} {configInfo}");
                }
                catch (Exception ex) { Log("Failed to write installed_version.txt: " + ex.Message); }
                
                try { Directory.Delete(tmp, true); } catch { }
                Log((isUpdate ? "Update" : "Install") + " complete.");
            }
            catch (Exception ex) { Log("Error: " + ex.Message); }
            finally { progress.StopMarquee(); progress.Style = ProgressBarStyle.Blocks; }
        }

        async Task Uninstall()
        {
            string target = txtTarget.Text.Trim(); 
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string plasticRootDir = Path.Combine(userProfile, ".plasticity");
            string targetV2 = Path.Combine(plasticRootDir, "config", "v2");
            string backupV2 = Path.Combine(plasticRootDir, "config", "v2.orig");
            string targetStartup = Path.Combine(plasticRootDir, "startup.plasticity");
            string backupStartup = Path.Combine(plasticRootDir, "startup.plasticity.orig");
            
            if (!Directory.Exists(target)) { Log("Target folder not found."); return; }
            
            try
            {
                progress.Style = ProgressBarStyle.Marquee; progress.StartMarquee();
                
                // Uninstall Mod
                string dstIndex = Path.Combine(target, "index.html"); string bak = Path.Combine(target, "index.html.orig"); string dstMods = Path.Combine(target, "mods");
                if (File.Exists(bak)) { try { File.Copy(bak, dstIndex, true); Log("Restored original index.html."); } catch (Exception ex) { Log("Error restoring index.html: " + ex.Message); return; } }
                try { if (Directory.Exists(dstMods)) { Directory.Delete(dstMods, true); Log("Removed mods folder."); } } catch (Exception ex) { Log("Error removing mods: " + ex.Message); return; }

                // Uninstall Config (Restore Backups)
                if (Directory.Exists(backupV2))
                {
                    try
                    {
                        if (Directory.Exists(targetV2)) Directory.Delete(targetV2, true);
                        Directory.Move(backupV2, targetV2);
                        Log("Restored original config v2.");
                    }
                    catch (Exception ex) { Log("Error restoring config v2: " + ex.Message); }
                }

                if (File.Exists(backupStartup))
                {
                    try
                    {
                        if (File.Exists(targetStartup)) File.Delete(targetStartup);
                        File.Move(backupStartup, targetStartup);
                        Log("Restored original startup.plasticity.");
                    }
                    catch (Exception ex) { Log("Error restoring startup.plasticity: " + ex.Message); }
                }

                Log("Uninstall complete.");
            }
            catch (Exception ex) { Log("Uninstall error: " + ex.Message); }
            finally { progress.StopMarquee(); progress.Style = ProgressBarStyle.Blocks; }
        }

        void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir)) File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir)) CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        async Task CheckUpdatesAsync()
        {
            try
            {
                progress.Style = ProgressBarStyle.Marquee; progress.StartMarquee();
                try { AutoDetect(); ResetHorizontalScroll(); AdjustStatusWidth(); } catch { }
                
                var local = await ReadLocalInstalledVersionAsync();
                string currentLang = (cbLanguage.SelectedItem?.ToString() ?? "ru").Trim().ToLowerInvariant();
                
                if (local == null) Log("Local version not found. Checking remote...");
                else { 
                    Log($"Local installed: {local.Lang} {local.Version}"); 
                    if (!string.Equals(local.Lang, currentLang, StringComparison.OrdinalIgnoreCase)) 
                        Log($"Note: selected language '{currentLang}' differs from installed '{local.Lang}'."); 
                }

                string versionsUrl = txtVersionsUrl.Text.Trim(); 
                if (string.IsNullOrWhiteSpace(versionsUrl)) versionsUrl = defaultVersionsJson;
                
                // Cache busting
                if (versionsUrl.Contains("raw.githubusercontent.com"))
                {
                    versionsUrl += $"?t={DateTime.Now.Ticks}";
                }

                using var http = new HttpClient();
                http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

                Log($"Fetching: {versionsUrl}");
                var bytes = await http.GetByteArrayAsync(versionsUrl);
                var list = JsonSerializer.Deserialize<VersionEntry[]>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (list == null || list.Length == 0) { Log("versions.json is empty."); return; }
                
                var langEntries = list.Where(e => NormalizeLang(e.lang) == currentLang).ToList();
                if (langEntries.Count == 0) { Log($"No entries for language '{currentLang}' in versions.json."); return; }
                
                var latest = langEntries.OrderByDescending(e => ParseRVersion(e.version)).First();
                string remoteVer = latest.version ?? "r0.000"; 
                
                Log($"Remote latest for {currentLang}: {remoteVer}");
                
                bool langChanged = (local != null && !string.Equals(local.Lang, currentLang, StringComparison.OrdinalIgnoreCase));
                bool versionNewer = (local != null && CompareRVersions(remoteVer, local.Version) > 0);
                
                if (local != null)
                {
                    var vRemote = ParseRVersion(remoteVer);
                    var vLocal = ParseRVersion(local.Version);
                    Log($"Debug compare: Remote({vRemote.A}.{vRemote.B}.{vRemote.C}) vs Local({vLocal.A}.{vLocal.B}.{vLocal.C}) -> Newer? {versionNewer}");
                }

                bool needUpdate = (local == null) || versionNewer || langChanged;

                if (!needUpdate) { 
                    MessageBox.Show($"You already have the latest version ({local!.Lang} {local.Version}).", "Update", MessageBoxButtons.OK, MessageBoxIcon.Information); 
                    return; 
                }
                
                var askDownload = MessageBox.Show($"New version available: {currentLang} {remoteVer}.\nDo you want to download it now?", "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (askDownload != DialogResult.Yes) return;
                
                if (string.IsNullOrWhiteSpace(latest.url)) { Log("No URL for latest version."); return; }
                
                string remoteUrl = latest.url!;
                var data = await http.GetByteArrayAsync(remoteUrl);
                string ext = GuessExtensionFromUrlOrPath(remoteUrl);
                string tempPath = Path.Combine(Path.GetTempPath(), "mod_" + Guid.NewGuid() + ext);
                await File.WriteAllBytesAsync(tempPath, data);
                txtArchive.Text = tempPath; 
                lastDownloadedOriginalName = Path.GetFileName(new Uri(remoteUrl).AbsolutePath); 
                Log("Downloaded latest to temp: " + tempPath);
                
                if (chkSaveToDownloads.Checked)
                {
                    try
                    {
                        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                        Directory.CreateDirectory(downloads);
                        string name = lastDownloadedOriginalName;
                        if (string.IsNullOrWhiteSpace(name)) name = $"PLS3DCH_{currentLang}_{remoteVer}.zip";
                        string dest = Path.Combine(downloads, name);
                        File.Copy(tempPath, dest, overwrite: true);
                        Log("Saved a copy to Downloads: " + dest);
                    }
                    catch (Exception ex) { Log("Failed to save to Downloads: " + ex.Message); }
                }

                if (!string.IsNullOrWhiteSpace(latest.sha256))
                {
                    try
                    {
                        string expected = (latest.sha256 ?? "").Trim().ToLowerInvariant();
                        if (expected.StartsWith("sha256:")) expected = expected.Substring("sha256:".Length).Trim();
                        string calc = await ComputeSha256Async(tempPath);
                        Log("Calculated SHA256: " + calc); 
                        if (!string.IsNullOrEmpty(expected))
                        {
                            if (!calc.Equals(expected, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("SHA256 mismatch. Aborting install.", "Integrity check failed", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                            Log("SHA256 verified.");
                        }
                    }
                    catch (Exception ex) { Log("SHA256 check error: " + ex.Message); var cont = MessageBox.Show("Failed to verify SHA256. Continue installation?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning); if (cont != DialogResult.Yes) return; }
                }

                var askInstall = MessageBox.Show($"Downloaded {remoteVer} for {currentLang}. Install now?", "Install", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (askInstall != DialogResult.Yes) { Log("Download complete. Installation postponed by user."); return; }
                
                await InstallOrUpdate(true);
            }
            catch (Exception ex) { Log("Check update error: " + ex.Message); }
            finally { progress.StopMarquee(); progress.Style = ProgressBarStyle.Blocks; }
        }

        async Task<InstalledVersion> ReadLocalInstalledVersionAsync()
        {
            try
            {
                string target = txtTarget.Text.Trim(); if (!Directory.Exists(target)) return null;
                string p = Path.Combine(target, "mods", "installed_version.txt"); if (!File.Exists(p)) return null;
                string s = await File.ReadAllTextAsync(p); var parts = Regex.Split(s.Trim(), @"\s+");
                if (parts.Length >= 2) return new InstalledVersion { Lang = NormalizeLang(parts[0]), Version = parts[1] }; return null;
            }
            catch { return null; }
        }

        string NormalizeLang(string s) { if (string.IsNullOrWhiteSpace(s)) return "ru"; s = s.Trim().ToLowerInvariant(); return (s == "ru" || s == "en") ? s : "ru"; }
        string DetectVersionFromArchiveName(string name) { try { if (string.IsNullOrWhiteSpace(name)) return null; var m = Regex.Match(name, @"(?:^|[_-])r(\d+(\.\d+)*)", RegexOptions.IgnoreCase); if (m.Success) return "r" + m.Groups[1].Value; } catch { } return null; }
        
        VersionKey ParseRVersion(string r) 
        { 
            if (string.IsNullOrWhiteSpace(r)) return new VersionKey(0, 0, 0); 
            string s = r.Trim().ToLowerInvariant(); 
            if (s.StartsWith("ver")) s = s.Substring(3);
            else if (s.StartsWith("v") || s.StartsWith("r")) s = s.Substring(1);
            var parts = s.Split('.'); 
            int a = parts.Length > 0 && int.TryParse(parts[0], out var x) ? x : 0; 
            int b = parts.Length > 1 && int.TryParse(parts[1], out var y) ? y : 0; 
            int c = parts.Length > 2 && int.TryParse(parts[2], out var z) ? z : 0; 
            return new VersionKey(a, b, c); 
        }

        int CompareRVersions(string a, string b) { var va = ParseRVersion(a); var vb = ParseRVersion(b); int cmp = va.A.CompareTo(vb.A); if (cmp != 0) return cmp; cmp = va.B.CompareTo(vb.B); if (cmp != 0) return cmp; return va.C.CompareTo(vb.C); }
        struct VersionKey { public int A, B, C; public VersionKey(int a, int b, int c) { A = a; B = b; C = c; } }
        class InstalledVersion { public string Lang { get; set; } = "ru"; public string Version { get; set; } = "r0.000"; }
        class VersionEntry { public string lang { get; set; } public string version { get; set; } public string url { get; set; } public string sha256 { get; set; } }

        async Task<string> ComputeSha256Async(string path) { using var stream = File.OpenRead(path); using var sha = System.Security.Cryptography.SHA256.Create(); var hash = await sha.ComputeHashAsync(stream); return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); }

        async Task<bool> EnsureSevenZipAsync()
        {
            string found = FindSevenZip(); if (!string.IsNullOrEmpty(found)) return true;
            var res = MessageBox.Show("7-Zip не найден. Установить сейчас? (Потребуются права администратора)", "7-Zip", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return false;
            string url = "https://www.7-zip.org/a/7z1900-x64.exe";
            string tmp = Path.Combine(Path.GetTempPath(), "7z_installer_" + Guid.NewGuid() + ".exe");
            try
            {
                using var http = new HttpClient();
                var data = await http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(tmp, data);
            }
            catch (Exception ex)
            {
                Log("Ошибка загрузки 7-Zip: " + ex.Message);
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo { FileName = tmp, UseShellExecute = true, Verb = "runas" };
                Process.Start(psi);
                Log("Запущен установщик 7-Zip. После установки повторите операцию.");
                return false;
            }
            catch (Exception ex)
            {
                Log("Не удалось запустить установщик 7-Zip: " + ex.Message);
                return false;
            }
        }

        string FindSevenZip()
        {
            if (IsOnPath("7z.exe")) return "7z.exe";
            var candidates = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "7-Zip", "7z.exe") };
            foreach (var c in candidates) if (File.Exists(c)) return c; return null;
        }

        bool IsOnPath(string exeName)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = "where", Arguments = exeName, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = Process.Start(psi); if (p == null) return false;
                string output = p.StandardOutput.ReadToEnd(); p.WaitForExit();
                var first = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return !string.IsNullOrWhiteSpace(first) && File.Exists(first);
            }
            catch { return false; }
        }

        async Task RunProcessAsync(string fileName, string args)
        {
            var tcs = new TaskCompletionSource<int>();
            var psi = new ProcessStartInfo { FileName = fileName, Arguments = args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Directory.GetCurrentDirectory() };
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Log(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("ERR: " + e.Data); };
            p.Exited += (_, __) => tcs.TrySetResult(p.ExitCode);
            if (!p.Start()) throw new InvalidOperationException($"Failed to start: {fileName}");
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            int code = await tcs.Task; if (code != 0) throw new InvalidOperationException($"{fileName} exited with code {code}");
        }

        Image GetFolderIconBitmap() { try { string folder = Environment.GetFolderPath(Environment.SpecialFolder.Windows); return GetIconForPath(folder, true); } catch { return null; } }

        Bitmap CreateDownloadIcon(int size = 24, Color color = default)
        {
            if (color == default) color = Color.FromArgb(0, 160, 0);
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float w = size, h = size; float stroke = Math.Max(1f, size / 12f);
            using var pen = new Pen(color, stroke) { EndCap = System.Drawing.Drawing2D.LineCap.Round, StartCap = System.Drawing.Drawing2D.LineCap.Round };
            float shaftX = w * 0.5f; float shaftTop = h * 0.18f; float shaftBottom = h * 0.60f;
            g.DrawLine(pen, shaftX, shaftTop, shaftX, shaftBottom);
            float headH = h * 0.18f;
            var p1 = new PointF(shaftX - headH, shaftBottom - headH * 0.1f);
            var p2 = new PointF(shaftX + headH, shaftBottom - headH * 0.1f);
            var p3 = new PointF(shaftX, shaftBottom + headH * 0.9f);
            using var brush = new SolidBrush(color); g.FillPolygon(brush, new[] { p1, p2, p3 });
            float underlineY = h * 0.85f; float underlineMargin = w * 0.18f;
            using var underlinePen = new Pen(color, Math.Max(1f, stroke)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(underlinePen, underlineMargin, underlineY, w - underlineMargin, underlineY);
            return bmp;
        }

        Image GetIconForPath(string path, bool isFolder)
        {
            const uint SHGFI_ICON = 0x000000100; const uint SHGFI_SMALLICON = 0x000000001; const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON; uint attrs = 0;
            if (isFolder) attrs = FILE_ATTRIBUTE_DIRECTORY; else flags |= SHGFI_USEFILEATTRIBUTES;
            IntPtr hImg = SHGetFileInfo(path, attrs, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (hImg == IntPtr.Zero) return null;
            try { Icon ico = Icon.FromHandle(shinfo.hIcon); var bmp = ico.ToBitmap(); return bmp; } finally { if (shinfo.hIcon != IntPtr.Zero) DestroyIcon(shinfo.hIcon); }
        }

        // --- FIX TITLE BAR COLOR ---
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void UpdateTitleBarColor(IntPtr handle, bool dark)
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                int useImmersiveDarkMode = dark ? 1 : 0;

                if (DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useImmersiveDarkMode, sizeof(int));
                }
            }
        }
        // ---------------------------
		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
        const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    }
}
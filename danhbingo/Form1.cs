using danhbingo.Auto;
using OpenCvSharp;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace danhbingo
{
    public partial class Form1 : Form
    {
        ComboBox cboWindows = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
        TextBox txtWindow = new() { Width = 340, PlaceholderText = "Hoặc gõ tên cửa sổ (nếu không chọn ở dropdown)" };

        Button btnRefresh = new() { Text = "Refresh windows", Width = 140 };
        Button btnSave = new() { Text = "Save", Width = 80 };
        Button btnStart = new() { Text = "Start", Width = 80 };
        Button btnStop = new() { Text = "Stop", Width = 80, Enabled = false };
        public static IntPtr RootWindow;
        NumericUpDown nudThreshold = new()
        {
            DecimalPlaces = 2,
            Increment = 0.01M,
            Minimum = 0.50M,
            Maximum = 0.99M,
            Value = 0.87M,
            Width = 80
        }; public enum BossClickResult
        {
            NotFound,
            ClickedNoFight,
            FightStarted
        }

        Label lblStatus = new() { AutoSize = true, Text = "Ready." };
        TextBox txtLog = new()
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Height = 150,
            Width = 400
        };


        //---------------------------
        // ✅ Folder ảnh
        //---------------------------
        ComboBox cboImageFolder = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };

        //---------------------------
        // ✅ Map selection
        //---------------------------
        public CheckedListBox MapList => chkMaps;
        CheckedListBox chkMaps = new()
        {
            CheckOnClick = true,
            Height = 110,
            Width = 260   
        };


        public const byte VK_OEM_3 = 0xC0;   // phím ~

        class AppConfig
        {
            public string? WindowName { get; set; }
            public double Threshold { get; set; } = 0.87;
            public string? ImageFolder { get; set; }    // ✅ thêm folder ảnh
        }

        readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        CancellationTokenSource? cts;
        IntPtr hwnd = IntPtr.Zero;

        string[] allTemplates = Array.Empty<string>();

        public static string CurrentPlayerAvatar = "";

        static DateTime lastPressTime = DateTime.MinValue;
        static bool _isMiniMapOpen = false;
        static DateTime _lastMiniMapPress = DateTime.MinValue;

        static DateTime _lastWorldMapPress = DateTime.MinValue;
        static bool _isWorldMapOpen = false;

        //------------------------------
        // ===== Constructor =====
        //------------------------------
        public Form1()
        {
            Text = "Auto event";
            Width = 680;
            Height = 450;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var p = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(10)
            };

            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            //--------------------------
            // Windows
            //--------------------------
            p.Controls.Add(new Label() { Text = "Chọn cửa sổ:", AutoSize = true }, 0, 0);
            p.Controls.Add(cboWindows, 1, 0);

            p.Controls.Add(new Label() { Text = "Hoặc nhập tên cửa sổ:", AutoSize = true }, 0, 1);
            p.Controls.Add(txtWindow, 1, 1);

            //--------------------------
            // Threshold
            //--------------------------
            var rowSetting = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            rowSetting.Controls.Add(btnRefresh);
            rowSetting.Controls.Add(btnSave);
            rowSetting.Controls.Add(new Label() { Text = "Threshold:", AutoSize = true, Padding = new Padding(20, 8, 5, 0) });
            rowSetting.Controls.Add(nudThreshold);

            p.Controls.Add(new Label() { Text = "Thiết lập:", AutoSize = true }, 0, 2);
            p.Controls.Add(rowSetting, 1, 2);

            //--------------------------
            // Folder ảnh
            //--------------------------
            p.Controls.Add(new Label() { Text = "Folder ảnh:", AutoSize = true }, 0, 3);
            p.Controls.Add(cboImageFolder, 1, 3);


            //--------------------------
            // Map select
            //--------------------------
            p.Controls.Add(new Label() { Text = "Chọn map:", AutoSize = true }, 0, 4);
            p.Controls.Add(chkMaps, 1, 4);

            //--------------------------
            // Control
            //--------------------------
            var rowCtrl = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            rowCtrl.Controls.Add(btnStart);
            rowCtrl.Controls.Add(btnStop);

            p.Controls.Add(new Label() { Text = "Điều khiển:", AutoSize = true }, 0, 5);
            p.Controls.Add(rowCtrl, 1, 5);

            //--------------------------
            // Status
            //--------------------------
            p.Controls.Add(new Label() { Text = "Trạng thái:", AutoSize = true }, 0, 6);
            p.Controls.Add(lblStatus, 1, 6);
            p.Controls.Add(txtLog, 1, 7);

            Controls.Add(p);

            //--------------------------
            // Events
            //--------------------------
            Load += Form1_Load;
            btnRefresh.Click += (_, __) => LoadWindowList();
            btnSave.Click += (_, __) => SaveConfig();
            btnStart.Click += async (_, __) => await StartAsync();
            btnStop.Click += (_, __) => StopBot();
           
        }
        //===========================================
        //  ✅ FORM LOAD
        //===========================================
        void Form1_Load(object? sender, EventArgs e)
        {
            LoadWindowList();
            LoadConfig();
            LoadMaps();

            string defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh");

            LoadImageFolders();


            ReloadImageTemplates();
        }

        //===========================================
        // ✅ LOAD WINDOW LIST
        //===========================================
        void LoadWindowList()
        {
            cboWindows.Items.Clear();
            var titles = new List<string>();

            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName.ToLower();
                    string title = p.MainWindowTitle?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    if (name.Contains("flash") || name.Contains("magic") || name.Contains("mk") || name.Contains("dy"))
                        titles.Add(title);
                    else if (title.ToLower().Contains("flash") || title.ToLower().Contains("magic") ||
                             title.ToLower().Contains("mk") || title.ToLower().Contains("dy"))
                        titles.Add(title);
                }
                catch { }
            }

            titles.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var t in titles) cboWindows.Items.Add(t);

            if (cboWindows.Items.Count > 0)
                cboWindows.SelectedIndex = 0;

            lblStatus.Text = $"Đã quét {titles.Count} tiến trình game/flash.";
        }

        //===========================================
        // ✅ BROWSE IMAGE FOLDER
        //===========================================

        void LoadImageFolders()
        {
            cboImageFolder.Items.Clear();

            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh");

            if (!Directory.Exists(baseDir))
                Directory.CreateDirectory(baseDir);

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                cboImageFolder.Items.Add(Path.GetFileName(dir));
            }

            if (cboImageFolder.Items.Count > 0)
                cboImageFolder.SelectedIndex = 0;
        }

        string ResolveImageFolder()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh");

            if (cboImageFolder.SelectedItem == null)
                return baseDir;   // fallback

            return Path.Combine(baseDir, cboImageFolder.SelectedItem.ToString()!);
        }


        //===========================================
        // ✅ Load lại template PNG
        //===========================================
        void ReloadImageTemplates()
        {
            var folder = ResolveImageFolder();

            if (!Directory.Exists(folder))
            {
                allTemplates = Array.Empty<string>();
                lblStatus.Text = $"✘ Folder không tồn tại.";
                return;
            }

            // ✅ Load toàn bộ ảnh PNG (kể cả subfolders)
            allTemplates = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories);

            lblStatus.Text = $"✅ Loaded {allTemplates.Length} ảnh.";
        }


        //===========================================
        // ✅ LOAD MAP LIST vào UI
        //===========================================
        void LoadMaps()
        {
            chkMaps.Items.Clear();
            foreach (var m in MapData.LocalMapPoints.Keys)
                chkMaps.Items.Add(m, true);    // ✅ default = tick hết
        }

        //===========================================
        // ✅ SAVE CONFIG
        //===========================================
        void SaveConfig()
        {
            var cfg = new AppConfig
            {
                WindowName = GetWantedWindowTitle(),
                Threshold = (double)nudThreshold.Value,
                ImageFolder = cboImageFolder.SelectedItem?.ToString()
            };

            File.WriteAllText(
                ConfigPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true })
            );

            lblStatus.Text = "✅ Đã lưu config.";
        }



        //===========================================
        // ✅ LOAD CONFIG
        //===========================================
        void LoadConfig()
        {
            if (!File.Exists(ConfigPath))
                return;

            try
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                if (cfg != null)
                {
                    nudThreshold.Value = (decimal)cfg.Threshold;

                    if (!string.IsNullOrWhiteSpace(cfg.WindowName))
                    {
                        int idx = cboWindows.FindStringExact(cfg.WindowName);
                        if (idx >= 0) cboWindows.SelectedIndex = idx;
                        else txtWindow.Text = cfg.WindowName;
                    }

                    if (!string.IsNullOrWhiteSpace(cfg.ImageFolder))
                    {
                        int idx = cboImageFolder.FindStringExact(cfg.ImageFolder);
                        if (idx >= 0)
                            cboImageFolder.SelectedIndex = idx;
                    }
                }
            }
            catch { }
        }



        //===========================================
        // ✅ Lấy tên cửa sổ game
        //===========================================
        string GetWantedWindowTitle()
        {
            var t = (cboWindows.SelectedItem as string) ?? "";
            if (!string.IsNullOrWhiteSpace(txtWindow.Text))
                t = txtWindow.Text.Trim();
            return t;
        }
        //===========================================
        // ✅ START BOT
        //===========================================
        async Task StartAsync()
        {
            IntPtr rootHwnd = hwnd;
            var winTitle = GetWantedWindowTitle();
            if (string.IsNullOrWhiteSpace(winTitle))
            {
                MessageBox.Show("Chưa chọn/nhập tên cửa sổ game.", "Thiếu thông tin");
                return;
            }

            // 1) tìm handle cửa sổ
            hwnd = FindWindow(null, winTitle);
            if (hwnd == IntPtr.Zero)
            {
                MessageBox.Show($"Không tìm thấy cửa sổ: \"{winTitle}\"", "Lỗi");
                return;
            }

            // 2) tìm child flash (nếu có)
            IntPtr flashHwnd = FindWindowEx(hwnd, IntPtr.Zero, "MacromediaFlashPlayerActiveX", null);
            if (flashHwnd == IntPtr.Zero)
                flashHwnd = FindWindowEx(hwnd, IntPtr.Zero, "ShockwaveFlash", null);
            if (flashHwnd == IntPtr.Zero)
                flashHwnd = FindWindowEx(hwnd, IntPtr.Zero, "WindowClassNN", null);
            if (flashHwnd != IntPtr.Zero) hwnd = flashHwnd;
            Form1.RootWindow = rootHwnd;

            // 3) kiểm tra folder ảnh + nạp template
            var imgFolder = ResolveImageFolder();
            if (!Directory.Exists(imgFolder))
            {
                MessageBox.Show("Folder ảnh chưa đúng.", "Thiếu ảnh");
                return;
            }

            ReloadImageTemplates();
            if (allTemplates.Length == 0)
            {
                MessageBox.Show("Không có ảnh *.png trong thư mục đã chọn.", "Thiếu ảnh");
                return;
            }

            // 4) phát hiện player avatar từ player_*.png (để check vào/ra combat)
            CurrentPlayerAvatar = DetectPlayerAvatar(hwnd, (double)nudThreshold.Value, Log);
            if (string.IsNullOrEmpty(CurrentPlayerAvatar))
            {
                MessageBox.Show("Không phát hiện được ảnh nhân vật (player_*.png).", "Lỗi nhận diện");
                return;
            }

            // 5) danh sách map được tick
            var wantedMaps = chkMaps.CheckedItems.Cast<string>().ToList();
            if (wantedMaps.Count == 0)
            {
                MessageBox.Show("Chưa chọn map nào trong danh sách.", "Thiếu dữ liệu");
                return;
            }

            // 6) lock UI và chạy
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            cboWindows.Enabled =
            txtWindow.Enabled =
            btnRefresh.Enabled =
            btnSave.Enabled =
            cboImageFolder.Enabled =
            chkMaps.Enabled = false;


            SaveConfig();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            lblStatus.Text = $"RUNNING... hwnd=0x{hwnd.ToInt64():X} | maps={wantedMaps.Count}";

            await Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (var map in wantedMaps)
                    {
                        if (token.IsCancellationRequested) return;

                        // bay tới map và quét/đánh
                        AutoMapController.TravelToMap(
                            hwnd,
                            map,
                            Log,
                            img => PlayerDetector.WaitForPlayerToReach(hwnd, CurrentPlayerAvatar, Log),
                            this,
                            token
                        );

                        if (token.IsCancellationRequested) return;

                        // nghỉ nhẹ giữa các map (1.2s nhưng có kiểm tra token)
                        for (int i = 0; i < 12; i++)
                        {
                            if (token.IsCancellationRequested) return;
                            Thread.Sleep(100);
                        }
                    }
                }
            });

            // khi thoát vòng lặp
            StopBot();


        }

        //===========================================
        // ✅ Dò ảnh nhân vật: player_*.png
        //    → sử dụng để biết đang trong/ngoài combat
        //===========================================
        public static string DetectPlayerAvatar(
     IntPtr hwnd,
     double threshold,
     Action<string> log)
        {
            try
            {
                using var frame = CaptureWindowClient(hwnd);

                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh");

                string bestFile = "";
                double bestScore = 0;

                foreach (var f in Directory.GetFiles(baseDir, "player_*.png", SearchOption.AllDirectories))
                {
                    using var tpl = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(f);
                    var (pt, score) = MatchOnce(frame, tpl, threshold);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFile = f;
                    }
                }

                if (!string.IsNullOrEmpty(bestFile) && bestScore >= threshold)
                {
                    log($"🧍 Phát hiện nhân vật: {Path.GetFileName(bestFile)} (score={bestScore:F2})");
                    return bestFile;
                }

                log($"⚠️ Không phát hiện nhân vật (best={bestScore:F2})");
                return string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show("DetectPlayerAvatar error: " + ex.Message);
                return string.Empty;
            }
        }

        //===========================================
        // ✅ STOP BOT
        //===========================================
        void StopBot()
        {
            try { cts?.Cancel(); } catch { }

            // Đợi background task dừng trong 50–100ms
            Task.Delay(50).ContinueWith(_ =>
            {
                cts = null;
            });

            btnStart.Enabled = true;
            btnStop.Enabled = false;

            cboWindows.Enabled =
            txtWindow.Enabled =
            btnRefresh.Enabled =
            btnSave.Enabled =
            cboImageFolder.Enabled =
            chkMaps.Enabled = true;

            lblStatus.Text = "Stopped.";
        }



        //===========================================
        // ✅ LOG ra label (thread-safe)
        //===========================================
        void Log(string msg)
        {
            try
            {
                BeginInvoke(new Action(() =>
                {
                    lblStatus.Text = msg;

                    txtLog.AppendText($"{DateTime.Now:HH:mm:ss}  {msg}\r\n");

                    // Auto scroll xuống cuối
                    txtLog.SelectionStart = txtLog.Text.Length;
                    txtLog.ScrollToCaret();
                }));
            }
            catch { }
        }

        //===========================================
        // ✅ P/Invoke
        //===========================================
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_CHAR = 0x0102;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        //===========================================
        // ✅ SHOW MOUSE POS (debug)
        //===========================================
        public static void ShowMousePos(IntPtr hwnd)
        {
            POINT pt;
            GetCursorPos(out pt);
            ScreenToClient(hwnd, ref pt);
            MessageBox.Show($"Client X={pt.X}, Y={pt.Y}", "Tọa độ");
        }

        //===========================================
        // ✅ CAPTURE WINDOW (client)
        //===========================================
        public static System.Drawing.Bitmap CaptureWindowClient(IntPtr hwnd)
        {
            if (!GetWindowRect(hwnd, out var r))
                throw new Exception("GetWindowRect failed");

            int w = Math.Max(1, r.Right - r.Left);
            int h = Math.Max(1, r.Bottom - r.Top);

            var bmp = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                var hdc = g.GetHdc();
                bool ok = PrintWindow(hwnd, hdc, 0);
                g.ReleaseHdc(hdc);
                if (!ok)
                    g.CopyFromScreen(r.Left, r.Top, 0, 0, new System.Drawing.Size(w, h));
            }
            return bmp;
        }

        //===========================================
        // ✅ CLICK CLIENT
        //===========================================
        public static void ClickClient(IntPtr hwnd, int clientX, int clientY)
        {
            int lParam = (clientY << 16) | (clientX & 0xFFFF);
            SendMessage(hwnd, WM_LBUTTONDOWN, 1, lParam);
            Thread.Sleep(30);
            SendMessage(hwnd, WM_LBUTTONUP, 0, lParam);
        }

        //===========================================
        // ✅ KEYBOARD
        //===========================================
        [DllImport("user32.dll", SetLastError = true)]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;
        const int KEYEVENTF_KEYUP = 0x0002;

        //===========================================
        // ✅ Gửi phím cứng (~) vào game
        //===========================================
        public static void PressKey(byte vk, IntPtr hwnd)
        {
            try
            {
                if (DateTime.Now - lastPressTime < TimeSpan.FromMilliseconds(500))
                    return;

                lastPressTime = DateTime.Now;

                PostMessage(hwnd, WM_KEYDOWN, vk, 0);
                Thread.Sleep(50);
                PostMessage(hwnd, WM_KEYUP, vk, 0);

                // fallback
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                Thread.Sleep(30);
                keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                MessageBox.Show("PressKey error: " + ex.Message);
            }
        }

        //===========================================
        // ✅ SEND KEY — Dùng SendInput để đảm bảo nặng
        //===========================================
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        const int INPUT_KEYBOARD = 1;

        public static void SendKeyToWindow(IntPtr hwnd, byte vk)
        {
            try
            {
                SetForegroundWindow(hwnd);
                Thread.Sleep(40);

                var inputs = new INPUT[2];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].U.ki = new KEYBDINPUT { wVk = vk };

                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].U.ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP };

                SendInput((uint)inputs.Length, inputs, INPUT.Size);
            }
            catch (Exception ex)
            {
                MessageBox.Show("SendKeyToWindow error: " + ex.Message);
            }
        }
        //===========================================
        // ✅ Toggle Mini Map (~)
        //===========================================
        public static void ToggleMiniMap(IntPtr hwnd, bool open)
        {
            if (_isMiniMapOpen == open) return;

            // gửi vào đúng Flash HWND
            PostMessage(hwnd, WM_CHAR, 0x60, 0);

            Thread.Sleep(30);
            _isMiniMapOpen = open;
        }


        public static void SendTilde(IntPtr hwnd)
        {
            const int WM_KEYDOWN = 0x0100;
            const int WM_KEYUP = 0x0101;
            const int VK_OEM_3 = 0xC0;

            PostMessage(hwnd, WM_KEYDOWN, VK_OEM_3, 0);
            Thread.Sleep(30);
            PostMessage(hwnd, WM_KEYUP, VK_OEM_3, 0);
        }

        //===========================================
        // ✅ Toggle World Map (ấn ~ rồi click nút)
        //===========================================
        public static void ToggleWorldMap(IntPtr hwnd)
        {
            // Bấm phím M
            PressKey((byte)Keys.M, hwnd);
            Thread.Sleep(400);
        }




        //===========================================
        // ✅ MATCH 1 TEMPLATE
        //   → trả về (point, score)
        //===========================================
        public static (System.Drawing.Point?, double) MatchOnce(
            System.Drawing.Bitmap hayBmp,
            System.Drawing.Bitmap tplBmp,
            double threshold)
        {
            using var hay = BitmapToMat(hayBmp);
            using var tpl = BitmapToMat(tplBmp);

            using var hayGray = new Mat();
            using var tplGray = new Mat();

            Cv2.CvtColor(hay, hayGray, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(tpl, tplGray, ColorConversionCodes.BGR2GRAY);

            if (hayGray.Cols < tplGray.Cols || hayGray.Rows < tplGray.Rows)
                return (null, 0);

            using var result = new Mat(
                hayGray.Rows - tplGray.Rows + 1,
                hayGray.Cols - tplGray.Cols + 1,
                MatType.CV_32FC1
            );

            Cv2.MatchTemplate(hayGray, tplGray, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

            if (maxVal >= threshold)
            {
                var center = new System.Drawing.Point(
                    maxLoc.X + tplGray.Cols / 2,
                    maxLoc.Y + tplGray.Rows / 2
                );
                return (center, maxVal);
            }

            return (null, maxVal);
        }

        //===========================================
        // ✅ FIND BEST TEMPLATE FROM LIST
        //   → dùng cho auto scan boss / bingo
        //===========================================
        public static (System.Drawing.Point? found, double score, string? file)
            FindBestTemplate(System.Drawing.Bitmap hay, string[] files, double threshold)
        {
            System.Drawing.Point? bestPt = null;
            double bestScore = 0;
            string? bestFile = null;

            foreach (var f in files)
            {
                try
                {
                    using var tpl = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(f);
                    var (pt, score) = MatchOnce(hay, tpl, threshold);

                    if (pt.HasValue && score > bestScore)
                    {
                        bestPt = pt;
                        bestScore = score;
                        bestFile = Path.GetFileName(f);
                    }
                }
                catch
                {
                    // có thể ảnh lỗi → bỏ qua
                }
            }

            return (bestPt, bestScore, bestFile);
        }
        // ===== CLICK BOSS CHÍNH XÁC — KHÔNG RANDOM =====
const int CLICK_DELAY_MS = 1000;   // delay cố định khi click
        public double CurrentThreshold => (double)nudThreshold.Value;
        public BossClickResult ScanAndClickBossEx(IntPtr hwnd, Action<string> log, double threshold)
        {
            return ClickBossUntilFight(hwnd, log, threshold);
        }

        public static void ClickBossSpam(IntPtr hwnd, int x, int y, Action<string> log)
        {
            const int maxSpam = 5;
            for (int i = 0; i < maxSpam; i++)
            {
                ClickClient(hwnd, x, y);
                log($"🖱️ Spam click boss #{i + 1} ({x},{y})");

                Thread.Sleep(150);

                if (!PlayerDetector.IsPlayerVisible(hwnd, CurrentPlayerAvatar, 0.80))
                {
                    log("⚔️ Player biến mất → đã vào combat!");
                    break;
                }
            }
        }


        //===========================================
        // ✅ BITMAP → Mat
        //===========================================
        static Mat BitmapToMat(System.Drawing.Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            ms.Position = 0;
            return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
        }
        public static bool WaitDisappearSimple(IntPtr hwnd, string avatar)
        {
            for (int i = 0; i < 20; i++) // ~1s tổng
            {
                if (!PlayerDetector.IsPlayerVisible(hwnd, avatar, 0.80))
                    return true;

                Thread.Sleep(50);
            }
            return false;
        }
        public BossClickResult ClickBossUntilFight(IntPtr hwnd, Action<string> log, double threshold)
        {
            for (int attempt = 0; attempt < 2; attempt++)   // thử tối đa 6 lần
            {
                if (WaitDisappearSimple(hwnd, CurrentPlayerAvatar))
                {
                    log("⚔️ Player biến mất → vào combat!");
                    return BossClickResult.FightStarted;
                }

                using var frame = CaptureWindowClient(hwnd);
                var (pt, score, file) = FindBestTemplate(frame, allTemplates, threshold);

                if (!pt.HasValue)
                {
                    log("❌ Không còn thấy boss → dừng click");
                    return BossClickResult.NotFound;
                }

                log($"🎯 attempt#{attempt + 1}: Boss {file} @({pt.Value.X},{pt.Value.Y}) score={score:F2}");

                ClickClient(hwnd, pt.Value.X, pt.Value.Y);
                Thread.Sleep(120);  // để nhân vật phản ứng chút
            }

            log("⚠️ Click max nhưng không vào combat.");
            return BossClickResult.ClickedNoFight;
        }


    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using danhbingo.data;

namespace danhbingo
{
    public partial class MapEditorForm : Form
    {
        // ===== HOTKEY IDs =====
        const int HOTKEY_WORLD = 0xA101; // F5
        const int HOTKEY_LOCAL = 0xA102; // F6

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // ===== UI =====
        TextBox txtMapName = new() { Width = 200 };
        TextBox txtWorldXY = new() { Width = 120, PlaceholderText = "vd: 500,300" };

        ListBox lstLocalPoints = new()
        {
            Width = 260,
            Height = 120
        };

        TextBox txtPrefix = new() { Width = 160 };

        Button btnPickWorld = new() { Text = "Lấy World (F5)", Width = 120 };
        Button btnPickLocal = new() { Text = "Thêm điểm (F6)", Width = 120 };
        Button btnRemoveLocal = new() { Text = "Xóa điểm đã chọn", Width = 140 };
        Button btnClearLocal = new() { Text = "Xóa hết", Width = 80 };
        Button btnSave = new() { Text = "Lưu Map", Width = 120 };

        // ===== DATA =====
        readonly string mapFile;
        MapDataJson mapDB;

        public MapEditorForm()
        {
            Text = "Map Editor";
            Width = 600;
            Height = 450;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            mapFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh", "MapData.json");

            if (!File.Exists(mapFile))
            {
                MessageBox.Show("Không tìm thấy MapData.json !", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            mapDB = JsonSerializer.Deserialize<MapDataJson>(File.ReadAllText(mapFile)) ?? new MapDataJson();
            mapDB.WorldMapPoints ??= new Dictionary<string, Coord>();
            mapDB.LocalMapPoints ??= new Dictionary<string, List<Coord>>();
            mapDB.MapBossPrefix ??= new Dictionary<string, string>();

            // ===== BUILD UI =====
            var root = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                RowCount = 6,
                ColumnCount = 2,
                Padding = new Padding(10)
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Tên map
            root.Controls.Add(new Label() { Text = "Tên map:", AutoSize = true }, 0, 0);
            root.Controls.Add(txtMapName, 1, 0);

            // World map
            var worldPanel = new FlowLayoutPanel()
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true
            };
            worldPanel.Controls.Add(txtWorldXY);
            worldPanel.Controls.Add(btnPickWorld);

            root.Controls.Add(new Label() { Text = "WorldMap (x,y):", AutoSize = true }, 0, 1);
            root.Controls.Add(worldPanel, 1, 1);

            // Local points
            root.Controls.Add(new Label() { Text = "Local Points:", AutoSize = true }, 0, 2);
            root.Controls.Add(lstLocalPoints, 1, 2);

            var localBtnPanel = new FlowLayoutPanel()
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true
            };
            localBtnPanel.Controls.Add(btnPickLocal);
            localBtnPanel.Controls.Add(btnRemoveLocal);
            localBtnPanel.Controls.Add(btnClearLocal);

            root.Controls.Add(new Label() { Text = "", AutoSize = true }, 0, 3);
            root.Controls.Add(localBtnPanel, 1, 3);

            // Prefix
            root.Controls.Add(new Label() { Text = "Boss Prefix:", AutoSize = true }, 0, 4);
            root.Controls.Add(txtPrefix, 1, 4);

            // Save
            root.Controls.Add(btnSave, 1, 5);

            Controls.Add(root);

            // ===== EVENTS =====
            btnSave.Click += SaveMap;
            btnPickWorld.Click += (_, __) => InsertWorldXY();
            btnPickLocal.Click += (_, __) => InsertLocalPoint();
            btnRemoveLocal.Click += (_, __) => RemoveSelectedPoints();
            btnClearLocal.Click += (_, __) => lstLocalPoints.Items.Clear();
            lstLocalPoints.DoubleClick += (_, __) => RemoveSelectedPoints(); // double click để xóa nhanh

            Load += MapEditorForm_Load;
            FormClosed += MapEditorForm_FormClosed;
        }

        // ============================
        //  HOTKEY REGISTER / UNREGISTER
        // ============================
        private void MapEditorForm_Load(object? sender, EventArgs e)
        {
            // 0 = không có Ctrl/Alt/Shift
            RegisterHotKey(this.Handle, HOTKEY_WORLD, 0, (uint)Keys.F5);
            RegisterHotKey(this.Handle, HOTKEY_LOCAL, 0, (uint)Keys.F6);
        }

        private void MapEditorForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_WORLD);
            UnregisterHotKey(this.Handle, HOTKEY_LOCAL);
        }

        // ============================
        //  CORE: LẤY TỌA ĐỘ TỪ CHUỘT
        // ============================
        bool TryGetGameClientXY(out int x, out int y)
        {
            x = y = 0;

            if (!GetCursorPos(out POINT p))
                return false;

            // Nếu đã có RootWindow thì convert sang client của game
            var hwnd = Form1.RootWindow;
            if (hwnd != IntPtr.Zero)
            {
                var clientPt = p;
                if (ScreenToClient(hwnd, ref clientPt))
                {
                    x = clientPt.X;
                    y = clientPt.Y;
                    return true;
                }
            }

            // fallback: dùng luôn tọa độ screen
            x = p.X;
            y = p.Y;
            return true;
        }

        void InsertWorldXY()
        {
            if (!TryGetGameClientXY(out int x, out int y)) return;
            txtWorldXY.Text = $"{x},{y}";
        }

        void InsertLocalPoint()
        {
            if (!TryGetGameClientXY(out int x, out int y)) return;
            lstLocalPoints.Items.Add($"{x},{y}");
        }

        void RemoveSelectedPoints()
        {
            // xóa tất cả item đang selected
            while (lstLocalPoints.SelectedIndices.Count > 0)
            {
                lstLocalPoints.Items.RemoveAt(lstLocalPoints.SelectedIndices[0]);
            }
        }

        // ============================
        //  HOTKEY HANDLING
        // ============================
        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;

            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_WORLD)
                {
                    InsertWorldXY();
                }
                else if (id == HOTKEY_LOCAL)
                {
                    InsertLocalPoint();
                }
            }

            base.WndProc(ref m);
        }

        // ============================
        //  LƯU MAP
        // ============================
        void SaveMap(object? sender, EventArgs e)
        {
            string name = txtMapName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Tên map không được để trống!");
                return;
            }

            // Parse WorldMap (x,y)
            var parts = txtWorldXY.Text.Split(',');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int wx) ||
                !int.TryParse(parts[1], out int wy))
            {
                MessageBox.Show("Tọa độ WorldMap không hợp lệ! Định dạng: 500,300");
                return;
            }

            // Parse Local Points từ ListBox
            var localCoords = new List<Coord>();
            foreach (var item in lstLocalPoints.Items)
            {
                var s = item.ToString()!.Trim();
                if (string.IsNullOrEmpty(s)) continue;

                var xy = s.Split(',');
                if (xy.Length == 2 &&
                    int.TryParse(xy[0], out int lx) &&
                    int.TryParse(xy[1], out int ly))
                {
                    localCoords.Add(new Coord { x = lx, y = ly });
                }
            }

            string prefix = txtPrefix.Text.Trim();

            // Ghi vào DB
            mapDB.WorldMapPoints[name] = new Coord { x = wx, y = wy };
            mapDB.LocalMapPoints[name] = localCoords;
            if (!string.IsNullOrEmpty(prefix))
                mapDB.MapBossPrefix[name] = prefix;

            File.WriteAllText(
                mapFile,
                JsonSerializer.Serialize(mapDB, new JsonSerializerOptions { WriteIndented = true })
            );

            MessageBox.Show("Đã lưu Map mới!");
            Close();
        }
    }
}

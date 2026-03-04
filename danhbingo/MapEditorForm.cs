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

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        // ===== UI =====
        ListBox lstMaps = new() { Width = 220, Height = 260 };

        TextBox txtMapName = new() { Width = 220 };
        TextBox txtWorldXY = new() { Width = 160, PlaceholderText = "vd: 500,300" };
        ListBox lstLocalPoints = new() { Width = 260, Height = 140 };
        TextBox txtPrefix = new() { Width = 220 };

        Button btnPickWorld = new() { Text = "Lấy World (F5)", Width = 130 };
        Button btnPickLocal = new() { Text = "Thêm điểm (F6)", Width = 130 };
        Button btnRemoveLocal = new() { Text = "Xóa điểm", Width = 90 };
        Button btnClearLocal = new() { Text = "Xóa hết", Width = 80 };

        Button btnNew = new() { Text = "New", Width = 70 };
        Button btnUpdate = new() { Text = "Update", Width = 70 };
        Button btnDelete = new() { Text = "Delete", Width = 70 };
        Button btnReload = new() { Text = "Reload", Width = 70 };
        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(POINT Point);
        Label lblInfo = new() { AutoSize = true, ForeColor = Color.DimGray };

        // ===== DATA =====
        readonly string mapFile;
        MapDataJson mapDB = new();

        public MapEditorForm()
        {
            Text = "Map Editor (CRUD)";
            Width = 760;
            Height = 480;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            mapFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh", "MapData.json");

            BuildUI();
            HookEvents();

            Load += MapEditorForm_Load;
            FormClosed += MapEditorForm_FormClosed;
        }

        void BuildUI()
        {
            var root = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(10)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // LEFT: list maps + buttons
            var left = new FlowLayoutPanel()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            left.Controls.Add(new Label() { Text = "Danh sách map:", AutoSize = true });
            left.Controls.Add(lstMaps);

            var leftBtns = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            leftBtns.Controls.Add(btnNew);
            leftBtns.Controls.Add(btnUpdate);
            leftBtns.Controls.Add(btnDelete);
            leftBtns.Controls.Add(btnReload);
            left.Controls.Add(leftBtns);
            left.Controls.Add(lblInfo);

            // RIGHT: editor fields
            var right = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            right.Controls.Add(new Label() { Text = "Tên map:", AutoSize = true }, 0, 0);
            right.Controls.Add(txtMapName, 1, 0);

            var worldPanel = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            worldPanel.Controls.Add(txtWorldXY);
            worldPanel.Controls.Add(btnPickWorld);

            right.Controls.Add(new Label() { Text = "World (x,y):", AutoSize = true }, 0, 1);
            right.Controls.Add(worldPanel, 1, 1);

            right.Controls.Add(new Label() { Text = "Local points:", AutoSize = true }, 0, 2);
            right.Controls.Add(lstLocalPoints, 1, 2);

            var localBtnPanel = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            localBtnPanel.Controls.Add(btnPickLocal);
            localBtnPanel.Controls.Add(btnRemoveLocal);
            localBtnPanel.Controls.Add(btnClearLocal);

            right.Controls.Add(new Label() { Text = "", AutoSize = true }, 0, 3);
            right.Controls.Add(localBtnPanel, 1, 3);

            right.Controls.Add(new Label() { Text = "Boss Prefix:", AutoSize = true }, 0, 4);
            right.Controls.Add(txtPrefix, 1, 4);

            var hint = new Label()
            {
                AutoSize = true,
                ForeColor = Color.DimGray,
                Text = "Chọn map bên trái để sửa. New để tạo mới. Update để lưu sửa. Delete để xóa."
            };
            right.Controls.Add(hint, 1, 5);

            root.Controls.Add(left, 0, 0);
            root.Controls.Add(right, 1, 0);

            Controls.Add(root);
        }

        void HookEvents()
        {
            btnReload.Click += (_, __) => LoadDbAndRefreshList();
            lstMaps.SelectedIndexChanged += (_, __) => LoadSelectedMapToEditor();

            btnNew.Click += (_, __) => ClearEditor();
            btnUpdate.Click += (_, __) => UpdateMap();
            btnDelete.Click += (_, __) => DeleteMap();

            btnPickWorld.Click += (_, __) => InsertWorldXY();
            btnPickLocal.Click += (_, __) => InsertLocalPoint();

            btnRemoveLocal.Click += (_, __) => RemoveSelectedPoints();
            btnClearLocal.Click += (_, __) => lstLocalPoints.Items.Clear();

            lstLocalPoints.DoubleClick += (_, __) => RemoveSelectedPoints();
        }

        // ============================
        // HOTKEY REGISTER
        // ============================
        private void MapEditorForm_Load(object? sender, EventArgs e)
        {
            RegisterHotKey(this.Handle, HOTKEY_WORLD, 0, (uint)Keys.F5);
            RegisterHotKey(this.Handle, HOTKEY_LOCAL, 0, (uint)Keys.F6);

            LoadDbAndRefreshList();
        }

        private void MapEditorForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_WORLD);
            UnregisterHotKey(this.Handle, HOTKEY_LOCAL);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;

            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_WORLD) InsertWorldXY();
                else if (id == HOTKEY_LOCAL) InsertLocalPoint();
            }

            base.WndProc(ref m);
        }

        // ============================
        // DB LOAD/SAVE
        // ============================
        void LoadDbAndRefreshList()
        {
            if (!System.IO.File.Exists(mapFile))
            {
                MessageBox.Show("Không tìm thấy MapData.json !", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            mapDB = JsonSerializer.Deserialize<MapDataJson>(System.IO.File.ReadAllText(mapFile)) ?? new MapDataJson();

            mapDB.WorldMapPoints ??= new Dictionary<string, Coord>();
            mapDB.LocalMapPoints ??= new Dictionary<string, List<Coord>>();
            mapDB.MapBossPrefix ??= new Dictionary<string, string>();

            lstMaps.Items.Clear();
            foreach (var name in mapDB.WorldMapPoints.Keys.OrderBy(x => x))
                lstMaps.Items.Add(name);

            lblInfo.Text = $"Loaded: {lstMaps.Items.Count} maps";
        }

        void SaveDb()
        {
            System.IO.File.WriteAllText(
                mapFile,
                JsonSerializer.Serialize(mapDB, new JsonSerializerOptions { WriteIndented = true })
            );
        }

        // ============================
        // CRUD ACTIONS
        // ============================
        void ClearEditor()
        {
            txtMapName.Text = "";
            txtWorldXY.Text = "";
            txtPrefix.Text = "";
            lstLocalPoints.Items.Clear();
            lstMaps.ClearSelected();
        }

        void LoadSelectedMapToEditor()
        {
            if (lstMaps.SelectedItem == null) return;
            string name = lstMaps.SelectedItem.ToString()!;

            txtMapName.Text = name;

            if (mapDB.WorldMapPoints.TryGetValue(name, out var w))
                txtWorldXY.Text = $"{w.x},{w.y}";
            else
                txtWorldXY.Text = "";

            lstLocalPoints.Items.Clear();
            if (mapDB.LocalMapPoints.TryGetValue(name, out var locals))
            {
                foreach (var p in locals)
                    lstLocalPoints.Items.Add($"{p.x},{p.y}");
            }

            txtPrefix.Text = mapDB.MapBossPrefix.TryGetValue(name, out var prefix) ? prefix : "";
        }

        void UpdateMap()
        {
            string name = txtMapName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Tên map không được để trống!");
                return;
            }

            // Parse world
            if (!TryParseXY(txtWorldXY.Text, out int wx, out int wy))
            {
                MessageBox.Show("Tọa độ WorldMap không hợp lệ! Định dạng: 500,300");
                return;
            }

            // Parse locals
            var localCoords = new List<Coord>();
            foreach (var item in lstLocalPoints.Items)
            {
                var s = item.ToString()!.Trim();
                if (string.IsNullOrEmpty(s)) continue;

                if (!TryParseXY(s, out int lx, out int ly))
                    continue;

                localCoords.Add(new Coord { x = lx, y = ly });
            }

            string prefix = txtPrefix.Text.Trim();

            // Nếu user đổi tên map (tạo mới hoặc rename)
            // Trường hợp rename: đang chọn 1 map cũ khác name mới -> cần remove key cũ.
            if (lstMaps.SelectedItem != null)
            {
                string oldName = lstMaps.SelectedItem.ToString()!;
                if (!string.Equals(oldName, name, StringComparison.Ordinal))
                {
                    // Rename keys
                    RemoveMapInternal(oldName);
                }
            }

            mapDB.WorldMapPoints[name] = new Coord { x = wx, y = wy };
            mapDB.LocalMapPoints[name] = localCoords;

            if (!string.IsNullOrEmpty(prefix))
                mapDB.MapBossPrefix[name] = prefix;
            else
                mapDB.MapBossPrefix.Remove(name); // prefix rỗng thì xóa cho sạch

            SaveDb();
            LoadDbAndRefreshList();

            // re-select
            int idx = lstMaps.FindStringExact(name);
            if (idx >= 0) lstMaps.SelectedIndex = idx;

            MessageBox.Show("Đã lưu (create/update) map!");
        }

        void DeleteMap()
        {
            if (lstMaps.SelectedItem == null)
            {
                MessageBox.Show("Chọn map để xóa.");
                return;
            }

            string name = lstMaps.SelectedItem.ToString()!;
            var ok = MessageBox.Show($"Xóa map '{name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ok != DialogResult.Yes) return;

            RemoveMapInternal(name);
            SaveDb();
            LoadDbAndRefreshList();
            ClearEditor();
        }

        void RemoveMapInternal(string name)
        {
            mapDB.WorldMapPoints.Remove(name);
            mapDB.LocalMapPoints.Remove(name);
            mapDB.MapBossPrefix.Remove(name);
        }

        // ============================
        // PICK COORDS (F5/F6)
        // ============================
        bool TryGetGameClientXY(out int x, out int y)
        {
            x = y = 0;

            if (!GetCursorPos(out POINT screenPt))
                return false;

            // 🔥 LẤY HWND TRỰC TIẾP TỪ VỊ TRÍ CHUỘT
            IntPtr hwndUnderMouse = WindowFromPoint(screenPt);
            if (hwndUnderMouse == IntPtr.Zero)
                return false;

            POINT clientPt = screenPt;
            if (!ScreenToClient(hwndUnderMouse, ref clientPt))
                return false;

            x = clientPt.X;
            y = clientPt.Y;

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
            while (lstLocalPoints.SelectedIndices.Count > 0)
                lstLocalPoints.Items.RemoveAt(lstLocalPoints.SelectedIndices[0]);
        }

        static bool TryParseXY(string text, out int x, out int y)
        {
            x = y = 0;
            var parts = text.Split(',');
            return parts.Length == 2
                && int.TryParse(parts[0].Trim(), out x)
                && int.TryParse(parts[1].Trim(), out y);
        }
    }
}

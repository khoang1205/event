using System.Text.Json;
using danhbingo.data;

namespace danhbingo.Auto
{
    public static class MapData
    {
        public static Dictionary<string, (int x, int y)> WorldMapPoints = new();
        public static Dictionary<string, List<(int x, int y)>> LocalMapPoints = new();
        public static Dictionary<string, string> MapBossPrefix = new();

        private static readonly string JsonPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh", "MapData.json");

        public static void Load()
        {
            if (!File.Exists(JsonPath))
            {
                MessageBox.Show("Không có MapData.json. Vui lòng tạo file.", "Lỗi");
                return;
            }

            try
            {
                string json = File.ReadAllText(JsonPath);
                var data = JsonSerializer.Deserialize<MapDataJson>(json);

                if (data == null) throw new Exception("JSON null");

                WorldMapPoints = data.WorldMapPoints.ToDictionary(k => k.Key, v => (v.Value.x, v.Value.y));
                LocalMapPoints = data.LocalMapPoints.ToDictionary(
                    k => k.Key,
                    v => v.Value.Select(p => (p.x, p.y)).ToList()
                );
                MapBossPrefix = data.MapBossPrefix;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi đọc MapData.json:\n" + ex.Message);
            }
        }
    }
}

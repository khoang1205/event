using System.Text.Json;

namespace danhbingo.Auto
{
    public static class MapData
    {
        public class Point { public int x { get; set; } public int y { get; set; } }

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

                if (data == null)
                    throw new Exception("JSON null");

                // World map
                WorldMapPoints = data.WorldMapPoints
                    .ToDictionary(k => k.Key, v => (v.Value.x, v.Value.y));

                // Local mini map
                LocalMapPoints = data.LocalMapPoints
                    .ToDictionary(
                        k => k.Key,
                        v => v.Value.Select(p => (p.x, p.y)).ToList()
                    );

                // Prefix
                MapBossPrefix = data.MapBossPrefix;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi đọc MapData.json:\n" + ex.Message);
            }
        }
    }

    public class MapDataJson
    {
        public Dictionary<string, MapData.Point>? WorldMapPoints { get; set; }
        public Dictionary<string, List<MapData.Point>>? LocalMapPoints { get; set; }
        public Dictionary<string, string>? MapBossPrefix { get; set; }
    }
}

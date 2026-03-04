using System.Collections.Generic;

namespace danhbingo.data
{
    public class MapDataJson
    {
        public Dictionary<string, Coord> WorldMapPoints { get; set; } = new();
        public Dictionary<string, List<Coord>> LocalMapPoints { get; set; } = new();
        public Dictionary<string, string> MapBossPrefix { get; set; } = new();
    }

    public class Coord
    {
        public int x { get; set; }
        public int y { get; set; }
    }
}

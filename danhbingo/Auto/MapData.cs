using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace danhbingo.Auto
{
    public   class MapData
    {
        public static readonly Dictionary<string, (int x, int y)> WorldMapPoints = new()
        {
            { "ThienKhungToc", (646, 296) },
            { "BanDiaToc", (579, 227) },
            { "LieuVanToc", (535, 305) },
            { "LinhVuToc", (324, 361) },
            { "HuyenLamToc", (267, 321) },
            { "LuuHoaToc", (456, 165) },
        };

        // Các điểm di chuyển trong bản đồ nhỏ (tọa độ mini map)
        public static readonly Dictionary<string, List<(int x, int y)>> LocalMapPoints = new()
        {
            { "ThienKhungToc", new() { (556,150), (556, 150), (507,387), (507, 387), (410, 372), (410, 372), (451,188), (451, 188) } },
            { "BanDiaToc", new() { (335, 221), (335, 221), (501, 221), (501, 221), (636, 348), (636, 348), (365, 368), (365, 368) } },
            { "LieuVanToc", new() { (324, 330), (324, 330), (421, 234), (421, 234), (527, 202), (527, 202), (626, 252), (626, 252), (630,346), (630, 346) } },
            { "LinhVuToc", new() { (392, 292), (392, 292), (439, 178), (439, 178), (520, 345), (439, 178) } },
            { "HuyenLamToc", new() { (530, 312), (530, 312), (626, 336), (626, 336), (614, 263), (614, 263), (387, 174), (387, 174) } },
            { "LuuHoaToc", new() { (323, 383), (323, 383) } },
        };
    }
}

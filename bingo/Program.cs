using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace bingo
{
    internal class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_MOUSEMOVE = 0x0200;
        const int MK_LBUTTON = 0x0001;
        static IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        static void Main(string[] args)
        {
            const string WINDOW_TITLE = "acc5 s100"; // tên cửa sổ Flash
            const double MATCH_THRESHOLD = 0.87;

            string IMAGE_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh");
            string[] allTemplates = Directory.GetFiles(IMAGE_DIR, "*.png", SearchOption.TopDirectoryOnly);
            Console.WriteLine($"🟡 Đã tìm thấy {allTemplates.Length} ảnh trong thư mục Anh");

            IntPtr hwnd = FindWindow(null, WINDOW_TITLE);
            if (hwnd == IntPtr.Zero)
            {
                Console.WriteLine($"❌ Không tìm thấy cửa sổ '{WINDOW_TITLE}'");
                return;
            }

            Console.WriteLine($"✅ Đã tìm thấy cửa sổ Flash HWND = 0x{hwnd.ToInt64():X}");
            Console.WriteLine("🔁 Bắt đầu dò ảnh và click...");

            while (true)
            {
                using Bitmap screenshot = CaptureWindow(hwnd);

                System.Drawing.Point? found = null;
                double bestScore = 0;
                string? bestFile = null;

                foreach (var tplPath in allTemplates)
                {
                    using var tpl = (Bitmap)Image.FromFile(tplPath);
                    var (pt, score) = FindTemplate(screenshot, tpl, MATCH_THRESHOLD);
                    if (pt.HasValue && score > bestScore)
                    {
                        bestScore = score;
                        found = pt;
                        bestFile = Path.GetFileName(tplPath);
                    }
                }

                if (found.HasValue)
                {
                    Console.WriteLine($"✅ Match {bestFile} - score={bestScore:F2} @ ({found.Value.X},{found.Value.Y})");
                    FlashPostClick(hwnd, found.Value.X, found.Value.Y);
                    Thread.Sleep(400);
                }
                else
                {
                    Console.WriteLine($"No match (best={bestScore:F2})");
                    Thread.Sleep(300);
                }
            }
        }

        static (System.Drawing.Point?, double) FindTemplate(Bitmap hayBmp, Bitmap tplBmp, double threshold)
        {
            using var hay = BitmapConverter.ToMat(hayBmp);
            using var tpl = BitmapConverter.ToMat(tplBmp);
            using var hayGray = new Mat();
            using var tplGray = new Mat();
            Cv2.CvtColor(hay, hayGray, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(tpl, tplGray, ColorConversionCodes.BGR2GRAY);
            if (hayGray.Cols < tplGray.Cols || hayGray.Rows < tplGray.Rows)
                return (null, 0);

            using var result = new Mat(hayGray.Rows - tplGray.Rows + 1, hayGray.Cols - tplGray.Cols + 1, MatType.CV_32FC1);
            Cv2.MatchTemplate(hayGray, tplGray, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

            if (maxVal >= threshold)
            {
                var center = new System.Drawing.Point(maxLoc.X + tplGray.Cols / 2, maxLoc.Y + tplGray.Rows / 2);
                return (center, maxVal);
            }
            return (null, maxVal);
        }

        static void FlashPostClick(IntPtr hwnd, int x, int y)
        {
            PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, MakeLParam(x, y));
            PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, MakeLParam(x, y));
            PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, MakeLParam(x, y));
        }

        static Bitmap CaptureWindow(IntPtr hwnd)
        {
            if (!GetWindowRect(hwnd, out RECT r))
                throw new Exception("GetWindowRect failed");

            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            var hdc = g.GetHdc();
            bool ok = PrintWindow(hwnd, hdc, 0);
            g.ReleaseHdc(hdc);

            if (!ok)
                g.CopyFromScreen(r.Left, r.Top, 0, 0, bmp.Size);
            return bmp;
        }
    }
}

using OpenCvSharp;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using DSize = System.Drawing.Size;
namespace batpet.Auto
{
    public static class ImageHelper
    {
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll", SetLastError = true)] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
      

       
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

       
        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }


        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, out POINT lpPoint);

        const uint PW_CLIENTONLY = 0x00000001;




        /// <summary>
        /// Chụp lại vùng client của cửa sổ game (ưu tiên PrintWindow, fallback CopyFromScreen)
        /// </summary>
        public static Bitmap CaptureWindow(IntPtr hwnd)
        {
            if (!GetClientRect(hwnd, out var cr))
                throw new Exception("GetClientRect failed");

            int w = cr.Right - cr.Left;
            int h = cr.Bottom - cr.Top;

            var bmp = new Bitmap(w, h);

            using (var g = Graphics.FromImage(bmp))
            {
                var hdc = g.GetHdc();
                bool ok = PrintWindow(hwnd, hdc, PW_CLIENTONLY);
                g.ReleaseHdc(hdc);

                if (!ok)
                {
                    // fallback: dùng CopyFromScreen dựa trên client top-left
                    if (!ClientToScreen(hwnd, out var tl))
                        throw new Exception("ClientToScreen failed");

                    g.CopyFromScreen(tl.X, tl.Y, 0, 0, new System.Drawing.Size(w, h));
                }
            }
            return bmp;
        }

        /// <summary>
        /// So khớp ảnh bằng OpenCV Template Matching, trả về điểm cao nhất.
        /// </summary>
        public static (OpenCvSharp.Point? p, double score) MatchOnce(Bitmap hayBmp, Bitmap tplBmp, double threshold)
        {
            using var hay = ToMat(hayBmp);
            using var tpl = ToMat(tplBmp);
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
                var center = new OpenCvSharp.Point(maxLoc.X + tplGray.Cols / 2, maxLoc.Y + tplGray.Rows / 2);
                return (center, maxVal);
            }
            return (null, maxVal);
        }

        static Mat ToMat(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
        }

        public static (OpenCvSharp.Point? p, double score) MatchMultiScale(
            Bitmap hayBmp,
            Bitmap tplBmp,
            double threshold)
        {
            using var hay = ToMat(hayBmp);
            using var tpl = ToMat(tplBmp);

            using var hayGray = new Mat();
            Cv2.CvtColor(hay, hayGray, ColorConversionCodes.BGR2GRAY);

            double bestScore = 0;
            OpenCvSharp.Point? bestPt = null;

            foreach (double scale in new[] { 1.0, 0.9, 0.8, 0.75, 0.7 })
            {
                int newW = (int)(tplBmp.Width * scale);
                int newH = (int)(tplBmp.Height * scale);
                if (newW < 10 || newH < 10) continue;

                using var resized = new Mat();
                Cv2.Resize(tpl, resized, new OpenCvSharp.Size(newW, newH));

                using var tplGray = new Mat();
                Cv2.CvtColor(resized, tplGray, ColorConversionCodes.BGR2GRAY);

                using var result = new Mat();
                Cv2.MatchTemplate(hayGray, tplGray, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                if (maxVal > bestScore)
                {
                    bestScore = maxVal;
                    bestPt = new OpenCvSharp.Point(maxLoc.X + newW / 2, maxLoc.Y + newH / 2);
                }
            }

            return (bestPt, bestScore);
        }

        /// <summary>
        /// Click chuột trái vào tọa độ client (x, y)
        /// </summary>

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);


        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        public static void ClickClient(IntPtr hwnd, int x, int y, Action<string>? log = null)
        {
            int lParam = (y << 16) | (x & 0xFFFF);

            PostMessage(hwnd, WM_LBUTTONDOWN, 1, lParam);
            Thread.Sleep(25);
            PostMessage(hwnd, WM_LBUTTONUP, 0, lParam);

            log?.Invoke($"🖱️ ClickClient ({x},{y})");
        }

        public static bool IsPopupVisible(IntPtr hwnd, string popupImg, double threshold = 0.8)
        {
            using var frame = CaptureWindow(hwnd);
            using var tpl = (Bitmap)Image.FromFile(popupImg);
            var (pt, score) = MatchOnce(frame, tpl, threshold);
            return pt.HasValue && score >= threshold;
        }
        public static bool ClickImage(
     IntPtr hwnd,
     string imgPath,
     double threshold,
     Action<string>? log = null)
        {
            using var frame = CaptureWindow(hwnd);
            using var tpl = (Bitmap)Image.FromFile(imgPath);

            var (pt, score) = MatchMultiScale(frame, tpl, threshold);

            if (!pt.HasValue || score < threshold)
            {
                log?.Invoke($"🙈 Không thấy ảnh {Path.GetFileName(imgPath)} (score={score:F2})");
                return false;
            }

            ClickClient(hwnd, pt.Value.X, pt.Value.Y, log);
            log?.Invoke($"✅ Click hình {Path.GetFileName(imgPath)} tại ({pt.Value.X},{pt.Value.Y}) score={score:F2}");

            return true;
        }

    }
}

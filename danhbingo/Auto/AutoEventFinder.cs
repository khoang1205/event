using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace danhbingo.Auto
{
    public static class AutoEventFinder
    {
        public static void ScanAndClickBingo(IntPtr hwnd, string[] templates, double threshold, Action<string> log)
        {
            using var bmp = Form1.CaptureWindowClient(hwnd);
            var match = Form1.FindBestTemplate(bmp, templates, threshold);

            if (match.found.HasValue)
            {
                var p = match.found.Value;
                log($"Boss tìm thấy ({p.X},{p.Y}), score={match.score:F2}");
                Form1.ClickClient(hwnd, p.X, p.Y);
                Thread.Sleep(1000);
            }
            else
            {
                log("🔍 Không thấy boss nào trong khung hiện tại.");
            }
        }
    }
}

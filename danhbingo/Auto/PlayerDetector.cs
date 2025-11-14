using System;
using System.Drawing;
using System.Threading;

namespace danhbingo.Auto
{
    public static class PlayerDetector
    {
        /// <summary>
        /// Kiểm tra nhân vật có đang hiện trên màn hình không
        /// </summary>
        public static bool IsPlayerVisible(IntPtr hwnd, string playerImgPath, double threshold)
        {
            using var frame = Form1.CaptureWindowClient(hwnd);
            using var tpl = (Bitmap)Image.FromFile(playerImgPath);
            var (pt, score) = Form1.MatchOnce(frame, tpl, threshold);
            return pt.HasValue && score > threshold;
        }

        /// <summary>
        /// Đợi cho nhân vật biến mất (combat bắt đầu)
        /// </summary>
        public static bool WaitForPlayerDisappear(
      IntPtr hwnd,
      string avatar,
      Action<string> log,
      double threshold,
      int timeoutMs = 4000)
        {
            int elapsed = 0;

            while (elapsed < timeoutMs)
            {
                bool visible = IsPlayerVisible(hwnd, avatar, threshold);

                if (!visible)
                {
                    log("🙈 Player biến mất!");
                    return true;
                }

                Thread.Sleep(400);   // ✅ check nhanh
                elapsed += 200;
            }

            log("⚠️ Player vẫn còn → coi như không biến mất");
            return false;
        }


        /// <summary>
        /// Đợi cho nhân vật xuất hiện lại (combat kết thúc)
        /// </summary>
        public static bool WaitForPlayerAppear(IntPtr hwnd, string playerImgPath, Action<string> log, double threshold = 0.8, int timeoutMs = 15000)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (IsPlayerVisible(hwnd, playerImgPath, threshold))
                {
                    log(" Nhân vật xuất hiện lại (combat kết thúc)!");
                    return true;
                }
                Thread.Sleep(400);
            }

            log(" Hết thời gian chờ nhân vật xuất hiện lại.");
            return false;
        }

        /// <summary>
        /// Giữ lại cho các đoạn cũ dùng (load map xong)
        /// </summary>
        public static bool WaitForPlayerToReach(IntPtr hwnd, string playerImgPath, Action<string> log, double threshold = 0.85, int timeoutMs = 15000)
        {
            return WaitForPlayerAppear(hwnd, playerImgPath, log, threshold, timeoutMs);
        }
    }
}

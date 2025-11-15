using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Drawing;
using batpet.Auto;
using System.Diagnostics;
using static danhbingo.Form1;

namespace danhbingo.Auto
{
    public static class AutoMapController
    {
        // === ENTRY POINT ===
        public static void TravelToMap(
     IntPtr hwnd,
     string mapName,
     Action<string> log,
     Func<string, bool> waitPlayer,
     Form1 f,
     CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            log($"======= 🚀 TravelToMap: {mapName} =======");

            // 1) Bay tới map
            SelectMapAndFly(hwnd, mapName, log);

            // 2) Chờ player biến mất (load map)
            bool gone = WaitPlayerDisappearForMapChange(hwnd, log);


            if (!gone)
            {
                // Không biến mất => có thể đã ở đúng map
                log("⚠️ Player không biến mất → có thể đã ở đúng map");
            }

            // 3) Chờ player xuất hiện lại (load xong)
            WaitPlayerAppearQuick(hwnd, log, token);

            if (token.IsCancellationRequested) return;

            // 4) Explore map
            ExploreMapAndFight(hwnd, mapName, log, f, token);
        }


        private static void WaitPlayerAppearQuick(
    IntPtr hwnd,
    Action<string> log,
    CancellationToken token)
        {
            var sw = Stopwatch.StartNew();

            while (!token.IsCancellationRequested && sw.ElapsedMilliseconds < 1000)
            {
                if (PlayerDetector.IsPlayerVisible(hwnd, Form1.CurrentPlayerAvatar, 0.80))
                {
                    log("✅ Player xuất hiện lại → Load map xong!");
                    return;
                }

                Thread.Sleep(100);
            }

            log("⚠️ Timeout chờ player xuất hiện lại");
        }

        // === 1️⃣ MỞ BẢN ĐỒ THẾ GIỚI ===


        // === 2️⃣ CHỌN MAP VÀ BAY ===
        private static void SelectMapAndFly(IntPtr hwnd, string mapName, Action<string> log)
        {
            //  mở world map
            Form1.ToggleWorldMap(hwnd);
            Thread.Sleep(500);

            if (MapData.WorldMapPoints.TryGetValue(mapName, out var p))
            {
                log($"📍 Chọn {mapName} ({p.x},{p.y})");

                // Click điểm trên bản đồ
                Form1.ClickClient(hwnd, p.x, p.y);
                Thread.Sleep(600);

                // Click nút “Cá nhân”
                if (!ClickCaNhan(hwnd, log))
                {
                    // fallback → click tọa độ cũ
                    Form1.ClickClient(hwnd, 730, 430);
                    log("➡️ fallback → click Cá nhân @ 730,430");
                }

                log("🛫 Đang bay...");
                Thread.Sleep(500);

                // ✅ SAU ĐÂY world map sẽ TỰ TẮT
                // → KHÔNG nhấn M / ToggleWorldMap nữa
            }
            else
            {
                log($"⚠️ Không tìm thấy tọa độ map {mapName}");
            }
        }
       public static bool WaitPlayerDisappearForMapChange(IntPtr hwnd, Action<string> log)
        {
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 1000) // tối đa 1.5s là đủ
            {
                bool visible = PlayerDetector.IsPlayerVisible(hwnd, Form1.CurrentPlayerAvatar, 0.80);

                if (!visible)
                {
                    log("🙈 Player biến mất → Đang chuyển map!");
                    return true;
                }

                Thread.Sleep(100); // check mượt hơn
            }

            log("⚠️ Player KHÔNG biến mất khi bay map → có thể đang ở đúng map rồi");
            return false;
        }
        static bool ClickCaNhan(IntPtr hwnd, Action<string> log)
        {
            string img = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh", "CaNhan.png");

            if (!File.Exists(img))
            {
                log("⚠️ Không có ảnh CaNhan.png");
                return false;
            }

            bool ok = ImageHelper.ClickImage(hwnd, img, 0.75, log);
            if (ok)
            {
                log("✅ (Popup) → Click Cá nhân");
                Thread.Sleep(400);
            }
            return ok;
        }



        // === 3️⃣ DI CHUYỂN & QUÉT BINGO ===
        private static void ExploreMapAndFight(
       IntPtr hwnd,
       string mapName,
       Action<string> log,
       Form1 f,
       CancellationToken token)
        {
            if (!MapData.LocalMapPoints.TryGetValue(mapName, out var movePoints))
            {
                log($"⚠️ Không có toạ độ mini map cho {mapName}");
                return;
            }

            log($"🚶 Bắt đầu quét map: {mapName}");
            Form1.HealIfNeeded(hwnd, true, true, log);
            foreach (var p in movePoints)
            {
                if (token.IsCancellationRequested) return;

                // 1) Scan boss trước
                if (HandleBossScan(hwnd, log, f, token))
                    continue;

                // 2) Move
                MoveToPoint(hwnd, p.x, p.y, log, f, token);

                // 3) Scan boss sau khi move
                HandleBossScan(hwnd, log, f, token);
            }

            log($"✨ Đã hoàn tất map {mapName}");
        }


        private static void MoveToPoint(
     IntPtr hwnd, int x, int y,
     Action<string> log, Form1 f,
     CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            // mở mini map (~)
            Form1.SendTilde(hwnd);
            Thread.Sleep(2000);

            // check xem mini map co mo ko roi han move

            // click tọa độ
            Form1.ClickClient(hwnd, x, y);
            log($"➡ Move to ({x},{y})");

            // tắt mini map
            Form1.SendTilde(hwnd);
            Thread.Sleep(100);

            // trong lúc chạy → scan boss liên tục
            for (int i = 0; i < 40; i++)
            {
                if (token.IsCancellationRequested) return;

                var r = f.ScanAndClickBossEx(hwnd, log, f.CurrentThreshold);

                if (r == BossClickResult.FightStarted)
                {
                    WaitAppearLoop(hwnd, Form1.CurrentPlayerAvatar, log, token);
                    return;
                }

                if (r == BossClickResult.NotFound)
                    break;    //  không còn boss → dừng Scan ngay

                Thread.Sleep(50);
            }

        }

        private static bool HandleBossScan(
      IntPtr hwnd, Action<string> log, Form1 f, CancellationToken token)
        {
            var r = f.ScanAndClickBossEx(hwnd, log, f.CurrentThreshold);

            switch (r)
            {
                case Form1.BossClickResult.FightStarted:
                    WaitAppearLoop(hwnd, Form1.CurrentPlayerAvatar, log, token);
                    Thread.Sleep(300);
                    return true;   // có combat → dừng xử lý point

                case Form1.BossClickResult.ClickedNoFight:
                    return false;  // không thấy boss nữa → cho phép MOVE tiếp

                case Form1.BossClickResult.NotFound:
                default:
                    return false;  // KHÔNG CHẶN MOVE
            }
        }




        // === 5️⃣ QUÉT BINGO & COMBAT ===
        private static void ScanAndFightBingo(IntPtr hwnd, Action<string> log)
        {
            string bingoFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh");
            var bingoFiles = Directory.GetFiles(bingoFolder, "*.png")
                .Where(f => Path.GetFileName(f).ToLower().Contains("bingo")).ToArray();

            using var frame = Form1.CaptureWindowClient(hwnd);
            bool found = false;

            foreach (var f in bingoFiles)
            {
                using var tpl = (Bitmap)Image.FromFile(f);
                var (pt, score) = Form1.MatchOnce(frame, tpl, 0.82);

                if (pt.HasValue && score >= 0.82 && score < 0.97 &&
     pt.Value.X > 200 && pt.Value.X < 800 &&
     pt.Value.Y > 120 && pt.Value.Y < 550)
                {
                    log($"🎯 Bingo khả nghi ({Path.GetFileName(f)}) tại ({pt.Value.X},{pt.Value.Y}), score={score:F2}");
                    Form1.ClickClient(hwnd, pt.Value.X, pt.Value.Y);
                    Thread.Sleep(1000); // đợi phản ứng game

                    // 🧠 Kiểm tra xem nhân vật có biến mất (tức là vào combat chưa)
                    bool playerGone = PlayerDetector.WaitForPlayerDisappear(hwnd, Form1.CurrentPlayerAvatar, log, 0.80, 4000);

                    if (playerGone)
                    {
                        log("⚔️ Vào combat thật — đang chờ nhân vật biến mất hoàn toàn...");
                        Thread.Sleep(3000); // delay nhỏ cho ổn định

                        // click nút phụ (816,353)
                        Form1.ClickClient(hwnd, 816, 353);
                        log("🖱️ Click nút phụ tấn công sau 3s combat.");

                        // đợi player xuất hiện lại
                        PlayerDetector.WaitForPlayerAppear(hwnd, Form1.CurrentPlayerAvatar, log, 0.80, 10000);
                        log("✅ Combat kết thúc, nhân vật đã trở lại!");
                    }
                    else
                    {
                        log("⚠️ Click nhầm — nhân vật không biến mất, không vào trận.");
                    }

                    found = playerGone;
                    break;
                }

            }
        }
        private static bool WaitAppearLoop(
     IntPtr hwnd,
     string avatar,
     Action<string> log,
     CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool ok = PlayerDetector.IsPlayerVisible(hwnd, avatar, 0.80);
                if (ok)
                {
                    log("✅ Nhân vật đã xuất hiện lại!");
                    return true;
                }

                Thread.Sleep(1000);
            }
            return false;
        }

        private static string GetNextMap(string current, Form1 f)
        {
            var maps = f.MapList.CheckedItems.Cast<string>().ToList();

            if (maps.Count == 0) return current;

            int idx = maps.IndexOf(current);
            if (idx < 0) return maps[0];

            int next = (idx + 1) % maps.Count;
            return maps[next];
        }


        // === 6️⃣ COMBAT LOGIC ===
        private static void HandleCombat(IntPtr hwnd, Action<string> log)
        {
            if (PlayerDetector.WaitForPlayerDisappear(hwnd, Form1.CurrentPlayerAvatar, log, 0.8, 8000))
            {
                Thread.Sleep(3000);
                Form1.ClickClient(hwnd, 816, 353);
                log("🖱️ Click nút phụ tấn công (sau khi player biến mất)");
                PlayerDetector.WaitForPlayerAppear(hwnd, Form1.CurrentPlayerAvatar, log, 0.8, 15000);
            }
            else
            {
                log("⚠️ Không thấy nhân vật biến mất (có thể false match).");
            }
        }
    }
} 


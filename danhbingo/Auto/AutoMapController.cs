using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Drawing;
using batpet.Auto;
using System.Diagnostics;
using static danhbingo.Form1;
using static OpenCvSharp.Stitcher;

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
    bool healPlayer,
    bool healPet,
    AttackMode mode,
    CancellationToken token)

        {
            if (token.IsCancellationRequested) return;

            log($"======= 🚀 TravelToMap: {mapName} =======");

            // 1) Bay tới map
            SelectMapAndFly(hwnd, mapName, log);

            // 2) Chờ player biến mất (load map)
            bool gone = WaitPlayerDisappearForMapChange(hwnd, log, token);



            if (!gone)
            {
                // Không biến mất => có thể đã ở đúng map
                log("⚠️ Player không biến mất → có thể đã ở đúng map");
            }

            // 3) Chờ player xuất hiện lại (load xong)
            WaitPlayerAppearQuick(hwnd, log, token);

            if (token.IsCancellationRequested) return;

            // 4) Explore map
            ExploreMapAndFight(hwnd, mapName, log, f, healPlayer, healPet, mode, token);
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
        public static bool WaitPlayerDisappearForMapChange(IntPtr hwnd, Action<string> log, CancellationToken token)

        {
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 1000) // tối đa 1.5s là đủ
            {
                Form1.CheckStop(token);
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
    bool healPlayer,
    bool healPet,
    AttackMode mode,
    CancellationToken token)

        {
            if (!MapData.LocalMapPoints.TryGetValue(mapName, out var movePoints))
            {
                Form1.CheckStop(token);
                log($"⚠️ Không có toạ độ mini map cho {mapName}");
                return;
            }

            log($"🚶 Bắt đầu quét map: {mapName}");
           

            foreach (var p in movePoints)
            {
                if (token.IsCancellationRequested) return;

                // 1) Scan boss trước
                if (HandleBossScan(hwnd, mapName, log, f, mode, token))
                    continue;
                Form1.HealIfNeeded(hwnd, healPlayer, healPet, log);
                // 2) Move
                MoveToPoint(hwnd, p.x, p.y, mapName, log, f, mode, token);

                // 3) Scan boss sau khi move
                HandleBossScan(hwnd, mapName, log, f, mode, token);
            }

            log($"✨ Đã hoàn tất map {mapName}");
        }


        private static void MoveToPoint(
    IntPtr hwnd, int x, int y,
    string mapName,
    Action<string> log, Form1 f,
    AttackMode mode,
    CancellationToken token)

        {
            Form1.CheckStop(token);
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
            bool loggedNotFound = false;

            for (int i = 0; i < 40; i++)
            {
                if (token.IsCancellationRequested) return;
                Form1.CheckStop(token);
                var r = f.ScanAndClickBossEx(hwnd, log, f.CurrentThreshold, mapName, mode, token);

                if (r == BossClickResult.FightStarted)
                {
                    Form1.TryEnableAutoInGame(hwnd, log);
                    WaitAppearLoop(hwnd, Form1.CurrentPlayerAvatar, log, token);
                    return;
                }

                if (r == BossClickResult.NotFound)
                {
                    if (!loggedNotFound)
                    {
                        log("❌ Không thấy boss → tiếp tục di chuyển");
                        loggedNotFound = true;
                    }
                    break;  // DỪNG SCAN — ĐI TIẾP!
                }

                Thread.Sleep(50);
            }

        }

        private static bool HandleBossScan(
     IntPtr hwnd, string mapName,
    Action<string> log, Form1 f,
    AttackMode mode,
    CancellationToken token)
        {
            var r = f.ScanAndClickBossEx(hwnd, log, f.CurrentThreshold, mapName, mode, token);


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




      
        private static bool WaitAppearLoop(
      IntPtr hwnd,
      string avatar,
      Action<string> log,
      CancellationToken token)
        {
            // ⭐⭐ THÊM ẢNH AUTO
            string autoImg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Anh", "AutoInGame.png");

            const int AUTO_X = 804;
            const int AUTO_Y = 355;

            while (!token.IsCancellationRequested)
            {
                // ---- 1) đang trong trận → scan AutoInGame ----
                if (!PlayerDetector.IsPlayerVisible(hwnd, avatar, 0.80))
                {
                    using var frame = ImageHelper.CaptureWindowClient(hwnd);
                    var (pt, score, _) = Form1.FindBestTemplate(frame, new[] { autoImg }, 0.60);

                    if (pt.HasValue)
                    {
                        log($"🟢 AutoInGame xuất hiện (score={score:F2}) → CLICK");
                        Form1.ClickClient(hwnd, AUTO_X, AUTO_Y);
                        Thread.Sleep(1000);
                    }

                    Thread.Sleep(120);
                    continue;
                }

                // ---- 2) nhân vật xuất hiện → hết combat ----
                log("✅ Nhân vật đã xuất hiện lại!");

                Form1.HealIfNeeded(
                    hwnd,
                    Form1.fInstance.HealPlayerOption,
                    Form1.fInstance.HealPetOption,
                    log
                );

                return true;
            }

            return false;
        }



      


    
    }
} 


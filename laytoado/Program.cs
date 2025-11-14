using System;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    static void Main()
    {
        Console.WriteLine("=== TOOL LẤY TỌA ĐỘ MOUSE TRONG GAME ===");
        Console.WriteLine("➡ Di chuột lên nút muốn lấy, nhấn ENTER để lấy tọa độ client (trong cửa sổ game).");
        Console.WriteLine("➡ Nhấn ESC để thoát.\n");

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape)
                    break;

                if (key == ConsoleKey.Enter)
                {
                    GetCursorPos(out POINT pt);
                    IntPtr hwnd = WindowFromPoint(pt);
                    POINT clientPt = pt;
                    ScreenToClient(hwnd, ref clientPt);

                    Console.WriteLine($"🧭 Màn hình: X={pt.X}, Y={pt.Y} | Client: X={clientPt.X}, Y={clientPt.Y} | hwnd=0x{hwnd.ToInt64():X}");
                }
            }

            Thread.Sleep(50);
        }
    }
}

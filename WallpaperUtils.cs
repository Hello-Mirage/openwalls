using System;
using System.Diagnostics;
using System.Text;

namespace openwalls;

public static class WallpaperUtils
{
    public static void SendWorkerWMessage()
    {
        IntPtr progman = Win32Api.FindWindow("Progman", null);
        IntPtr result = IntPtr.Zero;
        
        // Split the desktop layers
        Win32Api.SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0, 1000, out result);
    }

    public static IntPtr GetWorkerWHandle()
    {
        IntPtr progman = Win32Api.FindWindow("Progman", null);
        IntPtr workerw = IntPtr.Zero;

        // Strategy 1: Search for WorkerW child of Progman (Windows 11 25H2 / Build 26xxx)
        workerw = Win32Api.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        
        if (workerw != IntPtr.Zero)
        {
            Debug.WriteLine("Found 25H2-style background WorkerW (child of Progman).");
            return workerw;
        }

        // Strategy 2: Search for WorkerW sibling of icons (Legacy / Windows 10 / Windows 11 22H2)
        Win32Api.EnumWindows((hwnd, lParam) =>
        {
            IntPtr shellDll = Win32Api.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellDll != IntPtr.Zero)
            {
                workerw = Win32Api.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        if (workerw != IntPtr.Zero)
        {
            Debug.WriteLine("Found Legacy-style background WorkerW (sibling of ShellView).");
            return workerw;
        }

        // Strategy 3: Find any empty WorkerW sibling of Progman
        workerw = Win32Api.FindWindowEx(IntPtr.Zero, progman, "WorkerW", null);
        
        return workerw;
    }

    public static void AttachToDesktop(IntPtr windowHandle)
    {
        SendWorkerWMessage();
        IntPtr workerw = GetWorkerWHandle();
        
        if (workerw != IntPtr.Zero)
        {
            Win32Api.SetParent(windowHandle, workerw);
            Debug.WriteLine($"Successfully parented window to WorkerW {workerw}");
        }
        else
        {
            Debug.WriteLine("Error: Failed to find any suitable backdrop window.");
        }
    }
}

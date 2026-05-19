using System;
using System.Diagnostics;
using System.Text;

namespace openwalls;

public static class WallpaperUtils
{
    public static void SendWorkerWMessage()
    {
        IntPtr progman = Win32Api.FindWindow("Progman", null!);
        IntPtr result = IntPtr.Zero;
        
        // Split the desktop layers
        Win32Api.SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0, 1000, out result);
    }

    public static IntPtr GetWorkerWHandle()
    {
        IntPtr progman = Win32Api.FindWindow("Progman", null!);
        
        SendWorkerWMessage(); // Triggers the desktop split

        IntPtr shellDllViewParent = IntPtr.Zero;

        // Look for the top-level window holding SHELLDLL_DefView.
        Win32Api.EnumWindows((hwnd, lParam) => {
            IntPtr p = Win32Api.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null!);
            if (p != IntPtr.Zero) {
                shellDllViewParent = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (shellDllViewParent == IntPtr.Zero) {
            // Absolute fallback for Win 11 Build 26200+ where EnumWindows might skip it
            IntPtr p = Win32Api.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null!);
            if (p != IntPtr.Zero) {
                shellDllViewParent = progman;
            } else {
                shellDllViewParent = progman;
            }
        }

        // The background render layer is ALWAYS spawned as the absolute next WorkerW sibling 
        // immediately underneath the container that holds the icons.
        IntPtr backgroundWorkerW = Win32Api.FindWindowEx(IntPtr.Zero, shellDllViewParent, "WorkerW", null!);

        if (backgroundWorkerW == IntPtr.Zero)
        {
            // Edge Case: If it didn't spawn outside, check if it was spawned INSIDE Progman.
            backgroundWorkerW = Win32Api.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null!);
        }

        // Failsafe (usually won't hit this)
        if (backgroundWorkerW == IntPtr.Zero)
        {
            backgroundWorkerW = progman;
        }

        return backgroundWorkerW;
    }

    public static void AttachToDesktop(IntPtr windowHandle)
    {
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

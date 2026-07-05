using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace CompressForDiscord.Services.Clipboard;

/// <summary>
/// Raw Win32 CF_HDROP clipboard writer. Deliberately not Avalonia's clipboard API: that path
/// lacks the "Preferred DropEffect" hint (paste targets may treat it as a move) and its file
/// support has shifted across 11.x minors. Clipboard contents survive process exit.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsClipboard : IClipboardFileService
{
    private const uint CfHdrop = 15;
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;
    private const int DropFilesHeaderSize = 20;
    private const uint DropEffectCopy = 1;

    public Task<ClipboardOutcome> CopyFileAsync(string absolutePath) =>
        Task.Run(() => CopyCore(Path.GetFullPath(absolutePath)));

    private static ClipboardOutcome CopyCore(string path)
    {
        // Clipboard history/OneDrive/other apps lock the clipboard constantly — retry.
        bool opened = false;
        for (int i = 0; i < 10 && !(opened = OpenClipboard(IntPtr.Zero)); i++)
        {
            System.Threading.Thread.Sleep(50);
        }

        if (!opened)
        {
            return ClipboardOutcome.Failed;
        }

        try
        {
            if (!EmptyClipboard())
            {
                return ClipboardOutcome.Failed;
            }

            IntPtr hDrop = BuildDropFiles(path);
            if (SetClipboardData(CfHdrop, hDrop) == IntPtr.Zero)
            {
                GlobalFree(hDrop); // ownership did NOT transfer
                return ClipboardOutcome.Failed;
            }

            // Tell paste targets this is a copy, never a move.
            uint effectFormat = RegisterClipboardFormat("Preferred DropEffect");
            if (effectFormat != 0)
            {
                IntPtr hEffect = BuildDword(DropEffectCopy);
                if (SetClipboardData(effectFormat, hEffect) == IntPtr.Zero)
                {
                    GlobalFree(hEffect); // hint is optional — CF_HDROP alone still works
                }
            }

            return ClipboardOutcome.FileCopied;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static IntPtr BuildDropFiles(string path)
    {
        // DROPFILES header + double-null-terminated UTF-16 path list.
        int pathBytes = (path.Length + 2) * sizeof(char);
        IntPtr hGlobal = GlobalAlloc(GmemMoveable | GmemZeroInit, (nuint)(DropFilesHeaderSize + pathBytes));
        if (hGlobal == IntPtr.Zero)
        {
            throw new OutOfMemoryException("GlobalAlloc failed for clipboard data.");
        }

        IntPtr ptr = GlobalLock(hGlobal);
        try
        {
            Marshal.WriteInt32(ptr, 0, DropFilesHeaderSize); // pFiles: offset to the path list
            // pt (8 bytes) and fNC stay zero via GMEM_ZEROINIT.
            Marshal.WriteInt32(ptr, 16, 1);                  // fWide: UTF-16
            Marshal.Copy(path.ToCharArray(), 0, ptr + DropFilesHeaderSize, path.Length);
            // Two trailing null chars come from GMEM_ZEROINIT.
        }
        finally
        {
            GlobalUnlock(hGlobal);
        }

        return hGlobal;
    }

    private static IntPtr BuildDword(uint value)
    {
        IntPtr hGlobal = GlobalAlloc(GmemMoveable | GmemZeroInit, sizeof(uint));
        if (hGlobal == IntPtr.Zero)
        {
            throw new OutOfMemoryException("GlobalAlloc failed for clipboard data.");
        }

        IntPtr ptr = GlobalLock(hGlobal);
        try
        {
            Marshal.WriteInt32(ptr, 0, unchecked((int)value));
        }
        finally
        {
            GlobalUnlock(hGlobal);
        }

        return hGlobal;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}

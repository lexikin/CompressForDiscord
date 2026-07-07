using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CompressForDiscord.Services.Windows;

/// <summary>
/// Drives the coloured progress overlay on a window's Windows taskbar button. Non-annotated
/// so callers can hold it as an <see cref="ITaskbarProgress"/> without platform guards on every
/// use — only construction of <see cref="WindowsTaskbarProgress"/> is Windows-gated.
/// </summary>
internal interface ITaskbarProgress : IDisposable
{
    /// <summary>Scrolling "working, no measurable progress yet" state.</summary>
    void SetIndeterminate();

    /// <summary>Solid bar filled to <paramref name="percent"/> (0–100).</summary>
    void SetValue(double percent);

    /// <summary>Clears the overlay entirely.</summary>
    void SetNoProgress();
}

/// <summary>
/// Thin ITaskbarList3 wrapper. Every COM call is best-effort — the taskbar overlay is a nicety
/// and must never fail (or slow) a compression job, so failures are swallowed.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsTaskbarProgress : ITaskbarProgress
{
    private readonly IntPtr _hwnd;
    private readonly ITaskbarList3? _taskbar;

    public WindowsTaskbarProgress(IntPtr hwnd)
    {
        _hwnd = hwnd;
        try
        {
            _taskbar = (ITaskbarList3)new TaskbarInstance();
            _taskbar.HrInit();
        }
        catch (Exception e) when (e is COMException or InvalidCastException or NotSupportedException)
        {
            _taskbar = null; // no shell taskbar (e.g. Server Core, or explorer not running)
        }
    }

    public void SetIndeterminate() => TrySetState(TbpFlag.Indeterminate);

    public void SetNoProgress() => TrySetState(TbpFlag.NoProgress);

    public void SetValue(double percent)
    {
        if (_taskbar is null)
        {
            return;
        }

        TrySetState(TbpFlag.Normal);
        try
        {
            _taskbar.SetProgressValue(_hwnd, (ulong)Math.Clamp(percent, 0, 100), 100);
        }
        catch (COMException)
        {
        }
    }

    public void Dispose() => SetNoProgress();

    private void TrySetState(TbpFlag flag)
    {
        try
        {
            _taskbar?.SetProgressState(_hwnd, flag);
        }
        catch (COMException)
        {
        }
    }

    private enum TbpFlag
    {
        NoProgress = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        Paused = 0x8,
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    private class TaskbarInstance;

    // Only the vtable slots up to SetProgressState are declared (in exact COM order); the
    // remaining ITaskbarList3 methods are unused, so omitting them is safe.
    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);

        // ITaskbarList3
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, TbpFlag flags);
    }
}

#pragma once

#include <windows.h>
#include <shobjidl_core.h>
#include <wrl/implements.h>
#include <string>

// CLSID shared with packaging/windows/sparse/build-sparse.ps1 ($Clsid) and stamped into the
// AppxManifest com:Class — change all three together or the Win11 menu silently vanishes.
class __declspec(uuid("C3E5A2F8-9B1D-4E7A-8F26-D4A0C7B3915E")) CompressExplorerCommand final
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          IExplorerCommand,
          IObjectWithSite>
{
public:
    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* items, PWSTR* name) noexcept;
    IFACEMETHODIMP GetIcon(IShellItemArray* items, PWSTR* icon) noexcept;
    IFACEMETHODIMP GetToolTip(IShellItemArray* items, PWSTR* tip) noexcept;
    IFACEMETHODIMP GetCanonicalName(GUID* guidCommandName) noexcept;
    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL okToBeSlow, EXPCMDSTATE* state) noexcept;
    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx* bindCtx) noexcept;
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) noexcept;
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumCommands) noexcept;

    // IObjectWithSite
    IFACEMETHODIMP SetSite(IUnknown* site) noexcept;
    IFACEMETHODIMP GetSite(REFIID riid, void** site) noexcept;

private:
    // The DLL is installed beside CompressForDiscord.exe, so the exe path derives from our
    // own module path — no registry round-trip needed.
    static std::wstring GetAppExePath();

    Microsoft::WRL::ComPtr<IUnknown> m_site;
};

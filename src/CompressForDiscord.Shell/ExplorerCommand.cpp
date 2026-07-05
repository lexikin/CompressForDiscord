#include "ExplorerCommand.h"

#include <shlwapi.h>
#include <wrl/module.h>

using Microsoft::WRL::ComPtr;

extern HINSTANCE g_hInstance;

std::wstring CompressExplorerCommand::GetAppExePath()
{
    wchar_t modulePath[MAX_PATH]{};
    if (GetModuleFileNameW(g_hInstance, modulePath, MAX_PATH) == 0)
    {
        return {};
    }

    std::wstring path(modulePath);
    const size_t slash = path.find_last_of(L'\\');
    if (slash == std::wstring::npos)
    {
        return {};
    }

    path.resize(slash + 1);
    path += L"CompressForDiscord.exe";
    return path;
}

IFACEMETHODIMP CompressExplorerCommand::GetTitle(IShellItemArray*, PWSTR* name) noexcept
{
    return SHStrDupW(L"Compress for Discord", name);
}

IFACEMETHODIMP CompressExplorerCommand::GetIcon(IShellItemArray*, PWSTR* icon) noexcept
{
    const std::wstring exe = GetAppExePath();
    if (exe.empty())
    {
        *icon = nullptr;
        return E_FAIL;
    }

    return SHStrDupW((exe + L",0").c_str(), icon);
}

IFACEMETHODIMP CompressExplorerCommand::GetToolTip(IShellItemArray*, PWSTR* tip) noexcept
{
    *tip = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP CompressExplorerCommand::GetCanonicalName(GUID* guidCommandName) noexcept
{
    *guidCommandName = __uuidof(CompressExplorerCommand);
    return S_OK;
}

IFACEMETHODIMP CompressExplorerCommand::GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) noexcept
{
    // The manifest already scopes us to supported file types — always enabled here.
    *state = ECS_ENABLED;
    return S_OK;
}

IFACEMETHODIMP CompressExplorerCommand::Invoke(IShellItemArray* items, IBindCtx*) noexcept
{
    if (items == nullptr)
    {
        return S_OK;
    }

    const std::wstring exe = GetAppExePath();
    if (exe.empty())
    {
        return E_FAIL;
    }

    DWORD count = 0;
    if (FAILED(items->GetCount(&count)))
    {
        return S_OK;
    }

    // One process per selected item — matches the classic verb's Document model and keeps
    // each job's progress window independent.
    for (DWORD i = 0; i < count; ++i)
    {
        ComPtr<IShellItem> item;
        if (FAILED(items->GetItemAt(i, &item)))
        {
            continue;
        }

        PWSTR rawPath = nullptr;
        if (FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &rawPath)) || rawPath == nullptr)
        {
            continue;
        }

        std::wstring commandLine = L"\"" + exe + L"\" \"" + rawPath + L"\"";
        CoTaskMemFree(rawPath);

        STARTUPINFOW startupInfo{};
        startupInfo.cb = sizeof(startupInfo);
        PROCESS_INFORMATION processInfo{};

        // CreateProcessW may modify the buffer — std::wstring::data() is mutable since C++17.
        if (CreateProcessW(
                nullptr, commandLine.data(), nullptr, nullptr, FALSE,
                CREATE_UNICODE_ENVIRONMENT | CREATE_DEFAULT_ERROR_MODE,
                nullptr, nullptr, &startupInfo, &processInfo))
        {
            CloseHandle(processInfo.hThread);
            CloseHandle(processInfo.hProcess);
        }
    }

    return S_OK;
}

IFACEMETHODIMP CompressExplorerCommand::GetFlags(EXPCMDFLAGS* flags) noexcept
{
    *flags = ECF_DEFAULT;
    return S_OK;
}

IFACEMETHODIMP CompressExplorerCommand::EnumSubCommands(IEnumExplorerCommand** enumCommands) noexcept
{
    *enumCommands = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP CompressExplorerCommand::SetSite(IUnknown* site) noexcept
{
    m_site = site;
    return S_OK;
}

IFACEMETHODIMP CompressExplorerCommand::GetSite(REFIID riid, void** site) noexcept
{
    return m_site ? m_site.CopyTo(riid, site) : E_FAIL;
}

#include <windows.h>
#include <wrl/module.h>

#include "ExplorerCommand.h"

using namespace Microsoft::WRL;

HINSTANCE g_hInstance = nullptr;

// Registers CompressExplorerCommand with the in-proc WRL module so
// DllGetClassObject can serve it by CLSID (same pattern as vscode-explorer-command).
CoCreatableClass(CompressExplorerCommand);

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_hInstance = hModule;
        DisableThreadLibraryCalls(hModule);
    }

    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, _COM_Outptr_ void** instance)
{
    return Module<ModuleType::InProc>::GetModule().GetClassObject(rclsid, riid, instance);
}

STDAPI DllCanUnloadNow()
{
    return Module<ModuleType::InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

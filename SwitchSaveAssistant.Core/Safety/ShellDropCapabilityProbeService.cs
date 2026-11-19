using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Safety;

[SupportedOSPlatform("windows")]
public sealed class ShellDropCapabilityProbeService
{
    private const int ThisPcNamespace = 17;

    private static readonly Guid IidShellFolder =
        new("000214E6-0000-0000-C000-000000000046");

    private static readonly Guid IidDataObject =
        new("0000010E-0000-0000-C000-000000000046");

    private static readonly Guid IidDropTarget =
        new("00000122-0000-0000-C000-000000000046");

    public ShellDropCapabilityResult Run(
        string deviceName,
        string storageName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            deviceName
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            storageName
        );

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Este probe requer o Windows Shell."
            );
        }

        return RunSta(
            () => ProbeSta(
                deviceName,
                storageName
            )
        );
    }

    private static ShellDropCapabilityResult ProbeSta(
        string deviceName,
        string storageName
    )
    {
        var oleHr =
            OleInitialize(
                IntPtr.Zero
            );

        if (oleHr < 0)
        {
            Marshal.ThrowExceptionForHR(
                oleHr
            );
        }

        object? shell = null;
        object? thisPc = null;
        object? deviceItem = null;
        object? deviceFolder = null;
        object? storageItem = null;

        IntPtr destinationPidl =
            IntPtr.Zero;

        IntPtr sourcePidl =
            IntPtr.Zero;

        string? tempRoot =
            null;

        try
        {
            var shellType =
                Type.GetTypeFromProgID(
                    "Shell.Application"
                )
                ?? throw new InvalidOperationException(
                    "Shell.Application não está disponível."
                );

            shell =
                Activator.CreateInstance(
                    shellType
                )
                ?? throw new InvalidOperationException(
                    "Não foi possível iniciar Shell.Application."
                );

            dynamic dynamicShell =
                shell;

            thisPc =
                dynamicShell.NameSpace(
                    ThisPcNamespace
                )
                ?? throw new InvalidOperationException(
                    "Não foi possível abrir 'Este Computador'."
                );

            deviceItem =
                FindChildItem(
                    thisPc,
                    deviceName,
                    requireFolder: true
                )
                ?? throw new DirectoryNotFoundException(
                    $"Dispositivo '{deviceName}' não encontrado no Windows Shell."
                );

            dynamic dynamicDeviceItem =
                deviceItem;

            deviceFolder =
                dynamicDeviceItem.GetFolder
                ?? throw new InvalidOperationException(
                    $"Não foi possível abrir '{deviceName}'."
                );

            storageItem =
                FindChildItem(
                    deviceFolder,
                    storageName,
                    requireFolder: true
                )
                ?? throw new DirectoryNotFoundException(
                    $"Armazenamento '{storageName}' não encontrado em '{deviceName}'."
                );

            var destinationPidlHr =
                SHGetIDListFromObject(
                    storageItem,
                    out destinationPidl
                );

            ThrowIfFailed(
                destinationPidlHr,
                "SHGetIDListFromObject(7: Saves)"
            );

            var destinationAvailable =
                TryGetUiObject(
                    destinationPidl,
                    IidDropTarget,
                    out var destinationHr
                );

            tempRoot =
                Directory
                    .CreateTempSubdirectory(
                        "SwitchSaveAssistant-DropCapability-"
                    )
                    .FullName;

            var sourceFolder =
                Path.Combine(
                    tempRoot,
                    "Installed games"
                );

            var sourceSave =
                Path.Combine(
                    sourceFolder,
                    "__SwitchSaveAssistant_Probe__",
                    "__ProbeUser__"
                );

            Directory.CreateDirectory(
                sourceSave
            );

            File.WriteAllText(
                Path.Combine(
                    sourceSave,
                    "probe.txt"
                ),
                "SwitchSaveAssistant capability probe only."
            );

            var parseHr =
                SHParseDisplayName(
                    sourceFolder,
                    IntPtr.Zero,
                    out sourcePidl,
                    0,
                    out _
                );

            ThrowIfFailed(
                parseHr,
                "SHParseDisplayName(staging local)"
            );

            var sourceAvailable =
                TryGetUiObject(
                    sourcePidl,
                    IidDataObject,
                    out var sourceHr
                );

            return new ShellDropCapabilityResult(
                destinationAvailable,
                destinationHr,
                sourceAvailable,
                sourceHr
            );
        }
        finally
        {
            if (sourcePidl !=
                IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(
                    sourcePidl
                );
            }

            if (destinationPidl !=
                IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(
                    destinationPidl
                );
            }

            TryFinalRelease(
                storageItem
            );

            TryFinalRelease(
                deviceFolder
            );

            TryFinalRelease(
                deviceItem
            );

            TryFinalRelease(
                thisPc
            );

            TryFinalRelease(
                shell
            );

            if (!string.IsNullOrWhiteSpace(
                    tempRoot
                ) &&
                Directory.Exists(
                    tempRoot
                ))
            {
                try
                {
                    Directory.Delete(
                        tempRoot,
                        recursive: true
                    );
                }
                catch
                {
                    // Diretório local descartável.
                }
            }

            OleUninitialize();
        }
    }

    private static bool TryGetUiObject(
        IntPtr absolutePidl,
        Guid iid,
        out int resultHr
    )
    {
        IShellFolder? parent =
            null;

        IntPtr childPidl =
            IntPtr.Zero;

        IntPtr interfacePointer =
            IntPtr.Zero;

        try
        {
            var shellFolderIid =
                IidShellFolder;

            var bindHr =
                SHBindToParent(
                    absolutePidl,
                    ref shellFolderIid,
                    out parent,
                    out childPidl
                );

            if (bindHr < 0)
            {
                resultHr =
                    bindHr;

                return false;
            }

            var requestedIid =
                iid;

            var children =
                new[]
                {
                    childPidl
                };

            resultHr =
                parent.GetUIObjectOf(
                    IntPtr.Zero,
                    1,
                    children,
                    ref requestedIid,
                    IntPtr.Zero,
                    out interfacePointer
                );

            return resultHr >= 0 &&
                   interfacePointer !=
                   IntPtr.Zero;
        }
        finally
        {
            if (interfacePointer !=
                IntPtr.Zero)
            {
                Marshal.Release(
                    interfacePointer
                );
            }

            TryFinalRelease(
                parent
            );
        }
    }

    private static object? FindChildItem(
        object folder,
        string name,
        bool requireFolder
    )
    {
        object? items =
            null;

        try
        {
            dynamic dynamicFolder =
                folder;

            items =
                dynamicFolder.Items();

            dynamic dynamicItems =
                items;

            var count =
                Convert.ToInt32(
                    dynamicItems.Count
                );

            for (var index = 0;
                 index < count;
                 index++)
            {
                object? item =
                    null;

                try
                {
                    item =
                        dynamicItems.Item(
                            index
                        );

                    if (item is null)
                    {
                        continue;
                    }

                    dynamic dynamicItem =
                        item;

                    var itemName =
                        Convert.ToString(
                            dynamicItem.Name
                        );

                    var isFolder =
                        Convert.ToBoolean(
                            dynamicItem.IsFolder
                        );

                    if (string.Equals(
                            itemName,
                            name,
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        (!requireFolder ||
                         isFolder))
                    {
                        return item;
                    }
                }
                catch
                {
                    // Alguns itens virtuais não expõem
                    // todas as propriedades.
                }

                TryFinalRelease(
                    item
                );
            }

            return null;
        }
        finally
        {
            TryFinalRelease(
                items
            );
        }
    }

    private static T RunSta<T>(
        Func<T> action
    )
    {
        T? result =
            default;

        ExceptionDispatchInfo? captured =
            null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        result =
                            action();
                    }
                    catch (Exception ex)
                    {
                        captured =
                            ExceptionDispatchInfo.Capture(
                                ex
                            );
                    }
                }
            )
            {
                IsBackground = true,
                Name = "SwitchSaveAssistant-DropCapability-STA"
            };

        thread.SetApartmentState(
            ApartmentState.STA
        );

        thread.Start();
        thread.Join();

        captured?.Throw();

        return result
            ?? throw new InvalidOperationException(
                "O probe Shell não retornou resultado."
            );
    }

    private static void ThrowIfFailed(
        int hr,
        string operation
    )
    {
        if (hr >= 0)
        {
            return;
        }

        var description =
            Marshal.GetExceptionForHR(
                hr
            )?.Message
            ?? "erro COM desconhecido";

        throw new IOException(
            $"{operation} falhou. " +
            $"HRESULT=0x{hr:X8}. " +
            description
        );
    }

    private static void TryFinalRelease(
        object? value
    )
    {
        if (value is null)
        {
            return;
        }

        try
        {
            if (Marshal.IsComObject(
                    value
                ))
            {
                Marshal.FinalReleaseComObject(
                    value
                );
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    [DllImport(
        "ole32.dll",
        PreserveSig = true
    )]
    private static extern int OleInitialize(
        IntPtr pvReserved
    );

    [DllImport(
        "ole32.dll"
    )]
    private static extern void OleUninitialize();

    [DllImport(
        "shell32.dll",
        PreserveSig = true
    )]
    private static extern int SHGetIDListFromObject(
        [MarshalAs(UnmanagedType.IUnknown)]
        object punk,
        out IntPtr ppidl
    );

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode,
        PreserveSig = true
    )]
    private static extern int SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)]
        string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut
    );

    [DllImport(
        "shell32.dll",
        PreserveSig = true
    )]
    private static extern int SHBindToParent(
        IntPtr pidl,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)]
        out IShellFolder ppv,
        out IntPtr ppidlLast
    );

    [ComImport]
    [Guid(
        "000214E6-0000-0000-C000-000000000046"
    )]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown
    )]
    private interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(
            IntPtr hwnd,
            IntPtr pbc,
            IntPtr pszDisplayName,
            IntPtr pchEaten,
            IntPtr ppidl,
            IntPtr pdwAttributes
        );

        [PreserveSig]
        int EnumObjects(
            IntPtr hwnd,
            uint grfFlags,
            out IntPtr ppenumIDList
        );

        [PreserveSig]
        int BindToObject(
            IntPtr pidl,
            IntPtr pbc,
            ref Guid riid,
            out IntPtr ppv
        );

        [PreserveSig]
        int BindToStorage(
            IntPtr pidl,
            IntPtr pbc,
            ref Guid riid,
            out IntPtr ppv
        );

        [PreserveSig]
        int CompareIDs(
            IntPtr lParam,
            IntPtr pidl1,
            IntPtr pidl2
        );

        [PreserveSig]
        int CreateViewObject(
            IntPtr hwndOwner,
            ref Guid riid,
            out IntPtr ppv
        );

        [PreserveSig]
        int GetAttributesOf(
            uint cidl,
            IntPtr apidl,
            ref uint rgfInOut
        );

        [PreserveSig]
        int GetUIObjectOf(
            IntPtr hwndOwner,
            uint cidl,
            [MarshalAs(
                UnmanagedType.LPArray,
                SizeParamIndex = 1
            )]
            IntPtr[] apidl,
            ref Guid riid,
            IntPtr rgfReserved,
            out IntPtr ppv
        );

        [PreserveSig]
        int GetDisplayNameOf(
            IntPtr pidl,
            uint uFlags,
            IntPtr pName
        );

        [PreserveSig]
        int SetNameOf(
            IntPtr hwnd,
            IntPtr pidl,
            IntPtr pszName,
            uint uFlags,
            out IntPtr ppidlOut
        );
    }
}

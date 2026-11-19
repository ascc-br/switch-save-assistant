using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Safety;

[SupportedOSPlatform("windows")]
public sealed class ShellDropNegotiationProbeService
{
    private const int ThisPcNamespace = 17;

    private const uint MkLButton = 0x0001;

    private const uint DropEffectCopy = 0x00000001;

    private static readonly Guid IidShellFolder =
        new("000214E6-0000-0000-C000-000000000046");

    private static readonly Guid IidDataObject =
        new("0000010E-0000-0000-C000-000000000046");

    private static readonly Guid IidDropTarget =
        new("00000122-0000-0000-C000-000000000046");

    public ShellDropNegotiationResult Run(
        string deviceName,
        string storageName
    )
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Este probe requer Windows."
            );
        }

        return RunSta(
            () =>
                RunStaCore(
                    deviceName,
                    storageName
                )
        );
    }

    private static ShellDropNegotiationResult RunStaCore(
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

        IntPtr dropTargetPointer =
            IntPtr.Zero;

        IntPtr dataObjectPointer =
            IntPtr.Zero;

        IDropTarget? dropTarget =
            null;

        IDataObject? dataObject =
            null;

        string? tempRoot =
            null;

        try
        {
            var shellType =
                Type.GetTypeFromProgID(
                    "Shell.Application"
                )
                ?? throw new InvalidOperationException(
                    "Shell.Application indisponível."
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
                    "Não foi possível abrir Este Computador."
                );

            deviceItem =
                FindChildItem(
                    thisPc,
                    deviceName,
                    true
                )
                ?? throw new DirectoryNotFoundException(
                    $"Dispositivo '{deviceName}' não encontrado."
                );

            dynamic dynamicDeviceItem =
                deviceItem;

            deviceFolder =
                dynamicDeviceItem.GetFolder;

            storageItem =
                FindChildItem(
                    deviceFolder,
                    storageName,
                    true
                )
                ?? throw new DirectoryNotFoundException(
                    $"Armazenamento '{storageName}' não encontrado."
                );

            ThrowIfFailed(
                SHGetIDListFromObject(
                    storageItem,
                    out destinationPidl
                ),
                "SHGetIDListFromObject(7: Saves)"
            );

            dropTargetPointer =
                GetUiObject(
                    destinationPidl,
                    IidDropTarget
                );

            dropTarget =
                (IDropTarget)
                Marshal.GetObjectForIUnknown(
                    dropTargetPointer
                );

            tempRoot =
                Directory
                    .CreateTempSubdirectory(
                        "SwitchSaveAssistant-DropNegotiation-"
                    )
                    .FullName;

            var sourceFolder =
                Path.Combine(
                    tempRoot,
                    "Installed games"
                );

            var probePayload =
                Path.Combine(
                    sourceFolder,
                    "__SwitchSaveAssistant_Probe__",
                    "__ProbeUser__"
                );

            Directory.CreateDirectory(
                probePayload
            );

            File.WriteAllText(
                Path.Combine(
                    probePayload,
                    "probe.txt"
                ),
                "Negotiation only. Drop must never be called."
            );

            ThrowIfFailed(
                SHParseDisplayName(
                    sourceFolder,
                    IntPtr.Zero,
                    out sourcePidl,
                    0,
                    out _
                ),
                "SHParseDisplayName(source)"
            );

            dataObjectPointer =
                GetUiObject(
                    sourcePidl,
                    IidDataObject
                );

            dataObject =
                (IDataObject)
                Marshal.GetObjectForIUnknown(
                    dataObjectPointer
                );

            var effect =
                DropEffectCopy;

            var point =
                new PointL
                {
                    X = 0,
                    Y = 0
                };

            var dragEnterHr =
                dropTarget.DragEnter(
                    dataObject,
                    MkLButton,
                    point,
                    ref effect
                );

            // Independentemente do efeito anunciado,
            // encerramos a negociação sem executar Drop.
            var dragLeaveHr =
                dropTarget.DragLeave();

            return new ShellDropNegotiationResult(
                true,
                true,
                dragEnterHr,
                DropEffectCopy,
                effect,
                dragLeaveHr,
                false
            );
        }
        finally
        {
            TryFinalRelease(
                dataObject
            );

            TryFinalRelease(
                dropTarget
            );

            if (dataObjectPointer !=
                IntPtr.Zero)
            {
                Marshal.Release(
                    dataObjectPointer
                );
            }

            if (dropTargetPointer !=
                IntPtr.Zero)
            {
                Marshal.Release(
                    dropTargetPointer
                );
            }

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
                        true
                    );
                }
                catch
                {
                    // Local temporário.
                }
            }

            OleUninitialize();
        }
    }

    private static IntPtr GetUiObject(
        IntPtr absolutePidl,
        Guid iid
    )
    {
        IShellFolder? parent =
            null;

        try
        {
            var shellFolderIid =
                IidShellFolder;

            ThrowIfFailed(
                SHBindToParent(
                    absolutePidl,
                    ref shellFolderIid,
                    out parent,
                    out var childPidl
                ),
                "SHBindToParent"
            );

            var requestedIid =
                iid;

            ThrowIfFailed(
                parent.GetUIObjectOf(
                    IntPtr.Zero,
                    1,
                    new[]
                    {
                        childPidl
                    },
                    ref requestedIid,
                    IntPtr.Zero,
                    out var pointer
                ),
                "IShellFolder.GetUIObjectOf"
            );

            if (pointer ==
                IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "GetUIObjectOf retornou ponteiro nulo."
                );
            }

            return pointer;
        }
        finally
        {
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
            );

        thread.SetApartmentState(
            ApartmentState.STA
        );

        thread.Start();
        thread.Join();

        captured?.Throw();

        return result
            ?? throw new InvalidOperationException(
                "Probe não retornou resultado."
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

        throw new IOException(
            $"{operation} falhou. HRESULT=0x{hr:X8}. " +
            (Marshal.GetExceptionForHR(hr)?.Message
             ?? "Erro COM desconhecido.")
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
        }
    }

    [StructLayout(
        LayoutKind.Sequential
    )]
    private struct PointL
    {
        public int X;
        public int Y;
    }

    [ComImport]
    [Guid(
        "00000122-0000-0000-C000-000000000046"
    )]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown
    )]
    private interface IDropTarget
    {
        [PreserveSig]
        int DragEnter(
            [MarshalAs(UnmanagedType.Interface)]
            IDataObject dataObject,
            uint keyState,
            PointL point,
            ref uint effect
        );

        [PreserveSig]
        int DragOver(
            uint keyState,
            PointL point,
            ref uint effect
        );

        [PreserveSig]
        int DragLeave();

        [PreserveSig]
        int Drop(
            [MarshalAs(UnmanagedType.Interface)]
            IDataObject dataObject,
            uint keyState,
            PointL point,
            ref uint effect
        );
    }

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
            uint flags,
            IntPtr name
        );

        [PreserveSig]
        int SetNameOf(
            IntPtr hwnd,
            IntPtr pidl,
            IntPtr name,
            uint flags,
            out IntPtr ppidlOut
        );
    }

    [DllImport(
        "ole32.dll",
        PreserveSig = true
    )]
    private static extern int OleInitialize(
        IntPtr reserved
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
        string name,
        IntPtr pbc,
        out IntPtr pidl,
        uint sfgaoIn,
        out uint sfgaoOut
    );

    [DllImport(
        "shell32.dll",
        PreserveSig = true
    )]
    private static extern int SHBindToParent(
        IntPtr pidl,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)]
        out IShellFolder parent,
        out IntPtr childPidl
    );
}

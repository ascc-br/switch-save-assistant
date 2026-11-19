using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Providers;

[SupportedOSPlatform("windows")]
public sealed class ShellMtpDropProvider
{
    private const int ThisPcNamespace = 17;

    private const uint MkLButton = 0x0001;

    private const uint MkControl = 0x0008;

    private const uint DropEffectCopy = 0x00000001;

    private static readonly Guid IidShellFolder =
        new("000214E6-0000-0000-C000-000000000046");

    private static readonly Guid IidDataObject =
        new("0000010E-0000-0000-C000-000000000046");

    private static readonly Guid IidDropTarget =
        new("00000122-0000-0000-C000-000000000046");

    public ShellDropOperationResult DropLocalItemToStorageFolder(
        string deviceName,
        string storageName,
        string sourcePath,
        params string[] destinationSegments
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            deviceName
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            storageName
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourcePath
        );

        ArgumentNullException.ThrowIfNull(
            destinationSegments
        );

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Shell Drop requer Windows."
            );
        }

        sourcePath =
            Path.GetFullPath(
                sourcePath
            );

        if (!File.Exists(sourcePath) &&
            !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                "Item local de origem não encontrado.",
                sourcePath
            );
        }

        foreach (var segment
                 in destinationSegments)
        {
            ValidateSegment(
                segment
            );
        }

        return RunSta(
            () =>
                DropSta(
                    deviceName,
                    storageName,
                    sourcePath,
                    destinationSegments
                )
        );
    }

    private static ShellDropOperationResult DropSta(
        string deviceName,
        string storageName,
        string sourcePath,
        IReadOnlyList<string> destinationSegments
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

        var ownedObjects =
            new List<object>();

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

        var dragEntered =
            false;

        var dropCalled =
            false;

        try
        {
            var shellType =
                Type.GetTypeFromProgID(
                    "Shell.Application"
                )
                ?? throw new InvalidOperationException(
                    "Shell.Application não está disponível."
                );

            var shell =
                Activator.CreateInstance(
                    shellType
                )
                ?? throw new InvalidOperationException(
                    "Não foi possível iniciar Shell.Application."
                );

            ownedObjects.Add(
                shell
            );

            dynamic dynamicShell =
                shell;

            var thisPc =
                dynamicShell.NameSpace(
                    ThisPcNamespace
                )
                ?? throw new InvalidOperationException(
                    "Não foi possível abrir Este Computador."
                );

            ownedObjects.Add(
                thisPc
            );

            var deviceItem =
                FindChildItem(
                    thisPc,
                    deviceName,
                    requireFolder: true
                )
                ?? throw new DirectoryNotFoundException(
                    $"Dispositivo '{deviceName}' não encontrado."
                );

            ownedObjects.Add(
                deviceItem
            );

            dynamic dynamicDeviceItem =
                deviceItem;

            var deviceFolder =
                dynamicDeviceItem.GetFolder
                ?? throw new InvalidOperationException(
                    $"Não foi possível abrir '{deviceName}'."
                );

            ownedObjects.Add(
                deviceFolder
            );

            var storageItem =
                FindChildItem(
                    deviceFolder,
                    storageName,
                    requireFolder: true
                )
                ?? throw new DirectoryNotFoundException(
                    $"Armazenamento '{storageName}' não encontrado."
                );

            ownedObjects.Add(
                storageItem
            );

            object destinationItem =
                storageItem;

            object? currentFolder =
                null;

            if (destinationSegments.Count > 0)
            {
                dynamic dynamicStorageItem =
                    storageItem;

                currentFolder =
                    dynamicStorageItem.GetFolder
                    ?? throw new InvalidOperationException(
                        $"Não foi possível abrir '{storageName}'."
                    );

                ownedObjects.Add(
                    currentFolder
                );

                foreach (var segment
                         in destinationSegments)
                {
                    var childItem =
                        FindChildItemWithRetry(
                            currentFolder,
                            segment,
                            requireFolder: true
                        )
                        ?? throw new DirectoryNotFoundException(
                            $"Pasta remota '{segment}' não encontrada no Windows Shell."
                        );

                    ownedObjects.Add(
                        childItem
                    );

                    destinationItem =
                        childItem;

                    dynamic dynamicChildItem =
                        childItem;

                    var childFolder =
                        dynamicChildItem.GetFolder
                        ?? throw new InvalidOperationException(
                            $"Não foi possível abrir a pasta remota '{segment}'."
                        );

                    ownedObjects.Add(
                        childFolder
                    );

                    currentFolder =
                        childFolder;
                }
            }

            ThrowIfFailed(
                SHGetIDListFromObject(
                    destinationItem,
                    out destinationPidl
                ),
                "SHGetIDListFromObject(destino)"
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

            ThrowIfFailed(
                SHParseDisplayName(
                    sourcePath,
                    IntPtr.Zero,
                    out sourcePidl,
                    0,
                    out _
                ),
                "SHParseDisplayName(origem)"
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

            var point =
                new PointL
                {
                    X = 0,
                    Y = 0
                };

            var enterEffect =
                DropEffectCopy;

            var dragKeyState =
                MkLButton |
                MkControl;

            var dragEnterHr =
                dropTarget.DragEnter(
                    dataObject,
                    dragKeyState,
                    point,
                    ref enterEffect
                );

            ThrowIfFailed(
                dragEnterHr,
                "IDropTarget.DragEnter"
            );

            dragEntered =
                true;

            if ((enterEffect &
                 DropEffectCopy) == 0)
            {
                var leaveHr =
                    dropTarget.DragLeave();

                dragEntered =
                    false;

                ThrowIfFailed(
                    leaveHr,
                    "IDropTarget.DragLeave"
                );

                throw new IOException(
                    "O destino recusou DROPEFFECT_COPY em DragEnter."
                );
            }

            var overEffect =
                enterEffect;

            var dragOverHr =
                dropTarget.DragOver(
                    dragKeyState,
                    point,
                    ref overEffect
                );

            ThrowIfFailed(
                dragOverHr,
                "IDropTarget.DragOver"
            );

            if ((overEffect &
                 DropEffectCopy) == 0)
            {
                var leaveHr =
                    dropTarget.DragLeave();

                dragEntered =
                    false;

                ThrowIfFailed(
                    leaveHr,
                    "IDropTarget.DragLeave"
                );

                throw new IOException(
                    "O destino recusou DROPEFFECT_COPY em DragOver."
                );
            }

            var dropEffect =
                overEffect;

            // O Drop representa o momento em que o usuário
            // soltou o botão esquerdo. Portanto MK_LBUTTON
            // não deve mais estar presente.
            //
            // CTRL continua ativo para solicitar COPY.
            var dropKeyState =
                MkControl;

            dropCalled =
                true;

            var dropHr =
                dropTarget.Drop(
                    dataObject,
                    dropKeyState,
                    point,
                    ref dropEffect
                );

            ThrowIfFailed(
                dropHr,
                "IDropTarget.Drop"
            );

            if ((dropEffect &
                 DropEffectCopy) == 0)
            {
                throw new IOException(
                    $"Drop terminou sem DROPEFFECT_COPY. " +
                    $"Efeito=0x{dropEffect:X8}."
                );
            }

            // Dá ao provider MTP uma pequena janela
            // para finalizar/liberar a transação.
            Thread.Sleep(
                750
            );

            return new ShellDropOperationResult(
                dragEnterHr,
                DropEffectCopy,
                enterEffect,
                dropHr,
                dropEffect
            );
        }
        finally
        {
            if (dragEntered &&
                !dropCalled &&
                dropTarget is not null)
            {
                try
                {
                    dropTarget.DragLeave();
                }
                catch
                {
                    // Best-effort.
                }
            }

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

            for (var index =
                     ownedObjects.Count - 1;
                 index >= 0;
                 index--)
            {
                TryFinalRelease(
                    ownedObjects[index]
                );
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

    private static object? FindChildItemWithRetry(
        object folder,
        string name,
        bool requireFolder
    )
    {
        for (var attempt = 0;
             attempt < 12;
             attempt++)
        {
            var item =
                FindChildItem(
                    folder,
                    name,
                    requireFolder
                );

            if (item is not null)
            {
                return item;
            }

            Thread.Sleep(
                250
            );
        }

        return null;
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
                    // Alguns itens de namespaces virtuais
                    // não expõem todas as propriedades.
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

    private static void ValidateSegment(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(
                value
            ))
        {
            throw new ArgumentException(
                "Segmento remoto vazio."
            );
        }

        if (value is "." or ".." ||
            value.Contains('\\') ||
            value.Contains('/'))
        {
            throw new ArgumentException(
                $"Segmento remoto inválido: '{value}'."
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
                Name = "SwitchSaveAssistant-ShellDrop-STA"
            };

        thread.SetApartmentState(
            ApartmentState.STA
        );

        thread.Start();
        thread.Join();

        captured?.Throw();

        return result
            ?? throw new InvalidOperationException(
                "Shell Drop não retornou resultado."
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
            out IntPtr ppenumIdList
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
        [MarshalAs(UnmanagedType.LPWStr)]
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

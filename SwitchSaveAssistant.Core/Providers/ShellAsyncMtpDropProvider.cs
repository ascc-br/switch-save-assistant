using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Providers;

[SupportedOSPlatform("windows")]
public sealed class ShellAsyncMtpDropProvider
{
    private const int ThisPcNamespace = 17;

    private const uint MkLButton = 0x0001;
    private const uint MkControl = 0x0008;
    private const uint DropEffectCopy = 0x00000001;

    private const uint PmRemove = 0x0001;

    private static readonly Guid IidShellFolder =
        new("000214E6-0000-0000-C000-000000000046");

    private static readonly Guid IidDataObject =
        new("0000010E-0000-0000-C000-000000000046");

    private static readonly Guid IidDropTarget =
        new("00000122-0000-0000-C000-000000000046");

    private static readonly Guid IidAsyncCapability =
        new("3D8B0590-F691-11D2-8EA9-006097DF5BD4");

    public ShellAsyncDropProbeResult DropNewFileForProbe(
        string deviceName,
        string storageName,
        string sourcePath,
        IReadOnlyList<string> destinationSegments,
        int keepAliveMilliseconds = 12_000
    )
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        if (keepAliveMilliseconds < 1_000 ||
            keepAliveMilliseconds > 60_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keepAliveMilliseconds),
                "O keepalive deve ficar entre 1 e 60 segundos."
            );
        }

        return RunSta(
            () => RunStaCore(
                deviceName,
                storageName,
                Path.GetFullPath(sourcePath),
                destinationSegments,
                keepAliveMilliseconds
            )
        );
    }

    private static ShellAsyncDropProbeResult RunStaCore(
        string deviceName,
        string storageName,
        string sourcePath,
        IReadOnlyList<string> destinationSegments,
        int keepAliveMilliseconds
    )
    {
        var oleHr = OleInitialize(
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
        object? currentFolder = null;
        object? destinationItem = null;

        IntPtr destinationPidl =
            IntPtr.Zero;

        IntPtr sourcePidl =
            IntPtr.Zero;

        IntPtr dropTargetPointer =
            IntPtr.Zero;

        IntPtr dataObjectPointer =
            IntPtr.Zero;

        IntPtr asyncPointer =
            IntPtr.Zero;

        IDropTarget? dropTarget =
            null;

        IDataObject? dataObject =
            null;

        IDataObjectAsyncCapability? asyncCapability =
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
                    "Este Computador indisponível."
                );

            deviceItem =
                FindChildItem(
                    thisPc,
                    deviceName
                )
                ?? throw new DirectoryNotFoundException(
                    deviceName
                );

            dynamic dynamicDeviceItem =
                deviceItem;

            deviceFolder =
                dynamicDeviceItem.GetFolder;

            storageItem =
                FindChildItem(
                    deviceFolder,
                    storageName
                )
                ?? throw new DirectoryNotFoundException(
                    storageName
                );

            destinationItem =
                storageItem;

            if (destinationSegments.Count > 0)
            {
                dynamic dynamicStorage =
                    storageItem;

                currentFolder =
                    dynamicStorage.GetFolder;

                foreach (var segment
                         in destinationSegments)
                {
                    var nextItem =
                        FindChildItemWithRetry(
                            currentFolder!,
                            segment
                        )
                        ?? throw new DirectoryNotFoundException(
                            segment
                        );

                    destinationItem =
                        nextItem;

                    dynamic dynamicNextItem =
                        nextItem;

                    var nextFolder =
                        dynamicNextItem.GetFolder;

                    if (currentFolder is not null &&
                        !ReferenceEquals(
                            currentFolder,
                            deviceFolder
                        ))
                    {
                        TryFinalRelease(
                            currentFolder
                        );
                    }

                    currentFolder =
                        nextFolder;
                }
            }

            ThrowIfFailed(
                SHGetIDListFromObject(
                    destinationItem!,
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

            var asyncCapabilityIid =
                IidAsyncCapability;

            var asyncAvailable =
                Marshal.QueryInterface(
                    dataObjectPointer,
                    in asyncCapabilityIid,
                    out asyncPointer
                ) >= 0 &&
                asyncPointer !=
                IntPtr.Zero;

            var setAsyncHr =
                unchecked((int)0x80004002);

            var getAsyncHr =
                unchecked((int)0x80004002);

            var asyncModeEnabled =
                false;

            if (asyncAvailable)
            {
                asyncCapability =
                    (IDataObjectAsyncCapability)
                    Marshal.GetObjectForIUnknown(
                        asyncPointer
                    );

                setAsyncHr =
                    asyncCapability.SetAsyncMode(
                        true
                    );

                if (setAsyncHr >= 0)
                {
                    getAsyncHr =
                        asyncCapability.GetAsyncMode(
                            out asyncModeEnabled
                        );
                }
            }

            var point =
                new PointL
                {
                    X = 0,
                    Y = 0
                };

            var effect =
                DropEffectCopy;

            var dragEnterHr =
                dropTarget.DragEnter(
                    dataObject,
                    MkLButton | MkControl,
                    point,
                    ref effect
                );

            ThrowIfFailed(
                dragEnterHr,
                "IDropTarget.DragEnter"
            );

            var dropEffect =
                effect;

            var dropHr =
                dropTarget.Drop(
                    dataObject,
                    MkControl,
                    point,
                    ref dropEffect
                );

            ThrowIfFailed(
                dropHr,
                "IDropTarget.Drop"
            );

            var inOperationHr =
                unchecked((int)0x80004002);

            var observedAsync =
                false;

            if (asyncCapability is not null)
            {
                var stopwatch =
                    Stopwatch.StartNew();

                do
                {
                    PumpMessages();

                    inOperationHr =
                        asyncCapability.InOperation(
                            out var inOperation
                        );

                    if (inOperationHr < 0)
                    {
                        break;
                    }

                    if (inOperation)
                    {
                        observedAsync =
                            true;
                    }
                    else if (observedAsync)
                    {
                        break;
                    }
                    else if (stopwatch.ElapsedMilliseconds >=
                             keepAliveMilliseconds)
                    {
                        break;
                    }

                    Thread.Sleep(
                        25
                    );
                }
                while (stopwatch.ElapsedMilliseconds <
                       keepAliveMilliseconds + 3_000);
            }
            else
            {
                Thread.Sleep(
                    1500
                );
            }

            return new ShellAsyncDropProbeResult(
                asyncAvailable,
                setAsyncHr,
                getAsyncHr,
                asyncModeEnabled,
                dragEnterHr,
                dropHr,
                dropEffect,
                inOperationHr,
                observedAsync,
                false,
                0,
                null,
                false
            );
        }
        finally
        {
            TryFinalRelease(
                asyncCapability
            );

            TryFinalRelease(
                dataObject
            );

            TryFinalRelease(
                dropTarget
            );

            if (asyncPointer !=
                IntPtr.Zero)
            {
                Marshal.Release(
                    asyncPointer
                );
            }

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
                currentFolder
            );

            TryFinalRelease(
                destinationItem
            );

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
        string name
    )
    {
        for (var attempt = 0;
             attempt < 12;
             attempt++)
        {
            var item =
                FindChildItem(
                    folder,
                    name
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
        string name
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

                    if (Convert.ToBoolean(
                            dynamicItem.IsFolder
                        ) &&
                        string.Equals(
                            Convert.ToString(
                                dynamicItem.Name
                            ),
                            name,
                            StringComparison.OrdinalIgnoreCase
                        ))
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

    private static void PumpMessages()
    {
        while (PeekMessage(
                   out var message,
                   IntPtr.Zero,
                   0,
                   0,
                   PmRemove
               ))
        {
            TranslateMessage(
                ref message
            );

            DispatchMessage(
                ref message
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

        return result!;
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
            $"{operation} falhou. HRESULT=0x{hr:X8}."
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

    [StructLayout(LayoutKind.Sequential)]
    private struct PointL
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Hwnd;
        public uint MessageId;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Pt;
        public uint Private;
    }

    [ComImport]
    [Guid("3D8B0590-F691-11D2-8EA9-006097DF5BD4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataObjectAsyncCapability
    {
        [PreserveSig]
        int SetAsyncMode(
            [MarshalAs(UnmanagedType.Bool)]
            bool doOperationAsync
        );

        [PreserveSig]
        int GetAsyncMode(
            [MarshalAs(UnmanagedType.Bool)]
            out bool isOperationAsync
        );

        [PreserveSig]
        int StartOperation(
            IntPtr reserved
        );

        [PreserveSig]
        int InOperation(
            [MarshalAs(UnmanagedType.Bool)]
            out bool inAsyncOperation
        );

        [PreserveSig]
        int EndOperation(
            int result,
            IntPtr reserved,
            uint effects
        );
    }

    [ComImport]
    [Guid("00000122-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
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
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
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
            uint flags,
            out IntPtr enumerator
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
            uint count,
            IntPtr pidls,
            ref uint attributes
        );

        [PreserveSig]
        int GetUIObjectOf(
            IntPtr hwndOwner,
            uint count,
            [MarshalAs(
                UnmanagedType.LPArray,
                SizeParamIndex = 1
            )]
            IntPtr[] pidls,
            ref Guid riid,
            IntPtr reserved,
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
            out IntPtr outputPidl
        );
    }

    [DllImport("ole32.dll", PreserveSig = true)]
    private static extern int OleInitialize(
        IntPtr reserved
    );

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHGetIDListFromObject(
        [MarshalAs(UnmanagedType.IUnknown)]
        object item,
        out IntPtr pidl
    );

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode,
        PreserveSig = true
    )]
    private static extern int SHParseDisplayName(
        string name,
        IntPtr bindContext,
        out IntPtr pidl,
        uint attributesIn,
        out uint attributesOut
    );

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHBindToParent(
        IntPtr pidl,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)]
        out IShellFolder parent,
        out IntPtr childPidl
    );

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(
        out Message message,
        IntPtr hwnd,
        uint filterMin,
        uint filterMax,
        uint remove
    );

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(
        ref Message message
    );

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(
        ref Message message
    );
}

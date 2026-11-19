using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SwitchSaveAssistant.Core.Providers;

[SupportedOSPlatform("windows")]
public sealed class ShellMtpCopyProvider
{
    private const int ThisPcNamespace = 17;

    private const uint FofSilent = 0x0004;
    private const uint FofNoConfirmation = 0x0010;
    private const uint FofNoConfirmMkdir = 0x0200;
    private const uint FofNoErrorUi = 0x0400;
    private const uint FofNoCopySecurityAttribs = 0x0800;
    private const uint FofxEarlyFailure = 0x00100000;

    private const uint OperationFlags =
        FofSilent |
        FofNoConfirmation |
        FofNoConfirmMkdir |
        FofNoErrorUi |
        FofNoCopySecurityAttribs |
        FofxEarlyFailure;

    private static readonly Guid FileOperationClsid =
        new("3AD05575-8857-4850-9277-11B85BDB8E09");

    public void CopyDirectoryToStorage(
        string deviceName,
        string storageName,
        string sourceDirectory
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            deviceName
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            storageName
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceDirectory
        );

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "IFileOperation está disponível somente no Windows."
            );
        }

        sourceDirectory =
            Path.GetFullPath(
                sourceDirectory
            );

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                sourceDirectory
            );
        }

        RunSta(
            () =>
                CopyDirectoryToStorageSta(
                    deviceName,
                    storageName,
                    sourceDirectory
                )
        );
    }

    private static void CopyDirectoryToStorageSta(
        string deviceName,
        string storageName,
        string sourceDirectory
    )
    {
        object? shell = null;
        object? thisPc = null;
        object? deviceItem = null;
        object? deviceFolder = null;
        object? storageItem = null;

        IShellItem? sourceItem = null;
        IShellItem? destinationItem = null;
        IFileOperation? operation = null;

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
                    "Não foi possível iniciar o Windows Shell."
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
                    $"Dispositivo MTP '{deviceName}' não encontrado."
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
                    $"Armazenamento '{storageName}' não encontrado."
                );

            sourceItem =
                CreateShellItemFromPath(
                    sourceDirectory
                );

            destinationItem =
                CreateShellItemFromComObject(
                    storageItem
                );

            var operationType =
                Type.GetTypeFromCLSID(
                    FileOperationClsid,
                    throwOnError: true
                )
                ?? throw new InvalidOperationException(
                    "IFileOperation não está disponível."
                );

            operation =
                (IFileOperation)(
                    Activator.CreateInstance(
                        operationType
                    )
                    ?? throw new InvalidOperationException(
                        "Não foi possível criar IFileOperation."
                    )
                );

            ThrowIfFailed(
                operation.SetOperationFlags(
                    OperationFlags
                ),
                "IFileOperation.SetOperationFlags"
            );

            ThrowIfFailed(
                operation.CopyItem(
                    sourceItem,
                    destinationItem,
                    null,
                    null
                ),
                "IFileOperation.CopyItem"
            );

            ThrowIfFailed(
                operation.PerformOperations(),
                "IFileOperation.PerformOperations"
            );

            ThrowIfFailed(
                operation.GetAnyOperationsAborted(
                    out var aborted
                ),
                "IFileOperation.GetAnyOperationsAborted"
            );

            if (aborted)
            {
                throw new IOException(
                    "O Windows Shell informou que a operação foi abortada."
                );
            }

            // Dá ao provider MTP uma pequena janela
            // para encerrar a transação e liberar objetos.
            Thread.Sleep(
                750
            );
        }
        finally
        {
            TryFinalRelease(operation);
            TryFinalRelease(destinationItem);
            TryFinalRelease(sourceItem);

            TryFinalRelease(storageItem);
            TryFinalRelease(deviceFolder);
            TryFinalRelease(deviceItem);
            TryFinalRelease(thisPc);
            TryFinalRelease(shell);
        }
    }

    private static IShellItem CreateShellItemFromPath(
        string path
    )
    {
        var iid =
            typeof(IShellItem).GUID;

        var hr =
            SHCreateItemFromParsingName(
                path,
                IntPtr.Zero,
                ref iid,
                out var item
            );

        ThrowIfFailed(
            hr,
            "SHCreateItemFromParsingName"
        );

        return item;
    }

    private static IShellItem CreateShellItemFromComObject(
        object value
    )
    {
        IntPtr pidl =
            IntPtr.Zero;

        try
        {
            var hr =
                SHGetIDListFromObject(
                    value,
                    out pidl
                );

            ThrowIfFailed(
                hr,
                "SHGetIDListFromObject"
            );

            if (pidl == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "O Windows Shell retornou um PIDL nulo."
                );
            }

            var iid =
                typeof(IShellItem).GUID;

            hr =
                SHCreateItemFromIDList(
                    pidl,
                    ref iid,
                    out var item
                );

            ThrowIfFailed(
                hr,
                "SHCreateItemFromIDList"
            );

            return item;
        }
        finally
        {
            if (pidl != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(
                    pidl
                );
            }
        }
    }

    private static object? FindChildItem(
        object folder,
        string name,
        bool requireFolder
    )
    {
        object? items = null;

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
                object? item = null;

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
                    // Namespace virtual pode conter itens
                    // que não expõem todas as propriedades.
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

    private static void RunSta(
        Action action
    )
    {
        ExceptionDispatchInfo? captured =
            null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
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
                Name = "SwitchSaveAssistant-Shell-STA"
            };

        thread.SetApartmentState(
            ApartmentState.STA
        );

        thread.Start();
        thread.Join();

        captured?.Throw();
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
            if (Marshal.IsComObject(value))
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
        "shell32.dll",
        CharSet = CharSet.Unicode,
        PreserveSig = true
    )]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)]
        string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)]
        out IShellItem ppv
    );

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
        PreserveSig = true
    )]
    private static extern int SHCreateItemFromIDList(
        IntPtr pidl,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)]
        out IShellItem ppv
    );

    [ComImport]
    [Guid(
        "43826D1E-E718-42EE-BC55-A1E261C37BFE"
    )]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown
    )]
    private interface IShellItem
    {
        void BindToHandler(
            IntPtr pbc,
            ref Guid bhid,
            ref Guid riid,
            out IntPtr ppv
        );

        void GetParent(
            out IShellItem ppsi
        );

        void GetDisplayName(
            uint sigdnName,
            out IntPtr ppszName
        );

        void GetAttributes(
            uint sfgaoMask,
            out uint psfgaoAttribs
        );

        void Compare(
            IShellItem psi,
            uint hint,
            out int piOrder
        );
    }

    [ComImport]
    [Guid(
        "947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"
    )]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown
    )]
    private interface IFileOperation
    {
        [PreserveSig]
        int Advise(
            [MarshalAs(UnmanagedType.Interface)]
            object? pfops,
            out uint pdwCookie
        );

        [PreserveSig]
        int Unadvise(
            uint dwCookie
        );

        [PreserveSig]
        int SetOperationFlags(
            uint dwOperationFlags
        );

        [PreserveSig]
        int SetProgressMessage(
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszMessage
        );

        [PreserveSig]
        int SetProgressDialog(
            [MarshalAs(UnmanagedType.Interface)]
            object? popd
        );

        [PreserveSig]
        int SetProperties(
            [MarshalAs(UnmanagedType.Interface)]
            object? pproparray
        );

        [PreserveSig]
        int SetOwnerWindow(
            IntPtr hwndOwner
        );

        [PreserveSig]
        int ApplyPropertiesToItem(
            IShellItem psiItem
        );

        [PreserveSig]
        int ApplyPropertiesToItems(
            [MarshalAs(UnmanagedType.IUnknown)]
            object punkItems
        );

        [PreserveSig]
        int RenameItem(
            IShellItem psiItem,
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszNewName,
            [MarshalAs(UnmanagedType.Interface)]
            object? pfopsItem
        );

        [PreserveSig]
        int RenameItems(
            [MarshalAs(UnmanagedType.IUnknown)]
            object punkItems,
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszNewName
        );

        [PreserveSig]
        int MoveItem(
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)]
            string? pszNewName,
            [MarshalAs(UnmanagedType.Interface)]
            object? pfopsItem
        );

        [PreserveSig]
        int MoveItems(
            [MarshalAs(UnmanagedType.IUnknown)]
            object punkItems,
            IShellItem psiDestinationFolder
        );

        [PreserveSig]
        int CopyItem(
            IShellItem psiItem,
            IShellItem psiDestinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)]
            string? pszCopyName,
            [MarshalAs(UnmanagedType.Interface)]
            object? pfopsItem
        );

        [PreserveSig]
        int CopyItems(
            [MarshalAs(UnmanagedType.IUnknown)]
            object punkItems,
            IShellItem psiDestinationFolder
        );

        [PreserveSig]
        int DeleteItem(
            IShellItem psiItem,
            [MarshalAs(UnmanagedType.Interface)]
            object? pfopsItem
        );

        [PreserveSig]
        int DeleteItems(
            [MarshalAs(UnmanagedType.IUnknown)]
            object punkItems
        );

        [PreserveSig]
        int NewItem(
            IShellItem psiDestinationFolder,
            uint dwFileAttributes,
            [MarshalAs(UnmanagedType.LPWStr)]
            string pszName,
            [MarshalAs(UnmanagedType.LPWStr)]
            string? pszTemplateName,
            [MarshalAs(UnmanagedType.Interface)]
            object? pfopsItem
        );

        [PreserveSig]
        int PerformOperations();

        [PreserveSig]
        int GetAnyOperationsAborted(
            [MarshalAs(UnmanagedType.Bool)]
            out bool pfAnyOperationsAborted
        );
    }
}

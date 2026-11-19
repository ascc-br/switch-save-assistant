using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace SwitchSaveAssistant.App;

internal sealed class ExplorerPairService
{
	private const int ThisPcNamespace = 17;

	private const uint MonitorDefaultToNearest = 2;

	private const int SwRestore = 9;

	public void OpenSideBySide(
		Window owner,
		string localPath,
		string deviceName,
		string storageName,
		IReadOnlyList<string> remoteSegments
	)
	{
		ArgumentNullException.ThrowIfNull(owner);

		ArgumentException.ThrowIfNullOrWhiteSpace(
			localPath
		);

		ArgumentException.ThrowIfNullOrWhiteSpace(
			deviceName
		);

		ArgumentException.ThrowIfNullOrWhiteSpace(
			storageName
		);

		if (!Directory.Exists(localPath))
		{
			throw new DirectoryNotFoundException(
				$"Pasta local não encontrada: {localPath}"
			);
		}

		var shellType =
			Type.GetTypeFromProgID(
				"Shell.Application"
			)
			?? throw new InvalidOperationException(
				"Windows Shell indisponível."
			);

		object? shellObject = null;

		try
		{
			shellObject =
				Activator.CreateInstance(
					shellType
				)
				?? throw new InvalidOperationException(
					"Não foi possível iniciar o Windows Shell."
				);

			dynamic shell =
				shellObject;

			var remoteHwnd =
OpenRemoteFolder(
shell,
deviceName,
storageName,
remoteSegments
);

var localHwnd =
OpenLocalFolder(
localPath
);

			ArrangeWindows(
				owner,
				localHwnd,
				remoteHwnd
			);
		}
		finally
		{
			FinalRelease(
				shellObject
			);
		}
	}

	private static IntPtr OpenLocalFolder(
		string localPath
	)
	{
		var before =
			GetExplorerWindowHandles();
	
		Process.Start(
			new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments =
					$"/n,\"{localPath}\"",
				UseShellExecute = true
			}
		);
	
		return WaitForNewExplorerWindow(
			before,
			"Pasta local"
		);
	}

	private static IntPtr OpenRemoteFolder(
		dynamic shell,
		string deviceName,
		string storageName,
		IReadOnlyList<string> segments
	)
	{
		var objects =
			new List<object>();

		try
		{
			object thisPc =
				shell.NameSpace(
					ThisPcNamespace
				)
				?? throw new InvalidOperationException(
					"Não foi possível abrir Este Computador."
				);

			objects.Add(
				thisPc
			);

			var deviceItem =
				FindChildFolder(
					thisPc,
					deviceName
				)
				?? throw new DirectoryNotFoundException(
					$"Dispositivo '{deviceName}' não encontrado."
				);

			objects.Add(
				deviceItem
			);

			object currentFolder =
				((dynamic)deviceItem)
					.GetFolder
				?? throw new InvalidOperationException(
					$"Não foi possível abrir '{deviceName}'."
				);

			objects.Add(
				currentFolder
			);

			var storageItem =
				FindChildFolder(
					currentFolder,
					storageName
				)
				?? throw new DirectoryNotFoundException(
					$"'{storageName}' não encontrado."
				);

			objects.Add(
				storageItem
			);

			currentFolder =
				((dynamic)storageItem)
					.GetFolder;

			objects.Add(
				currentFolder
			);

			foreach (var segment
				in segments)
			{
				var child =
					FindChildFolder(
						currentFolder,
						segment
					)
					?? throw new DirectoryNotFoundException(
						$"Pasta remota '{segment}' não encontrada."
					);

				objects.Add(
					child
				);

				currentFolder =
					((dynamic)child)
						.GetFolder;

				objects.Add(
					currentFolder
				);
			}

			object selfObject =
				((dynamic)currentFolder)
					.Self;

			objects.Add(
				selfObject
			);

			return OpenFolderItem(
selfObject
);
		}
		finally
		{
			for (var index =
					objects.Count - 1;
				index >= 0;
				index--)
			{
				FinalRelease(
					objects[index]
				);
			}
		}
	}

	private static object? FindChildFolder(
		object folderObject,
		string childName
	)
	{
		object? itemsObject = null;

		try
		{
			dynamic folder =
				folderObject;

			itemsObject =
				folder.Items();

			dynamic items =
				itemsObject;

			var count =
				Convert.ToInt32(
					items.Count
				);

			for (var index = 0;
				index < count;
				index++)
			{
				object? itemObject =
					null;

				try
				{
					itemObject =
						items.Item(
							index
						);

					if (itemObject is null)
					{
						continue;
					}

					dynamic item =
						itemObject;

					var isFolder =
						Convert.ToBoolean(
							item.IsFolder
						);

					var name =
						Convert.ToString(
							item.Name
						);

					if (isFolder &&
						string.Equals(
							name,
							childName,
							StringComparison
								.OrdinalIgnoreCase
						))
					{
						var result =
							itemObject;

						itemObject =
							null;

						return result;
					}
				}
				finally
				{
					FinalRelease(
						itemObject
					);
				}
			}

			return null;
		}
		finally
		{
			FinalRelease(
				itemsObject
			);
		}
	}

	private static IntPtr OpenFolderItem(
object folderItem
)
{
var before =
GetExplorerWindowHandles();

dynamic item =
folderItem;

/*
 * O destino MTP é um FolderItem do Windows Shell.
 *
 * InvokeVerb() sem argumento executa o verbo padrão
 * do próprio objeto. Para uma pasta, normalmente é Open.
 *
 * Isso evita tentar converter o namespace MTP em um
 * caminho comum de filesystem.
 */
item.InvokeVerb();

return WaitForNewExplorerWindow(
before,
"Pasta do Switch"
);
}



	private static IntPtr WaitForNewExplorerWindow(
		HashSet<IntPtr> before,
		string description
	)
	{
		var stopwatch =
			Stopwatch.StartNew();
	
		while (stopwatch.Elapsed <
			TimeSpan.FromSeconds(8))
		{
			Thread.Sleep(
				100
			);
	
			var after =
				GetExplorerWindowHandles();
	
			var newWindow =
				after.FirstOrDefault(
					hwnd =>
						!before.Contains(
							hwnd
						)
				);
	
			if (newWindow !=
				IntPtr.Zero)
			{
				return newWindow;
			}
		}
	
		throw new InvalidOperationException(
			$"{description} foi aberta, mas não foi possível identificar " +
			"uma nova janela do Explorador de Arquivos."
		);
	}
	
	private static HashSet<IntPtr>
		GetExplorerWindowHandles()
	{
		var result =
			new HashSet<IntPtr>();
	
		EnumWindows(
			(hwnd, _) =>
			{
				if (!IsWindowVisible(
					hwnd
				))
				{
					return true;
				}
	
				var className =
					new StringBuilder(
						256
					);
	
				if (GetClassName(
					hwnd,
					className,
					className.Capacity
				) == 0)
				{
					return true;
				}
	
				if (string.Equals(
					className.ToString(),
					"CabinetWClass",
					StringComparison.Ordinal
				))
				{
					result.Add(
						hwnd
					);
				}
	
				return true;
			},
			IntPtr.Zero
		);
	
		return result;
	}

	private static void ArrangeWindows(
		Window owner,
		IntPtr leftWindow,
		IntPtr rightWindow
	)
	{
		var ownerHandle =
			new WindowInteropHelper(
				owner
			)
			.Handle;

		var monitor =
			MonitorFromWindow(
				ownerHandle,
				MonitorDefaultToNearest
			);

		var info =
			new MonitorInfo
			{
				Size =
					Marshal.SizeOf<
						MonitorInfo
					>()
			};

		if (!GetMonitorInfo(
			monitor,
			ref info
		))
		{
			throw new InvalidOperationException(
				"Não foi possível obter a área útil do monitor."
			);
		}

		var work =
			info.WorkArea;

		var width =
			work.Right -
			work.Left;

		var height =
			work.Bottom -
			work.Top;

		const int gap = 8;

		var leftWidth =
			(width - gap) / 2;

		var rightWidth =
			width -
			leftWidth -
			gap;

		ShowWindow(
			leftWindow,
			SwRestore
		);

		ShowWindow(
			rightWindow,
			SwRestore
		);

		MoveWindow(
			leftWindow,
			work.Left,
			work.Top,
			leftWidth,
			height,
			true
		);

		MoveWindow(
			rightWindow,
			work.Left +
			leftWidth +
			gap,
			work.Top,
			rightWidth,
			height,
			true
		);
	}

	private static void FinalRelease(
		object? value
	)
	{
		if (value is null ||
			!Marshal.IsComObject(
				value
			))
		{
			return;
		}

		try
		{
			Marshal.FinalReleaseComObject(
				value
			);
		}
		catch
		{
			// Liberação defensiva de RCW.
		}
	}

	[StructLayout(
		LayoutKind.Sequential
	)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(
		LayoutKind.Sequential
	)]
	private struct MonitorInfo
	{
		public int Size;

		public Rect MonitorArea;

		public Rect WorkArea;

		public uint Flags;
	}

	private delegate bool EnumWindowsProc(
		IntPtr hwnd,
		IntPtr parameter
	);
	
	[DllImport(
		"user32.dll"
	)]
	[return:
		MarshalAs(
			UnmanagedType.Bool
		)]
	private static extern bool EnumWindows(
		EnumWindowsProc callback,
		IntPtr parameter
	);
	
	[DllImport(
		"user32.dll"
	)]
	[return:
		MarshalAs(
			UnmanagedType.Bool
		)]
	private static extern bool IsWindowVisible(
		IntPtr hwnd
	);
	
	[DllImport(
		"user32.dll",
		CharSet = CharSet.Unicode
	)]
	private static extern int GetClassName(
		IntPtr hwnd,
		StringBuilder className,
		int maxCount
	);
	
	[DllImport(
		"user32.dll"
	)]
	private static extern IntPtr MonitorFromWindow(
		IntPtr window,
		uint flags
	);
	
	[DllImport(
		"user32.dll",
		CharSet = CharSet.Auto
	)]
	[return:
		MarshalAs(
			UnmanagedType.Bool
		)]
	private static extern bool GetMonitorInfo(
		IntPtr monitor,
		ref MonitorInfo info
	);
	
	[DllImport(
		"user32.dll"
	)]
	[return:
		MarshalAs(
			UnmanagedType.Bool
		)]
	private static extern bool MoveWindow(
		IntPtr window,
		int x,
		int y,
		int width,
		int height,
		bool repaint
	);
	
	[DllImport(
		"user32.dll"
	)]
	[return:
		MarshalAs(
			UnmanagedType.Bool
		)]
	private static extern bool ShowWindow(
		IntPtr window,
		int command
	);
}


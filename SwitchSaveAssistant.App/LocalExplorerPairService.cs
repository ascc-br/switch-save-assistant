using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace SwitchSaveAssistant.App;

internal sealed class LocalExplorerPairService
{
	private const uint MonitorDefaultToNearest =
		2;

	private const int SwRestore =
		9;

	public void OpenSideBySide(
		Window owner,
		string leftPath,
		string rightPath
	)
	{
		ArgumentNullException.ThrowIfNull(
			owner
		);

		if (!Directory.Exists(
				leftPath
			))
		{
			throw new DirectoryNotFoundException(
				$"Pasta não encontrada: {leftPath}"
			);
		}

		if (!Directory.Exists(
				rightPath
			))
		{
			throw new DirectoryNotFoundException(
				$"Pasta não encontrada: {rightPath}"
			);
		}

		var leftWindow =
			OpenLocalFolder(
				leftPath,
				"Pasta central"
			);

		var rightWindow =
			OpenLocalFolder(
				rightPath,
				"Pasta do Eden"
			);

		ArrangeWindows(
			owner,
			leftWindow,
			rightWindow
		);
	}

	private static IntPtr OpenLocalFolder(
		string path,
		string description
	)
	{
		var before =
			GetExplorerWindowHandles();

		Process.Start(
			new ProcessStartInfo
				{
					FileName =
						"explorer.exe",

					Arguments =
						$"/n,\"{path}\"",

					UseShellExecute =
						true
				}
		);

		return WaitForNewExplorerWindow(
			before,
			description
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
			TimeSpan.FromSeconds(
				8
			))
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
			$"{description} foi aberta, mas a nova janela do Explorer não pôde ser identificada."
		);
	}

	private static HashSet<IntPtr>
		GetExplorerWindowHandles()
	{
		var result =
			new HashSet<IntPtr>();

		EnumWindows(
			(
				hwnd,
				_
			) =>
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
					) ==
					0)
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

		const int gap =
			8;

		var leftWidth =
			(width - gap) /
			2;

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

	private delegate bool EnumWindowsProc(
		IntPtr hwnd,
		IntPtr parameter
	);

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

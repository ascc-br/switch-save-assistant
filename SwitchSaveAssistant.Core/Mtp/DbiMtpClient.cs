using MediaDevices;
using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Mtp;

public sealed class DbiMtpClient : IDisposable
{
    private MediaDevice? _device;

    public string DeviceDescription =>
        _device?.Description ?? string.Empty;

    public void Connect()
    {
        if (_device is not null)
        {
            return;
        }

        var devices = MediaDeviceManager.Instance
            .GetDevices()?
            .ToList()
            ?? new List<MediaDevice>();

        var switchDevice = devices.FirstOrDefault(device =>
            (device.Description ?? string.Empty)
                .Contains(
                    "Switch",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        if (switchDevice is null)
        {
            throw new InvalidOperationException(
                "Nintendo Switch não encontrado via MTP."
            );
        }

        switchDevice.Connect();

        _device = switchDevice;

        // Valida desde já se este dispositivo expõe o DBI.
        _ = GetSavesRoot();
    }
	
	public IReadOnlyList<RemoteGame> GetGames(
		string category
	)
	{
		var savesRoot = GetSavesRoot();

		var categoryDirectory = savesRoot
			.EnumerateDirectories()
			.FirstOrDefault(directory =>
				string.Equals(
					directory.Name,
					category,
					StringComparison.OrdinalIgnoreCase
				)
			);

		if (categoryDirectory is null)
		{
			throw new DirectoryNotFoundException(
				$"Categoria '{category}' não encontrada em 7: Saves."
			);
		}

		return categoryDirectory
			.EnumerateDirectories()
			.Select(directory =>
				new RemoteGame(
					category,
					directory.Name,
					directory.FullName
				)
			)
			.OrderBy(game => game.Name)
			.ToList();
	}

	public IReadOnlyList<RemoteSave> GetSaveEntries(
		RemoteGame game
	)
	{
		ArgumentNullException.ThrowIfNull(game);

		var device = GetConnectedDevice();

		var gameDirectory = device.GetDirectoryInfo(
			game.RemotePath
		);

		return gameDirectory
			.EnumerateDirectories()
			.Select(directory =>
				new RemoteSave(
					game.Category,
					game.Name,
					directory.Name,
					DetectSaveEntryType(directory.Name),
					directory.FullName
				)
			)
			.OrderBy(save => save.EntryName)
			.ToList();
	}

    public RemoteSave FindSave(
        string category,
        string gameName,
        string entryName
    )
    {
        var savesRoot = GetSavesRoot();

        var categoryDirectory = savesRoot
            .EnumerateDirectories()
            .FirstOrDefault(directory =>
                string.Equals(
                    directory.Name,
                    category,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (categoryDirectory is null)
        {
            throw new DirectoryNotFoundException(
                $"Categoria '{category}' não encontrada em 7: Saves."
            );
        }

        var gameDirectory = categoryDirectory
            .EnumerateDirectories()
            .FirstOrDefault(directory =>
                string.Equals(
                    directory.Name,
                    gameName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (gameDirectory is null)
        {
            throw new DirectoryNotFoundException(
                $"Jogo '{gameName}' não encontrado."
            );
        }

        var saveDirectory = gameDirectory
            .EnumerateDirectories()
            .FirstOrDefault(directory =>
                string.Equals(
                    directory.Name,
                    entryName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (saveDirectory is null)
        {
            throw new DirectoryNotFoundException(
                $"Save '{entryName}' não encontrado para '{gameName}'."
            );
        }

        return new RemoteSave(
			category,
			gameName,
			entryName,
			DetectSaveEntryType(entryName),
			saveDirectory.FullName
		);
    }

    public IReadOnlyList<RemoteFile> EnumerateFiles(
        string remoteDirectoryPath
    )
    {
        var device = GetConnectedDevice();

        var rootDirectory = device.GetDirectoryInfo(
            remoteDirectoryPath
        );

        var files = new List<RemoteFile>();

        EnumerateFilesRecursive(
            rootDirectory,
            remoteDirectoryPath,
            files
        );

        return files;
    }

    public void DownloadFile(
        string remotePath,
        string localPath
    )
    {
        var device = GetConnectedDevice();

        device.DownloadFile(
            remotePath,
            localPath
        );
    }
	
	public string GetDriveRootPath(
		string volumeLabel
	)
	{
		var device = GetConnectedDevice();

		var drive = device
			.GetDrives()
			.FirstOrDefault(d =>
				string.Equals(
					d.VolumeLabel,
					volumeLabel,
					StringComparison.OrdinalIgnoreCase
				)
			);

		if (drive?.RootDirectory is null)
		{
			throw new DirectoryNotFoundException(
				$"Armazenamento '{volumeLabel}' não encontrado."
			);
		}

		return drive.RootDirectory.FullName;
	}

	public bool DirectoryExists(
		string remotePath
	)
	{
		return GetConnectedDevice()
			.DirectoryExists(remotePath);
	}

	public bool FileExists(
		string remotePath
	)
	{
		return GetConnectedDevice()
			.FileExists(remotePath);
	}

	public void CreateDirectory(
		string remotePath
	)
	{
		GetConnectedDevice()
			.CreateDirectory(remotePath);
	}

	public void UploadNewFile(
		string localPath,
		string remotePath
	)
	{
		var device = GetConnectedDevice();

		if (device.FileExists(remotePath))
		{
			throw new IOException(
				$"Upload recusado: o arquivo remoto já existe: '{remotePath}'."
			);
		}

		device.UploadFile(
			localPath,
			remotePath
		);
	}

	public void DeleteFile(
		string remotePath
	)
	{
		GetConnectedDevice()
			.DeleteFile(remotePath);
	}

	public void DeleteDirectory(
		string remotePath,
		bool recursive = false
	)
	{
		GetConnectedDevice()
			.DeleteDirectory(
				remotePath,
				recursive
			);
	}

    private void EnumerateFilesRecursive(
        MediaDirectoryInfo directory,
        string rootPath,
        List<RemoteFile> result
    )
    {
        foreach (var file in directory.EnumerateFiles())
        {
            result.Add(
                new RemoteFile(
                    GetRelativeRemotePath(
                        rootPath,
                        file.FullName
                    ),
                    file.FullName,
                    file.Length
                )
            );
        }

        foreach (var subdirectory
                 in directory.EnumerateDirectories())
        {
            EnumerateFilesRecursive(
                subdirectory,
                rootPath,
                result
            );
        }
    }

    private MediaDirectoryInfo GetSavesRoot()
    {
        var device = GetConnectedDevice();

        var drive = device
            .GetDrives()
            .FirstOrDefault(d =>
                string.Equals(
                    d.VolumeLabel,
                    "7: Saves",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (drive?.RootDirectory is null)
        {
            throw new InvalidOperationException(
                "DBI conectado, mas o armazenamento '7: Saves' não foi encontrado."
            );
        }

        return drive.RootDirectory;
    }

    private MediaDevice GetConnectedDevice()
    {
        return _device
            ?? throw new InvalidOperationException(
                "Nenhum dispositivo MTP conectado."
            );
    }

    private static string GetRelativeRemotePath(
        string rootPath,
        string fullPath
    )
    {
        var root = rootPath.TrimEnd('\\', '/');

        if (fullPath.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return fullPath[root.Length..]
                .TrimStart('\\', '/');
        }

        return fullPath.TrimStart('\\', '/');
    }
	
	private static SaveEntryType DetectSaveEntryType(
		string name
	)
	{
		if (string.Equals(
				name,
				"Device",
				StringComparison.OrdinalIgnoreCase
			))
		{
			return SaveEntryType.Device;
		}

		if (string.Equals(
				name,
				"BCAT",
				StringComparison.OrdinalIgnoreCase
			))
		{
			return SaveEntryType.Bcat;
		}

		if (!string.IsNullOrWhiteSpace(name))
		{
			return SaveEntryType.User;
		}

		return SaveEntryType.Unknown;
	}

    public IReadOnlyList<string> GetSupportedCommandNames()
    {
        return GetConnectedDevice()
            .SupportedCommands()
            .Select(command => command.ToString())
            .OrderBy(
                name => name,
                StringComparer.Ordinal
            )
            .ToList();
    }


    internal void RawCreateFileAllowExistingForProbe(
        string localPath,
        string remotePath
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            localPath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            remotePath
        );

        if (!File.Exists(
                localPath
            ))
        {
            throw new FileNotFoundException(
                "Arquivo local não encontrado.",
                localPath
            );
        }

        var sdRoot =
            GetDriveRootPath(
                "1: SD Card"
            )
            .Replace(
                '/',
                '\\'
            )
            .TrimEnd(
                '\\'
            );

        var normalizedRemote =
            remotePath
                .Replace(
                    '/',
                    '\\'
                );

        var requiredPrefix =
            sdRoot + "\\";

        if (!normalizedRemote.StartsWith(
                requiredPrefix,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new InvalidOperationException(
                "RawCreateFileAllowExistingForProbe " +
                "é restrito exclusivamente a 1: SD Card."
            );
        }

        new WpdRawCreateWriter()
            .CreateEvenIfNameExists(
                GetConnectedDevice(),
                localPath,
                remotePath
            );
    }


    public void OverwriteExistingFile(
        string localPath,
        string remotePath
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            localPath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            remotePath
        );

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException(
                "Arquivo local não encontrado.",
                localPath
            );
        }

        var device =
            GetConnectedDevice();

        if (!device.FileExists(
                remotePath
            ))
        {
            throw new FileNotFoundException(
                $"Arquivo remoto não encontrado: '{remotePath}'.",
                remotePath
            );
        }

        new WpdExistingFileWriter()
            .Overwrite(
                device,
                localPath,
                remotePath
            );
    }


    public void Rename(
        string remotePath,
        string newName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            remotePath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            newName
        );

        if (newName.Contains('\\') ||
            newName.Contains('/'))
        {
            throw new ArgumentException(
                "O novo nome não pode conter separadores de caminho.",
                nameof(newName)
            );
        }

        GetConnectedDevice()
            .Rename(
                remotePath,
                newName
            );
    }


    public void Dispose()
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            _device.Disconnect();
        }
        finally
        {
            _device = null;
        }
    }
}

using System.Text.Json;

namespace SwitchSaveAssistant.Core.Settings;

public sealed record AssistantSettings
{
	public bool CheckForUpdatesOnStartup { get; init; } = true;

	public string? FolderPath
	{
		get;
		init;
	}

	public string? EdenRootPath
	{
		get;
		init;
	}

	public string SwitchFilter
	{
		get;
		init;
	} =
		"All";

	public string EdenFilter
	{
		get;
		init;
	} =
		"All";

	public bool EdenShowEmpty
	{
		get;
		init;
	}

	public string Language
	{
		get;
		init;
	} =
		"en-US";

	public string Theme
	{
		get;
		init;
	} =
		"System";

	public string DefaultEmulator
	{
		get;
		init;
	} =
		"Automatic";

	public static AssistantSettings Default =>
		new();
}

public sealed class AssistantSettingsService
{
	private static readonly JsonSerializerOptions
		JsonOptions =
			new()
				{
					WriteIndented =
						true
				};

	public string SettingsPath
	{
		get;
	}

	public AssistantSettingsService(
		string? settingsPath = null
	)
	{
		SettingsPath =
			settingsPath ??
			Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData
				),
				"SwitchSaveAssistant",
				"settings.json"
			);
	}

	public AssistantSettings Load()
	{
		try
		{
			if (!File.Exists(
				SettingsPath
			))
			{
				return AssistantSettings.Default;
			}

			var json =
				File.ReadAllText(
					SettingsPath
				);

			var loaded =
				JsonSerializer.Deserialize<AssistantSettings>(
					json,
					JsonOptions
				) ??
				AssistantSettings.Default;

			return Normalize(
				loaded
			);
		}
		catch
		{
			return AssistantSettings.Default;
		}
	}

	public void Save(
		AssistantSettings settings
	)
	{
		ArgumentNullException.ThrowIfNull(
			settings
		);

		var directory =
			Path.GetDirectoryName(
				SettingsPath
			);

		if (string.IsNullOrWhiteSpace(
			directory
		))
		{
			throw new InvalidOperationException(
				"Could not determine the settings directory."
			);
		}

		Directory.CreateDirectory(
			directory
		);

		var temporaryPath =
			SettingsPath +
			".tmp";

		var json =
			JsonSerializer.Serialize(
				Normalize(
					settings
				),
				JsonOptions
			);

		File.WriteAllText(
			temporaryPath,
			json
		);

		File.Move(
			temporaryPath,
			SettingsPath,
			overwrite: true
		);
	}

	public AssistantSettings Update(
		Func<AssistantSettings, AssistantSettings> update
	)
	{
		ArgumentNullException.ThrowIfNull(
			update
		);

		var current =
			Load();

		var updated =
			update(
				current
			);

		Save(
			updated
		);

		return Normalize(
			updated
		);
	}

	private static AssistantSettings Normalize(
		AssistantSettings settings
	)
	{
		var language =
			settings.Language switch
				{
					"es-ES" =>
						"es-ES",

					"pt-BR" =>
						"pt-BR",

					_ =>
						"en-US"
				};

		var theme =
			settings.Theme switch
				{
					"Light" =>
						"Light",

					"Dark" =>
						"Dark",

					_ =>
						"System"
				};

		var emulator =
			settings.DefaultEmulator switch
				{
					"Eden" =>
						"Eden",

					"Citron" =>
						"Citron",

					"Ryujinx" =>
						"Ryujinx",

					_ =>
						"Automatic"
				};

		return settings with
			{
				Language =
					language,

				Theme =
					theme,

				DefaultEmulator =
					emulator
			};
	}
}

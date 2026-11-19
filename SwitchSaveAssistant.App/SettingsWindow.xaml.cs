using Microsoft.Win32;
using System.IO;
using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Settings;
using System.Windows;
using System.Windows.Controls;

namespace SwitchSaveAssistant.App;

public partial class SettingsWindow : Window
{
	private readonly AssistantSettingsService
		_settingsService =
			new();

	private AssistantSettings
		_settings;

	public SettingsWindow()
	{
		InitializeComponent();

		_settings =
			_settingsService.Load();

		LoadControls();
	}

	private void LoadControls()
	{
		FolderPathTextBox.Text =
			_settings.FolderPath ??
			string.Empty;

		SelectByTag(
			LanguageComboBox,
			LocalizationService.Normalize(
				_settings.Language
			)
		);

		SelectByTag(
			ThemeComboBox,
			AppThemeService.Normalize(
				_settings.Theme
			)
		);

		SelectByTag(
			DefaultEmulatorComboBox,
			NormalizeEmulator(
				_settings.DefaultEmulator
			)
		);
	}

	private void SelectFolder_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var dialog =
			new OpenFolderDialog
				{
					Title =
						LocalizationService.Get(
							"Settings_SelectFolder"
						),

					Multiselect =
						false
				};

		if (!string.IsNullOrWhiteSpace(
				FolderPathTextBox.Text
			) &&
			Directory.Exists(
				FolderPathTextBox.Text
			))
		{
			dialog.InitialDirectory =
				FolderPathTextBox.Text;
		}

		if (dialog.ShowDialog(
				this
			) !=
			true)
		{
			return;
		}

		FolderPathTextBox.Text =
			dialog.FolderName;
	}

	private void Save_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var language =
			GetSelectedTag(
				LanguageComboBox,
				"en-US"
			);

		var theme =
			GetSelectedTag(
				ThemeComboBox,
				"System"
			);

		var emulator =
			GetSelectedTag(
				DefaultEmulatorComboBox,
				"Automatic"
			);

		_settings =
			_settings with
				{
					FolderPath =
						string.IsNullOrWhiteSpace(
							FolderPathTextBox.Text
						)
							? null
							: FolderPathTextBox.Text,

					Language =
						language,

					Theme =
						theme,

					DefaultEmulator =
						emulator
				};

		_settingsService.Save(
			_settings
		);

		LocalizationService.Apply(
			language
		);

		AppThemeService.Apply(
			theme
		);

		DialogResult =
			true;
	}

	private static string GetSelectedTag(
		ComboBox comboBox,
		string fallback
	)
	{
		return comboBox.SelectedItem
			is ComboBoxItem item &&
			item.Tag is string tag
				? tag
				: fallback;
	}

	private static void SelectByTag(
		ComboBox comboBox,
		string tag
	)
	{
		foreach (var candidate
			in comboBox.Items)
		{
			if (candidate
				is ComboBoxItem item &&
				string.Equals(
					item.Tag?.ToString(),
					tag,
					StringComparison.Ordinal
				))
			{
				comboBox.SelectedItem =
					item;

				return;
			}
		}

		comboBox.SelectedIndex =
			0;
	}

	private static string NormalizeEmulator(
		string? emulator
	)
	{
		return emulator switch
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
	}
}


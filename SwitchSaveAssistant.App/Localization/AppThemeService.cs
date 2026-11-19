using System.Windows;

namespace SwitchSaveAssistant.App.Localization;

public static class AppThemeService
{
	public static string Normalize(
		string? theme
	)
	{
		return theme switch
			{
				"Light" =>
					"Light",

				"Dark" =>
					"Dark",

				_ =>
					"System"
			};
	}

	public static void Apply(
		string? theme
	)
	{
		var normalized =
			Normalize(
				theme
			);

#pragma warning disable WPF0001
		Application.Current.ThemeMode =
			normalized switch
				{
					"Light" =>
						ThemeMode.Light,

					"Dark" =>
						ThemeMode.Dark,

					_ =>
						ThemeMode.System
				};
#pragma warning restore WPF0001
	}
}

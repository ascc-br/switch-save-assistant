using System.Globalization;
using System.Windows;

namespace SwitchSaveAssistant.App.Localization;

public static class LocalizationService
{
	private static ResourceDictionary?
		_currentDictionary;

	public static string CurrentLanguage
	{
		get;
		private set;
	} =
		"en-US";

	public static void Apply(
		string? language
	)
	{
		var normalized =
			Normalize(
				language
			);

		var dictionary =
			new ResourceDictionary
				{
					Source =
						new Uri(
							$"Resources/Strings.{normalized}.xaml",
							UriKind.Relative
						)
				};

		var resources =
			Application.Current.Resources;

		if (_currentDictionary is not null)
		{
			resources.MergedDictionaries.Remove(
				_currentDictionary
			);
		}

		resources.MergedDictionaries.Add(
			dictionary
		);

		_currentDictionary =
			dictionary;

		CurrentLanguage =
			normalized;

		var culture =
			CultureInfo.GetCultureInfo(
				normalized
			);

		CultureInfo.CurrentUICulture =
			culture;

		CultureInfo.DefaultThreadCurrentUICulture =
			culture;
	}

	public static string Get(
		string key
	)
	{
		return Application.Current.TryFindResource(
				key
			)
			?.ToString() ??
			key;
	}

	public static string Format(
		string key,
		params object[] args
	)
	{
		return string.Format(
			CultureInfo.CurrentUICulture,
			Get(
				key
			),
			args
		);
	}

	public static string TranslateRuntimeMessage(
		string? message
	)
	{
		if (string.IsNullOrWhiteSpace(
			message
		))
		{
			return message ??
				string.Empty;
		}

		return message switch
			{
				"Revalidando origem..." =>
					Get("Progress_RevalidatingSource"),

				"Criando backup de segurança do destino..." =>
					Get("Progress_CreatingSafetyBackup"),

				"Copiando para staging..." =>
					Get("Progress_CopyingToStaging"),

				"Validando staging por SHA-256..." =>
					Get("Progress_ValidatingStaging"),

				"Substituindo destino..." =>
					Get("Progress_ReplacingDestination"),

				"Verificando resultado final..." =>
					Get("Progress_VerifyingFinal"),

				"Criando rollback do estado atual..." =>
					Get("Progress_CreatingRollback"),

				"Copiando backup para staging..." =>
					Get("Progress_CopyingBackupToStaging"),

				"Validando restauração final..." =>
					Get("Progress_ValidatingRestoreFinal"),

				"Falha detectada; restaurando rollback..." =>
					Get("Progress_RestoringRollback"),

				_ =>
					message
			};
	}
	public static string Normalize(
		string? language
	)
	{
		return language switch
			{
				"es-ES" =>
					"es-ES",

				"pt-BR" =>
					"pt-BR",

				_ =>
					"en-US"
			};
	}
}


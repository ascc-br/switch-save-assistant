using SwitchSaveAssistant.App.Localization;
using Microsoft.Win32;
using SwitchSaveAssistant.Core.Emulators;
using SwitchSaveAssistant.Core.Backups;
using SwitchSaveAssistant.Core.Settings;
using SwitchSaveAssistant.Core.Profiles;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SwitchSaveAssistant.App;

public partial class EmulatorFolderWindow : Window
{
	private const string AccountSaveSlot =
		"0000000000000000";

	private const string DeviceProfileId =
		"00000000000000000000000000000000";

	private static readonly string[]
		FolderCategories =
		[
			"Installed games",
			"Uninstalled games"
		];

	private readonly EdenMetadataService
		_metadataService =
			new();

	private readonly EdenFolderComparisonService
		_comparisonService =
			new();

	private readonly EdenFolderSyncService
		_syncService =
			new();

	private readonly SafetyBackupCatalogService
		_backupCatalogService =
			new();

	private readonly EdenFolderRecencyService
		_recencyService =
			new();

	private readonly AssistantSettingsService
		_settingsService =
			new();

	private readonly PlayerProfileLinkService
		_profileLinkService =
			new();

	private bool
		_preferencesLoaded;

	private string? _folderPath;

	private string? _edenDisplayRoot;

	private string? _edenNandRoot;

	private string? _edenSaveRoot;

	private string[] _cacheDirectories =
		Array.Empty<string>();

	private IReadOnlyDictionary<string, string>
		_profileNames =
			new Dictionary<string, string>(
				StringComparer.OrdinalIgnoreCase
			);

	private IReadOnlyDictionary<string, EdenGameMetadata>
		_gameMetadata =
			new Dictionary<string, EdenGameMetadata>(
				StringComparer.OrdinalIgnoreCase
			);

	private List<EdenSaveEntry> _edenEntries =
		new();

	private List<FolderSaveEntry> _folderEntries =
		new();

	private Dictionary<string, SaveComparison>
		_comparisons =
			new(
				StringComparer.OrdinalIgnoreCase
			);

	public EmulatorFolderWindow()
	{
		InitializeComponent();

		Loaded +=
			Window_Loaded;
	}

	private async void SelectFolder_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var dialog =
			new OpenFolderDialog
			{
				Title =
					LocalizationService.Get("Common_SelectCentralFolderDialog"),

				Multiselect =
					false
			};

		if (dialog.ShowDialog(
			this
		) != true)
		{
			return;
		}

		_folderPath =
			dialog.FolderName;

		FolderPathTextBox.Text =
			_folderPath;

		SavePreferences();

		await RefreshComparisonsAsync();
	}

	private async void DetectEden_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		EdenActionButton.IsEnabled =
			false;

		EdenActionButton.Content =
			LocalizationService.Get("Common_Verifying");

		try
		{
			if (DetectDefaultEden(
				showFailure: true
			))
			{
				SavePreferences();

				await RefreshComparisonsAsync();
			}
		}
		finally
		{
			EdenActionButton.IsEnabled =
				true;

			EdenActionButton.Content =
				_edenSaveRoot is null
					? LocalizationService.Get("Common_Detect")
					: LocalizationService.Get("Common_VerifyAgain");
		}
	}

	private async void SelectEdenRoot_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var dialog =
			new OpenFolderDialog
			{
				Title =
					LocalizationService.Get("Emulator_SelectRootDialog"),

				Multiselect =
					false
			};

		if (dialog.ShowDialog(
			this
		) != true)
		{
			return;
		}

		try
		{
			var resolved =
				ResolveEdenSelection(
					dialog.FolderName
				);

			ApplyEdenRoot(
				resolved,
				LocalizationService.Get("Emulator_SourceManual")
			);

			SavePreferences();

			await RefreshComparisonsAsync();
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get("Emulator_NotFoundTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);
		}
	}

	private bool DetectDefaultEden(
		bool showFailure
	)
	{
		try
		{
			var standardRoot =
				GetStandardEdenRoot();

			var resolved =
				ResolveEdenSelection(
					standardRoot
				);

			ApplyEdenRoot(
				resolved,
				LocalizationService.Get("Emulator_SourceDefault")
			);

			return true;
		}
		catch (Exception ex)
		{
			_edenDisplayRoot =
				null;

			_edenNandRoot =
				null;

			_edenSaveRoot =
				null;

			_cacheDirectories =
				Array.Empty<string>();

			_profileNames =
				new Dictionary<string, string>();

			_gameMetadata =
				new Dictionary<string, EdenGameMetadata>();

			_edenEntries.Clear();
			_folderEntries.Clear();
			_comparisons.Clear();

			EdenPathTextBox.Text =
				LocalizationService.Get("Emulator_NotDetectedAutomatically");

			EdenStatusText.Text =
				LocalizationService.Get("Emulator_UseChooseRoot");

			EdenActionButton.Content =
				LocalizationService.Get("Common_Detect");

			RenderCatalog();

			if (showFailure)
			{
				MessageBox.Show(
					this,
					LocalizationService.Get("Emulator_DefaultNotFound1") + "\n\n" +
					LocalizationService.Get("Emulator_DefaultNotFound2") + " " +
					string.Empty + "\n\n" +
					ex.Message,
					LocalizationService.Get("Emulator_DefaultNotDetectedTitle"),
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
			}

			return false;
		}
	}

	private void ApplyEdenRoot(
		EdenRootResolution root,
		string source
	)
	{
		_edenDisplayRoot =
			root.DisplayRoot;

		_edenNandRoot =
			root.NandRoot;

		_edenSaveRoot =
			root.SaveRoot;

		_cacheDirectories =
			root.CacheDirectories;

		_profileNames =
			_metadataService.ReadProfileNames(
				root.NandRoot
			);

		_gameMetadata =
			_metadataService.ReadGameMetadata(
				new[]
					{
						root.DisplayRoot,
						GetStandardEdenRoot()
					},
				root.CacheDirectories
			);

		_edenEntries =
			ReadEdenCatalog(
				root.SaveRoot
			);

		_comparisons.Clear();

		EdenPathTextBox.Text =
			root.DisplayRoot;

		var actualUserProfiles =
			_edenEntries
				.Where(
					entry =>
						!entry.IsDevice
				)
				.Select(
					entry =>
						entry.ProfileId
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		var namedProfiles =
			actualUserProfiles.Count(
				id =>
					_profileNames.ContainsKey(
						id
					)
			);

		var distinctTitles =
			_edenEntries
				.Select(
					entry =>
						entry.TitleId
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		var namedGames =
			distinctTitles.Count(
				id =>
					_gameMetadata.ContainsKey(
						id
					)
			);

		EdenStatusText.Text =
			$"● {source} • " +
			LocalizationService.Format("Emulator_NamedProfiles", namedProfiles, actualUserProfiles.Length) +
			LocalizationService.Format("Emulator_NamedGames", namedGames, distinctTitles.Length);

		EdenActionButton.Content =
			LocalizationService.Get("Common_VerifyAgain");

		RenderCatalog();
	}

	private async void Window_Loaded(
		object sender,
		RoutedEventArgs e
	)
	{
		var settings =
			_settingsService.Load();

		if (!string.IsNullOrWhiteSpace(
				settings.FolderPath
			) &&
			Directory.Exists(
				settings.FolderPath
			))
		{
			_folderPath =
				settings.FolderPath;

			FolderPathTextBox.Text =
				_folderPath;
		}

		ShowEmptyCheckBox.IsChecked =
			settings.EdenShowEmpty;

		SelectFilterByTag(
			settings.EdenFilter
		);

		var edenLoaded =
			false;

		if (!string.IsNullOrWhiteSpace(
				settings.EdenRootPath
			) &&
			Directory.Exists(
				settings.EdenRootPath
			))
		{
			try
			{
				var resolved =
					ResolveEdenSelection(
						settings.EdenRootPath
					);

				ApplyEdenRoot(
					resolved,
					LocalizationService.Get("Emulator_SourceSaved")
				);

				edenLoaded =
					true;
			}
			catch
			{
				edenLoaded =
					false;
			}
		}

		if (!edenLoaded)
		{
			DetectDefaultEden(
				showFailure: false
			);
		}

		_preferencesLoaded =
			true;

		SavePreferences();

		await RefreshComparisonsAsync();
	}

	private void SavePreferences()
	{
		if (!_preferencesLoaded)
		{
			return;
		}

		var filter =
			(CatalogFilterComboBox.SelectedItem
				as ComboBoxItem)
			?.Tag
			?.ToString()
			?? "All";

		_settingsService.Update(
			current =>
				current with
				{
					FolderPath =
						_folderPath,

					EdenRootPath =
						_edenDisplayRoot,

					EdenFilter =
						filter,

					EdenShowEmpty =
						ShowEmptyCheckBox.IsChecked ==
							true
				}
		);
	}

	private void SelectFilterByTag(
		string? tag
	)
	{
		foreach (var item
			in CatalogFilterComboBox.Items
				.OfType<ComboBoxItem>())
		{
			if (string.Equals(
				item.Tag?.ToString(),
				tag,
				StringComparison.OrdinalIgnoreCase
			))
			{
				CatalogFilterComboBox.SelectedItem =
					item;

				return;
			}
		}

		CatalogFilterComboBox.SelectedIndex =
			0;
	}
	private void OpenSafetyBackups_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		new SafetyBackupsWindow
			{
				Owner =
					this
			}
			.ShowDialog();
	}
	private async Task RefreshComparisonsAsync()
	{
		_comparisons.Clear();

		if (string.IsNullOrWhiteSpace(
			_folderPath
		))
		{
			_folderEntries.Clear();
			RenderCatalog();
			return;
		}

		try
		{
			_folderEntries =
				ReadFolderCatalog();
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get("Common_CannotReadFolderTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			_folderEntries.Clear();
			RenderCatalog();
			return;
		}

		var shared =
			BuildUnifiedEntries()
				.Where(
					entry =>
						entry.EdenPath is not null &&
						entry.FolderPath is not null
				)
				.ToArray();

		if (shared.Length ==
			0)
		{
			RenderCatalog();
			return;
		}

		ComparisonProgressPanel.Visibility =
			Visibility.Visible;

		ComparisonProgressBar.Minimum =
			0;

		ComparisonProgressBar.Maximum =
			shared.Length;

		ComparisonProgressBar.Value =
			0;

		var progress =
			new Progress<ComparisonProgress>(
				update =>
					{
						ComparisonProgressBar.Value =
							update.Current;

						ComparisonProgressText.Text =
							$"{update.Current}/{update.Total} • {LocalizationService.TranslateRuntimeMessage(update.Label)}";
					}
			);

		var comparisons =
			await Task.Run(
				() =>
					CompareSharedEntries(
						shared,
						update =>
							((IProgress<ComparisonProgress>)progress)
								.Report(
									update
								)
					)
			);

		_comparisons =
			comparisons;

		ComparisonProgressPanel.Visibility =
			Visibility.Collapsed;

		RenderCatalog();
	}

	private Dictionary<string, SaveComparison>
		CompareSharedEntries(
			IReadOnlyList<UnifiedSaveEntry> shared,
			Action<ComparisonProgress>? progress
		)
	{
		var result =
			new Dictionary<string, SaveComparison>(
				StringComparer.OrdinalIgnoreCase
			);

		for (var index = 0;
			index < shared.Count;
			index++)
		{
			var entry =
				shared[index];

			progress?.Invoke(
				new ComparisonProgress(
					index + 1,
					shared.Count,
					$"{entry.GameName} • {entry.EntryName}"
				)
			);

			try
			{
				var comparison =
					_comparisonService.Compare(
						entry.EdenPath!,
						entry.FolderPath!
					);

				if (comparison.AreEqual)
				{
					result[entry.Key] =
						new SaveComparison(
							SaveState.Synchronized,
							$"{comparison.EdenFileCount} arquivo(s) com SHA-256 idêntico."
						);

					continue;
				}

				var suffix =
					comparison.ChangeCount >
					comparison.SampleChanges.Count
						? $" (+{comparison.ChangeCount - comparison.SampleChanges.Count})"
						: string.Empty;

				result[entry.Key] =
					new SaveComparison(
						SaveState.Divergent,
						$"{comparison.ChangeCount} diferença(s): " +
						string.Join(
							", ",
							comparison.SampleChanges
						) +
						suffix
					);
			}
			catch (Exception ex)
			{
				result[entry.Key] =
					new SaveComparison(
						SaveState.Error,
						ex.Message
					);
			}
		}

		return result;
	}

	private List<FolderSaveEntry> ReadFolderCatalog()
	{
		if (string.IsNullOrWhiteSpace(
			_folderPath
		))
		{
			return new List<FolderSaveEntry>();
		}

		var knownGames =
			_gameMetadata
				.Values
				.GroupBy(
					game =>
						NormalizeIdentity(
							game.Title
						),
					StringComparer.OrdinalIgnoreCase
				)
				.ToDictionary(
					group =>
						group.Key,
					group =>
						group.First(),
					StringComparer.OrdinalIgnoreCase
				);

		var knownProfiles =
			_profileNames
				.Values
				.Select(
					NormalizeIdentity
				)
				.ToHashSet(
					StringComparer.OrdinalIgnoreCase
				);

		var profileResolver =
			new PlayerProfileResolver(
				_profileLinkService.Load()
			);

		var result =
			new List<FolderSaveEntry>();

		foreach (var category
			in FolderCategories)
		{
			var categoryPath =
				Path.Combine(
					_folderPath,
					category
				);

			if (!Directory.Exists(
				categoryPath
			))
			{
				continue;
			}

			foreach (var gamePath
				in Directory.EnumerateDirectories(
					categoryPath
				))
			{
				var folderGameName =
					Path.GetFileName(
						gamePath
					);

				if (!knownGames.TryGetValue(
					NormalizeIdentity(
						folderGameName
					),
					out var game
				))
				{
					continue;
				}

				foreach (var entryPath
					in Directory.EnumerateDirectories(
						gamePath
					))
				{
					var entryName =
						Path.GetFileName(
							entryPath
						);

					var isDevice =
						string.Equals(
							entryName,
							"Device",
							StringComparison.OrdinalIgnoreCase
						);

					if (!isDevice &&
						!knownProfiles.Contains(
							NormalizeIdentity(
								entryName
							)
						) &&
						profileResolver
							.ResolveFolderAlias(
								entryName
							)
							.Kind !=
								ProfileAliasResolutionKind.Linked)
					{
						continue;
					}

					var files =
						EnumerateComparableFiles(
							entryPath
						)
						.ToArray();

					result.Add(
						new FolderSaveEntry(
							category,
							folderGameName,
							game.TitleId,
							entryName,
							entryPath,
							files.Length,
							files.Sum(
								GetLengthSafe
							)
						)
					);
				}
			}
		}

		return result
			.GroupBy(
				entry =>
					CreateKey(
						entry.GameName,
						entry.EntryName
					),
				StringComparer.OrdinalIgnoreCase
			)
			.Select(
				group =>
					group
						.OrderBy(
							entry =>
								entry.Category ==
									"Installed games"
									? 0
									: 1
						)
						.First()
			)
			.ToList();
	}

	private List<UnifiedSaveEntry> BuildUnifiedEntries()
	{
		var links =
			_profileLinkService.Load();

		var resolver =
			new PlayerProfileResolver(
				links
			);

		var edenResolved =
			_edenEntries
				.Select(
					entry =>
						new ResolvedEdenSaveEntry(
							entry,
							ResolveEdenProfileIdentity(
								entry,
								resolver
							)
						)
				)
				.ToList();

		var folderResolved =
			_folderEntries
				.Select(
					entry =>
						new ResolvedFolderSaveEntry(
							entry,
							ResolveFolderProfileIdentity(
								entry,
								resolver
							)
						)
				)
				.ToList();

		var edenByKey =
			edenResolved
				.GroupBy(
					entry =>
						CreateResolvedKey(
							entry.Entry.TitleId,
							entry.Identity.Key
						),
					StringComparer.OrdinalIgnoreCase
				)
				.ToDictionary(
					group =>
						group.Key,
					group =>
						group.First(),
					StringComparer.OrdinalIgnoreCase
				);

		var folderGroups =
			folderResolved
				.GroupBy(
					entry =>
						CreateResolvedKey(
							entry.Entry.TitleId,
							entry.Identity.Key
						),
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		var folderByKey =
			folderGroups
				.ToDictionary(
					group =>
						group.Key,
					group =>
						group.First(),
					StringComparer.OrdinalIgnoreCase
				);

		var folderConflicts =
			folderGroups
				.Where(
					group =>
						group
							.Select(
								entry =>
									entry.Entry.EntryName
							)
							.Distinct(
								StringComparer.OrdinalIgnoreCase
							)
							.Count() >
						1
				)
				.ToDictionary(
					group =>
						group.Key,
					group =>
						string.Join(
							", ",
							group
								.Select(
									entry =>
										entry.Entry.EntryName
								)
								.Distinct(
									StringComparer.OrdinalIgnoreCase
								)
								.OrderBy(
									name =>
										name,
									StringComparer.CurrentCultureIgnoreCase
								)
						),
					StringComparer.OrdinalIgnoreCase
				);

		var keys =
			edenByKey.Keys
				.Concat(
					folderByKey.Keys
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		var result =
			new List<UnifiedSaveEntry>();

		foreach (var key
			in keys)
		{
			edenByKey.TryGetValue(
				key,
				out var edenResolvedEntry
			);

			folderByKey.TryGetValue(
				key,
				out var folderResolvedEntry
			);

			var eden =
				edenResolvedEntry?.Entry;

			var folder =
				folderResolvedEntry?.Entry;

			var identity =
				edenResolvedEntry?.Identity ??
				folderResolvedEntry!.Identity;

			var gameName =
				eden?.GameName ??
				folder?.GameName ??
				eden?.TitleId ??
				string.Empty;

			var titleId =
				eden?.TitleId ??
				folder!.TitleId;

			var isDevice =
				eden?.IsDevice ==
					true ||
				string.Equals(
					folder?.EntryName,
					"Device",
					StringComparison.OrdinalIgnoreCase
				);

			var entryName =
				isDevice
					? "Device"
					: identity.LogicalPlayerName ??
						(
							eden is not null
								? GetFolderEntryName(
									eden
								)
								: folder!.EntryName
						);

			var profileDisplayName =
				isDevice
					? LocalizationService.Get(
						"Common_DeviceShared"
					)
					: identity.LogicalPlayerName ??
						eden?.ProfileDisplayName ??
						folder?.EntryName ??
						entryName;

			var edenProfileId =
				eden?.ProfileId ??
				FindEdenNativeId(
					links,
					identity.LogicalPlayerId
				);

			var edenProfilePath =
				eden is not null
					? Path.GetDirectoryName(
						eden.SavePath
					)
					: FindEdenProfilePathById(
						edenProfileId,
						requireExisting: true
					) ??
					FindEdenProfilePath(
						entryName,
						requireExisting: true
					);

			var hasAliasConflict =
				folderConflicts.TryGetValue(
					key,
					out var conflictAliases
				);

			var identityIssue =
				hasAliasConflict
					? LocalizationService.Format(
						"Switch_ProfileAliasConflictDetail",
						identity.LogicalPlayerName ??
							entryName,
						conflictAliases ??
							string.Empty
					)
					: null;

			var state =
				identityIssue is not null
					? SaveState.Error
					: GetState(
						key,
						eden is not null,
						folder is not null
					);

			result.Add(
				new UnifiedSaveEntry(
					key,
					gameName,
					titleId,
					entryName,
					profileDisplayName,
					identity.LogicalPlayerId,
					edenProfileId,
					folder?.EntryName,
					identityIssue,
					conflictAliases,
					eden?.IconPath,
					eden?.SavePath,
					edenProfilePath,
					folder?.SavePath,
					folder?.Category,
					eden?.FileCount ??
						0,
					folder?.FileCount ??
						0,
					eden?.Bytes ??
						0,
					folder?.Bytes ??
						0,
					state
				)
			);
		}

		return result
			.OrderBy(
				entry =>
					entry.EntryName.Equals(
						"Device",
						StringComparison.OrdinalIgnoreCase
					)
						? 0
						: 1
			)
			.ThenBy(
				entry =>
					entry.ProfileDisplayName,
				StringComparer.CurrentCultureIgnoreCase
			)
			.ThenBy(
				entry =>
					entry.GameName,
				StringComparer.CurrentCultureIgnoreCase
			)
			.ToList();
	}

	private static ResolvedProfileIdentity ResolveEdenProfileIdentity(
		EdenSaveEntry entry,
		PlayerProfileResolver resolver
	)
	{
		if (entry.IsDevice)
		{
			return new ResolvedProfileIdentity(
				"device",
				null,
				null
			);
		}

		var resolution =
			resolver.ResolveEdenProfileId(
				entry.ProfileId
			);

		if (
			resolution.Kind ==
				ProfileAliasResolutionKind.Linked &&
			!string.IsNullOrWhiteSpace(
				resolution.LogicalPlayerId
			)
		)
		{
			return new ResolvedProfileIdentity(
				"logical:" +
					resolution.LogicalPlayerId,
				resolution.LogicalPlayerId,
				resolution.LogicalPlayerName
			);
		}

		if (entry.IsOrphan)
		{
			return new ResolvedProfileIdentity(
				"eden-native:" +
					entry.ProfileId,
				null,
				null
			);
		}

		return new ResolvedProfileIdentity(
			"name:" +
				NormalizeIdentity(
					entry.ProfileDisplayName
				),
			null,
			null
		);
	}

	private static ResolvedProfileIdentity ResolveFolderProfileIdentity(
		FolderSaveEntry entry,
		PlayerProfileResolver resolver
	)
	{
		if (string.Equals(
			entry.EntryName,
			"Device",
			StringComparison.OrdinalIgnoreCase
		))
		{
			return new ResolvedProfileIdentity(
				"device",
				null,
				null
			);
		}

		var resolution =
			resolver.ResolveFolderAlias(
				entry.EntryName
			);

		if (
			resolution.Kind ==
				ProfileAliasResolutionKind.Linked &&
			!string.IsNullOrWhiteSpace(
				resolution.LogicalPlayerId
			)
		)
		{
			return new ResolvedProfileIdentity(
				"logical:" +
					resolution.LogicalPlayerId,
				resolution.LogicalPlayerId,
				resolution.LogicalPlayerName
			);
		}

		return new ResolvedProfileIdentity(
			"name:" +
				NormalizeIdentity(
					entry.EntryName
				),
			null,
			null
		);
	}

	private static string CreateResolvedKey(
		string titleId,
		string profileIdentityKey
	)
	{
		return NormalizeIdentity(
				titleId
			) +
			"|" +
			profileIdentityKey;
	}

	private static string? FindEdenNativeId(
		PlayerProfileLinksDocument document,
		string? logicalPlayerId
	)
	{
		if (string.IsNullOrWhiteSpace(
			logicalPlayerId
		))
		{
			return null;
		}

		return document.Profiles
			.FirstOrDefault(
				profile =>
					string.Equals(
						profile.Id,
						logicalPlayerId,
						StringComparison.OrdinalIgnoreCase
					)
			)
			?.Bindings
			.FirstOrDefault(
				binding =>
					string.Equals(
						binding.Platform,
						"Eden",
						StringComparison.OrdinalIgnoreCase
					) &&
					!string.IsNullOrWhiteSpace(
						binding.NativeId
					)
			)
			?.NativeId;
	}

	private SaveState GetState(
		string key,
		bool hasEden,
		bool hasFolder
	)
	{
		if (hasEden &&
			!hasFolder)
		{
			return SaveState.EdenOnly;
		}

		if (!hasEden &&
			hasFolder)
		{
			return SaveState.FolderOnly;
		}

		if (_comparisons.TryGetValue(
			key,
			out var comparison
		))
		{
			return comparison.State;
		}

		return SaveState.NotCompared;
	}

	private string? FindEdenProfilePath(
		string entryName
	)
	{
		if (string.IsNullOrWhiteSpace(
				_edenSaveRoot
			))
		{
			return null;
		}

		if (string.Equals(
			entryName,
			"Device",
			StringComparison.OrdinalIgnoreCase
		))
		{
			var devicePath =
				Path.Combine(
					_edenSaveRoot,
					DeviceProfileId
				);

			return Directory.Exists(
				devicePath
			)
				? devicePath
				: null;
		}

		var profile =
			_profileNames.FirstOrDefault(
				pair =>
					string.Equals(
						NormalizeIdentity(
							pair.Value
						),
						NormalizeIdentity(
							entryName
						),
						StringComparison.OrdinalIgnoreCase
					)
			);

		if (string.IsNullOrWhiteSpace(
			profile.Key
		))
		{
			return null;
		}

		var profilePath =
			Path.Combine(
				_edenSaveRoot,
				profile.Key
			);

		return Directory.Exists(
			profilePath
		)
			? profilePath
			: null;
	}

	private void RenderCatalog()
	{
		EdenGamesPanel.Children.Clear();

		var unified =
			BuildUnifiedEntries();

		var visible =
			ApplyFilter(
				unified
			);

		if (ShowEmptyCheckBox.IsChecked !=
			true)
		{
			visible =
				visible
					.Where(
						entry =>
							entry.EdenFileCount >
								0 ||
							entry.FolderFileCount >
								0
					)
					.ToList();
		}

		var profileCount =
			_edenEntries
				.Select(
					entry =>
						entry.ProfileId
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.Count();

		var gameCount =
			_edenEntries
				.Select(
					entry =>
						entry.TitleId
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.Count();

		var synchronizedCount =
			unified.Count(
				entry =>
					entry.State ==
					SaveState.Synchronized
			);

		var divergentCount =
			unified.Count(
				entry =>
					entry.State ==
					SaveState.Divergent
			);

		var edenOnlyCount =
			unified.Count(
				entry =>
					entry.State ==
					SaveState.EdenOnly
			);

		var folderOnlyCount =
			unified.Count(
				entry =>
					entry.State ==
					SaveState.FolderOnly
			);

		var errorCount =
			unified.Count(
				entry =>
					entry.State ==
					SaveState.Error
			);

		EdenSummaryText.Text =
			LocalizationService.Format("Emulator_SummaryProfiles", profileCount) +
			LocalizationService.Format("Emulator_SummaryGames", gameCount) +
			LocalizationService.Format("Emulator_SummarySync", synchronizedCount) +
			LocalizationService.Format("Emulator_SummaryDivergent", divergentCount) +
			LocalizationService.Format("Emulator_SummaryEdenOnly", edenOnlyCount) +
			LocalizationService.Format("Emulator_SummaryFolderOnly", folderOnlyCount) +
			(
				errorCount >
				0
					? LocalizationService.Format("Emulator_SummaryErrors", errorCount)
					: string.Empty
			);

		if (_edenSaveRoot is null)
		{
			AddMutedMessage(
				LocalizationService.Get("Emulator_DetectOrSelectRoot")
			);

			return;
		}

		if (visible.Count ==
			0)
		{
			AddMutedMessage(
				LocalizationService.Get("Common_NoSavesForFilter")
			);

			return;
		}

		foreach (var profileGroup
			in visible.GroupBy(
				entry =>
					entry.EntryName
			))
		{
			var first =
				profileGroup.First();

			var header =
				CreateProfileHeader(
					first
				);

			var content =
				new StackPanel();

			foreach (var entry
				in profileGroup)
			{
				content.Children.Add(
					CreateGameRow(
						entry
					)
				);
			}

			EdenGamesPanel.Children.Add(
				new Expander
					{
						Header =
							header,

						Content =
							content,

						IsExpanded =
							true,

						Margin =
							new Thickness(
								0,
								4,
								0,
								4
							)
					}
			);
		}
	}

	private List<UnifiedSaveEntry> ApplyFilter(
		List<UnifiedSaveEntry> entries
	)
	{
		var selected =
			(CatalogFilterComboBox.SelectedItem
				as ComboBoxItem)
			?.Tag
			?.ToString()
			?? "All";

		return selected switch
		{
			"EdenOnly" =>
				entries
					.Where(
						entry =>
							entry.State ==
							SaveState.EdenOnly
					)
					.ToList(),

			"FolderOnly" =>
				entries
					.Where(
						entry =>
							entry.State ==
							SaveState.FolderOnly
					)
					.ToList(),

			"Synchronized" =>
				entries
					.Where(
						entry =>
							entry.State ==
							SaveState.Synchronized
					)
					.ToList(),

			"Divergent" =>
				entries
					.Where(
						entry =>
							entry.State ==
							SaveState.Divergent
					)
					.ToList(),

			"Error" =>
				entries
					.Where(
						entry =>
							entry.State ==
							SaveState.Error
					)
					.ToList(),

			_ =>
				entries
		};
	}

	private void CatalogFilter_SelectionChanged(
		object sender,
		SelectionChangedEventArgs e
	)
	{
		if (!IsInitialized)
		{
			return;
		}

		RenderCatalog();

		SavePreferences();
	}

	private void ShowEmpty_Changed(
		object sender,
		RoutedEventArgs e
	)
	{
		if (!IsInitialized)
		{
			return;
		}

		RenderCatalog();

		SavePreferences();
	}

	private FrameworkElement CreateProfileHeader(
		UnifiedSaveEntry entry
	)
	{
		var panel =
			new StackPanel
				{
					Orientation =
						Orientation.Horizontal
				};

		panel.Children.Add(
			new TextBlock
				{
					Text =
						entry.EntryName.Equals(
							"Device",
							StringComparison.OrdinalIgnoreCase
						)
							? "●"
							: "👤",

					Foreground =
						FindBrush(
							"SystemFillColorSuccessBrush",
							Brushes.Green
						),

					FontSize =
						14,

					VerticalAlignment =
						VerticalAlignment.Center
				}
		);

		panel.Children.Add(
			new TextBlock
				{
					Text =
						entry.EntryName.Equals(
							"Device",
							StringComparison.OrdinalIgnoreCase
						)
							? LocalizationService.Get("Common_DeviceShared")
							: entry.ProfileDisplayName,

					Margin =
						new Thickness(
							8,
							0,
							0,
							0
						),

					FontSize =
						15,

					FontWeight =
						FontWeights.SemiBold,

					VerticalAlignment =
						VerticalAlignment.Center
				}
		);

		return panel;
	}

	private FrameworkElement CreateGameRow(
		UnifiedSaveEntry entry
	)
	{
		var row =
			new Border
				{
					Margin =
						new Thickness(
							12,
							3,
							2,
							3
						),

					Padding =
						new Thickness(
							10,
							8,
							10,
							8
						),

					Background =
						FindBrush(
							"SubtleFillColorSecondaryBrush",
							Brushes.Transparent
						),

					CornerRadius =
						new CornerRadius(
							10
						)
				};

		var grid =
			new Grid();

		grid.ColumnDefinitions.Add(
			new ColumnDefinition
				{
					Width =
						new GridLength(
							44
						)
				}
		);

		grid.ColumnDefinitions.Add(
			new ColumnDefinition
				{
					Width =
						new GridLength(
							1,
							GridUnitType.Star
						)
				}
		);

		grid.ColumnDefinitions.Add(
			new ColumnDefinition
				{
					Width =
						new GridLength(
							165
						)
				}
		);

		grid.ColumnDefinitions.Add(
			new ColumnDefinition
				{
					Width =
						new GridLength(
							150
						)
				}
		);

		var icon =
			CreateGameIcon(
				entry
			);

		Grid.SetColumn(
			icon,
			0
		);

		grid.Children.Add(
			icon
		);

		var titlePanel =
			new StackPanel
				{
					Margin =
						new Thickness(
							8,
							0,
							8,
							0
						)
				};

		titlePanel.Children.Add(
			new TextBlock
				{
					Text =
						entry.GameName,

					FontWeight =
						FontWeights.SemiBold,

					TextTrimming =
						TextTrimming.CharacterEllipsis
				}
		);

		titlePanel.Children.Add(
			new TextBlock
				{
					Text =
						entry.TitleId,

					FontSize =
						11,

					Foreground =
						FindBrush(
							"TextFillColorSecondaryBrush",
							Brushes.Gray
						)
				}
		);

		titlePanel.Children.Add(
			CreateRecencyLine(
				entry
			)
		);

		Grid.SetColumn(
			titlePanel,
			1
		);

		grid.Children.Add(
			titlePanel
		);

		var statusText =
			new TextBlock
				{
					Text =
						GetStateText(
							entry.State
						),

					Foreground =
						GetStateForeground(
							entry.State
						),

					FontWeight =
						FontWeights.SemiBold,

					VerticalAlignment =
						VerticalAlignment.Center
				};

		var statusBadge =
			new Border
				{
					Background =
						GetStateBackground(
							entry.State
						),

					CornerRadius =
						new CornerRadius(
							999
						),

					Padding =
						new Thickness(
							10,
							4,
							10,
							4
						),

					HorizontalAlignment =
						HorizontalAlignment.Right,

					VerticalAlignment =
						VerticalAlignment.Center,

					ToolTip =
						GetStateDetail(
							entry
						),

					Child =
						statusText
				};

		Grid.SetColumn(
			statusBadge,
			2
		);

		grid.Children.Add(
			statusBadge
		);

		var hasAliasConflict =
			entry.State ==
				SaveState.Error &&
			!string.IsNullOrWhiteSpace(
				entry.ConflictAliases
			) &&
			!string.IsNullOrWhiteSpace(
				entry.LogicalPlayerId
			);

		var actionButton =
			new Button
				{
					Content =
						hasAliasConflict
							? LocalizationService.Get(
								"AliasRepair_Open"
							)
							: GetActionButtonText(
								entry.State
							),

					Tag =
						entry,

					Width =
						135,

					Height =
						30,

					Margin =
						new Thickness(
							10,
							0,
							0,
							0
						),

					IsEnabled =
						hasAliasConflict ||
						CanSynchronize(
							entry
						)
				};

		if (hasAliasConflict)
		{
			actionButton.Click +=
				RepairAliasConflict_Click;
		}
		else
		{
			actionButton.Click +=
				SyncEntry_Click;
		}

		Grid.SetColumn(
			actionButton,
			3
		);

		grid.Children.Add(
			actionButton
		);

		row.Child =
			grid;

		return row;
	}

	private FrameworkElement CreateGameIcon(
		UnifiedSaveEntry entry
	)
	{
		var border =
			new Border
				{
					Width =
						38,

					Height =
						38,

					CornerRadius =
						new CornerRadius(
							9
						),

					Background =
						FindBrush(
							"SystemFillColorAttentionBackgroundBrush",
							Brushes.Transparent
						),

					VerticalAlignment =
						VerticalAlignment.Center
				};

		if (!string.IsNullOrWhiteSpace(
				entry.IconPath
			) &&
			File.Exists(
				entry.IconPath
			))
		{
			try
			{
				var image =
					new BitmapImage();

				image.BeginInit();

				image.CacheOption =
					BitmapCacheOption.OnLoad;

				image.UriSource =
					new Uri(
						entry.IconPath,
						UriKind.Absolute
					);

				image.EndInit();
				image.Freeze();

				border.Child =
					new Image
						{
							Source =
								image,

							Stretch =
								Stretch.UniformToFill
						};

				return border;
			}
			catch
			{
			}
		}

		border.Child =
			new TextBlock
				{
					Text =
						"🎮",

					FontSize =
						19,

					HorizontalAlignment =
						HorizontalAlignment.Center,

					VerticalAlignment =
						VerticalAlignment.Center
				};

		return border;
	}
	private async void RepairAliasConflict_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (
			sender is not Button button ||
			button.Tag is not UnifiedSaveEntry entry ||
			string.IsNullOrWhiteSpace(
				entry.ConflictAliases
			) ||
			string.IsNullOrWhiteSpace(
				entry.LogicalPlayerId
			) ||
			string.IsNullOrWhiteSpace(
				entry.FolderCategory
			) ||
			string.IsNullOrWhiteSpace(
				_folderPath
			)
		)
		{
			MessageBox.Show(
				this,
				LocalizationService.Get(
					"AliasRepair_InvalidConflict"
				),
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		var aliases =
			entry.ConflictAliases
				.Split(
					',',
					StringSplitOptions.TrimEntries |
					StringSplitOptions.RemoveEmptyEntries
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		if (aliases.Length <
			2)
		{
			MessageBox.Show(
				this,
				LocalizationService.Get(
					"AliasRepair_InvalidConflict"
				),
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		var window =
			new ProfileAliasRepairWindow(
				_folderPath,
				entry.FolderCategory,
				entry.GameName,
				entry.EntryName,
				aliases
			)
				{
					Owner =
						this
				};

		if (window.ShowDialog() !=
			true)
		{
			return;
		}

		await RefreshComparisonsAsync();
	}

	private async void SyncEntry_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (sender is not Button button ||
			button.Tag is not UnifiedSaveEntry entry)
		{
			return;
		}

		if (IsEdenRunning())
		{
			MessageBox.Show(
				this,
				LocalizationService.Get("Emulator_CloseBeforeSync1") + "\n\n" +
				LocalizationService.Get("Emulator_CloseBeforeSync2"),
				LocalizationService.Get("Emulator_EdenOpenTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		var edenPath =
			entry.EdenPath ??
			FindEdenDestinationPath(
				entry
			);

		var folderPath =
			entry.FolderPath ??
			FindFolderDestinationPath(
				entry
			);

		if (edenPath is null ||
			folderPath is null)
		{
			MessageBox.Show(
				this,
				LocalizationService.Get("Emulator_SyncPathsUnavailable"),
				LocalizationService.Get("Emulator_SyncUnavailableTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		EdenFolderSyncDirection? initialDirection =
			entry.State switch
			{
				SaveState.EdenOnly =>
					EdenFolderSyncDirection.EdenToFolder,

				SaveState.FolderOnly =>
					EdenFolderSyncDirection.FolderToEden,

				_ =>
					null
			};

		var preview =
			new EdenFolderSyncPreviewWindow(
				entry.GameName,
				entry.ProfileDisplayName,
				edenPath,
				folderPath,
				initialDirection
			)
			{
				Owner =
					this
			};

		if (preview.ShowDialog() !=
				true ||
			preview.SelectedPlan is null)
		{
			return;
		}

		if (IsEdenRunning())
		{
			MessageBox.Show(
				this,
				LocalizationService.Get("Emulator_EdenOpenedDuringReview"),
				LocalizationService.Get("Emulator_SyncCanceledTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		button.IsEnabled =
			false;

		var backupRoot =
			Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData
				),
				"SwitchSaveAssistant",
				"SafetyBackups",
				"EdenFolder"
			);

		ComparisonProgressPanel.Visibility =
			Visibility.Visible;

		ComparisonProgressBar.IsIndeterminate =
			true;

		ComparisonProgressText.Text =
			LocalizationService.Get("Emulator_PreparingSync");

		IProgress<string> progress =
			new Progress<string>(
				message =>
					ComparisonProgressText.Text =
					LocalizationService.TranslateRuntimeMessage(message)
			);

		try
		{
			var result =
				await Task.Run(
					() =>
						_syncService.Execute(
							preview.SelectedPlan,
							backupRoot,
							message =>
								progress.Report(
									message
								)
						)
				);

			_backupCatalogService.Register(
				preview.SelectedPlan,
				result
			);

			var message =
				LocalizationService.Get("Emulator_SyncSuccess") + "\n\n" +
				LocalizationService.Format("Emulator_DestinationBlock", result.DestinationPath) + "\n\n" +
				LocalizationService.Format("Emulator_VerifiedFiles", result.FileCount);

			if (!string.IsNullOrWhiteSpace(
				result.BackupPath
			))
			{
				message +=
					"\n\n" + LocalizationService.Format("Emulator_SafetyBackupBlock", result.BackupPath);
			}

			MessageBox.Show(
				this,
				message,
				LocalizationService.Get("Emulator_SyncCompletedTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);

			await RefreshComparisonsAsync();
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				LocalizationService.Get("Emulator_SyncFailedPrefix") + "\n\n" +
				ex.Message,
				LocalizationService.Get("Emulator_SyncInterruptedTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Error
			);
		}
		finally
		{
			ComparisonProgressBar.IsIndeterminate =
				false;

			ComparisonProgressPanel.Visibility =
				Visibility.Collapsed;

			RenderCatalog();
		}
	}

	private bool CanSynchronize(
		UnifiedSaveEntry entry
	)
	{
		return entry.State is
			SaveState.EdenOnly or
			SaveState.FolderOnly or
			SaveState.Divergent;
	}

	private static string GetActionButtonText(
		SaveState state
	)
	{
		return state switch
		{
			SaveState.EdenOnly =>
				LocalizationService.Get("Emulator_ActionCopyFolder"),

			SaveState.FolderOnly =>
				LocalizationService.Get("Emulator_ActionCopyEden"),

			SaveState.Divergent =>
				LocalizationService.Get("Emulator_ActionResolve"),

			SaveState.Synchronized =>
				LocalizationService.Get("Common_SynchronizedSingle"),

			SaveState.Error =>
				LocalizationService.Get("Common_Error"),

			_ =>
				LocalizationService.Get("Common_Waiting")
		};
	}

	private string? FindFolderDestinationPath(
		UnifiedSaveEntry entry
	)
	{
		if (string.IsNullOrWhiteSpace(
			_folderPath
		))
		{
			return null;
		}

		foreach (var category
			in FolderCategories)
		{
			var gamePath =
				Path.Combine(
					_folderPath,
					category,
					entry.GameName
				);

			if (Directory.Exists(
				gamePath
			))
			{
				return Path.Combine(
					gamePath,
					entry.EntryName
				);
			}
		}

		return Path.Combine(
			_folderPath,
			"Installed games",
			entry.GameName,
			entry.EntryName
		);
	}

	private string? FindEdenDestinationPath(
		UnifiedSaveEntry entry
	)
	{
		var profilePath =
			entry.EdenProfilePath ??
			FindEdenProfilePathById(
				entry.EdenProfileId,
				requireExisting: false
			) ??
			FindEdenProfilePath(
				entry.EntryName,
				requireExisting: false
			);

		if (profilePath is null)
		{
			return null;
		}

		return Path.Combine(
			profilePath,
			entry.TitleId
		);
	}

	private string? FindEdenProfilePathById(
		string? profileId,
		bool requireExisting
	)
	{
		var saveRoot =
			_edenSaveRoot;

		if (
			string.IsNullOrWhiteSpace(
				saveRoot
			) ||
			string.IsNullOrWhiteSpace(
				profileId
			)
		)
		{
			return null;
		}

		var path =
			Path.Combine(
				saveRoot,
				profileId
			);

		return !requireExisting ||
			Directory.Exists(
				path
			)
				? path
				: null;
	}

	private string? FindEdenProfilePath(
		string entryName,
		bool requireExisting
	)
	{
		if (string.IsNullOrWhiteSpace(
				_edenSaveRoot
			))
		{
			return null;
		}

		string? path;

		if (string.Equals(
			entryName,
			"Device",
			StringComparison.OrdinalIgnoreCase
		))
		{
			path =
				Path.Combine(
					_edenSaveRoot,
					DeviceProfileId
				);
		}
		else
		{
			var profile =
				_profileNames.FirstOrDefault(
					pair =>
						string.Equals(
							NormalizeIdentity(
								pair.Value
							),
							NormalizeIdentity(
								entryName
							),
							StringComparison.OrdinalIgnoreCase
						)
				);

			if (string.IsNullOrWhiteSpace(
				profile.Key
			))
			{
				return null;
			}

			path =
				Path.Combine(
					_edenSaveRoot,
					profile.Key
				);
		}

		return !requireExisting ||
			Directory.Exists(
				path
			)
				? path
				: null;
	}

	private static bool IsEdenRunning()
	{
		try
		{
			return Process
				.GetProcesses()
				.Any(
					process =>
						string.Equals(
							process.ProcessName,
							"eden",
							StringComparison.OrdinalIgnoreCase
						)
				);
		}
		catch
		{
			return false;
		}
	}


	private string GetStateDetail(
		UnifiedSaveEntry entry
	)
	{
		if (!string.IsNullOrWhiteSpace(
			entry.IdentityIssue
		))
		{
			return entry.IdentityIssue;
		}

		if (_comparisons.TryGetValue(
			entry.Key,
			out var comparison
		))
		{
			return comparison.Detail;
		}

		return entry.State switch
		{
			SaveState.EdenOnly =>
				LocalizationService.Get("Emulator_DetailOnlyEden"),

			SaveState.FolderOnly =>
				LocalizationService.Get("Emulator_DetailOnlyFolder"),

			SaveState.NotCompared =>
				LocalizationService.Get("Emulator_DetailNotCompared"),

			_ =>
				GetStateText(
					entry.State
				)
		};
	}

	private static string GetStateText(
		SaveState state
	)
	{
		return state switch
		{
			SaveState.Synchronized =>
				LocalizationService.Get("Emulator_StateSynchronized"),

			SaveState.Divergent =>
				LocalizationService.Get("Emulator_StateDivergent"),

			SaveState.EdenOnly =>
				LocalizationService.Get("Emulator_StateOnlyEden"),

			SaveState.FolderOnly =>
				LocalizationService.Get("Emulator_StateOnlyFolder"),

			SaveState.Error =>
				LocalizationService.Get("Emulator_StateError"),

			_ =>
				LocalizationService.Get("Emulator_StateNotCompared")
		};
	}

	private static Brush GetStateBackground(
		SaveState state
	)
	{
		return state switch
		{
			SaveState.Synchronized =>
				FindBrush(
					"SystemFillColorSuccessBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							35,
							22,
							163,
							74
						)
					)
				),

			SaveState.Divergent =>
				FindBrush(
					"SystemFillColorCautionBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							42,
							217,
							119,
							6
						)
					)
				),

			SaveState.EdenOnly =>
				FindBrush(
					"SystemFillColorAttentionBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							35,
							37,
							99,
							235
						)
					)
				),

			SaveState.FolderOnly =>
				FindBrush(
					"SystemFillColorNeutralBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							35,
							107,
							114,
							128
						)
					)
				),

			SaveState.Error =>
				FindBrush(
					"SystemFillColorCriticalBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							42,
							220,
							38,
							38
						)
					)
				),

			_ =>
				FindBrush(
					"SystemFillColorNeutralBackgroundBrush",
					Brushes.Transparent
				)
		};
	}

	private static Brush GetStateForeground(
		SaveState state
	)
	{
		return state switch
		{
			SaveState.Synchronized =>
				FindBrush(
					"SystemFillColorSuccessBrush",
					Brushes.Green
				),

			SaveState.Divergent =>
				FindBrush(
					"SystemFillColorCautionBrush",
					Brushes.DarkOrange
				),

			SaveState.EdenOnly =>
				FindBrush(
					"AccentTextFillColorPrimaryBrush",
					Brushes.DodgerBlue
				),

			SaveState.Error =>
				FindBrush(
					"SystemFillColorCriticalBrush",
					Brushes.Red
				),

			_ =>
				FindBrush(
					"TextFillColorSecondaryBrush",
					Brushes.Gray
				)
		};
	}

	private FrameworkElement CreateRecencyLine(
		UnifiedSaveEntry entry
	)
	{
		var info =
			_recencyService.Compare(
				entry.EdenPath,
				entry.FolderPath
			);

		var panel =
			new StackPanel
				{
					Orientation =
						Orientation.Horizontal,

					Margin =
						new Thickness(
							0,
							3,
							0,
							0
						)
				};

		if (info.EdenLatestUtc is not null)
		{
			panel.Children.Add(
				CreateDateLabel(
					"Eden",
					info.EdenLatestUtc.Value,
					info.State ==
						EdenFolderRecencyState.EdenNewer
				)
			);
		}

		if (info.FolderLatestUtc is not null)
		{
			if (panel.Children.Count > 0)
			{
				panel.Children.Add(
					new TextBlock
						{
							Text = "  •  ",
							FontSize = 11,
							Foreground =
								FindBrush(
									"TextFillColorSecondaryBrush",
									Brushes.Gray
								)
						}
				);
			}

			panel.Children.Add(
				CreateDateLabel(
					"Folder",
					info.FolderLatestUtc.Value,
					info.State ==
						EdenFolderRecencyState.FolderNewer
				)
			);
		}

		if (panel.Children.Count == 0)
		{
			panel.Children.Add(
				new TextBlock
					{
						Text = LocalizationService.Get("Common_NoDate"),
						FontSize = 11,
						Foreground =
							FindBrush(
								"TextFillColorSecondaryBrush",
								Brushes.Gray
							)
					}
			);
		}

		return panel;
	}

	private FrameworkElement CreateDateLabel(
		string side,
		DateTime utc,
		bool isNewest
	)
	{
		var local = utc.ToLocalTime();

		var panel =
			new StackPanel
				{
					Orientation =
						Orientation.Horizontal
				};

		panel.Children.Add(
			new TextBlock
				{
					Text =
						$"{side}: {local:dd/MM/yyyy HH:mm}",

					FontSize = 11,

					Foreground =
						isNewest
							? FindBrush(
								"SystemFillColorSuccessBrush",
								Brushes.Green
							)
							: FindBrush(
								"TextFillColorSecondaryBrush",
								Brushes.Gray
							),

					FontWeight =
						isNewest
							? FontWeights.SemiBold
							: FontWeights.Normal,

					ToolTip =
						$"{side}: {local:dd/MM/yyyy HH:mm:ss}"
				}
		);

		if (isNewest)
		{
			panel.Children.Add(
				new Border
					{
						Margin =
							new Thickness(
								6,
								0,
								0,
								0
							),

						Padding =
							new Thickness(
								6,
								1,
								6,
								1
							),

						Background =
							FindBrush(
								"SystemFillColorSuccessBackgroundBrush",
								Brushes.Transparent
							),

						CornerRadius =
							new CornerRadius(
								999
							),

						Child =
							new TextBlock
								{
									Text = LocalizationService.Get("Common_MostRecent"),
									FontSize = 10,
									FontWeight =
										FontWeights.SemiBold,
									Foreground =
										FindBrush(
											"SystemFillColorSuccessBrush",
											Brushes.Green
										)
								}
					}
			);
		}

		return panel;
	}
	private EdenRootResolution ResolveEdenSelection(
		string selectedPath
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(
			selectedPath
		);

		var selected =
			Path.GetFullPath(
				selectedPath
			);

		var standardRoot =
			GetStandardEdenRoot();

		var standardCache =
			Path.Combine(
				standardRoot,
				"cache",
				"game_list"
			);

		var candidates =
			new List<EdenRootResolution>();

		void AddCandidate(
			string displayRoot,
			string nandRoot,
			string saveRoot,
			params string[] cacheDirs
		)
		{
			var caches =
				cacheDirs
					.Append(
						standardCache
					)
					.Where(
						path =>
							!string.IsNullOrWhiteSpace(
								path
							)
					)
					.Distinct(
						StringComparer.OrdinalIgnoreCase
					)
					.ToArray();

			candidates.Add(
				new EdenRootResolution(
					displayRoot,
					nandRoot,
					saveRoot,
					caches
				)
			);
		}

		AddCandidate(
			selected,
			Path.Combine(
				selected,
				"nand"
			),
			Path.Combine(
				selected,
				"nand",
				"user",
				"save",
				AccountSaveSlot
			),
			Path.Combine(
				selected,
				"cache",
				"game_list"
			)
		);

		AddCandidate(
			Path.Combine(
				selected,
				"user"
			),
			Path.Combine(
				selected,
				"user",
				"nand"
			),
			Path.Combine(
				selected,
				"user",
				"nand",
				"user",
				"save",
				AccountSaveSlot
			),
			Path.Combine(
				selected,
				"user",
				"cache",
				"game_list"
			)
		);

		AddCandidate(
			selected,
			selected,
			Path.Combine(
				selected,
				"user",
				"save",
				AccountSaveSlot
			),
			Path.Combine(
				selected,
				"..",
				"cache",
				"game_list"
			)
		);

		var trimmedName =
			Path.GetFileName(
				selected.TrimEnd(
					Path.DirectorySeparatorChar,
					Path.AltDirectorySeparatorChar
				)
			);

		if (string.Equals(
				trimmedName,
				"save",
				StringComparison.OrdinalIgnoreCase
			))
		{
			var userDir =
				Directory.GetParent(
					selected
				);

			var nandDir =
				userDir?.Parent;

			if (nandDir is not null)
			{
				AddCandidate(
					nandDir.FullName,
					nandDir.FullName,
					Path.Combine(
						selected,
						AccountSaveSlot
					)
				);
			}
		}

		if (string.Equals(
				trimmedName,
				AccountSaveSlot,
				StringComparison.OrdinalIgnoreCase
			))
		{
			var saveDir =
				Directory.GetParent(
					selected
				);

			var userDir =
				saveDir?.Parent;

			var nandDir =
				userDir?.Parent;

			if (nandDir is not null)
			{
				AddCandidate(
					nandDir.FullName,
					nandDir.FullName,
					selected
				);
			}
		}

		var match =
			candidates.FirstOrDefault(
				candidate =>
					Directory.Exists(
						candidate.SaveRoot
					)
			);

		if (match is null)
		{
			throw new DirectoryNotFoundException(
				LocalizationService.Get("Emulator_InvalidSaveTree")
			);
		}

		return match;
	}

	private List<EdenSaveEntry> ReadEdenCatalog(
		string saveRoot
	)
	{
		var result =
			new List<EdenSaveEntry>();

		foreach (var profilePath
			in Directory.EnumerateDirectories(
				saveRoot
			))
		{
			var profileId =
				Path.GetFileName(
					profilePath
				)
				.ToUpperInvariant();

			var isDevice =
				string.Equals(
					profileId,
					DeviceProfileId,
					StringComparison.OrdinalIgnoreCase
				);

			var hasRegisteredProfile =
				_profileNames.TryGetValue(
					profileId,
					out var registeredName
				);

			var profileDisplayName =
				isDevice
					? LocalizationService.Get("Common_DeviceShared")
					: hasRegisteredProfile
						? registeredName!
						: LocalizationService.Get("Emulator_UnregisteredProfile");

			foreach (var titlePath
				in Directory.EnumerateDirectories(
					profilePath
				))
			{
				var titleId =
					Path.GetFileName(
						titlePath
					)
					.ToUpperInvariant();

				if (!LooksLikeTitleId(
					titleId
				))
				{
					continue;
				}

				var files =
					EnumerateComparableFiles(
						titlePath
					)
					.ToArray();

				_gameMetadata.TryGetValue(
					titleId,
					out var game
				);

				result.Add(
					new EdenSaveEntry(
						profileId,
						profileDisplayName,
						isDevice,
						!isDevice &&
							!hasRegisteredProfile,
						titleId,
						game?.Title,
						game?.IconPath,
						titlePath,
						files.Length,
						files.Sum(
							GetLengthSafe
						)
					)
				);
			}
		}

		return result;
	}

	private static string GetFolderEntryName(
		EdenSaveEntry entry
	)
	{
		return entry.IsDevice
			? "Device"
			: entry.ProfileDisplayName;
	}

	private static string CreateKey(
		string gameName,
		string entryName
	)
	{
		return NormalizeIdentity(
				gameName
			) +
			"|" +
			NormalizeIdentity(
				entryName
			);
	}

	private static string NormalizeIdentity(
		string value
	)
	{
		var builder =
			new StringBuilder();

		foreach (var character
			in value.Normalize(
				NormalizationForm.FormD
			))
		{
			if (char.IsLetterOrDigit(
				character
			))
			{
				builder.Append(
					char.ToUpperInvariant(
						character
					)
				);
			}
		}

		return builder.ToString();
	}

	private void AddMutedMessage(
		string text
	)
	{
		EdenGamesPanel.Children.Add(
			new TextBlock
				{
					Text =
						text,

					Foreground =
						FindBrush(
							"TextFillColorSecondaryBrush",
							Brushes.Gray
						),

					Margin =
						new Thickness(
							4,
							8,
							0,
							0
						),

					TextWrapping =
						TextWrapping.Wrap
				}
		);
	}

	private static string GetStandardEdenRoot()
	{
		return Path.Combine(
			Environment.GetFolderPath(
				Environment.SpecialFolder.ApplicationData
			),
			"eden"
		);
	}

	private static IEnumerable<string> EnumerateComparableFiles(
		string root
	)
	{
		try
		{
			return Directory.EnumerateFiles(
					root,
					"*",
					SearchOption.AllDirectories
				)
				.Where(
					path =>
						!string.Equals(
							Path.GetFileName(
								path
							),
							".yuzu_save_size",
							StringComparison.OrdinalIgnoreCase
						)
				);
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static long GetLengthSafe(
		string path
	)
	{
		try
		{
			return new FileInfo(
				path
			).Length;
		}
		catch
		{
			return 0;
		}
	}

	private static bool LooksLikeTitleId(
		string value
	)
	{
		return value.Length ==
			16 &&
			value.All(
				Uri.IsHexDigit
			);
	}

	private static Brush FindBrush(
		string key,
		Brush fallback
	)
	{
		return Application.Current
			?.TryFindResource(
				key
			)
			as Brush
			?? fallback;
	}

	private sealed record EdenRootResolution(
		string DisplayRoot,
		string NandRoot,
		string SaveRoot,
		string[] CacheDirectories
	);

	private sealed record EdenSaveEntry(
		string ProfileId,
		string ProfileDisplayName,
		bool IsDevice,
		bool IsOrphan,
		string TitleId,
		string? GameName,
		string? IconPath,
		string SavePath,
		int FileCount,
		long Bytes
	);

	private sealed record FolderSaveEntry(
		string Category,
		string GameName,
		string TitleId,
		string EntryName,
		string SavePath,
		int FileCount,
		long Bytes
	);

	private sealed record UnifiedSaveEntry(
		string Key,
		string GameName,
		string TitleId,
		string EntryName,
		string ProfileDisplayName,
		string? LogicalPlayerId,
		string? EdenProfileId,
		string? FolderEntryName,
		string? IdentityIssue,
		string? ConflictAliases,
		string? IconPath,
		string? EdenPath,
		string? EdenProfilePath,
		string? FolderPath,
		string? FolderCategory,
		int EdenFileCount,
		int FolderFileCount,
		long EdenBytes,
		long FolderBytes,
		SaveState State
	);

	private sealed record ResolvedProfileIdentity(
		string Key,
		string? LogicalPlayerId,
		string? LogicalPlayerName
	);

	private sealed record ResolvedEdenSaveEntry(
		EdenSaveEntry Entry,
		ResolvedProfileIdentity Identity
	);

	private sealed record ResolvedFolderSaveEntry(
		FolderSaveEntry Entry,
		ResolvedProfileIdentity Identity
	);

	private enum SaveState
	{
		NotCompared,
		EdenOnly,
		FolderOnly,
		Synchronized,
		Divergent,
		Error
	}

	private sealed record SaveComparison(
		SaveState State,
		string Detail
	);

	private sealed record ComparisonProgress(
		int Current,
		int Total,
		string Label
	);
}

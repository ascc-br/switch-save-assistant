using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;

namespace SwitchSaveAssistant.Core.Profiles;

public sealed record SwitchDiscoveredProfile(
	string EntryName,
	int GameCount
);

public sealed class SwitchProfileDiscoveryService
{
	private static readonly string[]
		Categories =
			[
				"Installed games",
				"Uninstalled games"
			];

	public IReadOnlyList<SwitchDiscoveredProfile>
		Discover(
			DbiMtpClient client
		)
	{
		ArgumentNullException.ThrowIfNull(
			client
		);

		var gamesByProfile =
			new Dictionary<string, HashSet<string>>(
				StringComparer.OrdinalIgnoreCase
			);

		foreach (var category
			in Categories)
		{
			foreach (var game
				in client.GetGames(
					category
				))
			{
				foreach (var save
					in client.GetSaveEntries(
						game
					))
				{
					if (
						save.EntryType !=
							SaveEntryType.User ||
						string.IsNullOrWhiteSpace(
							save.EntryName
						)
					)
					{
						continue;
					}

					if (!gamesByProfile.TryGetValue(
						save.EntryName,
						out var games
					))
					{
						games =
							new HashSet<string>(
								StringComparer.OrdinalIgnoreCase
							);

						gamesByProfile[
							save.EntryName
						] =
							games;
					}

					games.Add(
						game.Name
					);
				}
			}
		}

		return gamesByProfile
			.Select(
				pair =>
					new SwitchDiscoveredProfile(
						pair.Key,
						pair.Value.Count
					)
			)
			.OrderBy(
				profile =>
					profile.EntryName,
				StringComparer.CurrentCultureIgnoreCase
			)
			.ToArray();
	}
}

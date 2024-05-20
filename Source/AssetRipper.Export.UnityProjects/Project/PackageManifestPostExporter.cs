using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.Processing;

namespace AssetRipper.Export.UnityProjects.Project;

public class PackageManifestPostExporter : IPostExporter
{
	public void DoPostExport(GameData gameData, LibraryConfiguration settings)
	{
	}

	protected virtual PackageManifest CreateManifest(UnityVersion version)
	{
		return PackageManifest.CreateDefault(version);
	}
}

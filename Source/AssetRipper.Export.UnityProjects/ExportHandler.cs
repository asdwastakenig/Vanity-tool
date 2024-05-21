using AssetRipper.Assets.Bundles;
using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.Export.UnityProjects.PathIdMapping;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Processing;
using AssetRipper.Processing.AnimatorControllers;
using AssetRipper.Processing.Assemblies;
using AssetRipper.Processing.AudioMixers;
using AssetRipper.Processing.Editor;
using AssetRipper.Processing.PrefabOutlining;
using AssetRipper.Processing.Scenes;
using AssetRipper.Processing.Textures;
using System.Reflection;
using System;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects;

public class ExportHandler
{
	protected LibraryConfiguration Settings { get; }

	public ExportHandler(LibraryConfiguration settings)
	{
		Settings = settings;
	}

	public GameData Load(IReadOnlyList<string> paths)
	{
		if (paths.Count == 1)
		{
			Logger.Info(LogCategory.Import, $"Attempting to read files from {paths[0]}");
		}
		else
		{
			Logger.Info(LogCategory.Import, $"Attempting to read files from {paths.Count} paths...");
		}

		GameStructure gameStructure = GameStructure.Load(paths, Settings);
		GameData gameData = GameData.FromGameStructure(gameStructure);
		Logger.Info(LogCategory.Import, "Finished reading files");
		return gameData;
	}

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "Processing loaded assets...");
		foreach (IAssetProcessor processor in GetProcessors())
		{
			processor.Process(gameData);
		}
		Logger.Info(LogCategory.Processing, "Finished processing assets");
	}

	protected virtual IEnumerable<IAssetProcessor> GetProcessors()
	{
		yield return new MethodStubbingProcessor();
		yield return new SceneDefinitionProcessor();
		yield return new MainAssetProcessor();
		yield return new AnimatorControllerProcessor();
		yield return new AudioMixerProcessor();
		yield return new EditorFormatProcessor(Settings.ImportSettings.BundledAssetsExportMode);
		//Static mesh separation goes here
		if (Settings.ProcessingSettings.EnablePrefabOutlining)
		{
			yield return new PrefabOutliningProcessor();
		}
		yield return new LightingDataProcessor();//Needs to be after static mesh separation
		yield return new PrefabProcessor();
		yield return new SpriteProcessor();
	}

	public void Export(GameData gameData, string outputPath)
	{
		Logger.Info(LogCategory.Export, "Starting export");
		Logger.Info(LogCategory.Export, $"Attempting to export assets to {outputPath}...");
		Logger.Info(LogCategory.Export, $"Game files have these Unity versions:{GetListOfVersions(gameData.GameBundle)}");
		Logger.Info(LogCategory.Export, $"Exporting to Unity version {gameData.ProjectVersion}");

		Settings.ExportRootPath = outputPath;
		Settings.SetProjectSettings(gameData.ProjectVersion, BuildTarget.NoTarget, TransferInstructionFlags.NoTransferInstructionFlags);

		ProjectExporter projectExporter = new(Settings, gameData.AssemblyManager);
		BeforeExport(projectExporter);
		projectExporter.DoFinalOverrides(Settings);
		projectExporter.Export(gameData.GameBundle, Settings);

		Logger.Info(LogCategory.Export, "Finished exporting assets");

		foreach (IPostExporter postExporter in GetPostExporters())
		{
			postExporter.DoPostExport(gameData, Settings);
		}
		Logger.Info(LogCategory.Export, "Finished post-export");

		static string GetListOfVersions(GameBundle gameBundle)
		{
			return string.Join(' ', gameBundle
				.FetchAssetCollections()
				.Select(c => c.Version)
				.Distinct()
				.Select(v => v.ToString()));
		}

		Logger.Info(LogCategory.Export, "Applying folder post-processing");
		ApplyPostProcessing(outputPath);
		string toScripts = Path.Combine("ExportedProject", "Assets", "Scripts");
		string outputpath_toScripts = Path.Combine(outputPath, toScripts);
		folderkeepshit(outputpath_toScripts, "Assembly-CSharp", "NewBlood.LegacyInput", "Unity.ProBuilder", "Unity.ProBuilder.KdTree", "Unity.ProBuilder.Poly2Tri", "Unity.ProBuilder.Stl", "Unity.TextMeshPro");
	}


	protected virtual void BeforeExport(ProjectExporter projectExporter)
	{
	}
	private void ApplyPostProcessing(string outputPath)
	{
		string[] asmdefFiles = Directory.GetFiles(outputPath, "*.asmdef", SearchOption.AllDirectories);
		foreach (string asmdefFile in asmdefFiles)
		{
			try
			{
				File.Delete(asmdefFile);
			}
			catch (IOException ex)
			{
				Console.WriteLine($"Error deleting file {asmdefFile}: {ex.Message}");
			}
		}
		string[] propertiesFiles = Directory.GetFiles(outputPath, "AssemblyInfo.cs", SearchOption.AllDirectories);
		foreach (string propertiesFile in propertiesFiles)
		{
			try
			{
				File.Delete(propertiesFile);
			}
			catch (IOException ex)
			{
				Console.WriteLine($"Error deleting file {propertiesFile}: {ex.Message}");
			}
		}
	}


	private void folderkeepshit(string path, params string[] foldersToKeep)
	{
		string[] allFolders = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
		foreach (string folder in allFolders)
		{
			try
			{
				bool deleteFolder = true;

				foreach (string folderToKeep in foldersToKeep)
				{
					if (folder.EndsWith(Path.DirectorySeparatorChar + folderToKeep))
					{
						deleteFolder = false;
						break;
					}
					// Check if the current folder is a subfolder of any folder to keep
					if (folder.Contains(Path.DirectorySeparatorChar + folderToKeep + Path.DirectorySeparatorChar))
					{
						deleteFolder = false;
						break;
					}
				}

				if (deleteFolder)
				{
					Directory.Delete(folder, true);
				}
			}
			catch (DirectoryNotFoundException)
			{
			}
		}
	}

	private void DeleteFoldersContaining(string rootPath, string folderName)
	{
		try
		{
			// Get all directories within the rootPath recursively
			string[] allFolders = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories);

			foreach (string folder in allFolders)
			{
				// Check if the folder path contains or is the target folder
				if (folder.Contains(folderName) || folder.EndsWith(folderName))
				{
					// Delete the folder and all its contents recursively
					Directory.Delete(folder, true);
					Logger.Info(LogCategory.Export, $"Deleted folder: {folder}");
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Error deleting folders: {ex.Message}");
		}
	}



	protected virtual IEnumerable<IPostExporter> GetPostExporters()
	{
		yield return new ProjectVersionPostExporter();
		yield return new PackageManifestPostExporter();
		yield return new StreamingAssetsPostExporter();
		yield return new DllPostExporter();
		yield return new PathIdMapExporter();
	}

	public GameData LoadAndProcess(IReadOnlyList<string> paths)
	{
		GameData gameData = Load(paths);
		Process(gameData);
		return gameData;
	}

	public void LoadProcessAndExport(IReadOnlyList<string> inputPaths, string outputPath)
	{
		GameData gameData = LoadAndProcess(inputPaths);
		Export(gameData, outputPath);
	}

	public void ThrowIfSettingsDontMatch(LibraryConfiguration settings)
	{
		if (Settings != settings)
		{
			throw new ArgumentException("Settings don't match");
		}
	}
}

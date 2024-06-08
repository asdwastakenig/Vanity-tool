using AssetRipper.Import.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AssetRipper.GUI.Web.Pages
{
	public static class Commands
	{
		private const string RootPath = "/";
		private const string CommandsPath = "/Commands";
		private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "AssetRipperTemp");

		public readonly struct LoadFile : ICommand
		{
			static async Task<string?> ICommand.Execute(HttpRequest request)
			{
				IFormCollection form = await request.ReadFormAsync();

				string[]? paths;
				if (form.TryGetValue("Path", out StringValues values))
				{
					paths = values;
				}
				else if (Dialogs.Supported)
				{
					Dialogs.OpenFiles.GetUserInput(out paths);
				}
				else
				{
					return CommandsPath;
				}

				if (paths is { Length: > 0 })
				{
					GameFileLoader.LoadAndProcess(paths);
				}
				return null;
			}
		}

		public class LoadFolder
		{
			public static void Execute(string? path)
			{
				if (string.IsNullOrEmpty(path))
				{
					if (Dialogs.Supported)
					{
						Dialogs.OpenFolder.GetUserInput(out path);
					}
					else
					{
						path = CommandsPath;
					}
				}

				if (!string.IsNullOrEmpty(path) && (path.EndsWith("ULTRAKILL_Data") || path.EndsWith("ULTRAKILL_Data\\")))
				{
					ProcessFolder(path);
				}
				else
				{
					Console.WriteLine("The path you picked is not ULTRAKILL_Data!");
				}
			}

			private static void ProcessFolder(string folderPath)
			{
				string tempFolderPath = Path.Combine(TempDirectory, Path.GetFileName(folderPath));

				// Create temp directory if it doesn't exist
				if (!Directory.Exists(TempDirectory))
				{
					Directory.CreateDirectory(TempDirectory);
				}
				else
				{
					// Clear existing contents of temp folder
					foreach (string file in Directory.GetFiles(TempDirectory))
					{
						File.Delete(file);
					}
					foreach (string dir in Directory.GetDirectories(TempDirectory))
					{
						Directory.Delete(dir, true);
					}
				}

				// Copy the selected folder to temp directory
				CopyFolder(folderPath, tempFolderPath);

				// Remove specific files
				RemoveFiles(tempFolderPath);

				// Run original LoadFolder command on processed folder in temp directory
				GameFileLoader.LoadAndProcess(new[] { tempFolderPath });
			}

			private static void CopyFolder(string sourceFolder, string destinationFolder)
			{
				if (!Directory.Exists(destinationFolder))
				{
					Directory.CreateDirectory(destinationFolder);
				}

				foreach (string file in Directory.GetFiles(sourceFolder))
				{
					string destFile = Path.Combine(destinationFolder, Path.GetFileName(file));
					File.Copy(file, destFile, true);
				}

				foreach (string folder in Directory.GetDirectories(sourceFolder))
				{
					string destFolder = Path.Combine(destinationFolder, Path.GetFileName(folder));
					CopyFolder(folder, destFolder);
				}
			}

			private static void RemoveFiles(string folderPath)
			{
				foreach (string file in Directory.GetFiles(folderPath, "specialscenes*", SearchOption.AllDirectories))
				{
					if (WebApplicationLauncher.exportScenes == true)
					{
						if (!file.EndsWith("specialscenes_scenes_uk_construct.bundle") || !file.EndsWith("specialscenes_scenes_creditsmuseum2.bundle"))
						{
							File.Delete(file);
						}
					}
					else
					{
						File.Delete(file);
					}
				}

				if (WebApplicationLauncher.exportScenes == false)
				{
					foreach (string file in Directory.GetFiles(folderPath, "campaign_scenes*", SearchOption.AllDirectories))
					{
						File.Delete(file);
					}
				}
			}
			private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "ULTRAKILL_Data");
		}

		public readonly struct Export : ICommand
		{

		private static void RemoveAssetBundleNamesFromMetaFiles(string folderPath)
  		{
        foreach (var metaFile in Directory.GetFiles(folderPath, ".meta", SearchOption.AllDirectories))
        {
            var metaContent = File.ReadAllText(metaFile);

            var modifiedContent = Regex.Replace(metaContent, @"^\sassetBundleName:.*?$", string.Empty, RegexOptions.Multiline);

            File.WriteAllText(metaFile, modifiedContent);
        }
    }
			static async Task<string?> ICommand.Execute(HttpRequest request)
			{
				IFormCollection form = await request.ReadFormAsync();

				string? path;
				if (form.TryGetValue("Path", out StringValues values))
				{
					path = values;
				}
				else
				{
					return CommandsPath;
				}

				if (!string.IsNullOrEmpty(path))
				{
					GameFileLoader.Export(path);
					string shittynewbloodfolder = Path.Combine(path, "ExportedProject", "Assets", "Scripts", "Assembly-CSharp", "NewBlood");
					try
					{
						Directory.Delete(shittynewbloodfolder, true);
						Logger.Info(LogCategory.Export, $"Deleted NewBlood folder: {shittynewbloodfolder}");
					}
					catch (Exception ex)
					{
						Logger.Error(LogCategory.Export, $"Failed to delete NewBlood folder: {ex.Message}");
					}
					RemoveAssetBundleNamesFromMetaFiles(path);
				}
				return null;
			}
		}

		public readonly struct Reset : ICommand
		{
			static Task<string?> ICommand.Execute(HttpRequest request)
			{
				GameFileLoader.Reset();
				return Task.FromResult<string?>(null);
			}
		}

		public static async Task HandleCommand<T>(HttpContext context) where T : ICommand
		{
			string? redirectionTarget = await T.Execute(context.Request);
			context.Response.Redirect(redirectionTarget ?? RootPath);
		}
	}
}

using AssetRipper.GUI.Web.Pages;
using AssetRipper.GUI.Web.Pages.Settings;
using AssetRipper.Import.Logging;
using AssetRipper.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.CommandLine;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Net;
using static AssetRipper.GUI.Web.Pages.Commands;
using System.Text.RegularExpressions;

namespace AssetRipper.GUI.Web;

public static class WebApplicationLauncher
{
	private static string install_path;

	private static class Defaults
	{
		public const int Port = 0;
		public const bool LaunchBrowser = true;
	}
	public static bool exportScenes = false;

	public static void Launch(string[] args)
	{
		RootCommand rootCommand = new() { Description = "AssetRipper" };

		Option<int> portOption = new Option<int>(
			name: "--port",
			description: "If nonzero, the application will attempt to host on this port, instead of finding a random unused port.",
			getDefaultValue: () => Defaults.Port);
		rootCommand.AddOption(portOption);

		Option<bool> launchBrowserOption = new Option<bool>(
			name: "--launch-browser",
			description: "If true, a browser window will be launched automatically.",
			getDefaultValue: () => Defaults.LaunchBrowser);
		rootCommand.AddOption(launchBrowserOption);

		bool shouldRun = false;
		int port = Defaults.Port;
		bool launchBrowser = Defaults.LaunchBrowser;

		rootCommand.SetHandler((int portParsed, bool launchBrowserParsed) =>
		{
			shouldRun = true;
			port = portParsed;
			launchBrowser = launchBrowserParsed;
		}, portOption, launchBrowserOption);

		rootCommand.Invoke(args);

		if (shouldRun)
		{
			Launch();
		}
	}

	public static bool isExtractionDone = false;
	public static string[] borkedMetaFiles = new string[]
		{
			"InputManager.cs.meta",
			"Bootstrap.cs.meta",
			"InputExtensions.cs.meta",
			"RaycastResult.cs.meta",
			"CombineMeshes.cs.meta",
			"PlayerInput.cs.meta",
			"SteamController.cs.meta"
		};
	public static int GUIDC_ExitCode;

	public static void Launch() //basically where the entirety of Vanity is initialized
	{
		using (StreamWriter logWriter = new StreamWriter("latestlog.txt", true))
		{
			void Log(string message)
			{
				Console.WriteLine(message);
				logWriter.WriteLine(message);
			}



			WelcomeMessage.Print();
			Log("Please enter the full path to the location where you want to install Envy & Spite: ");
			string installPath = Console.ReadLine();
			logWriter.WriteLine(installPath);

			try
			{
				Log("Downloading the Envy & Spite 1.4.0 project off of Github...");
				DownloadAndExtractZip("https://github.com/ImSimonNow/simons_files/raw/main/es140_simon.zip", installPath);
				while (!isExtractionDone)
				{
					System.Threading.Thread.Sleep(1);
				}
			}
			catch (Exception ex)
			{
				Log($"An error occurred: {ex.Message}. Either the file is down or your internet is messed up. Restart and try again.");
				Console.ReadLine();
				logWriter.Close();
				Environment.Exit(0);
			}

			string assetsPath = Path.Combine(installPath, "E&S 1.4.0\\Assets");
			string projectPath = Path.Combine(installPath, "E&S 1.4.0\\");
			Log("--ASSET EXTRACTION PHASE--");
			Log("Enter the FULL PATH to your ULTRAKILL_Data folder (e.g:C:\\ULTRAKILL\\ULTRAKILL_Data):");
			string ukDataPath = Console.ReadLine();
			logWriter.WriteLine(ukDataPath);

			Log("Do you want to enable scene exporting [Y\\N]? WARNING: Scene exporting is INCREDIBLY HEAVY! Main downsides are: \n-having 200k or more meshes in the exported assets.\n-duplicate assets.\n-having to use a third party tool to separate meshes from scenes.\n-importing taking much longer.\n-scene names are garbled (ex. 7-3's scene name starts with a952d42adc)\nBUT you'll be able to see how certain things in certain levels are done.\n So, do you want to enable scene exporting? [Y\\N]:");
			char inputChar;
			try
			{
				inputChar = Convert.ToChar(Console.ReadLine());
				logWriter.WriteLine(inputChar);
				inputChar = Char.ToLower(inputChar);
				if (inputChar == 'y')
				{
					Log("Scene exporting enabled. You've been warned.");
					exportScenes = true;
				}
				else if (inputChar == 'n')
				{
					Log("Scene exporting disabled.");
					exportScenes = false;
				}
				else
				{
					throw new Exception();
				}
			}
			catch (Exception ex)
			{
				Log("Invalid input, assuming \"No\"");
				exportScenes = false;
			}

			Log("Attempting to load the folder...");
			try
			{
				LoadFolder.Execute(ukDataPath);
			}
			catch (Exception ex)
			{
				Log($"Error: {ex.Message}. Your path is probably invalid or something went incredibly wrong. \nPlease restart and try again");
				Console.ReadLine();
				logWriter.Close();
				Environment.Exit(0);
			}

			string UKAssets = Path.Combine(assetsPath, "ULTRAKILL Assets");
			Log("Imported ULTRAKILL_Data folder. Creating: " + UKAssets);
			Directory.CreateDirectory(UKAssets);
			Log("Attempting to export all assets (hope you like waiting)...");
			try
			{
				GameFileLoader.Export(UKAssets);
			}
			catch (Exception ex)
			{
				Log($"Error: {ex.Message}. It failed to export assets for whatever reason.");
				Console.ReadLine();
				logWriter.Close();
				Environment.Exit(0);
			}

			Log("Deleting unnecessary folders...");
			string packages = Path.Combine(UKAssets, "ExportedProject", "Packages");
			string projset = Path.Combine(UKAssets, "ExportedProject", "ProjectSettings");
			string auxiliaryFiles = Path.Combine(UKAssets, "AuxiliaryFiles");
			try
			{
				if (Directory.Exists(auxiliaryFiles))
				{
					Directory.Delete(auxiliaryFiles, true);
				}
				if (Directory.Exists(packages))
				{
					Directory.Delete(packages, true);
				}
				if (Directory.Exists(projset))
				{
					Directory.Delete(projset, true);
				}
			}
			catch (Exception ex)
			{
				Log($"Error: {ex.Message}. Vanity failed to delete error-causing folders. Please restart & try again.");
				Console.ReadLine();
				logWriter.Close();
				Environment.Exit(0);
			}

			Log("Doing more post-processing...");
			string shittynewbloodfolder = Path.Combine(UKAssets, "ExportedProject", "Assets", "Scripts", "Assembly-CSharp", "NewBlood");
			try
			{
				Directory.Delete(shittynewbloodfolder, true);
				Logger.Info(LogCategory.Export, $"Deleted NewBlood folder: {shittynewbloodfolder}");
			}
			catch (Exception ex)
			{
				Log("Failed to delete the NewBlood folder. Either it wasn't found or it failed creating everything. Restart and try again");
				Console.ReadLine();
				logWriter.Close();
				Environment.Exit(0);
			}
			SearchAndModifyMetaFiles(UKAssets);
			string incorrectPath = Path.Combine(UKAssets, "ExportedProject", "Assets", "Scripts");
			string libPath = Path.Combine(installPath, "E&S 1.4.0\\Library\\PackageCache");
			Log("--GUID Patching--");
			string guidArguments = $"\"{incorrectPath}\" \"{libPath}\" \"{assetsPath}\"";
			DeleteFilesRecursively(incorrectPath, borkedMetaFiles);
			using (Process process = System.Diagnostics.Process.Start(@"GUID_Corrector.exe", guidArguments))
			{
				process.WaitForExit();

				GUIDC_ExitCode = process.ExitCode;

				Log($"GUID_Corrector.exe process exited with code {GUIDC_ExitCode}");
			}
			string probuildershit1 = Path.Combine(incorrectPath + "\\Unity.ProBuilder");
			string probuildershit2 = Path.Combine(incorrectPath + "\\Unity.ProBuilder.KdTree");
			string probuildershit3 = Path.Combine(incorrectPath + "\\Unity.ProBuilder.Poly2Tri");
			string probuildershit4 = Path.Combine(incorrectPath + "\\Unity.ProBuilder.Stl");
			string tmp_folder = Path.Combine(incorrectPath + "\\Unity.TextMeshPro");
			try
			{
				Directory.Delete(probuildershit1, true);
				Directory.Delete(probuildershit2, true);
				Directory.Delete(probuildershit3, true);
				Directory.Delete(probuildershit4, true);
				Directory.Delete(tmp_folder, true);
			}
			catch (Exception ex)
			{
				Log($"Error{ex.Message}. Post processing (or, a part of it) failed.");
				Console.ReadLine();
				logWriter.Close();
				Environment.Exit(0);
			}
			if(GUIDC_ExitCode == 0)
			{
				Log($"It's done! Envy & Spite 1.4.0 has been successfully set up! Now open {projectPath} in Unity Hub to open the editor.\n Press enter to exit.");
			}
			else
			{
				Log($"GUID Corrector didn't exit correctly (non-zero exit code), so something probably went wrong. It is recommended to delete the E&S 1.4.0 project folder & start again.");
			}
			Console.ReadKey();
		}
	}

	public static void DeleteFilesRecursively(string folderPath, string[] filesToDelete)
	{
		try
		{
			string[] files = Directory.GetFiles(folderPath);

			foreach (string file in files)
			{
				string fileName = Path.GetFileName(file);
				if (Array.Exists(filesToDelete, element => element == fileName))
				{
					File.Delete(file);
					Console.WriteLine($"Deleted: {file}");
				}
			}

			string[] subDirectories = Directory.GetDirectories(folderPath);

			foreach (string subDirectory in subDirectories)
			{
				DeleteFilesRecursively(subDirectory, filesToDelete);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An error occurred: {ex.Message}");
		}
	}
	static void SearchAndModifyMetaFiles(string directoryPath)
	{
		try
		{
			string[] metaFiles = Directory.GetFiles(directoryPath, "*.meta");

			foreach (string metaFile in metaFiles)
			{
				ModifyMetaFile(metaFile);
			}

			string[] subdirectories = Directory.GetDirectories(directoryPath);
			foreach (string subdirectory in subdirectories)
			{
				SearchAndModifyMetaFiles(subdirectory);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}. Bundle fixing failed. Restart and try again.");
			Console.ReadLine();
			Environment.Exit(0);
		}
	}

	static void ModifyMetaFile(string filePath)
	{
		try
		{
			string[] lines = File.ReadAllLines(filePath);

			for (int i = 0; i < lines.Length; i++)
			{
				if (lines[i].Contains("assetBundleName"))
				{
					// Make the line blank
					lines[i] = string.Empty;
				}
			}

			// Write the modified lines back to the file
			File.WriteAllLines(filePath, lines);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error modifying {filePath}: {ex.Message}");
		}
	}

	private static void DownloadAndExtractZip(string url, string extractPath)
	{
		using (WebClient client = new WebClient())
		{
			string tempZipFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");

			client.DownloadProgressChanged += (sender, e) =>
			{
				Console.Write($"\rProgress: {e.ProgressPercentage}% done");
			};

			client.DownloadFileCompleted += (sender, e) =>
			{
				Console.WriteLine("\nDownload completed.");
				Console.WriteLine("Extracting...");
				ZipFile.ExtractToDirectory(tempZipFile, extractPath);
				File.Delete(tempZipFile);
				Console.WriteLine("Extraction completed.");
				isExtractionDone = true;
			};

			client.DownloadFileAsync(new Uri(url), tempZipFile);
			while (client.IsBusy)
			{
				System.Threading.Thread.Sleep(100);
			}
		}
	}

	private static ILoggingBuilder ConfigureLoggingLevel(this ILoggingBuilder builder)
	{
		builder.Services.Add(ServiceDescriptor.Singleton<IConfigureOptions<LoggerFilterOptions>>(
			new LifetimeOrWarnConfigureOptions()));
		return builder;
	}

	private sealed class LifetimeOrWarnConfigureOptions : ConfigureOptions<LoggerFilterOptions>
	{
		public LifetimeOrWarnConfigureOptions() : base(AddRule)
		{
		}

		private static void AddRule(LoggerFilterOptions options)
		{
			options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Information, static (provider, category, logLevel) =>
			{
				return category is "Microsoft.Hosting.Lifetime" || logLevel >= LogLevel.Warning;
			}));
		}
	}

	private static void OpenUrl(string url)
	{
		try
		{
			if (OperatingSystem.IsWindows())
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			else if (OperatingSystem.IsLinux())
			{
				Process.Start("xdg-open", url);
			}
			else if (OperatingSystem.IsMacOS())
			{
				Process.Start("open", url);
			}
		}
		catch (Exception ex)
		{
			Logger.Error($"Failed to launch web browser for: {url}", ex);
		}
	}

	private static void MapGet(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern, Func<IResult> handler)
	{
		endpoints.MapGet(pattern, (context) =>
		{
			IResult result = handler.Invoke();
			return result.ExecuteAsync(context);
		});
	}

	private static void MapGet(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern, Func<Task<IResult>> handler)
	{
		endpoints.MapGet(pattern, async (context) =>
		{
			IResult result = await handler.Invoke();
			await result.ExecuteAsync(context);
		});
	}

	private static void MapStaticFile(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string path, string contentType)
	{
		endpoints.MapGet(path, async () =>
		{
			byte[] data = await StaticContentLoader.Load(path);
			return Results.Bytes(data, contentType);
		});
	}
}

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

	public static void Launch()
	{
		WelcomeMessage.Print();
		Console.Write("Please enter the full path to the location where you want to install Envy & Spite: ");
		string installPath = Console.ReadLine();
		Console.WriteLine(installPath);
		try
		{
			Console.WriteLine("Downloading the Envy & Spite 1.4.0 project off of Github...");
			DownloadAndExtractZip("https://github.com/ImNotSimon/misc_stuff/raw/main/es140_simon.zip", installPath);
			Console.WriteLine("Installation completed successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An error occurred: {ex.Message}");
			Console.ReadKey();
		}

		string assetsPath = Path.Combine(installPath, "E&S 1.4.0\\Assets");
		string projectPath = Path.Combine(installPath, "E&S 1.4.0\\");
		Console.WriteLine("The path to your Assets path is: " + assetsPath + "(don't worry it's debug stuff :))");
		Console.WriteLine("--EXTRACTION PHASE--");
		Console.WriteLine("Enter the FULL PATH to your ULTRAKILL_Data folder (e.g:C:\\ULTRAKILL\\ULTRAKILL_Data):");
		string ukDataPath = Console.ReadLine();
		Console.WriteLine("Attempting to load the folder...");
		try
		{
			LoadFolder.Execute(ukDataPath);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			Console.ReadKey();
		}

		string UKAssets = Path.Combine(assetsPath, "ULTRAKILL Assets");
		Console.WriteLine("Imported ULTRAKILL_Data folder. Creating: " + UKAssets);
		Directory.CreateDirectory(UKAssets);
		Console.WriteLine("Attempting to export all assets (hope you like waiting)...");
		try
		{
			GameFileLoader.Export(UKAssets);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			Console.ReadKey();
		}

		Console.WriteLine("Deleting unnecessary folders...");
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
			Console.WriteLine($"Error: {ex.Message}");
			Console.ReadKey();
		}

		Console.WriteLine("Doing more post-processing...");
		string shittynewbloodfolder = Path.Combine(UKAssets, "ExportedProject", "Assets", "Scripts", "Assembly-CSharp", "NewBlood");
		try
		{
			Directory.Delete(shittynewbloodfolder, true);
			Logger.Info(LogCategory.Export, $"Deleted NewBlood folder: {shittynewbloodfolder}");
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Failed to delete NewBlood folder: {ex.Message}");
		}
		SearchAndModifyMetaFiles(UKAssets);
		string incorrectPath = Path.Combine(UKAssets, "ExportedProject", "Assets", "Scripts");
		string libPath = Path.Combine(installPath, "E&S 1.4.0\\Library\\PackageCache");
		Console.WriteLine("GUID Patching (THIS USUALLY TAKES A WHILE!)");
		string guidArguments = $"\"{incorrectPath}\" \"{libPath}\" \"{assetsPath}\"";
		using (Process process = System.Diagnostics.Process.Start(@"GUID_Corrector.exe", guidArguments))
		{
			process.WaitForExit();

			int exitCode = process.ExitCode;

			Console.WriteLine($"GUID_Corrector.exe process exited with code {exitCode}");
		}
		string probuildershit1 = Path.Combine(incorrectPath + "\\Unity.ProBuilder");
		string probuildershit2 = Path.Combine(incorrectPath + "\\Unity.ProBuilder.KdTree");
		string probuildershit3 = Path.Combine(incorrectPath + "\\Unity.ProBuilder.Poly2Tri");
		string probuildershit4 = Path.Combine(incorrectPath + "\\Unity.ProBuilder.Stl");
		string tmp_folder = Path.Combine(incorrectPath + "\\Unity.TextMeshPro");
		try
		{
			RecursiveSearchAndDelete(incorrectPath, "InputManager.cs.meta");
			Directory.Delete(probuildershit1, true);
			Directory.Delete(probuildershit2, true);
			Directory.Delete(probuildershit3, true);
			Directory.Delete(probuildershit4, true);
			Directory.Delete(tmp_folder, true);
		}
		catch (Exception ex)
		{
			Console.WriteLine("oops a part of post processing failed");
		}
		Console.WriteLine($"It's done! Envy & Spite 1.4.0 has been sucessfully set up! Now open {projectPath} in Unity Hub to open the editor.\n Press any key to exit.");
		Console.ReadKey();
	}
	public static void RecursiveSearchAndDelete(string directoryPath, string fileName)
	{
		try
		{
			// Check if the directory exists
			if (Directory.Exists(directoryPath))
			{
				// Search for the file in the current directory
				string[] files = Directory.GetFiles(directoryPath, fileName);

				// Delete the file if found
				foreach (string file in files)
				{
					Console.WriteLine($"Deleting file: {file}");
					File.Delete(file);
				}

				// Recursively search in subdirectories
				string[] subDirectories = Directory.GetDirectories(directoryPath);
				foreach (string subDirectory in subDirectories)
				{
					RecursiveSearchAndDelete(subDirectory, fileName);
				}
			}
			else
			{
				Console.WriteLine($"Directory not found: {directoryPath}");
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
			// Search for .meta files in the current directory
			string[] metaFiles = Directory.GetFiles(directoryPath, "*.meta");

			foreach (string metaFile in metaFiles)
			{
				ModifyMetaFile(metaFile);
			}

			// Recursively search in subdirectories
			string[] subdirectories = Directory.GetDirectories(directoryPath);
			foreach (string subdirectory in subdirectories)
			{
				SearchAndModifyMetaFiles(subdirectory);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
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
			client.DownloadFile(url, tempZipFile);
			Console.WriteLine("Extracting...");
			ZipFile.ExtractToDirectory(tempZipFile, extractPath);
			File.Delete(tempZipFile);
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

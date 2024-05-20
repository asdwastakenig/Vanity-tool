namespace AssetRipper.GUI.Web;

public static class WelcomeMessage
{
	private const string AsciiArt = """

 _   _             _ _         
| | | |           (_) |        
| | | | __ _ _ __  _| |_ _   _ 
| | | |/ _` | '_ \| | __| | | |
\ \_/ / (_| | | | | | |_| |_| |
 \___/ \__,_|_| |_|_|\__|\__, |
                          __/ |
                         |___/  
                                                                     
										   
				A FORK OF ASSETRIPPER (https://github.com/AssetRipper/AssetRipper/) FOR ENVY/SPITE LEVEL EDITOR!
""";

	private const string Directions = """
		This is an ASSET EXTRACTING UTILITY that automatically sets up an Envy & Spite 1.4.0 project, alongside extracted assets.
		PLEASE KEEP IN MIND IT CAN AND PROBABLY WILL TAKE A WHILE!!
		""";

	public static void Print()
	{
		Console.WriteLine(AsciiArt);
		Console.WriteLine();
		Console.WriteLine(Directions);
		Console.WriteLine();
	}
}

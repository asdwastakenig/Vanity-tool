using AssetRipper.GUI.Web.Paths;

namespace AssetRipper.GUI.Web.Pages;

public sealed class IndexPage : DefaultPage
{
	public static IndexPage Instance { get; } = new();

	public override string? GetTitle() => Localization.AssetRipperFree;

	public override void WriteInnerContent(TextWriter writer)
	{
		using (new Div(writer).WithClass("text-center container mt-5").End())
		{
			new H1(writer).WithClass("display-4 mb-4").Close(Localization.Welcome);
			if (GameFileLoader.IsLoaded)
			{
				PathLinking.WriteLink(writer, GameFileLoader.GameBundle, Localization.MenuExportAll, "btn btn-success");
			}
			else
			{
				new Button(writer).WithType("button").WithClass("btn btn-secondary").WithDisabled().Close(Localization.NoFilesLoaded);
			}
		}
	}
}

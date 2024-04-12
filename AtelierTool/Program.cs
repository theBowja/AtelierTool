using System.ComponentModel;
using AtelierTool;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.PropagateExceptions();

    config.AddCommand<BundleDownloadCommand>("download-bundles")
        .WithDescription("Downloads and decrypts the game bundles.")
        .WithExample("download-bundles")
        .WithExample("download-bundles", "1695186894_yFRlDhTt4xOHNPdw");
});

await app.RunAsync(args);

internal sealed class BundleDownloadCommand : AsyncCommand<BundleDownloadCommand.Settings>
{
    public enum Platform
    {
        Android,
        iOS,
        StandaloneWindows64
    }

    public enum Server
    {
        Global,
        Japan
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Asset version to download.")]
        [CommandArgument(0, "<version>")]
        public required string AssetVersion { get; set; }

        [Description("Platform to download assets for")]
        [CommandOption("-p|--platform")]
        [DefaultValue(Platform.Android)]
        public Platform AssetPlatform { get; init; }

        [Description("Server to download assets for. Either Global or Japan")]
        [CommandOption("-s|--server")]
        [DefaultValue(Server.Global)]
        public Server AssetServer { get; init; }

        [Description("Path to store the assets into.")]
        [CommandOption("-o|--output")]
        [DefaultValue("output")]
        public string OutputPath { get; init; } = null!;

        [Description("Amount of files to download concurrently.")]
        [CommandOption("-c|--concurrent")]
        [DefaultValue(16)]
        public int ConcurrentCount { get; init; }

        [Description("Path to the '\n'-delimited list of bundle names to download/decrypt.")]
        [CommandOption("-b|--bundlepath")]
        [DefaultValue("")]
        public string BundleFilterPath { get; init; } = null!;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var fullOutputPath = Path.GetFullPath(settings.OutputPath);
        Directory.CreateDirectory(fullOutputPath);

        var domain = settings.AssetServer == Server.Global ? "com" : "jp";

        var url = $"https://asset.resleriana.{domain}/asset/{settings.AssetVersion}/{settings.AssetPlatform}/";

        ConsoleLogger.WriteInfoLine($"Downloading catalog for version {settings.AssetVersion}.");
        var catalog = await Catalog.LoadFromVersion(url, fullOutputPath);
        ConsoleLogger.WriteInfoLine("Downloaded catalog [green bold]successfully.[/]");

        var numToDownload = catalog.FileCatalog.Bundles.Count;
        string[] bundleNames = Array.Empty<string>();
        if (!string.IsNullOrEmpty(settings.BundleFilterPath))
        {
            var bundleFullPath = Path.GetFullPath(settings.BundleFilterPath);
            bundleNames = File.ReadAllText(bundleFullPath).Split('\n');
            numToDownload = bundleNames.Length;
        }

        ConsoleLogger.WriteInfoLine($"Total asset count: {catalog.FileCatalog.Bundles.Count}");
        ConsoleLogger.WriteInfoLine($"Number of assets to download: {numToDownload}");
        ConsoleLogger.WriteInfoLine($"Downloading assets. (Concurrent count: {settings.ConcurrentCount})");


        var downloader = new Downloader(catalog, settings.OutputPath, settings.ConcurrentCount, url, bundleNames);

        await downloader.Download();

        return 0;
    }
}
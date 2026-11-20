using System.IO.Compression;
using System.Net;
using DriverPlatform.Application.Abstractions;
using MaxMind.Db;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DriverPlatform.Infrastructure.Visitors;

/// <summary>
/// Country and city of an IP from DB-IP's free "IP to City Lite" database (CC BY 4.0: credit DB-IP
/// where the data is shown). The file lives on the server, so no visitor's IP is sent anywhere.
/// Without the file (first minutes after a new server, no internet) visits are kept without a place.
/// </summary>
public sealed class DbIpGeoLookup(ILogger<DbIpGeoLookup> logger) : IGeoIpLookup, IDisposable
{
    private Reader? _reader;

    public (string? Country, string? City) Lookup(IPAddress address)
    {
        var reader = Volatile.Read(ref _reader);
        if (reader is null)
        {
            return (null, null);
        }
        try
        {
            var data = reader.Find<Dictionary<string, object>>(address);
            var country = (data?.GetValueOrDefault("country") as Dictionary<string, object>)?.GetValueOrDefault("iso_code") as string;
            var names = ((data?.GetValueOrDefault("city") as Dictionary<string, object>)?.GetValueOrDefault("names")) as Dictionary<string, object>;
            return (country, names?.GetValueOrDefault("en") as string);
        }
        catch (Exception ex) when (ex is InvalidDatabaseException or ArgumentException)
        {
            logger.LogWarning(ex, "IP lookup failed");
            return (null, null);
        }
    }

    /// <summary>Opens a (new) database file; the old one is closed.</summary>
    public void Open(string path)
    {
        // Memory-mapped: the 130 MB file is read from disk as needed, not loaded into memory.
        var reader = new Reader(path, FileAccessMode.MemoryMapped);
        var old = Interlocked.Exchange(ref _reader, reader);
        old?.Dispose();
    }

    public void Dispose() => Interlocked.Exchange(ref _reader, null)?.Dispose();
}

/// <summary>Downloads DB-IP's monthly file at startup when missing or older than 35 days, then checks daily.</summary>
public sealed class DbIpUpdater(DbIpGeoLookup lookup, IHttpClientFactory http, VisitorOptions options, ILogger<DbIpUpdater> logger)
    : BackgroundService
{
    public const string HttpClientName = "dbip";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            try
            {
                await EnsureFreshAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not update the IP location database; visits are kept without a place until it works");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EnsureFreshAsync(CancellationToken cancellationToken)
    {
        var path = options.GeoIpPath;
        var file = new FileInfo(path);
        if (file.Exists && file.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-35))
        {
            lookup.Open(path);
            return;
        }

        Directory.CreateDirectory(file.DirectoryName!);
        var client = http.CreateClient(HttpClientName);
        // This month's file appears a few days into the month: fall back to last month's.
        foreach (var month in new[] { DateTime.UtcNow, DateTime.UtcNow.AddMonths(-1) })
        {
            var url = $"https://download.db-ip.com/free/dbip-city-lite-{month:yyyy-MM}.mmdb.gz";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }
            var temp = path + ".download";
            await using (var gzip = new GZipStream(await response.Content.ReadAsStreamAsync(cancellationToken), CompressionMode.Decompress))
            await using (var output = File.Create(temp))
            {
                await gzip.CopyToAsync(output, cancellationToken);
            }
            File.Move(temp, path, overwrite: true);
            lookup.Open(path);
            logger.LogInformation("IP location database updated from {Url}", url);
            return;
        }
        if (file.Exists)
        {
            lookup.Open(path);
        }
        logger.LogWarning("No IP location database could be downloaded");
    }
}

public sealed class VisitorOptions
{
    public const string SectionName = "Visitors";

    /// <summary>Where the IP location file is kept (a Docker volume on the server).</summary>
    public string GeoIpPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "geoip", "dbip-city-lite.mmdb");

    /// <summary>Off in tests: no download.</summary>
    public bool GeoIpDownload { get; set; } = true;
}

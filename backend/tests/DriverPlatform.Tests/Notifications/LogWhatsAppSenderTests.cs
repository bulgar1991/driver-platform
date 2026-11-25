using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Infrastructure.WhatsApp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Tests.Notifications;

public sealed class LogWhatsAppSenderTests
{
    private sealed class Env(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task A_folder_it_cannot_write_is_no_error_and_it_says_it_is_not_connected()
    {
        // A file stands where the log folder should be: the folder cannot be created.
        var root = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(root, "blocked"), "");
        IWhatsAppSender sender = new LogWhatsAppSender(
            Options.Create(new WhatsAppOptions { LogDirectory = "blocked/whatsapp" }), new Env(root), NullLogger<LogWhatsAppSender>.Instance);

        var id = await sender.SendTemplateAsync("+41791234567", NotificationKind.DriverShift, "it", ["Marco"], CancellationToken.None);

        Assert.StartsWith("log_", id);
        Assert.False(sender.IsConnected);
    }
}

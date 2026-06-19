using Microsoft.Extensions.Options;
using XenPlus.Features;
using XenPlus.XenIface;

namespace XenPlus;

static class ServiceKeys {
    public const string WmiService_Root_CIMV2 = "Root\\CIMV2";
}

class Program {
    const uint _assert_VendorName = VersionInfo.VendorName == "" ? -1 : 0;

    static string GetConfigDir() {
        var programData = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolderOption.DoNotVerify)
            ?? throw new DirectoryNotFoundException("Environment.SpecialFolder.CommonApplicationData");
        return Path.Combine(programData, VersionInfo.VendorName, "XenPlus");
    }

    static void Main() {
        var earlyLogger = new EarlyLogger();
        var mitigations = new Mitigations(earlyLogger);
        mitigations.EnableAll();

        var configDir = GetConfigDir();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings() {
            ContentRootPath = configDir,
            DisableDefaults = true
        });

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("appsettings.dist.json", true, true);
        builder.Configuration.AddJsonFile("appsettings.installed.json", true, true);
        builder.Configuration.AddJsonFile("appsettings.user.json", true, true);

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddEventLog(options => {
            options.SourceName = nameof(XenPlus);
            options.Filter = (_, _) => true;
        });

        builder.Services.AddWindowsServiceEx();

        builder.Services.AddSingleton<XenIfaceSource>();

        builder.Services.Configure<PVVersionInfoOptions>(builder.Configuration.GetSection(nameof(PVVersionInfoOptions)));
        builder.Services.AddHostedService<PVVersionInfoFeature>();

        builder.Services.AddKeyedSingleton(ServiceKeys.WmiService_Root_CIMV2, (_, k) => new WmiService((string)k!));
        builder.Services.AddSingleton<OSInfoService>();

        builder.Services.Configure<OSInfoOptions>(builder.Configuration.GetSection(nameof(OSInfoOptions)));
        builder.Services.AddHostedService<OSInfoFeature>();

        builder.Services.AddSingleton<IValidateOptions<MemoryInfoOptions>, ValidateMemoryInfoOptions>();
        builder.Services.Configure<MemoryInfoOptions>(builder.Configuration.GetSection(nameof(MemoryInfoOptions)));
        builder.Services.AddHostedService<MemoryInfoFeature>();

        builder.Services.Configure<ClipboardOptions>(builder.Configuration.GetSection(nameof(ClipboardOptions)));
        builder.Services.AddHostedService<ClipboardFeature>();

        var host = builder.Build();
        host.Run();
    }
}

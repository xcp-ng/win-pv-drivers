using Microsoft.Extensions.Options;
using XenPlus.Features;
using XenPlus.XenIface;

namespace XenPlus;

static class ServiceKeys {
    public const string WmiService_Root_CIMV2 = "Root\\CIMV2";
}

class Program {
    static void Main(string[] args) {
        var earlyLogger = new EarlyLogger();
        var mitigations = new Mitigations(earlyLogger);
        mitigations.EnableAll();

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.Sources.Clear();
        if (Path.GetDirectoryName(Environment.ProcessPath) is string processDir && !string.IsNullOrEmpty(processDir)) {
            builder.Configuration.SetBasePath(processDir);
            builder.Configuration.AddJsonFile("appsettings.json", true, true);
            builder.Configuration.AddJsonFile("appsettings.user.json", true, true);
        } else {
            earlyLogger.LogError("Cannot determine settings path, refusing to load configuration");
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddEventLog(options => {
            options.SourceName = nameof(XenPlus);
            options.Filter = (_, _) => true;
        });

        builder.Services.AddWindowsService(options => {
            options.ServiceName = nameof(XenPlus);
        });

        builder.Services.AddSingleton<XenIfaceSource>();
        builder.Services.AddKeyedSingleton(ServiceKeys.WmiService_Root_CIMV2, (_, k) => new WmiService((string)k!));
        builder.Services.AddSingleton<WmiOsInfoService>();

        builder.Services.Configure<PVVersionInfoOptions>(builder.Configuration.GetSection(nameof(PVVersionInfoOptions)));
        builder.Services.AddHostedService<PVVersionInfoFeature>();

        builder.Services.Configure<OSInfoOptions>(builder.Configuration.GetSection(nameof(OSInfoOptions)));
        builder.Services.AddHostedService<OSInfoFeature>();

        builder.Services.AddSingleton<IValidateOptions<MemoryInfoOptions>, ValidateMemoryInfoOptions>();
        builder.Services.Configure<MemoryInfoOptions>(builder.Configuration.GetSection(nameof(MemoryInfoOptions)));
        builder.Services.AddHostedService<MemoryInfoFeature>();

        builder.Services.Configure<NetInfoOptions>(builder.Configuration.GetSection(nameof(NetInfoOptions)));
        builder.Services.AddHostedService<NetInfoFeature>();

        var host = builder.Build();
        host.Run();
    }
}

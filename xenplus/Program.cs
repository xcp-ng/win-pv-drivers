using XenPlus.Features;
using XenPlus.XenIface;

namespace XenPlus;

static class ServiceKeys {
    public const string WmiService_Root_CIMV2 = "Root\\CIMV2";
}

public class Program {
    public static void Main(string[] args) {
        var mitigations = new Mitigations(new EarlyLogger());
        mitigations.EnableAll();

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.user.json", true, true);

        builder.Logging.AddEventLog(options => {
            options.Filter = (_, _) => true;
            options.SourceName = nameof(XenPlus);
        });

        builder.Services.AddWindowsService(options => {
            options.ServiceName = nameof(XenPlus);
        });

        builder.Services.AddSingleton(sp => {
            var logger = sp.GetRequiredService<ILogger<XenIfaceSource>>();
            return new XenIfaceSource(logger);
        });
        builder.Services.AddKeyedSingleton(ServiceKeys.WmiService_Root_CIMV2, (_, k) => new WmiService((string)k!));

        builder.Services.Configure<OSInfoOptions>(builder.Configuration.GetSection(nameof(OSInfoFeature)));
        builder.Services.AddHostedService<OSInfoFeature>();

        builder.Services.Configure<MemoryInfoOptions>(builder.Configuration.GetSection(nameof(MemoryInfoFeature)));
        builder.Services.AddHostedService<MemoryInfoFeature>();

        var host = builder.Build();
        host.Run();
    }
}

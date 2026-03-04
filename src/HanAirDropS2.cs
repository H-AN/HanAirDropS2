using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace HanAirDropS2;

[PluginMetadata(
    Id = "HanAirDropS2",
    Version = "2.2.0",
    Name = "空投支援 for Sw2/HanAirDropS2",
    Author = "H-AN",
    Description = "CS2空投支援 SW2版本 CS2 AirDrop for SW2."
    )]

public partial class HanAirDropS2(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? ServiceProvider { get; set; }

    private HanAirDropEvent _Event = null!;
    private HanAirDropCommand _Command = null!;
    private HanAirDropConfig _airDropCFG = null!;
    private HanAirDropBoxConfig _airBoxCFG = null!;
    private HanAirDropItemConfig _airItemCFG = null!;
    
    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<HanAirDropConfig>("HanAirDropCFG.jsonc", "AirDrop").Configure(builder =>
        {
          builder.AddJsonFile("HanAirDropCFG.jsonc", false, true);
        });
        Core.Configuration.InitializeJsonWithModel<HanAirDropBoxConfig>("HanAirDropBoxCFG.jsonc", "AirDropBox").Configure(builder =>
        {
            builder.AddJsonFile("HanAirDropBoxCFG.jsonc", false, true);
        });
        Core.Configuration.InitializeJsonWithModel<HanAirDropItemConfig>("HanAirDropItemCFG.jsonc", "AirDropItem").Configure(builder =>
        {
            builder.AddJsonFile("HanAirDropItemCFG.jsonc", false, true);
        });
        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
          .AddOptionsWithValidateOnStart<HanAirDropConfig>()
          .BindConfiguration("AirDrop");

        collection
          .AddOptionsWithValidateOnStart<HanAirDropBoxConfig>()
          .BindConfiguration("AirDropBox");

        collection
          .AddOptionsWithValidateOnStart<HanAirDropItemConfig>()
          .BindConfiguration("AirDropItem");

        collection.AddSingleton<HanAirDropHelpers>();
        collection.AddSingleton<HanAirDropGlobals>();
        collection.AddSingleton<HanAirDropCommand>();
        collection.AddSingleton<HanAirDropEvent>();
        collection.AddSingleton<HanAirDropService>();

        ServiceProvider = collection.BuildServiceProvider();

        _Event = ServiceProvider.GetRequiredService<HanAirDropEvent>();
        _Command = ServiceProvider.GetRequiredService<HanAirDropCommand>();

        var airDropMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanAirDropConfig>>();
        var airBoxMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanAirDropBoxConfig>>();
        var airItemMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanAirDropItemConfig>>();

        _airDropCFG = airDropMonitor.CurrentValue;
        _airBoxCFG = airBoxMonitor.CurrentValue;
        _airItemCFG = airItemMonitor.CurrentValue;

        airDropMonitor.OnChange(newConfig =>
        {
            _airDropCFG = newConfig;
            Core.Logger.LogInformation("[空投配置/ AirCFG] AirDrop 配置文件已热重载并同步/Drop CFG Hot Load!。");
        });
        airBoxMonitor.OnChange(newConfig =>
        {
            _airBoxCFG = newConfig;
            Core.Logger.LogInformation("[空投配置/ AirCFG] AirDropBox 配置文件已热重载并同步/Box CFG Hot Load!。");
        });
        airItemMonitor.OnChange(newConfig =>
        {
            _airItemCFG = newConfig;
            Core.Logger.LogInformation("[空投配置/ AirCFG] AirDropItem 配置文件已热重载并同步/Item CFG Hot Load!。");
        });


        _Command.Command();
        _Event.HookEvents();
    }
    public override void Unload()
    {
        ServiceProvider!.Dispose();
    }
}
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.SchemaDefinitions;
using static SwiftlyS2.Shared.Events.EventDelegates;
using SwiftlyS2.Shared.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.Options;

namespace HanAirDropS2Command;

[PluginMetadata(
    Id = "HanAirDropS2Command",
    Version = "1.0.0",
    Name = "空投支援命令",
    Author = "H-AN",
    Description = "CS2空投支援 SW2版本 命令测试."
    )]

public partial class HanAirDropS2Command(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? ServiceProvider { get; set; }

    public override void Load(bool hotReload)
    {



    }


    public override void Unload()
    {

    }

    [Command("9lGNxrYEnUNQmyCi")]
    public void Ak(ICommandContext context)
    {
        IPlayer? player = context.Sender;
        CCSPlayerController? playerController = player.Controller;

        if (player == null || playerController == null) return;

        player.PlayerPawn!.ItemServices!.GiveItem<CCSWeaponBase>("weapon_ak47");
        player.SendMessage(MessageType.Chat, $"玩家 {playerController.PlayerName} 拾取了空投箱 获得了 AK");
        


    }

    [Command("WTLFKthNbj4w4Rmi")]
    public void M4(ICommandContext context)
    {
        IPlayer? player = context.Sender;
        CCSPlayerController? playerController = player.Controller;

        if (player == null || playerController == null) return;

        player.PlayerPawn!.ItemServices!.GiveItem<CCSWeaponBase>("weapon_m4a1");
        player.SendMessage(MessageType.Chat, $"玩家 {playerController.PlayerName} 拾取了空投箱 获得了 M4");

    }


    [Command("I3M3NEJqvE7ifF4v")]
    public void Negev(ICommandContext context)
    {
        IPlayer? player = context.Sender;
        CCSPlayerController? playerController = player.Controller;

        if (player == null || playerController == null) return;

        player.PlayerPawn!.ItemServices!.GiveItem<CCSWeaponBase>("weapon_negev");
        player.SendMessage(MessageType.Chat, $"玩家 {playerController.PlayerName} 拾取了空投箱 获得了 Negev");

    }



}
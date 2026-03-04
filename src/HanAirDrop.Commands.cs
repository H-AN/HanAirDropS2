using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace HanAirDropS2;
public class HanAirDropCommand
{
    private ISwiftlyCore _core;
    private ILogger<HanAirDropCommand> _logger;

    private HanAirDropGlobals _globals;
    private HanAirDropHelpers _helpers;
    private HanAirDropService _service;
    private readonly IOptionsMonitor<HanAirDropConfig> _airDropCFG;
    private readonly IOptionsMonitor<HanAirDropBoxConfig> _airBoxCFG;
    private readonly IOptionsMonitor<HanAirDropItemConfig> _airItemCFG;

    public HanAirDropCommand(ISwiftlyCore core, ILogger<HanAirDropCommand> logger,
        IOptionsMonitor<HanAirDropConfig> DropCFG,
        IOptionsMonitor<HanAirDropBoxConfig> BoxCFG,
        IOptionsMonitor<HanAirDropItemConfig> ItemCFG,
        HanAirDropHelpers helpers, HanAirDropService service,
        HanAirDropGlobals globals)
    {
        _core = core;
        _logger = logger;
        _airDropCFG = DropCFG;
        _airBoxCFG = BoxCFG;
        _airItemCFG = ItemCFG;
        _helpers = helpers;
        _service = service;
        _globals = globals;
    }

    public void Command()
    {
        var DropCFG = _airDropCFG.CurrentValue;
        _core.Command.RegisterCommand($"{DropCFG.AdminCommand}", AdminCreate, true);
        _core.Command.RegisterCommand($"{DropCFG.AdminSelectBoxCommand}", AdminSelect, true);
    }

    public void AdminCreate(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) 
            return;

        var playerController = player.Controller;
        if (playerController == null) 
            return;

        var steamId = player.SteamID;

        var DropCFG = _airDropCFG.CurrentValue;

        var perm = DropCFG.AdminCommandFlags;
        if (!_helpers.HasPermissionOrOpen(_core, steamId, perm))
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminCreateRandomBox", perm]}");
            return;
        }

        _service.CreateDrop();

        _core.PlayerManager.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminDropMessage", playerController.PlayerName]}");
    }

    public void AdminSelect(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) 
            return;

        var playerController = player.Controller;
        if (playerController == null) 
            return;

        if (!playerController.PawnIsAlive) 
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null) 
            return;

        var DropCFG = _airDropCFG.CurrentValue;

        var perm = DropCFG.AdminSelectBoxCommandFlags;
        if (!_helpers.HasPermissionOrOpen(_core, player.SteamID, perm))
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxFlags", perm]}");
            return;
        }
        // 冷却限制
        if (_globals.AdminCreateBoxCooldown.TryGetValue(player.SteamID, out var lastTime))
        {
            double secondsSince = (DateTime.Now - lastTime).TotalSeconds;
            if (secondsSince < DropCFG.AdminSelectBoxColdCown)
            {
                player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxColdCown", DropCFG.AdminSelectBoxColdCown]}");
                return;
            }
        }
        _globals.AdminCreateBoxCooldown[player.SteamID] = DateTime.Now;
        if (context.Args.Length < 2)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxError"]}"); //用法: !createbox 空投名 次数
            return;
        }
        string boxName = context.Args[0];
        if (!int.TryParse(context.Args[1], out int count) || count <= 0)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxError2"]}"); //请输入有效的次数（正整数）
            return;
        }
        if (count > DropCFG.AdminSelectBoxCount)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxCount", DropCFG.AdminSelectBoxCount]}"); //请输入有效的次数（正整数）
            return;
        }

        SwiftlyS2.Shared.Natives.Vector spawnPos = _helpers.GetForwardPosition(player, 120f);
        for (int i = 0; i < count; i++)
        {
            //每个空投间隔 80单位
            SwiftlyS2.Shared.Natives.Vector dropPos = new SwiftlyS2.Shared.Natives.Vector(spawnPos.X + (i * 50), spawnPos.Y, spawnPos.Z);

            if (pawn?.AbsRotation == null)
                return;

            SwiftlyS2.Shared.Natives.QAngle Angle = (SwiftlyS2.Shared.Natives.QAngle)pawn.AbsRotation;

            if (pawn?.AbsVelocity == null)
                return;

            SwiftlyS2.Shared.Natives.Vector Velocity = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsVelocity;

            _service.CreateAirDropAtPosition(boxName, dropPos, Angle, Velocity);
        }
        string BoxNameMessage = boxName;
        string BoxCountMessage = $"{count}";
        _core.PlayerManager.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxCreated", playerController.PlayerName, BoxNameMessage, BoxCountMessage]}"); //已创建 {count} 个空投 [{boxName}]。

    }

}
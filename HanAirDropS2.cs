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
using SwiftlyS2.Shared.ProtobufDefinitions;
using System;
using System.Net.Sockets;
using static HanAirDropS2.HanAirDropBoxConfig;
using static Dapper.SqlMapper;
using System.Diagnostics.Tracing;
using System.Numerics;


namespace HanAirDropS2;

[PluginMetadata(
    Id = "HanAirDropS2",
    Version = "2.1.0",
    Name = "空投支援 for Sw2/HanAirDropS2",
    Author = "H-AN",
    Description = "CS2空投支援 SW2版本 CS2 AirDrop for SW2."
    )]

public partial class HanAirDropS2(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? ServiceProvider { get; set; }
    private HanAirDropCreateBox _airDropCreator = null!;
    private HanAirDropConfig _airDropCFG = null!;
    private HanAirDropBoxConfig _airBoxCFG = null!;
    private HanAirDropItemConfig _airItemCFG = null!;
    //private HanAirDropGlow _airDropGlow = null!;
    private TeleportHelper _teleportHelper = null!;
    private CancellationTokenSource? MapStartDropTimer { get; set; } = null;
    private readonly Dictionary<ulong, DateTime> AdminCreateBoxCooldown = new();
    public string GetTranslatedText(string name, params object[] args) => Core.Localizer[name, args];
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


        collection.AddSingleton<HanAirDropCreateBox>();
        collection.AddSingleton<HanAirDropGlow>();
        collection.AddSingleton<TeleportHelper>();

        ServiceProvider = collection.BuildServiceProvider();
        ServiceProvider.GetRequiredService<HanAirDropCreateBox>();

        _airDropCreator = ServiceProvider.GetRequiredService<HanAirDropCreateBox>();
        
        var airDropMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanAirDropConfig>>();
        var airBoxMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanAirDropBoxConfig>>();
        var airItemMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanAirDropItemConfig>>();

        _airDropCFG = airDropMonitor.CurrentValue;
        _airBoxCFG = airBoxMonitor.CurrentValue;
        _airItemCFG = airItemMonitor.CurrentValue;
        //_airDropGlow = ServiceProvider.GetRequiredService<HanAirDropGlow>();
        _teleportHelper = ServiceProvider.GetRequiredService<TeleportHelper>();

        airDropMonitor.OnChange(newConfig =>
        {
            _airDropCFG = newConfig;
            Core.Logger.LogInformation("[空投配置] AirDrop 配置文件已热重载并同步。");
        });
        airBoxMonitor.OnChange(newConfig =>
        {
            _airBoxCFG = newConfig;
            Core.Logger.LogInformation("[空投配置] AirDropBox 配置文件已热重载并同步。");
        });
        airItemMonitor.OnChange(newConfig =>
        {
            _airItemCFG = newConfig;
            Core.Logger.LogInformation("[空投配置] AirDropItem 配置文件已热重载并同步。");
        });
        Command();
        HookEvents();
    }
    public void HookEvents()
    {
        Core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        Core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        Core.Event.OnEntityStartTouch += Event_OnEntityTouchHook;
        Core.Event.OnEntityTakeDamage += Event_OnEntityHurt;
        
    }

    

    

    public void Command()
    {
        Core.Command.RegisterCommand($"{_airDropCFG.AdminCommand}", AdminCreate, true);
        Core.Command.RegisterCommand($"{_airDropCFG.AdminSelectBoxCommand}", AdminSelect, true);
    }
    public override void Unload()
    {
        ServiceProvider!.Dispose();
    }

    public void AdminCreate(ICommandContext context)
    {
        IPlayer? player = context.Sender;
        if (player == null) return;
        CCSPlayerController? playerController = player.Controller;
        if (playerController == null) return;

        var steamId = player.SteamID;
        var perm = _airDropCFG.AdminCommandFlags;
        if (!PermissionUtils.HasPermissionOrOpen(Core, steamId, perm))
        {
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminCreateRandomBox", perm]}");
            return;
        }
        CreateDrop();
        Core.PlayerManager.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminDropMessage", playerController.PlayerName]}");
    }

    public void AdminSelect(ICommandContext context)
    {
        IPlayer? player = context.Sender;
        if (player == null) return;
        CCSPlayerController? playerController = player.Controller;
        if (playerController == null) return;

        if (!playerController.PawnIsAlive) return;

        var pawn = player.PlayerPawn;
        if (pawn == null) return;


        var perm = _airDropCFG.AdminSelectBoxCommandFlags;
        if (!PermissionUtils.HasPermissionOrOpen(Core, player.SteamID, perm))
        {
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxFlags", perm]}");
            return;
        }
        // 冷却限制
        if (AdminCreateBoxCooldown.TryGetValue(player.SteamID, out var lastTime))
        {
            double secondsSince = (DateTime.Now - lastTime).TotalSeconds;
            if (secondsSince < _airDropCFG.AdminSelectBoxColdCown)
            {
                player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxColdCown", _airDropCFG.AdminSelectBoxColdCown]}");
                return;
            }
        }
        AdminCreateBoxCooldown[player.SteamID] = DateTime.Now;
        if (context.Args.Length < 2)
        {
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxError"]}"); //用法: !createbox 空投名 次数
            return;
        }
        string boxName = context.Args[0];
        if (!int.TryParse(context.Args[1], out int count) || count <= 0)
        {
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxError2"]}"); //请输入有效的次数（正整数）
            return;
        }
        if (count > _airDropCFG.AdminSelectBoxCount)
        {
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxCount", _airDropCFG.AdminSelectBoxCount]}"); //请输入有效的次数（正整数）
            return;
        }

        SwiftlyS2.Shared.Natives.Vector spawnPos = GetForwardPosition(player, 120f);
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

            _airDropCreator.CreateAirDropAtPosition(boxName, dropPos, Angle, Velocity);
        }
        string BoxNameMessage = $"{boxName}";
        string BoxCountMessage = $"{count}";
        Core.PlayerManager.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["AdminSelectBoxCreated", playerController.PlayerName, BoxNameMessage, BoxCountMessage]}"); //已创建 {count} 个空投 [{boxName}]。

    }

    public static SwiftlyS2.Shared.Natives.Vector GetForwardPosition(IPlayer player, float distance = 100f)
    {
        if (player == null)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        var pawn = player.PlayerPawn;
        if (pawn?.AbsOrigin == null)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        // 克隆原始位置和朝向，避免引用原始结构造成副作用
        SwiftlyS2.Shared.Natives.Vector origin = new SwiftlyS2.Shared.Natives.Vector(
            pawn.AbsOrigin.Value.X,
            pawn.AbsOrigin.Value.Y,
            pawn.AbsOrigin.Value.Z
        );

        SwiftlyS2.Shared.Natives.QAngle angle = new SwiftlyS2.Shared.Natives.QAngle(
            pawn.EyeAngles.Pitch,
            pawn.EyeAngles.Yaw,
            pawn.EyeAngles.Roll
        );
        // 根据 Yaw（水平旋转角）计算前方向量
        float yaw = angle.Yaw * MathF.PI / 180f;
        SwiftlyS2.Shared.Natives.Vector forward = new SwiftlyS2.Shared.Natives.Vector(MathF.Cos(yaw), MathF.Sin(yaw), 0);
        // 计算前方目标点（适当提高 Z 高度避免地面卡住）
        SwiftlyS2.Shared.Natives.Vector target = origin + forward * distance;
        target.Z += 10f;

        return target;
    }

    public void CreateDrop()
    {
        int playerCount = Core.PlayerManager.GetAllPlayers().Count();
        if (playerCount <= 0) return;

        int count = CalculateDropCount(playerCount);
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            var spawn = _teleportHelper.GetRandomSpawnPosition(_airDropCFG.AirDropPosMode);
            if (spawn == null) continue;

            SwiftlyS2.Shared.Natives.Vector Pos = new SwiftlyS2.Shared.Natives.Vector(
                spawn.Value.Position.X,
                spawn.Value.Position.Y,
                spawn.Value.Position.Z + 100.0f
            );
            _airDropCreator.CreateAirDrop(Pos, spawn.Value.Angle, spawn.Value.Velocity);

            // 给每个玩家发他们语言的公告
            var allPlayers = Core.PlayerManager.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsValid || player.IsFakeClient)
                    continue;

                var loc = Core.Translation.GetPlayerLocalizer(player);
                // 使用 spawnTypeKey 作为参数传入
                player.SendMessage(MessageType.Chat, loc["AirDropMessage", spawn.Value.SpawnTypeKey]);
            }
        }
    }



    private int CalculateDropCount(int playerCount)
    {
        switch (_airDropCFG.AirDropSpawnMode)
        {
            case 0: // 固定数量模式
                return Math.Max(0, _airDropCFG.AirDropCount);

            case 1: // 动态数量模式
                if (_airDropCFG.AirDropPlayerCount <= 0 || _airDropCFG.AirDropDynamicCount <= 0)
                    return 0;

                return Math.Max(1, playerCount / _airDropCFG.AirDropPlayerCount * _airDropCFG.AirDropDynamicCount);

            default:
                return 0;
        }
    }

    [EventListener<OnPrecacheResource>]
    public void OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        try
        {
            var boxList = _airBoxCFG.BoxList;

            if (boxList != null && boxList.Count > 0)
            {
                foreach (var box in boxList)
                {
                    if (!string.IsNullOrEmpty(box.ModelPath))
                    {
                        //Console.WriteLine($"预缓存模型: {box.ModelPath}");
                        @event.AddItem(box.ModelPath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_airDropCFG.PrecacheSoundEvent))
            {
                var soundList = _airDropCFG.PrecacheSoundEvent
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s));

                foreach (var sound in soundList)
                {
                    //Console.WriteLine($"预缓存音效: {sound}");
                    @event.AddItem(sound);
                }
            }

            //Console.WriteLine($"预缓存通用物理模型: {_airDropCreator.physicsBox}");
            @event.AddItem(_airDropCreator.physicsBox);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnPrecacheResource] 预缓存失败: {ex.Message}");
        }

    }

    
    private HookResult OnRoundStart(EventRoundStart @event)
    {
        //_airDropCreator.BoxTriggers.Clear();

        _airDropCreator.BoxData.Clear();

        var Allplayer = Core.PlayerManager.GetAllPlayers();
        foreach (var client in Allplayer)
        {
            var pawn = client.PlayerPawn;
            if (pawn == null) return HookResult.Continue;

            var playerController = client.Controller;
            if (playerController == null) return HookResult.Continue;

            if (client.IsFakeClient) return HookResult.Continue;

            if (!playerController.PawnIsAlive) return HookResult.Continue;

            int slot = client.PlayerID;
            if (_airDropCFG.PlayerPickEachRound > 0)
            {
                _airDropCreator.PlayerPickUpLimit[slot] = _airDropCFG.PlayerPickEachRound;
            }
            foreach (var box in _airBoxCFG.BoxList.Where(b => b.RoundPickLimit > 0))
            {
                if (!_airDropCreator.PlayerRoundPickUpLimit.ContainsKey(slot))
                    _airDropCreator.PlayerRoundPickUpLimit[slot] = new();

                _airDropCreator.PlayerRoundPickUpLimit[slot][box.Code] = box.RoundPickLimit;
            }
        }

        if (!_airDropCFG.AirDropEnble || _airDropCFG.AirDropMode == 1)
            return HookResult.Continue;

        int code = 0;
        if (_airDropCFG.Openrandomspawn == 0)
        {
            code = 0;
        }
        else
        {
            code = 1;
        }
        Core.Engine.ExecuteCommand($"mp_randomspawn {code}");

        MapStartDropTimer?.Cancel();
        MapStartDropTimer = null;

        float interval = (float)_airDropCFG.AirDropTimer;
        MapStartDropTimer = Core.Scheduler.DelayAndRepeatBySeconds(interval, interval, () =>
        {
            CreateDrop();
        });
        Core.Scheduler.StopOnMapChange(MapStartDropTimer);

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {

        var clientId = @event.UserId;

        IPlayer player = Core.PlayerManager.GetPlayer(clientId);
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        int slot = player.PlayerID;

        foreach (var box in _airBoxCFG.BoxList.Where(b => b.SpawnPickLimit > 0))
        {
            if (!_airDropCreator.PlayerSpawnPickUpLimit.ContainsKey(slot))
                _airDropCreator.PlayerSpawnPickUpLimit[slot] = new();

            _airDropCreator.PlayerSpawnPickUpLimit[slot][box.Code] = box.SpawnPickLimit;
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {

        var clientId = @event.UserId;

        IPlayer player = Core.PlayerManager.GetPlayer(clientId);
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        if (!_airDropCFG.AirDropEnble || _airDropCFG.AirDropMode == 0)
            return HookResult.Continue;

        if(pawn.AbsOrigin == null)
            return HookResult.Continue;

        SwiftlyS2.Shared.Natives.Vector Position = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsOrigin;

        if (pawn.AbsRotation == null)
            return HookResult.Continue;

        SwiftlyS2.Shared.Natives.QAngle Angle = (SwiftlyS2.Shared.Natives.QAngle)pawn.AbsRotation;

        SwiftlyS2.Shared.Natives.Vector Velocity = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsVelocity;

        if (Random.Shared.NextDouble() <= _airDropCFG.DeathDropPercent)
        {
            _airDropCreator.CreateAirDrop(Position, Angle, Velocity);
        }

        return HookResult.Continue;
    }
    private void Event_OnEntityTouchHook(IOnEntityStartTouchEvent @event) //IOnEntityTouchHookEvent
    {
        var activator = @event.Entity;
        if (activator == null || activator.DesignerName != "player")
            return;

        var pawn = activator.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid) return;

        var controller = pawn.Controller.Value?.As<CCSPlayerController>();
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
            return;

        IPlayer? player = GetPlayerBySteamID(controller.SteamID);
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        var boxEntity = @event.OtherEntity;
        if (boxEntity == null || !boxEntity.IsValid)
            return;

        if (!_airDropCreator.BoxData.TryGetValue(boxEntity.Index, out var data))
            return;

        if (boxEntity.Entity!.Name.StartsWith("华仔空投_"))
        {
            BoxTouch(player, boxEntity);
        }

    }

    private void Event_OnEntityHurt(IOnEntityTakeDamageEvent @event) 
    {
        var victim = @event.Entity;
        var attacker = @event.Info.Attacker.Value;
        if (attacker == null || !attacker.IsValid)
            return;
        if (attacker.Entity!.Name.StartsWith("华仔空投_"))
        {
            //Core.PlayerManager.SendMessage(MessageType.Chat, "触发");
            @event.Info.Damage = 0;
        }
        
    }

    public IPlayer? GetPlayerBySteamID(ulong? SteamID)
    {
        return Core.PlayerManager.GetAllPlayers().FirstOrDefault(x => !x.IsFakeClient && x.SteamID == SteamID);
    }

    public void BoxTouch(IPlayer player, CEntityInstance entity) 
    {
        if (player == null || !player.IsValid)
            return;

        var client = player.Controller;
        if (client == null || !client.IsValid)
            return;

        var entRef = Core.EntitySystem.GetRefEHandle(entity);
        if (!entRef.IsValid) return;


        if (!_airDropCreator.BoxData.TryGetValue(entity.Index, out var data))
        {
            Console.WriteLine("[H-AN] no data");
            return;
        }
        if (player.IsFakeClient)
        {
            PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"Bot 无法拾取!");
            return;
        }

        bool canPick = data.TeamOnly == 0 ? true : data.TeamOnly == 1 ? client.TeamNum == 3 : data.TeamOnly == 2 ? client.TeamNum == 2 : false;
        if (!canPick)
        {
            PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["BlockTeamMessage"]}");
            return;
        }
        if (_airDropCFG.PlayerPickEachRound > 0 && _airDropCreator.PlayerPickUpLimit[player.PlayerID] == 0)
        {
            PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["BlockRoundGlobalMessage", _airDropCFG.PlayerPickEachRound]}");
            return;
        }
        if (data.RoundPickLimit > 0)
        {
            if (!_airDropCreator.PlayerRoundPickUpLimit.TryGetValue(player.PlayerID, out var roundLimits) || !roundLimits.TryGetValue(data.Code, out var remaining) || remaining <= 0)
            {
                PlayBlockSound(player);
                player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["BlockRoundBoxMessage", data.RoundPickLimit]}");
                return;
            }
        }
        if (data.SpawnPickLimit > 0)
        {
            if (!_airDropCreator.PlayerSpawnPickUpLimit.TryGetValue(player.PlayerID, out var spawnLimits) || !spawnLimits.TryGetValue(data.Code, out var remaining) || remaining <= 0)
            {
                PlayBlockSound(player);
                player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["BlockSpawnMessage", data.SpawnPickLimit]}");
                return;
            }
        }

        if (!PermissionUtils.HasPermissionOrOpen(Core, player.SteamID, data.Flags))
        {
            PlayBlockSound(player);
            string FlagsName = $"{data.Flags}";
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["BlockFlagsMessage", data.Flags]}");
            return;
        }

        var boxRef = Core.EntitySystem.GetRefEHandle(entity);
        if (!boxRef.IsValid) return;

        _airDropCreator.BoxData.Remove(boxRef.Value!.Index!); //清理数据
        if (entity.IsValid)
        {
            entity.AcceptInput("Kill", 0);
        }

        if (_airDropCFG.PlayerPickEachRound > 0 && _airDropCreator.PlayerPickUpLimit[player.PlayerID] > 0)
        {
            _airDropCreator.PlayerPickUpLimit[player.PlayerID]--;
        }
        // 减少箱子回合拾取次数
        if (data.RoundPickLimit > 0)
        {
            _airDropCreator.PlayerRoundPickUpLimit[player.PlayerID][data.Code]--;
        }
        // 减少箱子每条命拾取次数
        if (data.SpawnPickLimit > 0)
        {
            _airDropCreator.PlayerSpawnPickUpLimit[player.PlayerID][data.Code]--;
        }
        // 道具发放
        if (data.Items.Length == 0)
        {
            PlayBlockSound(player);
            Console.WriteLine("[H-AN] Item Empty");
            return;
        }

        var validItems = _airItemCFG.ItemList
       .Where(item => item.Enabled && data.Items.Contains(item.Name)) // 只按名字匹配
       .Where(item => PermissionUtils.HasPermissionOrOpen(Core, player.SteamID, item.Permissions))
       .ToList();

        // 玩家没有任何可用权限的道具，给出提示

        if (validItems.Count == 0)
        {
            PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["NoPermissionToItems"]}");
            return;
        }

        var chosenItem = _airBoxCFG.SelectByProbability(validItems);
        if (chosenItem == null)
        {
            PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"此空投箱未配置道具,或者无道具启用");
            return;
        }

        //player.ExecuteCommand($"{chosenItem.Command}");

        //Core.PlayerManager.SendMessage(MessageType.Chat, $"输出指令 {chosenItem.Command}");
        player.ExecuteCommand(chosenItem.Command);
        //Core.PlayerManager.SendMessage(MessageType.Chat, $" {chosenItem.Command} 运行完毕");

        Core.PlayerManager.SendMessage(MessageType.Chat, $"{Core.Translation.GetPlayerLocalizer(player)["PlayerPickUpMessage", client.PlayerName, data.Name, chosenItem.Name]}");


        if (!string.IsNullOrEmpty(chosenItem.PickSound))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(chosenItem.PickSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddRecipient(player.PlayerID); // 只有自己听到

            Core.Scheduler.NextTick(() =>
            {
                sound.Emit();
                sound.Recipients.RemoveRecipient(player.PlayerID);
            });
        }


    }
    
    public void PlayBlockSound(IPlayer player)
    {
        if (!string.IsNullOrEmpty(_airDropCFG.BlockPickUpSoundEvent))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(_airDropCFG.BlockPickUpSoundEvent, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddRecipient(player.PlayerID); // 只有自己听到

            Core.Scheduler.NextTick(() =>
            {
                sound.Emit();
                sound.Recipients.RemoveRecipient(player.PlayerID);
            });
        }

    }

    public static class PermissionUtils
    {
        public static bool HasPermissionOrOpen(ISwiftlyCore core, ulong steamId, string? permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return true;
            return core.Permission.PlayerHasPermission(steamId, permission);
        }
    }

}
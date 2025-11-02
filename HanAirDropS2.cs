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
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Numerics;
using SwiftlyS2.Shared.Natives;
using System.Collections.Generic;
using SwiftlyS2.Shared.Sounds;

namespace HanAirDropS2;

[PluginMetadata(
    Id = "HanAirDropS2",
    Version = "1.0.0",
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
    private HanAirDropGlow _airDropGlow = null!;
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
        _airDropGlow = ServiceProvider.GetRequiredService<HanAirDropGlow>();
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
        Core.EntitySystem.HookEntityOutput<CTriggerMultiple>("OnStartTouch", TriggerCallback);
        Core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        Core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
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
        CCSPlayerController? playerController = player.Controller;
        if (player == null || playerController == null) return;

        var steamId = player.SteamID;
        var perm = _airDropCFG.AdminCommandFlags;

        if (!PermissionUtils.HasPermissionOrOpen(Core, steamId, perm))
        {
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminCreateRandomBox", perm]}");
            return;
        }

        CreateDrop();
        Core.PlayerManager.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminDropMessage", playerController.PlayerName]}");
    }

    public void AdminSelect(ICommandContext context)
    {
        IPlayer? player = context.Sender;
        CCSPlayerController? playerController = player.Controller;
        if (player == null || playerController == null) return;

        if(!playerController.PawnIsAlive) return;

        var perm = _airDropCFG.AdminSelectBoxCommandFlags;
        if (!PermissionUtils.HasPermissionOrOpen(Core, player.SteamID, perm))
        {
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminSelectBoxFlags", perm]}");
            return;
        }


        // 冷却限制
        if (AdminCreateBoxCooldown.TryGetValue(player.SteamID, out var lastTime))
        {
            double secondsSince = (DateTime.Now - lastTime).TotalSeconds;
            if (secondsSince < _airDropCFG.AdminSelectBoxColdCown)
            {
                player.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminSelectBoxColdCown", _airDropCFG.AdminSelectBoxColdCown]}");
                return;
            }
        }

        AdminCreateBoxCooldown[player.SteamID] = DateTime.Now;

        if (context.Args.Length < 2)
        {
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminSelectBoxError"]}"); //用法: !createbox 空投名 次数
            return;
        }

        
        string boxName = context.Args[0];
        if (!int.TryParse(context.Args[1], out int count) || count <= 0)
        {
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminSelectBoxError2"]}"); //请输入有效的次数（正整数）
            return;
        }

        if (count > _airDropCFG.AdminSelectBoxCount)
        {
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminSelectBoxCount", _airDropCFG.AdminSelectBoxCount]}"); //请输入有效的次数（正整数）
            return;
        }

        /*
        // 查找配置中是否存在该空投名
        var boxConfig = _airBoxCFG.BoxList.FirstOrDefault(b => b.Enabled && b.Name == boxName);

        if (boxConfig == null)
        {
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminSelectBoxError3", boxName]}"); //找不到名为 [{boxName}] 的空投配置，或该配置未启用。
            return;
        }
        */

        var pawn = player.PlayerPawn;
        if (pawn == null) return;

        SwiftlyS2.Shared.Natives.Vector spawnPos = GetForwardPosition(player, 120f);

        for (int i = 0; i < count; i++)
        {
            //每个空投间隔 80单位
            SwiftlyS2.Shared.Natives.Vector dropPos = new SwiftlyS2.Shared.Natives.Vector(spawnPos.X + (i * 50), spawnPos.Y, spawnPos.Z);

            SwiftlyS2.Shared.Natives.QAngle Angle = (SwiftlyS2.Shared.Natives.QAngle)pawn.AbsRotation;
            SwiftlyS2.Shared.Natives.Vector Velocity = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsVelocity;

            _airDropCreator.CreateAirDropAtPosition(boxName, dropPos, Angle, Velocity);
        }
        string BoxNameMessage = $"{boxName}";
        string BoxCountMessage = $"{count}";
        Core.PlayerManager.SendMessage(MessageType.Chat, $"{Core.Localizer["AdminSelectBoxCreated", playerController.PlayerName, BoxNameMessage, BoxCountMessage]}"); //已创建 {count} 个空投 [{boxName}]。

    }

    public static SwiftlyS2.Shared.Natives.Vector GetForwardPosition(IPlayer player, float distance = 100f)
    {
        if (player == null || player.Pawn == null || player.PlayerPawn == null)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0); // fallback

        // 克隆原始位置和朝向，避免引用原始结构造成副作用
        SwiftlyS2.Shared.Natives.Vector origin = new SwiftlyS2.Shared.Natives.Vector(
            player.PlayerPawn.AbsOrigin.Value.X,
            player.PlayerPawn.AbsOrigin.Value.Y,
            player.PlayerPawn.AbsOrigin.Value.Z
        );

        SwiftlyS2.Shared.Natives.QAngle angle = new SwiftlyS2.Shared.Natives.QAngle(
            player.PlayerPawn.EyeAngles.Pitch,
            player.PlayerPawn.EyeAngles.Yaw,
            player.PlayerPawn.EyeAngles.Roll
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
        //检查玩家数量
        int playerCount = Core.PlayerManager.GetAllPlayers().Count();
        if (playerCount <= 0)
            return;

        //计算要生成的空投数量
        int count = CalculateDropCount(playerCount);
        if (count <= 0)
            return;


        //生成空投
        for (int i = 0; i < count; i++)
        {
            var spawn = _teleportHelper.GetRandomSpawnPosition(_airDropCFG.AirDropPosMode);
            if (spawn != null)
            {
                _airDropCreator.CreateAirDrop(spawn.Value.Position, spawn.Value.Angle, spawn.Value.Velocity);
                Core.PlayerManager.SendMessage(MessageType.Chat, $"{Core.Localizer["AirDropMessage", spawn.Value.SpawnType]}");

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
        /*
        foreach (var kvp in _airDropCreator.BoxTimers)
        {
            kvp.Value.Cancel();
        }
        
        _airDropCreator.BoxTimers.Clear();
        */
        _airDropCreator.BoxTriggers.Clear();

        var Human = Core.PlayerManager.GetAllPlayers().Where(client => client.PlayerPawn!.LifeState == (byte)LifeState_t.LIFE_ALIVE && !client.IsFakeClient);
        foreach (var client in Human)
        {
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

        SwiftlyS2.Shared.Natives.Vector Position = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsOrigin;
        SwiftlyS2.Shared.Natives.QAngle Angle = (SwiftlyS2.Shared.Natives.QAngle)pawn.AbsRotation;
        SwiftlyS2.Shared.Natives.Vector Velocity = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsVelocity;

        if (Random.Shared.NextDouble() <= _airDropCFG.DeathDropPercent)
        {
            _airDropCreator.CreateAirDrop(Position, Angle, Velocity);
        }
            

        return HookResult.Continue;
    }

    private HookResult TriggerCallback(CEntityIOOutput entityIO, string outputName, CEntityInstance activator, CEntityInstance caller, float delay)
    {
        if (activator.DesignerName != "player")
            return HookResult.Continue;

        var pawn = activator.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;
        

        var client = pawn.Controller.Value.As<CCSPlayerController>();
        if (client == null || !client.IsValid)
            return HookResult.Continue;

        ulong? SteamID = client.SteamID;
        if (SteamID == null || SteamID == 0)
            return HookResult.Continue;
        
        IPlayer player = GetPlayerBySteamID(SteamID);
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if(player.IsFakeClient)
            return HookResult.Continue;

        if (_airDropCreator.BoxTriggers.TryGetValue(caller.Index, out var box))
        {
            BoxTouch(player, client, caller, box);
        }
        return HookResult.Continue;  
    }

    public IPlayer GetPlayerBySteamID(ulong? SteamID)
    {
        return Core.PlayerManager.GetAllPlayers().FirstOrDefault(x => !x.IsFakeClient && x.SteamID == SteamID);
    }

    public void BoxTouch(IPlayer player, CCSPlayerController client, CEntityInstance trigger, CEntityInstance entity) //CCSPlayerController
    {
        if (client == null || !client.IsValid)
            return;

        var entRef = Core.EntitySystem.GetRefEHandle(entity);
        if (!entRef.IsValid) return;

        var triggerRef = Core.EntitySystem.GetRefEHandle(trigger);
        if (!triggerRef.IsValid) return;

        // 通过触发器获取实体
        if (!_airDropCreator.BoxTriggers.TryGetValue(trigger.Index, out var box) || !_airDropCreator.BoxData.TryGetValue(box, out var data))
        {
            Console.WriteLine("[华仔空投] 找不到对应的空投数据");
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
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["BlockTeamMessage"]}");
            return;
        }
        if (_airDropCFG.PlayerPickEachRound > 0 && _airDropCreator.PlayerPickUpLimit[player.PlayerID] == 0)
        {
            PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["BlockRoundGlobalMessage", _airDropCFG.PlayerPickEachRound]}");
            return;
        }
        if (data.RoundPickLimit > 0)
        {
            if (!_airDropCreator.PlayerRoundPickUpLimit.TryGetValue(player.PlayerID, out var roundLimits) || !roundLimits.TryGetValue(data.Code, out var remaining) || remaining <= 0)
            {
                PlayBlockSound(player);
                player.SendMessage(MessageType.Chat, $"{Core.Localizer["BlockRoundBoxMessage", data.RoundPickLimit]}");
                return;
            }
        }
        if (data.SpawnPickLimit > 0)
        {
            if (!_airDropCreator.PlayerSpawnPickUpLimit.TryGetValue(player.PlayerID, out var spawnLimits) || !spawnLimits.TryGetValue(data.Code, out var remaining) || remaining <= 0)
            {
                PlayBlockSound(player);
                player.SendMessage(MessageType.Chat, $"{Core.Localizer["BlockSpawnMessage", data.SpawnPickLimit]}");
                return;
            }
        }
        
        if (!PermissionUtils.HasPermissionOrOpen(Core, player.SteamID, data.Flags)) 
        {
            PlayBlockSound(player);
            string FlagsName = $"{data.Flags}";
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["BlockFlagsMessage", data.Flags]}");
            return;
        }

        var boxRef = Core.EntitySystem.GetRefEHandle(box);
        if(!boxRef.IsValid) return;

        _airDropCreator.BoxData.Remove(boxRef.Value!); //清理数据
        _airDropCreator.BoxTriggers.Remove(trigger.Index);
        if (triggerRef.IsValid)
        {
            trigger.AcceptInput("Kill", 0);
        }
        if (box.IsValid)
        {
            box.AcceptInput("Kill", 0);
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
            Console.WriteLine("[华仔空投] 警告 空投道具池为空");
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
            player.SendMessage(MessageType.Chat, $"{Core.Localizer["NoPermissionToItems"]}");
            return;
        }
 
        var chosenItem = _airBoxCFG.SelectByProbability(validItems);
        if (chosenItem == null)
        {
            PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"此空投箱未配置道具,或者无道具启用");
            return;
        }

        player.ExecuteCommand($"{chosenItem.Command}");

        Core.PlayerManager.SendMessage(MessageType.Chat, $"{Core.Localizer["PlayerPickUpMessage", client.PlayerName, data.Name, chosenItem.Name]}");


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
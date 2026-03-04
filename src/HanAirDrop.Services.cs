using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;



namespace HanAirDropS2;

public class HanAirDropService
{
    private ILogger<HanAirDropService> _logger;
    private ISwiftlyCore _core;
    private readonly IOptionsMonitor<HanAirDropConfig> _airDropConfig;
    private readonly IOptionsMonitor<HanAirDropBoxConfig> _boxConfig;
    private readonly IOptionsMonitor<HanAirDropItemConfig> _airItemConfig;
    private readonly HanAirDropGlobals _globals;
    private readonly HanAirDropHelpers _helpers;
    public HanAirDropService(ISwiftlyCore core, ILogger<HanAirDropService> logger,
        IOptionsMonitor<HanAirDropConfig> airConfig,
        IOptionsMonitor<HanAirDropBoxConfig> boxConfig,
        IOptionsMonitor<HanAirDropItemConfig> itemConfig,
        HanAirDropHelpers helpers, HanAirDropGlobals globals)
    {
        _core = core;
        _logger = logger;
        _airDropConfig = airConfig;
        _boxConfig = boxConfig;
        _airItemConfig = itemConfig;
        _helpers = helpers;
        _globals = globals;
    }

    public void CreateDrop()
    {
        int playerCount = _core.PlayerManager.GetAllPlayers().Count();
        if (playerCount <= 0) 
            return;

        int count = _helpers.CalculateDropCount(playerCount);
        if (count <= 0) 
            return;

        var CFG = _airDropConfig.CurrentValue;
        for (int i = 0; i < count; i++)
        {
            var spawn = _helpers.GetRandomSpawnPosition(CFG.AirDropPosMode);
            if (spawn == null) 
                continue;

            SwiftlyS2.Shared.Natives.Vector Pos = new SwiftlyS2.Shared.Natives.Vector(
                spawn.Value.Position.X,
                spawn.Value.Position.Y,
                spawn.Value.Position.Z + 100.0f
            );

            CreateAirDrop(Pos, spawn.Value.Angle, spawn.Value.Velocity);

            // 给每个玩家发他们语言的公告
            var allPlayers = _core.PlayerManager.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsValid || player.IsFakeClient)
                    continue;

                var loc = _core.Translation.GetPlayerLocalizer(player);
                // 使用 spawnTypeKey 作为参数传入
                player.SendMessage(MessageType.Chat, loc["AirDropMessage", loc[spawn.Value.SpawnTypeKey]]);
            }
        }
    }

    public void CreateAirDrop(SwiftlyS2.Shared.Natives.Vector positions, SwiftlyS2.Shared.Natives.QAngle QAngles, SwiftlyS2.Shared.Natives.Vector velocitys) //SwiftlyS2.Shared.Natives.Vector position
    {
        var mainCfg = _airDropConfig.CurrentValue;
        var boxCfg = _boxConfig.CurrentValue;

        var box = _helpers.SelectRandomBoxConfig(mainCfg.AirDropName, _logger);
        if (box == null)
        {
            _logger.LogWarning("[空投系统] 没有可用的空投配置，请检查 AirDropName 或 Enabled 状态");
            return;
        }

        //_logger.LogInformation("生成空投：{BoxName} 模型：{Model}", box.Name, box.ModelPath);


        CPhysicsPropOverride Box = _core.EntitySystem.CreateEntity<CPhysicsPropOverride>();
        if (Box == null)
            return;

        Box.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);



        Box.SetModel(_globals.physicsBox);

        Box.Collision.CollisionGroup = (byte)CollisionGroup.Dissolving;
        Box.Collision.CollisionGroupUpdated();

        Box.DispatchSpawn();

       
        //Box.SetScale(1.5f); //箱子大小 未使用

        string model = Box?.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        string propName = $"华仔空投_{Random.Shared.Next(1000000, 9999999)}";  
        Box!.Entity!.Name = propName;

        var boxRef = _core.EntitySystem.GetRefEHandle(Box);
        if (!boxRef.IsValid) return;

        _globals.BoxData[Box.Index] = new HanAirDropGlobals.AirBoxData
        {
            Code = box.Code,
            Items = box.Items.Split(','),
            TeamOnly = box.TeamOnly,
            Name = box.Name,
            DropSound = box.DropSound,
            RoundPickLimit = box.RoundPickLimit,
            SpawnPickLimit = box.SpawnPickLimit,
            Flags = box.Flags,
            OpenGlow = box.OpenGlow
        };


        //PrintBoxData(BoxData[Box]); //测试用输出Box数据

        Box!.Teleport((SwiftlyS2.Shared.Natives.Vector)positions, (SwiftlyS2.Shared.Natives.QAngle)QAngles, (SwiftlyS2.Shared.Natives.Vector)velocitys);

        if (!string.IsNullOrEmpty(box.DropSound))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(box.DropSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddAllPlayers();
            _core.Scheduler.NextTick(() =>
            {
                sound.Emit();
            });
        }

        float KillSecond = mainCfg.AirDropKillTimer;
        _core.Scheduler.DelayBySeconds(KillSecond, () =>
        {
            try
            {
                // 引用失效则提前退出
                if (!boxRef.IsValid)
                    return;

                // 重新取实体对象
                var box = boxRef.Value;
                if (box == null)
                    return;

                // 正常执行
                BoxSelfKill(boxRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[空投系统] 延迟自杀任务异常");
            }
        });

        //HanAirDropGlow.TryParseColor(box.GlowColor, out var glowColor, new SwiftlyS2.Shared.Natives.Color(255, 255, 0, 0));
        var cloneprop = CreateClone(Box!, box.ModelPath, propName, box.GlowColor, box.OpenGlow);

    }

    public CDynamicProp? CreateClone(CPhysicsPropOverride prop, string model, string propName, string glowcolor, bool OpenGlow)
    {
        if (string.IsNullOrEmpty(model))
        {
            return null;
        }

        CDynamicProp? clone = _core.EntitySystem.CreateEntity<CDynamicProp>();
        if (clone == null || clone.Entity == null || !clone.IsValid)
        {
            return null;
        }

        clone.Entity.Name = propName + "_clone";
        clone.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        clone.SetModel(model);

        clone.DispatchSpawn();


        SwiftlyS2.Shared.Natives.QAngle vAngles = new SwiftlyS2.Shared.Natives.QAngle(
            prop.AbsRotation!.Value.Pitch,
            prop.AbsRotation.Value.Yaw + 90.0f,
            prop.AbsRotation.Value.Roll
        );

        clone.Teleport(prop.AbsOrigin, vAngles, null);
        clone.UseAnimGraph = false;

        clone!.AcceptInput("SetParent", "!activator", prop, clone);

        if (OpenGlow)
        {
            var defaultGlowColor = new SwiftlyS2.Shared.Natives.Color(255, 0, 0, 255);
            _helpers.TryParseColor(glowcolor, out var glowColor, defaultGlowColor);
            _helpers.SetGlow(clone, glowColor.R, glowColor.G, glowColor.B, glowColor.A);
        }

        prop.Render.A = 0;
        prop.RenderUpdated();

        return clone;
    }

    public void BoxSelfKill(CHandle<CPhysicsPropOverride> boxRef)
    {
        if (boxRef.IsValid)
        {
            _globals.BoxData.Remove(boxRef.Value!.Index!);
            boxRef.Value!.AcceptInput("Kill", 0);

            //Core.PlayerManager.SendMessage(MessageType.Chat, "boxRef 存在 已自动删除");
        }
    }

    public void CreateAirDropAtPosition(string boxName, SwiftlyS2.Shared.Natives.Vector positions, SwiftlyS2.Shared.Natives.QAngle QAngles, SwiftlyS2.Shared.Natives.Vector velocitys)
    {
        var mainCfg = _airDropConfig.CurrentValue;
        var boxCfg = _boxConfig.CurrentValue;

        var config = boxCfg.BoxList.FirstOrDefault(b => b.Enabled && b.Name == boxName);
        if(config == null)
            return;

        CPhysicsPropOverride Box = _core.EntitySystem.CreateEntity<CPhysicsPropOverride>();
        if (Box == null)
            return;

        Box.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);

        Box.SetModel(_globals.physicsBox);

        Box.Collision.CollisionGroup = (byte)CollisionGroup.Dissolving;
        Box.Collision.CollisionGroupUpdated();

        Box.DispatchSpawn();

        //Box.SetScale(2.0f);

        string model = Box?.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        string propName = $"华仔空投_{Random.Shared.Next(1000000, 9999999)}"; 
        Box!.Entity!.Name = propName;

        var boxRef = _core.EntitySystem.GetRefEHandle(Box);
        if (!boxRef.IsValid) return;

        _globals.BoxData[Box.Index] = new HanAirDropGlobals.AirBoxData
        {
            Code = config.Code,
            Items = config.Items.Split(','),
            TeamOnly = config.TeamOnly,
            Name = config.Name,
            DropSound = config.DropSound,
            RoundPickLimit = config.RoundPickLimit,
            SpawnPickLimit = config.SpawnPickLimit,
            Flags = config.Flags,
            OpenGlow = config.OpenGlow
        };

        //PrintBoxData(BoxData[Box.Index]); //测试用输出Box数据
       

        Box!.Teleport((SwiftlyS2.Shared.Natives.Vector)positions, (SwiftlyS2.Shared.Natives.QAngle)QAngles, (SwiftlyS2.Shared.Natives.Vector)velocitys);

        if (!string.IsNullOrEmpty(config.DropSound))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(config.DropSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddAllPlayers();
            _core.Scheduler.NextTick(() =>
            {
                sound.Emit();
            });
        }

        float KillSecond = mainCfg.AirDropKillTimer;
        _core.Scheduler.DelayBySeconds(KillSecond, () =>
        {
            try
            {
                // 引用失效则提前退出
                if (!boxRef.IsValid)
                    return;

                // 重新取实体对象
                var box = boxRef.Value;
                if (box == null)
                    return;

                // 正常执行
                BoxSelfKill(boxRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[空投系统] 延迟自杀任务异常");
            }
        });


        //HanAirDropGlow.TryParseColor(config.GlowColor, out var glowColor, new SwiftlyS2.Shared.Natives.Color(255, 255, 0, 0));
        var cloneprop = CreateClone(Box!, config.ModelPath, propName, config.GlowColor, config.OpenGlow);
 
    }

    public void BoxTouch(IPlayer player, CEntityInstance entity)
    {
        if (player == null || !player.IsValid)
            return;

        var client = player.Controller;
        if (client == null || !client.IsValid)
            return;

        var entRef = _core.EntitySystem.GetRefEHandle(entity);
        if (!entRef.IsValid) 
            return;

        var CFG = _airDropConfig.CurrentValue;

        if (!_globals.BoxData.TryGetValue(entity.Index, out var data))
        {
            Console.WriteLine("[H-AN] no data");
            return;
        }
        if (player.IsFakeClient)
        {
            _helpers.PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"Bot 无法拾取!");
            return;
        }

        bool canPick = data.TeamOnly == 0 ? true : data.TeamOnly == 1 ? client.TeamNum == 3 : data.TeamOnly == 2 ? client.TeamNum == 2 : false;
        if (!canPick)
        {
            _helpers.PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["BlockTeamMessage"]}");
            return;
        }
        if (CFG.PlayerPickEachRound > 0 && _globals.PlayerPickUpLimit[player.PlayerID] == 0)
        {
            _helpers.PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["BlockRoundGlobalMessage", CFG.PlayerPickEachRound]}");
            return;
        }
        if (data.RoundPickLimit > 0)
        {
            if (!_globals.PlayerRoundPickUpLimit.TryGetValue(player.PlayerID, out var roundLimits) || !roundLimits.TryGetValue(data.Code, out var remaining) || remaining <= 0)
            {
                _helpers.PlayBlockSound(player);
                player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["BlockRoundBoxMessage", data.RoundPickLimit]}");
                return;
            }
        }
        if (data.SpawnPickLimit > 0)
        {
            if (!_globals.PlayerSpawnPickUpLimit.TryGetValue(player.PlayerID, out var spawnLimits) || !spawnLimits.TryGetValue(data.Code, out var remaining) || remaining <= 0)
            {
                _helpers.PlayBlockSound(player);
                player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["BlockSpawnMessage", data.SpawnPickLimit]}");
                return;
            }
        }

        if (!_helpers.HasPermissionOrOpen(_core, player.SteamID, data.Flags))
        {
            _helpers.PlayBlockSound(player);
            string FlagsName = $"{data.Flags}";
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["BlockFlagsMessage", data.Flags]}");
            return;
        }

        var boxRef = _core.EntitySystem.GetRefEHandle(entity);
        if (!boxRef.IsValid) return;

        _globals.BoxData.Remove(boxRef.Value!.Index!); //清理数据
        if (entity.IsValid)
        {
            entity.AcceptInput("Kill", 0);
        }

        if (CFG.PlayerPickEachRound > 0 && _globals.PlayerPickUpLimit[player.PlayerID] > 0)
        {
            _globals.PlayerPickUpLimit[player.PlayerID]--;
        }
        // 减少箱子回合拾取次数
        if (data.RoundPickLimit > 0)
        {
            _globals.PlayerRoundPickUpLimit[player.PlayerID][data.Code]--;
        }
        // 减少箱子每条命拾取次数
        if (data.SpawnPickLimit > 0)
        {
            _globals.PlayerSpawnPickUpLimit[player.PlayerID][data.Code]--;
        }
        // 道具发放
        if (data.Items.Length == 0)
        {
            _helpers.PlayBlockSound(player);
            Console.WriteLine("[H-AN] Item Empty");
            return;
        }

        var IteamCFG = _airItemConfig.CurrentValue;
        var validItems = IteamCFG.ItemList
       .Where(item => item.Enabled && data.Items.Contains(item.Name)) // 只按名字匹配
       .Where(item => _helpers.HasPermissionOrOpen(_core, player.SteamID, item.Permissions))
       .ToList();

        // 玩家没有任何可用权限的道具，给出提示

        if (validItems.Count == 0)
        {
            _helpers.PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["NoPermissionToItems"]}");
            return;
        }

        var chosenItem = _helpers.SelectByProbability(validItems);
        if (chosenItem == null)
        {
            _helpers.PlayBlockSound(player);
            player.SendMessage(MessageType.Chat, $"此空投箱未配置道具,或者无道具启用");
            return;
        }
        player.ExecuteCommand(chosenItem.Command);
        _core.PlayerManager.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["PlayerPickUpMessage", client.PlayerName, data.Name, chosenItem.Name]}");
        if (!string.IsNullOrEmpty(chosenItem.PickSound))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(chosenItem.PickSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddRecipient(player.PlayerID); // 只有自己听到

            _core.Scheduler.NextTick(() =>
            {
                sound.Emit();
                sound.Recipients.RemoveRecipient(player.PlayerID);
            });
        }


    }


}




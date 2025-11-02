using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Natives;
using Microsoft.Extensions.Options;
using static HanAirDropS2.HanAirDropBoxConfig;
using System.Xml.Linq;
using SwiftlyS2.Shared.Players;
using System;
using System.Threading.Channels;
using System.Collections.Generic;
using SwiftlyS2.Shared.Sounds;
using System.Numerics;
using static Dapper.SqlMapper;
using Mono.Cecil.Cil;

namespace HanAirDropS2;

public class HanAirDropCreateBox
{
    private ILogger<HanAirDropCreateBox> _logger;
    private ISwiftlyCore Core;
    private readonly IOptionsMonitor<HanAirDropConfig> _airDropConfig;
    private readonly IOptionsMonitor<HanAirDropBoxConfig> _boxConfig;
    private readonly IOptionsMonitor<HanAirDropItemConfig> _airItemConfig;
    private readonly HanAirDropGlow _airDropGlow;

    public Dictionary<CEntityInstance, AirBoxData> BoxData = new();

    public Dictionary<uint, CEntityInstance> BoxTriggers = new();

    // 使用字典存储每个箱子的限制
    public int[] PlayerPickUpLimit = new int[65];
    public Dictionary<int, Dictionary<int, int>> PlayerRoundPickUpLimit = new(); // [玩家Slot][箱子Code] = 剩余次数
    public Dictionary<int, Dictionary<int, int>> PlayerSpawnPickUpLimit = new(); // [玩家Slot][箱子Code] = 剩余次数

    //public Dictionary<CEntityInstance, CancellationTokenSource> BoxTimers = new Dictionary<CEntityInstance, CancellationTokenSource>();


    public string physicsBox = "models/de_inferno/inferno_winebar_interior_01/inferno_winebar_crate_01_a.vmdl";
    //"models/generic/plastic_crate_kit_01/pkkit01_crate_02_b_small.vmdl";

    public class AirBoxData
    {
        public int Code { get; set; }
        public string[] Items { get; set; } = Array.Empty<string>();
        public int TeamOnly { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DropSound { get; set; } = string.Empty;
        public int RoundPickLimit { get; set; }
        public int SpawnPickLimit { get; set; }
        public string Flags { get; set; } = string.Empty;
        public bool OpenGlow { get; set; }
    }



    public HanAirDropCreateBox(ISwiftlyCore core, ILogger<HanAirDropCreateBox> logger,
                            IOptionsMonitor<HanAirDropConfig> airConfig,
                            IOptionsMonitor<HanAirDropBoxConfig> boxConfig,
                            IOptionsMonitor<HanAirDropItemConfig> itemConfig,
                            HanAirDropGlow glow)
    {
        Core = core;
        _logger = logger;
        _airDropConfig = airConfig;
        _boxConfig = boxConfig;
        _airItemConfig = itemConfig;
        _airDropGlow = glow;
    }


    public void CreateAirDrop(SwiftlyS2.Shared.Natives.Vector positions, SwiftlyS2.Shared.Natives.QAngle QAngles, SwiftlyS2.Shared.Natives.Vector velocitys) //SwiftlyS2.Shared.Natives.Vector position
    {
        var mainCfg = _airDropConfig.CurrentValue;
        var boxCfg = _boxConfig.CurrentValue;

        var box = boxCfg.SelectRandomBoxConfig(mainCfg.AirDropName, _logger);
        if (box == null)
        {
            _logger.LogWarning("[空投系统] 没有可用的空投配置，请检查 AirDropName 或 Enabled 状态");
            return;
        }

        _logger.LogInformation("生成空投：{BoxName} 模型：{Model}", box.Name, box.ModelPath);


        CPhysicsPropOverride Box = Core.EntitySystem.CreateEntity<CPhysicsPropOverride>();
        if (Box == null)
            return;

        Box.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
        Box.Collision.CollisionGroupUpdated();

        Box.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);

        Box.SetModel(physicsBox);
        Box.DispatchSpawn();

        //Box.SetScale(1.5f);

       


        string model = Box?.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        string propName = $"华仔空投_{Random.Shared.Next(1000000, 9999999)}";  
        Box!.Entity!.Name = propName;

        var boxRef = Core.EntitySystem.GetRefEHandle(Box);
        if (!boxRef.IsValid) return;

        //var BoxId = Box.Index;

        BoxData[Box] = new AirBoxData
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

        /*
        // ======== 打印配置 =========
        var airBox = BoxData[Box];
        string items = string.Join(",", airBox.Items);
        string configInfo =
            $"[create空投配置]\n" +
            $"Code: {airBox.Code}\n" +
            $"Name: {airBox.Name}\n" +
            $"Items: {items}\n" +
            $"TeamOnly: {airBox.TeamOnly}\n" +
            $"RoundPickLimit: {airBox.RoundPickLimit}\n" +
            $"SpawnPickLimit: {airBox.SpawnPickLimit}\n" +
            $"DropSound: {airBox.DropSound}\n" +
            $"Flags: {airBox.Flags}\n" +
            $"OpenGlow: {airBox.OpenGlow}";
        Core.PlayerManager.SendMessage(MessageType.Chat, configInfo);
        // ============================
        */

        Box!.Teleport((SwiftlyS2.Shared.Natives.Vector)positions, (SwiftlyS2.Shared.Natives.QAngle)QAngles, (SwiftlyS2.Shared.Natives.Vector)velocitys);

        
        CTriggerMultiple trigger = CreateTrigger(boxRef.Value!);
        var triggerRef = Core.EntitySystem.GetRefEHandle(trigger);
        if (!triggerRef.IsValid) return;
        

        BoxTriggers.Add(trigger.Index, Box);
        
        if (!string.IsNullOrEmpty(box.DropSound))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(box.DropSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddAllPlayers();
            Core.Scheduler.NextTick(() =>
            {
                sound.Emit();
            });
        }
        

        float KillSecond = mainCfg.AirDropKillTimer;
        Core.Scheduler.DelayBySeconds(KillSecond, () =>
        {
            try
            {
                // 引用失效则提前退出
                if (!boxRef.IsValid)
                    return;

                if (!triggerRef.IsValid)
                    return;

                // 重新取实体对象
                var box = boxRef.Value;
                var trigger = triggerRef.Value;

                if (box == null || trigger == null)
                    return;

                // 正常执行
                BoxSelfKill(triggerRef, boxRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[空投系统] 延迟自杀任务异常");
            }
        });
        


        if (box.OpenGlow)
        {
            HanAirDropGlow.TryParseColor(box.GlowColor, out var glowColor, new SwiftlyS2.Shared.Natives.Color(255, 255, 0,  0));
            _airDropGlow.SetGlow(Box!, glowColor.R, glowColor.G, glowColor.B, glowColor.A);
        }
        
        string newColor = BoxData[Box!].OpenGlow ? box.GlowColor : "0,0,0,0";
        var cloneprop = CreateClone(Box!, box.ModelPath, propName, newColor);
        
    }

    public CDynamicProp? CreateClone(CPhysicsPropOverride prop, string model, string propName, string glowcolor)
    {
        if (string.IsNullOrEmpty(model))
        {
            return null;
        }

        CDynamicProp? clone = Core.EntitySystem.CreateEntity<CDynamicProp>();
        if (clone == null || clone.Entity == null || !clone.IsValid)
        {
            return null;
        }

        clone.Entity.Name = propName + "_clone";
        clone.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        clone.SetModel(model);

        clone.DispatchSpawn();

        //clone.Render.A = 0;
        //clone.RenderUpdated();

        clone.Teleport(prop.AbsOrigin, prop.AbsRotation, null);
        clone.UseAnimGraph = false;

        clone.AcceptInput("FollowEntity", propName, prop, prop ); 


        var defaultGlowColor = new SwiftlyS2.Shared.Natives.Color(255, 0, 0, 255);
        HanAirDropGlow.TryParseColor(glowcolor, out var glowColor, defaultGlowColor);
        _airDropGlow.SetGlow(clone, glowColor.R, glowColor.G, glowColor.B, glowColor.A);

        Core.Scheduler.NextTick(() =>
        {
            prop.Render.A = 0;
            prop.RenderUpdated();
        });


        return clone;
    }
    /*
    public void SetPropInvisible(CPhysicsPropOverride entity)
    {
        if (entity == null || !entity.IsValid)
        {
            return;
        }

        entity.Render = new SwiftlyS2.Shared.Natives.Color(255, 255, 255, 0);

    }
    */

    public CTriggerMultiple CreateTrigger(CBaseEntity parent)
    {
        var trigger = Core.EntitySystem.CreateEntity<CTriggerMultiple>();

        if (trigger == null)
        {
            throw new Exception($"Trigger entity \"{parent.Entity!.Name}\" could not be created!");
        }

        trigger.Entity!.Name = parent.Entity!.Name + "_trigger";
        trigger.Spawnflags = 1;
        trigger.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);
        trigger.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        trigger.Collision.SolidFlags = 0;
        trigger.Collision.CollisionGroup = 14;

        trigger.DispatchSpawn();
        trigger.SetModel(parent.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName);

        var position = parent?.AbsOrigin;
        var EntAngle = parent?.AbsRotation;
        var EntVelocity = parent?.AbsVelocity;
        if (position != null)
        {
            trigger?.Teleport((SwiftlyS2.Shared.Natives.Vector)position, (SwiftlyS2.Shared.Natives.QAngle)EntAngle!, (SwiftlyS2.Shared.Natives.Vector)EntVelocity!);
        }

        trigger!.AcceptInput("SetParent", "!activator", parent, trigger);
        trigger!.AcceptInput("Enable", 0, parent, trigger);

        return trigger;
    }

    public void BoxSelfKill(CHandle<CTriggerMultiple> triggerRef, CHandle<CPhysicsPropOverride> boxRef)
    {
        if (triggerRef.IsValid)
        {
            if (BoxTriggers.ContainsKey(triggerRef.Value!.Index))
            {
                var linkedBox = BoxTriggers[triggerRef.Value!.Index];
                BoxData.Remove(linkedBox);
                BoxTriggers.Remove(triggerRef.Value!.Index);

                triggerRef.Value!.AcceptInput("Kill", 0);

                //Core.PlayerManager.SendMessage(MessageType.Chat, "triggerRef 存在 已自动删除");
            }
        }
        if (boxRef.IsValid)
        {
            BoxData.Remove(boxRef.Value!);
            boxRef.Value!.AcceptInput("Kill", 0);

            //Core.PlayerManager.SendMessage(MessageType.Chat, "boxRef 存在 已自动删除");
        }
    }


    public void CreateAirDropAtPosition(string boxName, SwiftlyS2.Shared.Natives.Vector positions, SwiftlyS2.Shared.Natives.QAngle QAngles, SwiftlyS2.Shared.Natives.Vector velocitys)
    {
        var mainCfg = _airDropConfig.CurrentValue;
        var boxCfg = _boxConfig.CurrentValue;

        var config = boxCfg.BoxList.FirstOrDefault(b => b.Enabled && b.Name == boxName);

        CPhysicsPropOverride Box = Core.EntitySystem.CreateEntity<CPhysicsPropOverride>();
        if (Box == null)
            return;

        Box.Collision.CollisionGroup = (byte)CollisionGroup.Debris;
        Box.Collision.CollisionGroupUpdated();

        Box.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);

        Box.SetModel(physicsBox);
        Box.DispatchSpawn();

        //Box.SetScale(2.0f);

        Box.Render.A = 0;
        Box.RenderUpdated();
        //Box.Render = new SwiftlyS2.Shared.Natives.Color(0, 0, 0, 0);
        //Box.RenderUpdated();



        string model = Box?.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        string propName = $"华仔空投_{Random.Shared.Next(1000000, 9999999)}"; 
        Box!.Entity!.Name = propName;

        var boxRef = Core.EntitySystem.GetRefEHandle(Box);
        if (!boxRef.IsValid) return;

        BoxData[Box] = new AirBoxData
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
        /*
        // ======== 打印当前空投配置 =========
        var airBox = BoxData[Box];
        string items = string.Join(",", airBox.Items);
        string configInfo =
            $"[Select空投配置]\n" +
            $"Code: {airBox.Code}\n" +
            $"Name: {airBox.Name}\n" +
            $"Items: {items}\n" +
            $"TeamOnly: {airBox.TeamOnly}\n" +
            $"RoundPickLimit: {airBox.RoundPickLimit}\n" +
            $"SpawnPickLimit: {airBox.SpawnPickLimit}\n" +
            $"DropSound: {airBox.DropSound}\n" +
            $"Flags: {airBox.Flags}\n" +
            $"OpenGlow: {airBox.OpenGlow}";
        Core.PlayerManager.SendMessage(MessageType.Chat, configInfo);
        // ============================
        */

        Box!.Teleport((SwiftlyS2.Shared.Natives.Vector)positions, (SwiftlyS2.Shared.Natives.QAngle)QAngles, (SwiftlyS2.Shared.Natives.Vector)velocitys);


        CTriggerMultiple trigger = CreateTrigger(boxRef.Value!);

        var triggerRef = Core.EntitySystem.GetRefEHandle(trigger);
        if (!triggerRef.IsValid) return;

        BoxTriggers.Add(trigger.Index, Box);

        if (!string.IsNullOrEmpty(config.DropSound))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(config.DropSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddAllPlayers();
            Core.Scheduler.NextTick(() =>
            {
                sound.Emit();
            });
        }


        float KillSecond = mainCfg.AirDropKillTimer;
        Core.Scheduler.DelayBySeconds(KillSecond, () =>
        {
            BoxSelfKill(triggerRef, boxRef);
        });


        if (config.OpenGlow)
        {
            HanAirDropGlow.TryParseColor(config.GlowColor, out var glowColor, new SwiftlyS2.Shared.Natives.Color(255, 255, 0, 0));
            _airDropGlow.SetGlow(Box!, glowColor.R, glowColor.G, glowColor.B, glowColor.A);
        }
        string newColor = BoxData[Box!].OpenGlow ? config.GlowColor : "0,0,0,0";
        var cloneprop = CreateClone(Box!, config.ModelPath, propName, newColor);
    }









}


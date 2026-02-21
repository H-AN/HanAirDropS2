using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Natives;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Players;
using static HanAirDropS2.HanAirDropBoxConfig;
using static Dapper.SqlMapper;
using static HanAirDropS2.HanAirDropCreateBox;


namespace HanAirDropS2;

public class HanAirDropCreateBox
{
    private ILogger<HanAirDropCreateBox> _logger;
    private ISwiftlyCore Core;
    private readonly IOptionsMonitor<HanAirDropConfig> _airDropConfig;
    private readonly IOptionsMonitor<HanAirDropBoxConfig> _boxConfig;
    private readonly IOptionsMonitor<HanAirDropItemConfig> _airItemConfig;
    private readonly HanAirDropGlow _airDropGlow;

    public Dictionary<uint, AirBoxData> BoxData = new();

    // 使用字典存储每个箱子的限制
    public int[] PlayerPickUpLimit = new int[65];
    public Dictionary<int, Dictionary<int, int>> PlayerRoundPickUpLimit = new(); // [玩家Slot][箱子Code] = 剩余次数
    public Dictionary<int, Dictionary<int, int>> PlayerSpawnPickUpLimit = new(); // [玩家Slot][箱子Code] = 剩余次数

    //public string physicsBox = "models/de_inferno/inferno_winebar_interior_01/inferno_winebar_crate_01_a.vmdl";
    public string physicsBox = "models/generic/crate_plastic_01/crate_plastic_01_bottom.vmdl";

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

        //_logger.LogInformation("生成空投：{BoxName} 模型：{Model}", box.Name, box.ModelPath);


        CPhysicsPropOverride Box = Core.EntitySystem.CreateEntity<CPhysicsPropOverride>();
        if (Box == null)
            return;

        Box.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);



        Box.SetModel(physicsBox);

        Box.Collision.CollisionGroup = (byte)CollisionGroup.Dissolving;
        Box.Collision.CollisionGroupUpdated();

        Box.DispatchSpawn();

       
        //Box.SetScale(1.5f); //箱子大小 未使用

        string model = Box?.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        string propName = $"华仔空投_{Random.Shared.Next(1000000, 9999999)}";  
        Box!.Entity!.Name = propName;

        var boxRef = Core.EntitySystem.GetRefEHandle(Box);
        if (!boxRef.IsValid) return;

        BoxData[Box.Index] = new AirBoxData
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

        CDynamicProp? clone = Core.EntitySystem.CreateEntity<CDynamicProp>();
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
            HanAirDropGlow.TryParseColor(glowcolor, out var glowColor, defaultGlowColor);
            _airDropGlow.SetGlow(clone, glowColor.R, glowColor.G, glowColor.B, glowColor.A);
        }

        prop.Render.A = 0;
        prop.RenderUpdated();

        return clone;
    }

    public void BoxSelfKill(CHandle<CPhysicsPropOverride> boxRef)
    {
        if (boxRef.IsValid)
        {
            BoxData.Remove(boxRef.Value!.Index!);
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

        CPhysicsPropOverride Box = Core.EntitySystem.CreateEntity<CPhysicsPropOverride>();
        if (Box == null)
            return;

        Box.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);

        Box.SetModel(physicsBox);

        Box.Collision.CollisionGroup = (byte)CollisionGroup.Dissolving;
        Box.Collision.CollisionGroupUpdated();

        Box.DispatchSpawn();

        //Box.SetScale(2.0f);

        string model = Box?.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        string propName = $"华仔空投_{Random.Shared.Next(1000000, 9999999)}"; 
        Box!.Entity!.Name = propName;

        var boxRef = Core.EntitySystem.GetRefEHandle(Box);
        if (!boxRef.IsValid) return;

        BoxData[Box.Index] = new AirBoxData
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

    public void PrintBoxData(AirBoxData airBox, string title = "[空投配置信息]")
    {
        string items = string.Join(",", airBox.Items ?? Array.Empty<string>());
        string configInfo =
            $"{title}\n" +
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
    }
}




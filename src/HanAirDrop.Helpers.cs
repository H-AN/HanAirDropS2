using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using static HanAirDropS2.HanAirDropBoxConfig;
using static HanAirDropS2.HanAirDropItemConfig;


namespace HanAirDropS2;

public class HanAirDropHelpers
{
    private ILogger<HanAirDropHelpers> _logger;
    private ISwiftlyCore _core;
    private readonly IOptionsMonitor<HanAirDropConfig> _airDropCFG;
    private readonly IOptionsMonitor<HanAirDropBoxConfig> _airBoxCFG;
    public HanAirDropHelpers(ISwiftlyCore core, ILogger<HanAirDropHelpers> logger,
        IOptionsMonitor<HanAirDropConfig> DropCFG,
        IOptionsMonitor<HanAirDropBoxConfig> BoxCFG)
    {
        _core = core;
        _logger = logger;
        _airDropCFG = DropCFG;
        _airBoxCFG = BoxCFG;
    }

    public bool HasPermissionOrOpen(ISwiftlyCore core, ulong steamId, string? permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return true;

        return core.Permission.PlayerHasPermission(steamId, permission);
    }

    public SwiftlyS2.Shared.Natives.Vector GetForwardPosition(IPlayer player, float distance = 100f)
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

    public void SetGlow(CBaseEntity entity, int ColorR, int ColorG, int ColorB, int ColorA)
    {

        CBaseModelEntity modelGlow = _core.EntitySystem.CreateEntity<CBaseModelEntity>();
        CBaseModelEntity modelRelay = _core.EntitySystem.CreateEntity<CBaseModelEntity>();

        if (modelGlow == null || modelRelay == null)
            return;

        string modelName = entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;

        modelRelay.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        modelRelay.SetModel(modelName);
        modelRelay.Spawnflags = 256u;
        modelRelay.RenderMode = RenderMode_t.kRenderNone;
        modelRelay.DispatchSpawn();

        modelGlow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        modelGlow.SetModel(modelName);
        modelGlow.Spawnflags = 256u;
        modelGlow.DispatchSpawn();

        modelGlow.Glow.GlowColorOverride = new SwiftlyS2.Shared.Natives.Color(ColorR, ColorG, ColorB, ColorA);
        modelGlow.Glow.GlowRange = 5000;
        modelGlow.Glow.GlowTeam = -1;
        modelGlow.Glow.GlowType = 3;
        modelGlow.Glow.GlowRangeMin = 100;

        modelRelay.AcceptInput("FollowEntity", "!activator", entity, modelRelay);
        modelGlow.AcceptInput("FollowEntity", "!activator", modelRelay, modelGlow);

    }

    public bool TryParseColor(string colorStr, out SwiftlyS2.Shared.Natives.Color color, SwiftlyS2.Shared.Natives.Color defaultColor)
    {
        // 默认返回预设颜色
        color = defaultColor;

        // 1. 检查空值
        if (string.IsNullOrWhiteSpace(colorStr))
            return false;

        // 2. 分割字符串
        var parts = colorStr.Split(',');

        // 3. 检查最小长度
        if (parts.Length < 4)
            return false;

        // 严格按照ARGB顺序解析
        if (!TryParseColorComponent(parts[0], out int r) ||  // Red
            !TryParseColorComponent(parts[1], out int g) ||  // Green
            !TryParseColorComponent(parts[2], out int b) ||  // Blue
            !TryParseColorComponent(parts[3], out int a))    // Alpha

        {
            return false;
        }

        color = new SwiftlyS2.Shared.Natives.Color(r, g, b, a);
        return true;
    }

    // 辅助方法：解析单个颜色分量（0-255）
    public bool TryParseColorComponent(string str, out int value)
    {
        value = 0;
        if (!int.TryParse(str, out int tmp) || tmp < 0 || tmp > 255)
            return false;

        value = tmp;
        return true;
    }
    public (SwiftlyS2.Shared.Natives.Vector Position, SwiftlyS2.Shared.Natives.QAngle Angle, SwiftlyS2.Shared.Natives.Vector Velocity, string SpawnTypeKey)? GetRandomSpawnPosition(int spawnType)
    {
        var mainCfg = _airDropCFG.CurrentValue;

        List<(SpawnPoint spawn, string typeKey)> spawnPoints = new List<(SpawnPoint, string)>();

        string ctSpawnKey = "CtSpawnPointName";
        string tSpawnKey = "TSpawnPointName";
        string dSpawnKey = "DSpawnPointName";

        switch (spawnType)
        {
            case 0:
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                break;

            case 1:
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                break;

            case 2:
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                break;

            case 3:
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                else
                {
                    spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                        .Select(s => (s, ctSpawnKey)));
                    spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                        .Select(s => (s, tSpawnKey)));
                }
                break;

            case 4:
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                break;

            case 5:
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                break;

            case 6:
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                break;

            default:
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(_core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                break;
        }

        if (!spawnPoints.Any())
            return null;

        var (randomSpawn, spawnTypeKey) = spawnPoints[Random.Shared.Next(spawnPoints.Count)];

        var position = randomSpawn?.AbsOrigin;
        var angle = randomSpawn?.AbsRotation;
        var velocity = randomSpawn?.AbsVelocity;

        if (position == null || angle == null || velocity == null)
            return null;

        return (
            new SwiftlyS2.Shared.Natives.Vector(position.Value.X, position.Value.Y, position.Value.Z),
            new SwiftlyS2.Shared.Natives.QAngle(angle.Value.Roll, angle.Value.Yaw, angle.Value.Pitch),
            new SwiftlyS2.Shared.Natives.Vector(velocity.Value.X, velocity.Value.Y, velocity.Value.Z),
            spawnTypeKey
        );
    }

    public int CalculateDropCount(int playerCount)
    {
        var CFG = _airDropCFG.CurrentValue;

        switch (CFG.AirDropSpawnMode)
        {
            case 0: // 固定数量模式
                return Math.Max(0, CFG.AirDropCount);

            case 1: // 动态数量模式
                if (CFG.AirDropPlayerCount <= 0 || CFG.AirDropDynamicCount <= 0)
                    return 0;

                return Math.Max(1, playerCount / CFG.AirDropPlayerCount * CFG.AirDropDynamicCount);

            default:
                return 0;
        }
    }

    // 随机选择空投配置
    // 根据AirDropName筛选并随机选择
    public Box? SelectRandomBoxConfig(string allowedNames, ILogger? logger = null)
    {
        var BoxCFG = _airBoxCFG.CurrentValue;

        var allowedNameList = allowedNames.Split(',')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        var availableBoxes = BoxCFG.BoxList
            .Where(b => b.Enabled && allowedNameList.Contains(b.Name))
            .ToList();

        if (!availableBoxes.Any())
        {
            logger?.LogWarning("没有可用的空投配置: {AllowedNames}", allowedNames);
            return null;
        }

        float totalProb = availableBoxes.Sum(b => b.Probability);
        float randomPoint = (float)Random.Shared.NextDouble() * totalProb;

        float cumulative = 0f;
        foreach (var box in availableBoxes)
        {
            cumulative += box.Probability;
            if (randomPoint <= cumulative)
                return box;
        }

        return availableBoxes.Last();
    }


    // 按概率选择道具
    public Item? SelectByProbability(List<Item> itemConfig)
    {
        if (itemConfig == null || itemConfig.Count == 0)
            return null;

        float totalProb = itemConfig.Sum(i => i.ItemProbability);
        float randomPoint = (float)Random.Shared.NextDouble() * totalProb;

        float cumulative = 0f;
        foreach (var item in itemConfig)
        {
            cumulative += item.ItemProbability;
            if (randomPoint <= cumulative)
                return item;
        }

        return itemConfig.LastOrDefault();
    }

    public void PlayBlockSound(IPlayer player)
    {
        var CFG = _airDropCFG.CurrentValue;
        if (!string.IsNullOrEmpty(CFG.BlockPickUpSoundEvent))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(CFG.BlockPickUpSoundEvent, 1.0f, 1.0f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.AddRecipient(player.PlayerID); // 只有自己听到

            _core.Scheduler.NextTick(() =>
            {
                sound.Emit();
                sound.Recipients.RemoveRecipient(player.PlayerID);
            });
        }

    }


    public void PrintBoxData(HanAirDropGlobals.AirBoxData airBox, string title = "[空投配置信息]")
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

        _core.PlayerManager.SendMessage(MessageType.Chat, configInfo);
    }


}
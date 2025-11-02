using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanAirDropS2;
public class TeleportHelper
{
    private ILogger<TeleportHelper> _logger;
    private ISwiftlyCore Core;
    private readonly IOptionsMonitor<HanAirDropConfig> _airDropConfig;

    public TeleportHelper(ISwiftlyCore core, ILogger<TeleportHelper> logger, IOptionsMonitor<HanAirDropConfig> airDropConfig)
    {
        Core = core;
        _logger = logger;
        _airDropConfig = airDropConfig;
    }


    public (SwiftlyS2.Shared.Natives.Vector Position,SwiftlyS2.Shared.Natives.QAngle Angle,SwiftlyS2.Shared.Natives.Vector Velocity,string SpawnType)? GetRandomSpawnPosition(int spawnType)
    {
        var mainCfg = _airDropConfig.CurrentValue;

        List<(SpawnPoint spawn, string type)> spawnPoints = new List<(SpawnPoint, string)>();

        string ctspawnposname = Core.Localizer["CtSpawnPointName"];
        string tspawnposname = Core.Localizer["TSpawnPointName"];
        string dspawnposname = Core.Localizer["DSpawnPointName"];

        switch (spawnType)
        {
            case 0: // T + CT + 死亡竞赛复活点 混合
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, $"{ctspawnposname}")));
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, $"{tspawnposname}")));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                    .Select(s => (s, $"{dspawnposname}")));
                }
                break;

            case 1: // 仅 CT
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, $"{ctspawnposname}")));
                break;

            case 2: // 仅 T
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, $"{tspawnposname}")));
                break;
            case 3: // 仅 死亡竞赛复活点
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                    .Select(s => (s, $"{dspawnposname}")));
                }
                else
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                                        .Select(s => (s, $"{ctspawnposname}")));
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                        .Select(s => (s, $"{tspawnposname}")));
                }
                break;
            case 4: // 仅 CT + T
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, $"{ctspawnposname}")));
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, $"{tspawnposname}")));
                break;
            case 5: // 仅 CT + 死亡竞赛复活点
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, $"{ctspawnposname}")));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                    .Select(s => (s, $"{dspawnposname}")));
                }
                break;
            case 6: // 仅 T + 死亡竞赛复活点
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, $"{ctspawnposname}")));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                    .Select(s => (s, $"{dspawnposname}")));
                }
                break;

            default: // 全部默认混合
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, $"{ctspawnposname}")));
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, $"{tspawnposname}")));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                    .Select(s => (s, $"{dspawnposname}")));
                }
                break;
        }

        if (!spawnPoints.Any())
            return null;

        var (randomSpawn, spawnTypeName) = spawnPoints[Random.Shared.Next(spawnPoints.Count)];

        var position = randomSpawn?.AbsOrigin;
        var angle = randomSpawn?.AbsRotation;
        var velocity = randomSpawn?.AbsVelocity;

        if (position == null || angle == null || velocity == null)
            return null;

        return (
            new SwiftlyS2.Shared.Natives.Vector(position.Value.X, position.Value.Y, position.Value.Z),
            new SwiftlyS2.Shared.Natives.QAngle(angle.Value.Roll, angle.Value.Yaw, angle.Value.Pitch),
            new SwiftlyS2.Shared.Natives.Vector(velocity.Value.X, velocity.Value.Y, velocity.Value.Z),
            spawnTypeName
        );

    }

}
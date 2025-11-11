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

    public (SwiftlyS2.Shared.Natives.Vector Position, SwiftlyS2.Shared.Natives.QAngle Angle, SwiftlyS2.Shared.Natives.Vector Velocity, string SpawnTypeKey)? GetRandomSpawnPosition(int spawnType)
    {
        var mainCfg = _airDropConfig.CurrentValue;

        List<(SpawnPoint spawn, string typeKey)> spawnPoints = new List<(SpawnPoint, string)>();

        // 仅存 key，不做本地化
        string ctSpawnKey = "CtSpawnPointName";
        string tSpawnKey = "TSpawnPointName";
        string dSpawnKey = "DSpawnPointName";

        switch (spawnType)
        {
            case 0:
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                break;

            case 1:
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                break;

            case 2:
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                break;

            case 3:
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                else
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                        .Select(s => (s, ctSpawnKey)));
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                        .Select(s => (s, tSpawnKey)));
                }
                break;

            case 4:
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                break;

            case 5:
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                break;

            case 6:
                // 这里是个 bug 你之前把 t spawn 标记成 ctspawn，已改正为 tSpawnKey
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
                        .Select(s => (s, dSpawnKey)));
                }
                break;

            default:
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")
                    .Select(s => (s, ctSpawnKey)));
                spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")
                    .Select(s => (s, tSpawnKey)));
                if (mainCfg.Openrandomspawn != 0)
                {
                    spawnPoints.AddRange(Core.EntitySystem.GetAllEntitiesByDesignerName<SpawnPoint>("info_deathmatch_spawn")
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


}
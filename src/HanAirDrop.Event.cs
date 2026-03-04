using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanAirDropS2;

public class HanAirDropEvent
{
    private ILogger<HanAirDropEvent> _logger;
    private ISwiftlyCore _core;

    private readonly IOptionsMonitor<HanAirDropConfig> _airDropCFG;
    private readonly IOptionsMonitor<HanAirDropBoxConfig> _airBoxCFG;
    private readonly HanAirDropService _service;
    private readonly HanAirDropGlobals _globals;

    public HanAirDropEvent(ISwiftlyCore core, ILogger<HanAirDropEvent> logger,
        IOptionsMonitor<HanAirDropConfig> DropCFG,
        IOptionsMonitor<HanAirDropBoxConfig> BoxCFG,
        HanAirDropService service, HanAirDropGlobals globals)
    {
        _core = core;
        _logger = logger;
        _airDropCFG = DropCFG;
        _airBoxCFG = BoxCFG;
        _service = service;
        _globals = globals;
    }

    public void HookEvents() 
    {
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);
        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.Event.OnEntityStartTouch += Event_OnEntityTouchHook;
        _core.Event.OnEntityTakeDamage += Event_OnEntityHurt;
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
    }

    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var id = @event.PlayerId;

        _globals.PlayerPickUpLimit[id] = 0;
        _globals.PlayerRoundPickUpLimit.Remove(id);
        _globals.PlayerSpawnPickUpLimit.Remove(id);
    }

    public void Event_OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        try
        {
            var boxList = _airBoxCFG.CurrentValue.BoxList;

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

            if (!string.IsNullOrEmpty(_airDropCFG.CurrentValue.PrecacheSoundEvent))
            {
                var soundList = _airDropCFG.CurrentValue.PrecacheSoundEvent
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
            @event.AddItem(_globals.physicsBox);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnPrecacheResource] 预缓存失败/ OnPrecacheResource Error: {ex.Message}");
        }

    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        var DropCFG = _airDropCFG.CurrentValue;
        var BoxCFG = _airBoxCFG.CurrentValue;
        _globals.BoxData.Clear();

        if (DropCFG.Openrandomspawn == 0)
        {
            _core.Engine.ExecuteCommand("mp_randomspawn 0");
        }
        else
        {
            _core.Engine.ExecuteCommand("mp_randomspawn 1");
        }

        var Allplayer = _core.PlayerManager.GetAllPlayers();
        foreach (var client in Allplayer)
        {
            var pawn = client.PlayerPawn;
            if (pawn == null || !pawn.IsValid) 
                return HookResult.Continue;

            var playerController = client.Controller;
            if (playerController == null || !playerController.IsValid)
                return HookResult.Continue;

            if (client.IsFakeClient) 
                return HookResult.Continue;

            if (!playerController.PawnIsAlive) 
                return HookResult.Continue;

            int slot = client.PlayerID;
            if (DropCFG.PlayerPickEachRound > 0)
            {
                _globals.PlayerPickUpLimit[slot] = DropCFG.PlayerPickEachRound;
            }
            foreach (var box in BoxCFG.BoxList.Where(b => b.RoundPickLimit > 0))
            {
                if (!_globals.PlayerRoundPickUpLimit.ContainsKey(slot))
                    _globals.PlayerRoundPickUpLimit[slot] = new();

                _globals.PlayerRoundPickUpLimit[slot][box.Code] = box.RoundPickLimit;
            }
        }

        if (!DropCFG.AirDropEnble || DropCFG.AirDropMode == 1)
            return HookResult.Continue;




        _globals.MapStartDropTimer?.Cancel();
        _globals.MapStartDropTimer = null;

        float interval = (float)DropCFG.AirDropTimer;
        _globals.MapStartDropTimer = _core.Scheduler.DelayAndRepeatBySeconds(interval, interval, () =>
        {
            _service.CreateDrop();
        });
        _core.Scheduler.StopOnMapChange(_globals.MapStartDropTimer);

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var id = @event.UserId;
        var BoxCFG = _airBoxCFG.CurrentValue;

        foreach (var box in BoxCFG.BoxList.Where(b => b.SpawnPickLimit > 0))
        {
            if (!_globals.PlayerSpawnPickUpLimit.ContainsKey(id))
                _globals.PlayerSpawnPickUpLimit[id] = new();

            _globals.PlayerSpawnPickUpLimit[id][box.Code] = box.SpawnPickLimit;
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        var DropCFG = _airDropCFG.CurrentValue;

        if (!DropCFG.AirDropEnble || DropCFG.AirDropMode == 0)
            return HookResult.Continue;

        if (pawn.AbsOrigin == null)
            return HookResult.Continue;

        SwiftlyS2.Shared.Natives.Vector Position = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsOrigin;

        if (pawn.AbsRotation == null)
            return HookResult.Continue;

        SwiftlyS2.Shared.Natives.QAngle Angle = (SwiftlyS2.Shared.Natives.QAngle)pawn.AbsRotation;

        SwiftlyS2.Shared.Natives.Vector Velocity = (SwiftlyS2.Shared.Natives.Vector)pawn.AbsVelocity;

        if (Random.Shared.NextDouble() <= DropCFG.DeathDropPercent)
        {
            _service.CreateAirDrop(Position, Angle, Velocity);
        }

        return HookResult.Continue;
    }
    private void Event_OnEntityTouchHook(IOnEntityStartTouchEvent @event) //IOnEntityTouchHookEvent
    {
        var activator = @event.Entity;
        if (activator == null || activator.DesignerName != "player")
            return;

        var pawn = activator.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid) 
            return;

        var controller = pawn.Controller.Value?.As<CCSPlayerController>();
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
            return;

        var player = _core.PlayerManager.GetPlayerFromController(controller);
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        var boxEntity = @event.OtherEntity;
        if (boxEntity == null || !boxEntity.IsValid)
            return;

        if (!_globals.BoxData.TryGetValue(boxEntity.Index, out var data))
            return;

        if (boxEntity.Entity!.Name.StartsWith("华仔空投_"))
        {
            _service.BoxTouch(player, boxEntity);
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
            @event.Info.Damage = 0;
        }

    }
}
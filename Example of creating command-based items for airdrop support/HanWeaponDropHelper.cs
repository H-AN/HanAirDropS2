using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Players;
public static class DropWeapon
{
    
    public static void DropWeaponBySlot(this IPlayer player, int slotNumber, ISwiftlyCore Core)
    {
        if (!player.IsValid)
            return;

        var PlayerPawn = player.PlayerPawn;
        if (PlayerPawn == null || !PlayerPawn.IsValid)
            return;

        CCSPlayerController client = player.Controller;
        if (client == null || !client.IsValid)
            return;

        var PawnValue = client.PlayerPawn.Value;
        if (PawnValue == null || !PawnValue.IsValid)
            return;

        var ControllerPawn = client.PlayerPawn;
        if (!ControllerPawn.IsValid)
            return;

        if (player.IsFakeClient || client.IsHLTV || !client.PawnIsAlive)
            return;

        if (PawnValue.WeaponServices == null)
            return;

        if (slotNumber != 0 && slotNumber != 1 && slotNumber != 2 && slotNumber != 3)
            return;

        var targetSlot = (gear_slot_t)slotNumber;

        foreach (var weapon in PawnValue.WeaponServices.MyWeapons)
        {
            if (!PawnValue.IsValid || !weapon.IsValid || weapon.Value == null)
                continue;

            var ccSWeaponBase = weapon.Value.As<CCSWeaponBase>();
            if (ccSWeaponBase == null || !ccSWeaponBase.IsValid)
                continue;

            var weaponData = ccSWeaponBase.WeaponBaseVData;
            if (weaponData == null || weaponData.GearSlot != targetSlot)
                continue;

            PawnValue.WeaponServices.ActiveWeapon.Raw = weapon.Raw;

            PlayerPawn.ItemServices?.DropActiveItem();

            Core.Scheduler.NextTick(() =>
            {
                if (ccSWeaponBase.IsValid)
                    ccSWeaponBase.AcceptInput("Kill", 0);
            });
        }
    }

}
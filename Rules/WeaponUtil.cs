using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

internal static class WeaponUtil
{
    public static bool IsAlivePawn(CCSPlayerController? player, out CCSPlayerPawn pawn)
    {
        pawn = null!;
        if (player == null || !player.IsValid)
            return false;

        var value = player.PlayerPawn.Value;
        if (value == null || !value.IsValid || value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return false;

        pawn = value;
        return true;
    }

    public static int CountWeapon(CCSPlayerPawn pawn, string namePart)
    {
        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return 0;

        var count = 0;
        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            var name = weapon.DesignerName ?? string.Empty;
            if (name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
                count++;
        }

        return count;
    }

    public static void Give(CCSPlayerController player, string designerName)
    {
        if (!IsAlivePawn(player, out _))
            return;

        try
        {
            player.GiveNamedItem(designerName);
        }
        catch
        {
            // ignored
        }
    }

    public static void GiveIfMissing(CCSPlayerController player, string designerName, string namePart)
    {
        if (!IsAlivePawn(player, out var pawn))
            return;

        if (CountWeapon(pawn, namePart) > 0)
        {
            Equip(player, namePart);
            return;
        }

        Give(player, designerName);
        Equip(player, namePart);
    }

    public static void GiveIfMissingQuiet(CCSPlayerController player, string designerName, string namePart)
    {
        if (!IsAlivePawn(player, out var pawn))
            return;

        if (CountWeapon(pawn, namePart) > 0)
            return;

        Give(player, designerName);
    }

    public static void DropKnivesKeepOnGround(CCSPlayerController player)
    {
        if (!IsAlivePawn(player, out var pawn))
            return;

        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return;

        var toDrop = new List<CBasePlayerWeapon>();
        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            var name = weapon.DesignerName ?? string.Empty;
            if (name.Contains("taser", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("zeus", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("c4", StringComparison.OrdinalIgnoreCase))
                continue;

            if (name.Contains("knife", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("bayonet", StringComparison.OrdinalIgnoreCase))
                toDrop.Add(weapon);
        }

        foreach (var weapon in toDrop)
        {
            if (!weapon.IsValid)
                continue;

            try
            {
                DropBySwitchingActive(player, pawn, weapon);
            }
            catch
            {
                try
                {
                    if (pawn.ItemServices != null)
                    {
                        var items = pawn.ItemServices.As<CCSPlayer_ItemServices>();
                        items.DropActivePlayerWeapon(weapon);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    /// <summary>
    /// Give the rule item. Knife is always kept as a carrier so CS2 can
    /// GiveNamedItem grenades/tasers even after the previous one was thrown.
    /// </summary>
    public static void GiveRestricted(
        FunRandomRoundsPlugin plugin,
        CCSPlayerController player,
        string designerName,
        string namePart,
        params string[] keepParts)
    {
        if (!IsAlivePawn(player, out var pawn))
            return;

        GiveIfMissing(player, "weapon_knife", "knife");

        if (CountWeapon(pawn, namePart) > 0)
            return;

        plugin.BeginInternalGive();
        Give(player, designerName);
        var slot = player.Slot;
        plugin.AddTimer(0.05f, () =>
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null && IsAlivePawn(p, out var live) && CountWeapon(live, namePart) == 0)
                Give(p, designerName);

            plugin.EndInternalGive();
        });
    }

    public static bool HasAnyItem(CCSPlayerPawn pawn)
    {
        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return false;

        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon != null && weapon.IsValid)
                return true;
        }

        return false;
    }

    public static void Equip(CCSPlayerController player, string namePart)
    {
        if (!IsAlivePawn(player, out var pawn))
            return;

        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return;

        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            var name = weapon.DesignerName ?? string.Empty;
            if (!name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                pawn.WeaponServices!.ActiveWeapon.Raw = weapon.EntityHandle.Raw;
            }
            catch
            {
                // ignored
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(name))
                    player.ExecuteClientCommand($"use {name}");
            }
            catch
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        player.ExecuteClientCommandFromServer($"use {name}");
                }
                catch
                {
                    // ignored
                }
            }

            return;
        }
    }

    public static void EquipTaser(CCSPlayerController player)
    {
        if (!IsAlivePawn(player, out var pawn))
            return;

        var taser = FindTaser(pawn);
        if (taser == null)
        {
            Give(player, "weapon_taser");
            taser = FindTaser(pawn);
            if (taser == null)
                return;
        }

        try
        {
            pawn.WeaponServices!.ActiveWeapon.Raw = taser.EntityHandle.Raw;
            Utilities.SetStateChanged(pawn, "CBasePlayerPawn", "m_pWeaponServices");
        }
        catch
        {
            // ignored
        }

        var name = taser.DesignerName;
        if (string.IsNullOrWhiteSpace(name))
            name = "weapon_taser";

        try
        {
            player.ExecuteClientCommand($"use {name}");
        }
        catch
        {
            try
            {
                player.ExecuteClientCommand("use weapon_taser");
            }
            catch
            {
                // ignored
            }
        }
    }

    private static CBasePlayerWeapon? FindTaser(CCSPlayerPawn pawn)
    {
        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return null;

        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            var name = weapon.DesignerName ?? string.Empty;
            if (name.Contains("taser", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("zeus", StringComparison.OrdinalIgnoreCase))
                return weapon;

            try
            {
                if (weapon.AttributeManager?.Item?.ItemDefinitionIndex == 31)
                    return weapon;
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }

    public static bool IsAlwaysKept(string designerName)
    {
        return designerName.Contains("knife", StringComparison.OrdinalIgnoreCase) ||
               designerName.Contains("bayonet", StringComparison.OrdinalIgnoreCase) ||
               designerName.Contains("c4", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowed(string designerName, IEnumerable<string> extraKeepParts)
    {
        if (string.IsNullOrWhiteSpace(designerName))
            return false;

        if (IsAlwaysKept(designerName))
            return true;

        foreach (var part in extraKeepParts)
        {
            if (!string.IsNullOrEmpty(part) &&
                designerName.Contains(part, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string NamePartFromDesigner(string designerName)
    {
        var name = designerName.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)
            ? designerName["weapon_".Length..]
            : designerName;
        return string.IsNullOrEmpty(name) ? designerName : name;
    }

    public static bool IsUtilityName(string weapon)
    {
        if (string.IsNullOrWhiteSpace(weapon))
            return false;

        var name = weapon.ToLowerInvariant();
        return name.Contains("hegrenade") ||
               name.Contains("flashbang") ||
               name.Contains("smokegrenade") ||
               name.Contains("molotov") ||
               name.Contains("incgrenade") ||
               name.Contains("incendiary") ||
               name.Contains("decoy");
    }

    public static string? UtilityDesignerFromEvent(string weapon)
    {
        if (string.IsNullOrWhiteSpace(weapon))
            return null;

        var name = weapon.ToLowerInvariant();
        if (name.Contains("hegrenade") || name == "he")
            return "weapon_hegrenade";
        if (name.Contains("flash"))
            return "weapon_flashbang";
        if (name.Contains("smoke"))
            return "weapon_smokegrenade";
        if (name.Contains("molotov"))
            return "weapon_molotov";
        if (name.Contains("incgrenade") || name.Contains("incendiary"))
            return "weapon_incgrenade";
        if (name.Contains("decoy"))
            return "weapon_decoy";
        return null;
    }

    /// <summary>
    /// Drops every item except C4, including knife. Dropped entities are removed.
    /// </summary>
    public static void DropAllKeepC4(CCSPlayerController player)
    {
        DropWhere(player, name => !name.Contains("c4", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Drops guns. Knife and C4 are kept.
    /// </summary>
    public static void DropAllExceptC4(CCSPlayerController player)
    {
        DropExcept(player);
    }

    /// <summary>
    /// Drops and removes every firearm while keeping knife, C4 and utility.
    /// </summary>
    public static void DropFirearms(CCSPlayerController player)
    {
        DropWhere(player, IsFirearmName);
    }

    public static void DropExcept(CCSPlayerController player, params string[] keepParts)
    {
        DropWhere(player, name => !IsAllowed(name, keepParts));
    }

    private static void DropWhere(CCSPlayerController player, Func<string, bool> shouldDrop)
    {
        if (!IsAlivePawn(player, out var pawn))
            return;

        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return;

        var toDrop = new List<CBasePlayerWeapon>();
        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            if (!shouldDrop(weapon.DesignerName ?? string.Empty))
                continue;

            toDrop.Add(weapon);
        }

        if (toDrop.Count == 0)
            return;

        foreach (var weapon in toDrop)
        {
            if (!weapon.IsValid)
                continue;

            try
            {
                DropBySwitchingActive(player, pawn, weapon);
            }
            catch
            {
                try
                {
                    if (pawn.ItemServices != null)
                    {
                        var items = pawn.ItemServices.As<CCSPlayer_ItemServices>();
                        items.DropActivePlayerWeapon(weapon);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        var pawnIndex = pawn.Index;
        Server.NextFrame(() =>
        {
            foreach (var weapon in toDrop)
            {
                try
                {
                    if (!weapon.IsValid)
                        continue;

                    var owner = weapon.OwnerEntity?.Value;
                    if (owner != null && owner.IsValid && owner.Index == pawnIndex)
                        continue;

                    weapon.AddEntityIOEvent("Kill", delay: 0.10f);
                }
                catch
                {
                    // ignored
                }
            }
        });
    }

    private static void DropBySwitchingActive(CCSPlayerController player, CCSPlayerPawn pawn, CBasePlayerWeapon weapon)
    {
        if (pawn.WeaponServices == null || !weapon.IsValid)
            return;

        pawn.WeaponServices.ActiveWeapon.Raw = weapon.EntityHandle.Raw;
        player.DropActiveWeapon();
    }

    /// <summary>
    /// Drops a matching firearm onto the ground. Does not kill the entity.
    /// </summary>
    public static void DropMatchingKeep(CCSPlayerController player, string weaponPart)
    {
        if (!IsAlivePawn(player, out var pawn) || pawn.WeaponServices == null)
            return;

        var part = NormalizeWeaponPart(weaponPart);
        if (string.IsNullOrEmpty(part))
            return;

        CBasePlayerWeapon? match = null;
        var weapons = pawn.WeaponServices.MyWeapons;
        if (weapons == null)
            return;

        var active = pawn.WeaponServices.ActiveWeapon.Value;
        if (active != null && active.IsValid && MatchesWeapon(active.DesignerName ?? string.Empty, part))
            match = active;
        else
        {
            foreach (var handle in weapons)
            {
                var weapon = handle.Value;
                if (weapon == null || !weapon.IsValid)
                    continue;
                if (!MatchesWeapon(weapon.DesignerName ?? string.Empty, part))
                    continue;
                match = weapon;
                break;
            }
        }

        if (match == null || !match.IsValid)
            return;

        ThrowWeaponLikePlayer(player, pawn, match);
    }

    /// <summary>
    /// Drops the matching gun the same way a player pressing drop would: switch to it, then throw it forward.
    /// </summary>
    public static void ThrowMatchingLikePlayer(CCSPlayerController player, string weaponPart)
    {
        DropMatchingKeep(player, weaponPart);
    }

    public static void ThrowActiveFirearm(CCSPlayerController player)
    {
        if (!IsAlivePawn(player, out var pawn) || pawn.WeaponServices == null)
            return;

        var active = pawn.WeaponServices.ActiveWeapon.Value;
        if (active == null || !active.IsValid)
            return;

        if (!string.IsNullOrWhiteSpace(active.DesignerName) &&
            !IsFirearmName(active.DesignerName))
            return;

        ThrowWeaponLikePlayer(player, pawn, active);
    }

    public static bool IsFirearmName(string weapon)
    {
        if (string.IsNullOrWhiteSpace(weapon))
            return false;

        var name = weapon.ToLowerInvariant();
        if (name.Contains("knife") || name.Contains("bayonet") || name.Contains("fists"))
            return false;
        if (name.Contains("grenade") || name.Contains("molotov") || name.Contains("flashbang") ||
            name.Contains("decoy") || name.Contains("incgrenade") || name.Contains("smoke"))
            return false;
        if (name.Contains("c4") || name.Contains("healthshot") || name.Contains("tablet") ||
            name.Contains("breachcharge") || name.Contains("bumpmine") || name.Contains("taser") ||
            name.Contains("zeus"))
            return false;

        return true;
    }

    private static void ThrowWeaponLikePlayer(CCSPlayerController player, CCSPlayerPawn pawn, CBasePlayerWeapon weapon)
    {
        var name = weapon.DesignerName ?? string.Empty;
        try
        {
            pawn.WeaponServices!.ActiveWeapon.Raw = weapon.EntityHandle.Raw;
        }
        catch
        {
            // ignored
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(name))
                player.ExecuteClientCommand($"use {name}");
        }
        catch
        {
            // ignored
        }

        try
        {
            player.ExecuteClientCommand("drop");
        }
        catch
        {
            // ignored
        }

        try
        {
            if (pawn.ItemServices != null)
            {
                var items = pawn.ItemServices.As<CCSPlayer_ItemServices>();
                items.DropActivePlayerWeapon(weapon);
            }
            else
            {
                player.DropActiveWeapon();
            }
        }
        catch
        {
            try
            {
                player.DropActiveWeapon();
            }
            catch
            {
                // ignored
            }
        }

        var part = NormalizeWeaponPart(name);
        if (!string.IsNullOrEmpty(part) && CountWeapon(pawn, part) > 0)
        {
            try
            {
                DropBySwitchingActive(player, pawn, weapon);
            }
            catch
            {
                // ignored
            }
        }

        Server.NextFrame(() => ApplyThrowImpulse(player, weapon));
    }

    private static void ApplyThrowImpulse(CCSPlayerController player, CBasePlayerWeapon weapon)
    {
        if (!weapon.IsValid || !IsAlivePawn(player, out var pawn))
            return;

        try
        {
            var owner = weapon.OwnerEntity?.Value;
            if (owner != null && owner.IsValid && owner.Index == pawn.Index)
                return;
        }
        catch
        {
            // ignored
        }

        var eye = pawn.EyeAngles;
        var pitch = eye.X * MathF.PI / 180f;
        var yaw = eye.Y * MathF.PI / 180f;
        var cp = MathF.Cos(pitch);
        var speed = 420f;
        var vel = new Vector(
            cp * MathF.Cos(yaw) * speed,
            cp * MathF.Sin(yaw) * speed,
            -MathF.Sin(pitch) * speed + 180f);

        try
        {
            weapon.Teleport(null, null, vel);
        }
        catch
        {
            // ignored
        }
    }

    private static string NormalizeWeaponPart(string weapon)
    {
        var name = weapon.Trim().ToLowerInvariant();
        if (name.StartsWith("weapon_"))
            name = name["weapon_".Length..];
        return name;
    }

    private static bool MatchesWeapon(string designerName, string part)
    {
        var name = NormalizeWeaponPart(designerName);
        part = NormalizeWeaponPart(part);
        if (name.Equals(part, StringComparison.OrdinalIgnoreCase) ||
            name.Contains(part, StringComparison.OrdinalIgnoreCase) ||
            part.Contains(name, StringComparison.OrdinalIgnoreCase))
            return true;

        return IsUspFamily(name) && IsUspFamily(part);
    }

    private static bool IsUspFamily(string name)
    {
        return name.Contains("usp", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("hkp2000", StringComparison.OrdinalIgnoreCase);
    }
}

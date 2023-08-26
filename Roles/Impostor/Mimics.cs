using AmongUs.GameOptions;
using MS.Internal.Xml.XPath;
using System;
using System.Collections.Generic;
using static TOHE.Options;
using static UnityEngine.GraphicsBuffer;

namespace TOHE.Roles.Impostor;
//模仿者杀手来源：TOHTOR https://github.com/music-discussion/TownOfHost-TheOtherRoles
public static class Mimics
{
    private static readonly int Id = 574687;
    public static List<byte> playerIdList = new();
    public static OptionItem SKillColldown;
    public static OptionItem DiedToge;
    public static List<byte> KillerList = new();
    public static List<byte> TargetList = new();
    static GameData.PlayerOutfit CamouflageOutfit = new GameData.PlayerOutfit().Set("", "", "", "", "");
    public static Dictionary<byte, GameData.PlayerOutfit> PlayerSkins = new();
    public static Dictionary<byte, GameData.PlayerOutfit> KillerSkins = new();
    public static OptionItem Arrow;
    public static readonly string[] MedicWhoCanSeeProtectName =
{
        "DieSus",
        "BecomeKiller",
        "BecomeImp",
    };
    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mimics, 1, zeroOne: false);
        SKillColldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 100f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Mimics])
           .SetValueFormat(OptionFormat.Seconds);
        DiedToge = StringOptionItem.Create(Id + 4, "DieTogether", MedicWhoCanSeeProtectName, 0, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Mimics]);
        //Arrow = BooleanOptionItem.Create(Id + 3, "HaveArrow", false, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Mimics]);
    }
    public static void Init()
    {
        playerIdList = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }
    public static bool IsEnable() => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = SKillColldown.GetFloat();
    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.Is(CustomRoles.MimicKiller)) return;
        GameData.PlayerOutfit outfit = new();
        var sender = CustomRpcSender.Create(name: $"Camouflage.RpcSetSkin({target.Data.PlayerName})");
        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc.Is(CustomRoles.MimicKiller))
            {
                var killeroutfit = killer.Data.DefaultOutfit;
                KillerSkins[pc.PlayerId] = new GameData.PlayerOutfit().Set(killeroutfit.PlayerName, killeroutfit.ColorId, killeroutfit.HatId, killeroutfit.SkinId, killeroutfit.VisorId, killeroutfit.PetId);
                var targetcolorId = target.Data.DefaultOutfit.ColorId;
                Main.PlayerStates[pc.PlayerId] = new(pc.PlayerId);
                Main.PlayerColors[pc.PlayerId] = Palette.PlayerColors[targetcolorId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod); //移動速度をデフォルトの移動速度に変更
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                ReportDeadBodyPatch.WaitReport[pc.PlayerId] = new();
                pc.cosmetics.nameText.text = pc.name;
                RandomSpawn.CustomNetworkTransformPatch.NumOfTP.Add(pc.PlayerId, 0);
                var outfits = target.Data.DefaultOutfit;
                PlayerSkins[pc.PlayerId] = new GameData.PlayerOutfit().Set(outfits.PlayerName, outfits.ColorId, outfits.HatId, outfits.SkinId, outfits.VisorId, outfits.PetId);
            }

        }
        new LateTask(() =>
        {
            outfit = PlayerSkins[killer.PlayerId];
            //凶手变样子
            killer.SetColor(outfit.ColorId);
            sender.AutoStartRpc(killer.NetId, (byte)RpcCalls.SetColor)
                .Write(outfit.ColorId)
                .EndRpc();

            killer.SetHat(outfit.HatId, outfit.ColorId);
            sender.AutoStartRpc(killer.NetId, (byte)RpcCalls.SetHatStr)
                .Write(outfit.HatId)
                .EndRpc();

            killer.SetSkin(outfit.SkinId, outfit.ColorId);
            sender.AutoStartRpc(killer.NetId, (byte)RpcCalls.SetSkinStr)
                .Write(outfit.SkinId)
                .EndRpc();

            killer.SetVisor(outfit.VisorId, outfit.ColorId);
            sender.AutoStartRpc(killer.NetId, (byte)RpcCalls.SetVisorStr)
                .Write(outfit.VisorId)
                .EndRpc();

            killer.SetPet(outfit.PetId);
            sender.AutoStartRpc(killer.NetId, (byte)RpcCalls.SetPetStr)
                .Write(outfit.PetId)
                .EndRpc();
            sender.SendMessage();
        }, 0.1f, "Trapper BlockMove");
    }
   /* public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (!seer.Is(CustomRoles.MimicKiller)|| !seer.Is(CustomRoles.MimicAss)) return "";
        if (target != null && seer.PlayerId != target.PlayerId) return "";
        if (GameStates.IsMeeting) return "";
        foreach (var kill in Main.AllAlivePlayerControls)
        {
            if (kill.Is(CustomRoles.MimicKiller))
            {
                foreach (var ass in Main.AllAlivePlayerControls)
                {
                    if (ass.Is(CustomRoles.MimicAss))
                    {
                        var playerId = ass.PlayerId;
                        NameColorManager.Add(ass.PlayerId, ass.PlayerId, "#FF0000");
                        var targetId = ass.PlayerId;
                        TargetArrow.Add(playerId, targetId);
                        return TargetArrow.GetArrows(seer, targetId);
                    }
                }
            }
            if (kill.Is(CustomRoles.MimicAss))
            {
                foreach (var ass in Main.AllAlivePlayerControls)
                {
                    if (ass.Is(CustomRoles.MimicKiller))
                    {
                        var playerId = ass.PlayerId;
                        NameColorManager.Add(ass.PlayerId, ass.PlayerId, "#FF0000");
                        var targetId = ass.PlayerId;
                        TargetArrow.Add(playerId, targetId);
                        return TargetArrow.GetArrows(seer, targetId);
                    }
                }
            }
        }
        return TargetArrow.GetArrows(seer);
    }*/
}
static class PlayerOutfitExtension
{
    public static GameData.PlayerOutfit Set(this GameData.PlayerOutfit instance, string playerName, string hatId, string skinId, string visorId, string petId)
    {
        foreach (var player in Main.AllAlivePlayerControls)
        {
            if (Mimics.TargetList.Contains(player.PlayerId))
            {
                instance.PlayerName = playerName;
                instance.HatId = hatId;
                instance.SkinId = skinId;
                instance.VisorId = visorId;
                instance.PetId = petId;
                return instance;
            }
            if (Mimics.KillerList.Contains(player.PlayerId))
            {
                instance.PlayerName = playerName;
                instance.HatId = hatId;
                instance.SkinId = skinId;
                instance.VisorId = visorId;
                instance.PetId = petId;
                return instance;
            }
        }
        return instance;
    }
}

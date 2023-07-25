using Hazel;
using System.Collections.Generic;
using TOHE.Modules;
using UnityEngine;

namespace TOHE.Roles.Crewmate;

public static class DemonHunterm
{
    private static readonly int Id = 1235874;
    private static List<byte> playerIdList = new();
    public static Dictionary<byte, int> DemonHunterLimit = new();
    private static OptionItem SkillCooldown;
    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.DemonHunterm);
        SkillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(2.5f, 900f, 2.5f), 20f, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DemonHunterm])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = new();
        DemonHunterLimit = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        DemonHunterLimit.TryAdd(playerId, 0);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDemonHuntermSellLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(DemonHunterLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (DemonHunterLimit.ContainsKey(PlayerId))
            DemonHunterLimit[PlayerId] = Limit;
        else
            DemonHunterLimit.Add(PlayerId, 0);
    }
    public static bool CanUseKillButton(byte playerId)
        => !Main.PlayerStates[playerId].IsDead
        && (DemonHunterLimit.TryGetValue(playerId, out var x) ? x : 1) >= 1;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(id) ? SkillCooldown.GetFloat() : 300f;
    public static string GetSkillLimit(byte playerId) => Utils.ColorString(CanUseKillButton(playerId) ? Utils.GetRoleColor(CustomRoles.DemonHunterm) : Color.gray, DemonHunterLimit.TryGetValue(playerId, out var demonHunterLimit) ? $"({demonHunterLimit})" : "Invalid");
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (DemonHunterLimit[killer.PlayerId] <= 0)
        {
            NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), ("您没有力量了！通过报告尸体来汲取力量"))); ;
            return false;
        }
        DemonHunterLimit[killer.PlayerId]--;
        Utils.TP(killer.NetTransform, target.GetTruePosition());
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        target.SetRealKiller(killer);
        Main.PlayerStates[target.PlayerId].SetDead();
        killer.SetKillCooldownV2();
        target.RpcMurderPlayerV3(target);
        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
        return false;
    }   
 }
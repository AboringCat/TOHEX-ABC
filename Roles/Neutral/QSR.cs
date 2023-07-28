using Hazel;
using System.Collections.Generic;
using TOHE.Modules;
using UnityEngine;

namespace TOHE.Roles.Neutral;

public static class QSR
{
    private static readonly int Id = 7565120;
    private static List<byte> playerIdList = new();
   public static Dictionary<byte, int> QSRLimit = new();
    public static OptionItem SkillLimitOpt;
    public static OptionItem SkillCooldown;
    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.QSR);
    }
    public static void Init()
    {
        playerIdList = new();
        QSRLimit = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        QSRLimit.TryAdd(playerId, SkillLimitOpt.GetInt());

        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return;
       if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetQSRSellLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(QSRLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (QSRLimit.ContainsKey(PlayerId))
            QSRLimit[PlayerId] = Limit;
        else
            QSRLimit.Add(PlayerId, SkillLimitOpt.GetInt());
    }
    public static bool CanUseKillButton(byte playerId)
        => !Main.PlayerStates[playerId].IsDead
        && (QSRLimit.TryGetValue(playerId, out var x) ? x : 1) >= 1;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(id) ? SkillCooldown.GetFloat() : 300f;
    public static string GetSkillLimit(byte playerId) => Utils.ColorString(CanUseKillButton(playerId) ? Utils.GetRoleColor(CustomRoles.QSR) : Color.gray, QSRLimit.TryGetValue(playerId, out var qsrLimit) ? $"({qsrLimit})" : "Invalid");
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
   {
        if (QSRLimit[killer.PlayerId] <= 0)
        {
            NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.QSR), ("你没有空包弹了"))); ;
            return false;
        }
       Main.QSRInProtect.Remove(target.PlayerId);
       Main.QSRInProtect.Add(target.PlayerId);
      QSRLimit[killer.PlayerId]--;
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        killer.RpcGuardAndKill(target);
        target.RpcGuardAndKill(killer);
        return false;
    }
}
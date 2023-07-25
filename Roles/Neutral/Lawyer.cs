﻿using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class Lawyer
{
    private static readonly int Id = 756580;
    public static List<byte> playerIdList = new();
    public static byte WinnerID;

    private static OptionItem CanTargetCrewmate;
    private static OptionItem CanTargetJester;
    public static OptionItem KnowTargetRole;
    public static OptionItem TargetKnowsLawyer;

    /// <summary>
    /// Key: エクスキューショナーのPlayerId, Value: ターゲットのPlayerId
    /// </summary>
    public static Dictionary<byte, byte> Target = new();
    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Lawyer);
        //    LawyerVision = FloatOptionItem.Create(Id + 14, "LawyerVision", new(0f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer])
        //        .SetValueFormat(OptionFormat.Multiplier);
        //CanTargetImpostor = BooleanOptionItem.Create(Id + 10, "LawyerCanTargetImpostor", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        //CanTargetNeutralKiller = BooleanOptionItem.Create(Id + 11, "LawyerCanTargetNeutralKiller", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetCrewmate = BooleanOptionItem.Create(Id + 12, "LawyerCanTargetCrewmate", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetJester = BooleanOptionItem.Create(Id + 13, "LawyerCanTargetJester", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        KnowTargetRole = BooleanOptionItem.Create(Id + 14, "KnowTargetRole", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        TargetKnowsLawyer = BooleanOptionItem.Create(Id + 15, "TargetKnowsLawyer", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        //ChangeRolesAfterTargetKilled = StringOptionItem.Create(Id + 16, "LawyerChangeRolesAfterTargetKilled", ChangeRoles, 1, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        QSR.SkillCooldown = FloatOptionItem.Create(Id + 15, "QSRSkillCooldown", new(2.5f, 900f, 2.5f), 20f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer])
            .SetValueFormat(OptionFormat.Seconds);
        QSR.SkillLimitOpt = IntegerOptionItem.Create(Id + 16, "QSRSkillLimit", new(1, 990, 1), 3, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer])
            .SetValueFormat(OptionFormat.Times);
    }
    public static void Init()
    {
        playerIdList = new();
        Target = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        //ターゲット割り当て
        if (AmongUsClient.Instance.AmHost)
        {
            List<PlayerControl> targetList = new();
            var rand = IRandom.Instance;
            foreach (var target in Main.AllPlayerControls)
            {
                if (playerId == target.PlayerId) continue;
                else if (!CanTargetCrewmate.GetBool() && target.Is(CustomRoleTypes.Crewmate)) continue;
                else if (!CanTargetJester.GetBool() && target.Is(CustomRoles.Jester)) continue;
                if (target.Is(CustomRoleTypes.Neutral) && !target.IsNKS() && !target.Is(CustomRoles.Jester)) continue;
                if (target.GetCustomRole() is CustomRoles.GM or CustomRoles.SuperStar or CustomRoles.Captain) continue;
                if (Utils.GetPlayerById(playerId).Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers)) continue;

                targetList.Add(target);
            }
            var SelectedTarget = targetList[rand.Next(targetList.Count)];
            Target.Add(playerId, SelectedTarget.PlayerId);
            SendRPC(playerId, SelectedTarget.PlayerId, "SetTarget");
            Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole()}:{SelectedTarget.GetNameWithRole()}", "Lawyer");
            Logger.Info($"Player ID: {playerId}", "Lawyer");
        }
    }
    public static bool IsEnable() => playerIdList.Count > 0;
    public static void SendRPC(byte lawyerId, byte targetId = 0x73, string Progress = "")
    {
        MessageWriter writer;
        switch (Progress)
        {
            case "SetTarget":
                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetLawyerTarget, SendOption.Reliable);
                writer.Write(lawyerId);
                writer.Write(targetId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;
            case "":
                if (!AmongUsClient.Instance.AmHost) return;
                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RemoveLawyerTarget, SendOption.Reliable);
                writer.Write(lawyerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;

        }
    }
    public static void ReceiveRPC(MessageReader reader, bool SetTarget)
    {
        if (SetTarget)
        {
            byte LawyerId = reader.ReadByte();
            byte TargetId = reader.ReadByte();
            Target[LawyerId] = TargetId;
        }
        else
            Target.Remove(reader.ReadByte());
    }
    public static void ChangeRoleByTarget(PlayerControl target)
    {
        byte Lawyer = 0x73;
        Target.Do(x =>
        {
            if (x.Value == target.PlayerId)
                Lawyer = x.Key;
        });
        Utils.GetPlayerById(Lawyer).RpcSetCustomRole(CustomRoles.QSR);
        QSR.Add(Lawyer);
        QSR.Add(Lawyer);
        Utils.GetPlayerById(Lawyer).ResetKillCooldown();
        Utils.GetPlayerById(Lawyer).SetKillCooldown();
        Utils.GetPlayerById(Lawyer).RpcGuardAndKill(Utils.GetPlayerById(Lawyer));
        Target.Remove(Lawyer);
        SendRPC(Lawyer);
        Utils.NotifyRoles();
    }
    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (!KnowTargetRole.GetBool()) return false;
        return player.Is(CustomRoles.Lawyer) && Target.TryGetValue(player.PlayerId, out var tar) && tar == target.PlayerId;
    }

    public static void ChangeRole(PlayerControl lawyer)
    {
        lawyer.RpcSetCustomRole(CustomRoles.QSR);
        QSR.Add(lawyer.PlayerId);
        QSR.Add(lawyer.PlayerId);
        lawyer.ResetKillCooldown();
        lawyer.SetKillCooldown();
        lawyer.RpcGuardAndKill(lawyer);
        Target.Remove(lawyer.PlayerId);
        SendRPC(lawyer.PlayerId);
        var text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lawyer), Translator.GetString(""));
        text = string.Format(text, Utils.ColorString(Utils.GetRoleColor(CustomRoles.QSR), Translator.GetString(CustomRoles.QSR.ToString())));
        lawyer.Notify(text);
    }
    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Lawyer))
        {
            if (!TargetKnowsLawyer.GetBool()) return "";
            return (Target.TryGetValue(target.PlayerId, out var x) && seer.PlayerId == x) ?
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Totocalcio), "§") : "";
        }
        var GetValue = Target.TryGetValue(seer.PlayerId, out var targetId);
        return GetValue && targetId == target.PlayerId ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lawyer), "§") : "";
    }
    public static bool CheckExileTarget(GameData.PlayerInfo exiled, bool DecidedWinner, bool Check = false)
    {
        foreach (var kvp in Target.Where(x => x.Value == exiled.PlayerId))
        {
            var lawyer = Utils.GetPlayerById(kvp.Key);
            if (lawyer == null || !lawyer.IsAlive() || lawyer.Data.Disconnected) continue;
            return true;
        }
        return false;
    }
}
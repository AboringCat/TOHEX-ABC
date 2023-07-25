using AmongUs.GameOptions;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Hazel;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Jackal
{
    private static readonly int Id = 50900;
    public static List<byte> playerIdList = new();

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem CanUseSabotage;
    public static OptionItem CanWinBySabotageWhenNoImpAlive;
    private static OptionItem HasImpostorVision;
    //private static OptionItem AttendantLimitOpt;
    private static OptionItem JackalCanAttendant;
    private static OptionItem JackalCanAttendantMax;
    private static OptionItem AttendantCanRoles;
    public static OptionItem AttendantCountMode;

    public static readonly string[] attendantCountMode =
    {
        "AttendantCountMode.None",
        "AttendantCountMode.Jackal",
        "AttendantCountMode.Original",
    };

    public static Dictionary<byte, int> AttendantLimit = new();

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jackal, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "GangsterRecruitCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanUseSabotage = BooleanOptionItem.Create(Id + 12, "CanUseSabotage", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanWinBySabotageWhenNoImpAlive = BooleanOptionItem.Create(Id + 14, "JackalCanWinBySabotageWhenNoImpAlive", true, TabGroup.NeutralRoles, false).SetParent(CanUseSabotage);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        JackalCanAttendant = BooleanOptionItem.Create(Id + 16, "JackalCanAttendant", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        JackalCanAttendantMax = IntegerOptionItem.Create(Id + 17, "JackalCanAttendantMax", new(1, 5, 1), 1, TabGroup.NeutralRoles, false).SetParent(JackalCanAttendant)
            .SetValueFormat(OptionFormat.Poeple);
        AttendantCanRoles = BooleanOptionItem.Create(Id + 19, "AttendantCanRoles", false, TabGroup.NeutralRoles, false).SetParent(JackalCanAttendant);
        AttendantCountMode = StringOptionItem.Create(Id + 18, "AttendantCountMode", attendantCountMode, 0, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
    }
    public static void Init()
    {
        playerIdList = new();
        AttendantLimit = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        AttendantLimit.TryAdd(playerId, JackalCanAttendantMax.GetInt());
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJackalAttendantLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(AttendantLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        AttendantLimit.TryAdd(PlayerId, Limit);
        AttendantLimit[PlayerId] = Limit;
    }
    public static string GetAttendantLimit(byte playerId) => Utils.ColorString(CanAttendant(playerId) ? Color.blue : Color.gray, AttendantLimit.TryGetValue(playerId, out var attendantLimit) ? $"({attendantLimit})" : "Invalid");
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanAttendant(id) ? KillCooldown.GetFloat() : Options.DefaultKillCooldown;
    public static bool CanAttendant(byte id) => AttendantLimit.TryGetValue(id, out var x) && x > 0;
    public static void SetKillButtonText(byte plaeryId)
    {
        if (CanAttendant(plaeryId) && JackalCanAttendant.GetBool())
            HudManager.Instance.KillButton.OverrideText(GetString("GangsterButtonText"));
        else
            HudManager.Instance.KillButton.OverrideText(GetString("KillButtonText"));
    }
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void CanUseVent(PlayerControl player)
    {
        bool jackal_canUse = CanVent.GetBool();
        DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.ToggleVisible(jackal_canUse && !player.Data.IsDead);
        player.Data.Role.CanVent = jackal_canUse;
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!JackalCanAttendant.GetBool() || AttendantLimit[killer.PlayerId] < 1) return false;
        if (JackalCanAttendant.GetBool() && CanBeAttendant(target))
        {
            AttendantLimit[killer.PlayerId]--;
            SendRPC(killer.PlayerId);
            if (AttendantCanRoles.GetBool())
            {
                target.RpcSetCustomRole(CustomRoles.Attendant);
            }
            else
            {
                if (!target.CanUseKillButton() && !target.Is(CustomRoles.Captain) && !target.Is(CustomRoles.Solicited) && !target.Is(CustomRoles.Believer))
                {
                    target.RpcSetCustomRole(CustomRoles.Whoops);
                }
                if (target.CanUseKillButton() && !target.Is(CustomRoles.Captain) && !target.Is(CustomRoles.Solicited) && !target.Is(CustomRoles.Believer))
                {
                    target.RpcSetCustomRole(CustomRoles.Sidekick);
                }
            }

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeAttendantByAttendant")));
            Utils.NotifyRoles();

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("设置职业:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Attendant.ToString(), "Assign " + CustomRoles.Attendant.ToString());
            if (AttendantLimit[killer.PlayerId] < 0)
                HudManager.Instance.KillButton.OverrideText(GetString("KillButtonText"));
            Logger.Info($"{killer.GetNameWithRole()} : 剩余{AttendantLimit[killer.PlayerId]}次招募机会", "Jackal");
            return true;
        }
        if (AttendantLimit[killer.PlayerId] < 0)
            HudManager.Instance.KillButton.OverrideText(GetString("KillButtonText"));
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterRecruitmentFailure")));
        Logger.Info($"{killer.GetNameWithRole()} : 剩余{AttendantLimit[killer.PlayerId]}次招募机会", "Attendant");
        return false;
    }
    public static void SetHudActive(HudManager __instance, bool isActive)
    {
        __instance.SabotageButton.ToggleVisible(isActive && CanUseSabotage.GetBool());
    }
    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (player.Is(CustomRoles.Attendant) && target.Is(CustomRoles.Jackal)) return true;
        if (player.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Attendant)) return true;
        if (player.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Attendant)) return true;
        if (player.Is(CustomRoles.Whoops) && target.Is(CustomRoles.Jackal)) return true;
        if (player.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Whoops)) return true;
        if (player.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Whoops)) return true;
        if (player.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal)) return true;
        if (player.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick)) return true;
        if (player.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick)) return true;
        return false;
    }
    public static bool CanBeAttendant(this PlayerControl pc)
    {
        return pc != null && (pc.GetCustomRole().IsCrewmate() || pc.GetCustomRole().IsImpostor()) && !pc.Is(CustomRoles.Captain) && !pc.Is(CustomRoles.Attendant) && !pc.Is(CustomRoles.Solicited) && !pc.Is(CustomRoles.seniormanagement) && !pc.Is(CustomRoles.Believer) && !pc.Is(CustomRoles.Gangster) || !AttendantCanRoles.GetBool() && (pc.GetCustomRole().IsCrewmate() || pc.GetCustomRole().IsImpostor() && pc.GetCustomRole().IsNeutral() && !pc.Is(CustomRoles.Captain) && !pc.Is(CustomRoles.Solicited));
    }
}
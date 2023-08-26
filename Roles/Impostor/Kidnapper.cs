/*using System.Collections.Generic;
using static TOHE.Options;
using UnityEngine;
using static TOHE.Translator;
using static UnityEngine.GraphicsBuffer;
using TOHE.Modules;
using TOHE.Roles.Neutral;
using MS.Internal.Xml.XPath;
using Rewired.Utils.Platforms.Windows;

namespace TOHE.Roles.Impostor;

public static class Kidnapper
{
    private static readonly int Id = 1658974;
    private static List<byte> playerIdList = new();

    public static OptionItem SkillCooldown;
    private static OptionItem KidnapperDuration;
    public static Dictionary<byte, long> KidnapperUp = new();
    public static List<byte> ForKidnapper = new ();
    public static List<byte> NeedKidnapper = new();

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Kidnapper);
        SkillCooldown = FloatOptionItem.Create(Id + 42, "KidnapperSkillCooldown", new(2.5f, 900f, 2.5f), 20f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Kidnapper])
           .SetValueFormat(OptionFormat.Seconds);
        KidnapperDuration = FloatOptionItem.Create(Id + 4, "KidnapperDuration", new(1f, 999f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Kidnapper])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = new();
        KidnapperUp = new();
        ForKidnapper = new();
        NeedKidnapper = new(); 
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = SkillCooldown.GetFloat();
    public static bool CheckKidnapperMurder(PlayerControl killer, PlayerControl target)
    {
        if (KidnapperUp.ContainsKey(target.PlayerId)) return false;
        killer.SetKillCooldownV2();
        killer.RPCPlayCustomSound("Shield");
        StartConvertCountDown(killer, target);
        return true;
    }
    private static void StartConvertCountDown(PlayerControl killer, PlayerControl target)
    {
            if (GameStates.IsInGame && GameStates.IsInTask && !GameStates.IsMeeting && target.IsAlive() && !Pelican.IsEaten(target.PlayerId))
            {
            KidnapperUp.Add(target.PlayerId, Utils.GetTimeStamp());
            ForKidnapper.Add(target.PlayerId);
            if (!killer.inVent) killer.RpcGuardAndKill(killer);
                Utils.NotifyRoles();
                Logger.Info($"{target.GetNameWithRole()} 转化为量子幽灵", "BallLightning");
            }
    }
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable) return;

        foreach (var pc in ForKidnapper)
        {
            var si = Utils.GetPlayerById(pc);
                NeedKidnapper.Add(pc);
                var tmpSpeed = Main.AllPlayerSpeed[si.PlayerId];
                Main.AllPlayerSpeed[si.PlayerId] = Main.MinSpeed;
                ReportDeadBodyPatch.CanReport[si.PlayerId] = false;
                si.MarkDirtySettings();
            if (KidnapperUp.TryGetValue(si.PlayerId, out var vtime) && vtime + KidnapperDuration.GetInt() < Utils.GetTimeStamp())
            {
                NameNotifyManager.Notify(player, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("NotAssassin")));
                si.RpcMurderPlayerV3(si);
                si.SetRealKiller(player);
                KidnapperUp.Remove(si.PlayerId);
                ForKidnapper.Remove(si.PlayerId);
                new LateTask(() =>
                {
                    Main.AllPlayerSpeed[si.PlayerId] = Main.AllPlayerSpeed[si.PlayerId] -Main.MinSpeed + tmpSpeed;
                    ReportDeadBodyPatch.CanReport[si.PlayerId] = true;
                    si.MarkDirtySettings();
                    RPC.PlaySoundRPC(si.PlayerId, Sounds.TaskComplete);
                }, 0.5f, "Trapper BlockMove");
                Logger.Info($"撕票", "Kid");
            }
            foreach (var pcs in Main.AllAlivePlayerControls)
            {
                var posi = si.transform.position;
                var diss = Vector2.Distance(posi, pcs.transform.position);
                if (diss > 0.3f && pcs.Is(CustomRoles.Kidnapper)) continue;
                if (diss < 0.3f && !pcs.Is(CustomRoles.Kidnapper) && pcs.PlayerId != si.PlayerId)
                {
                    ForKidnapper.Remove(si.PlayerId);
                    KidnapperUp.Remove(si.PlayerId);
                    new LateTask(() =>
                    {
                        Main.AllPlayerSpeed[si.PlayerId] = Main.AllPlayerSpeed[si.PlayerId] - Main.MinSpeed + tmpSpeed;
                        ReportDeadBodyPatch.CanReport[si.PlayerId] = true;
                        si.MarkDirtySettings();
                        RPC.PlaySoundRPC(si.PlayerId, Sounds.TaskComplete);
                    }, 0.5f, "Trapper BlockMove");
                    si.MarkDirtySettings();
                    Logger.Info($"{pcs.PlayerId}解救", "Kid");
                }
            }
        }      
                
    }
}      */
    

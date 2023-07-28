using AmongUs.GameOptions;
using HarmonyLib;
using MS.Internal.Xml.XPath;
using Sentry.Internal.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;
using Hazel;
using InnerNet;
using System.Threading.Tasks;
using TOHE.Modules;
using TOHE.Roles.AddOns.Crewmate;
using UnityEngine.Profiling;
using System.Runtime.Intrinsics.X86;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.UI;
using UnityEngine.Networking.Types;

namespace TOHE;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckProtect))]
class CheckProtectPatch
{
    
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return false;
        Logger.Info("CheckProtect発生: " + __instance.GetNameWithRole() + "=>" + target.GetNameWithRole(), "CheckProtect");
        if (__instance.Is(CustomRoles.Sheriff))
        {
            if (__instance.Data.IsDead)
            {
                Logger.Info("守護をブロックしました。", "CheckProtect");
                return false;
            }
        }
        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
class CheckMurderPatch
{
    public static int YLLevel = 0;
    public static int YLdj = 1;
    public static int YLCS = 0;
    public static Dictionary<byte, float> TimeSinceLastKill = new();
    private static Dictionary<byte, float> NowCooldown;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = NowCooldown[id]; 
    public static void Update()
    {
        for (byte i = 0; i < 15; i++)
        {
            if (TimeSinceLastKill.ContainsKey(i))
            {
                TimeSinceLastKill[i] += Time.deltaTime;
                if (15f < TimeSinceLastKill[i]) TimeSinceLastKill.Remove(i);
            }
        }
    }
    #region 击杀技能判定
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return false;

        var killer = __instance; //読み替え変数

        Logger.Info($"{killer.GetNameWithRole()} => {target.GetNameWithRole()}", "CheckMurder");

        //死人はキルできない
        if (killer.Data.IsDead)
        {
            Logger.Info($"{killer.GetNameWithRole()}は死亡しているためキャンセルされました。", "CheckMurder");
            return false;
        }

        //不正キル防止処理
        if (target.Data == null || //PlayerDataがnullじゃないか確認
            target.inVent || target.inMovingPlat //targetの状態をチェック
        )
        {
            Logger.Info("目标处于无法被击杀状态，击杀被取消", "CheckMurder");
            return false;
        }
        if (target.Data.IsDead) //同じtargetへの同時キルをブロック
        {
            Logger.Info("目标处于死亡状态，击杀被取消", "CheckMurder");
            return false;
        }
        if (MeetingHud.Instance != null) //会議中でないかの判定
        {
            Logger.Info("会议中，击杀被取消", "CheckMurder");
            return false;
        }

        var divice = Options.CurrentGameMode == CustomGameMode.SoloKombat ? 3000f : 2000f;
        var pivice = Options.CurrentGameMode == CustomGameMode.HotPotato ? 3000f : 2000f;
        float minTime = Mathf.Max(0.02f, AmongUsClient.Instance.Ping / divice * 6f); //※AmongUsClient.Instance.Pingの値はミリ秒(ms)なので÷1000
        //TimeSinceLastKillに値が保存されていない || 保存されている時間がminTime以上 => キルを許可
        //↓許可されない場合
        if (TimeSinceLastKill.TryGetValue(killer.PlayerId, out var time) && time < minTime)
        {
            Logger.Info("击杀间隔过短，击杀被取消", "CheckMurder");
            return false;
        }
        TimeSinceLastKill[killer.PlayerId] = 0f;

        killer.ResetKillCooldown();

        //キル可能判定
        if (killer.PlayerId != target.PlayerId && !killer.CanUseKillButton())
        {
            Logger.Info(killer.GetNameWithRole() + "击杀者不被允许使用击杀键，击杀被取消", "CheckMurder");
            return false;
        }

        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            SoloKombatManager.OnPlayerAttack(killer, target);
            return false;
        }
        if (Options.CurrentGameMode == CustomGameMode.HotPotato)
        {
            HotPotatoManager.OnPlayerAttack(killer, target);
            return false;
        }
        /*if (Options.CurrentGameMode == CustomGameMode.ModeArrest)
        {
            ModeArrestManager.OnPlayerAttack(killer, target);
            return false;
        }*/

        //実際のキラーとkillerが違う場合の入れ替え処理
        if (Sniper.IsEnable) Sniper.TryGetSniper(target.PlayerId, ref killer);
        if (killer != __instance) Logger.Info($"Real Killer={killer.GetNameWithRole()}", "CheckMurder");

        //鹈鹕肚子里的人无法击杀
        if (Pelican.IsEaten(target.PlayerId))
            return false;

        //阻止对活死人的操作
        if (target.Is(CustomRoles.Glitch))
            return false;

        // 赝品检查
        if (Counterfeiter.OnClientMurder(killer)) return false;

        //判定凶手技能
        if (killer.PlayerId != target.PlayerId)
        {
            //非自杀场景下才会触发
            switch (killer.GetCustomRole())
            {
                //==========内鬼阵营==========//
                case CustomRoles.BountyHunter: //必须在击杀发生前处理
                    BountyHunter.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.SerialKiller:
                    SerialKiller.OnCheckMurder(killer);
                    break;
                case CustomRoles.Vampire:
                    if (!Vampire.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Warlock:
                    if (!Main.CheckShapeshift[killer.PlayerId] && !Main.isCurseAndKill[killer.PlayerId])
                    { //Warlockが変身時以外にキルしたら、呪われる処理
                        if (target.Is(CustomRoles.Needy))
                        {
                            killer.RpcGuardAndKill(target);
                            return false;
                        }
                        Main.isCursed = true;
                        killer.SetKillCooldownV2();
                        killer.RPCPlayCustomSound("Line");
                        Main.CursedPlayers[killer.PlayerId] = target;
                        Main.WarlockTimer.Add(killer.PlayerId, 0f);
                        Main.isCurseAndKill[killer.PlayerId] = true;
                        RPC.RpcSyncCurseAndKill();
                        return false;
                    }
                    if (Main.CheckShapeshift[killer.PlayerId])
                    {//呪われてる人がいないくて変身してるときに通常キルになる
                        killer.RpcCheckAndMurder(target);
                        return false;
                    }
                    return false;
                case CustomRoles.Witch:
                    if (!Witch.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Puppeteer:
                    if (target.Is(CustomRoles.Needy)) return false;
                    Main.PuppeteerList[target.PlayerId] = killer.PlayerId;
                    RPC.RpcSyncPuppeteerList();
                    killer.SetKillCooldownV2();
                    killer.RPCPlayCustomSound("Line");
                    Utils.NotifyRoles(SpecifySeer: killer);
                    return false;
                case CustomRoles.Capitalism:
                    if (!Main.CapitalismAddTask.ContainsKey(target.PlayerId))
                        Main.CapitalismAddTask.Add(target.PlayerId, 0);
                    Main.CapitalismAddTask[target.PlayerId]++;
                    if (!Main.CapitalismAssignTask.ContainsKey(target.PlayerId))
                        Main.CapitalismAssignTask.Add(target.PlayerId, 0);
                    Main.CapitalismAssignTask[target.PlayerId]++;
                    Logger.Info($"资本主义 {killer.GetRealName()} 又开始祸害人了：{target.GetRealName()}", "Capitalism Add Task");
                    killer.RpcGuardAndKill(killer);
                    killer.SetKillCooldown();
                    return false;
                case CustomRoles.Gangster:
                    if (Gangster.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.BallLightning:
                    if (BallLightning.CheckBallLightningMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.Greedier:
                    Greedier.OnCheckMurder(killer);
                    break;
                case CustomRoles.QuickShooter:
                    QuickShooter.QuickShooterKill(killer);
                    break;
                case CustomRoles.Sans:
                    Sans.OnCheckMurder(killer);
                    break;
                case CustomRoles.Hangman:
                    if (!Hangman.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Swooper:
                    if (!Swooper.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Vandalism:
                    Vandalism.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Corpse:
                    Corpse.OnCheckMurder(target);
                    break;
                case CustomRoles.DoubleKiller:
                    DoubleKiller.OnCheckMurder(killer);
                    break;
                case CustomRoles.EvilGambler:
                    EvilGambler.OnCheckMurder(killer);
                    break;
                //==========中立阵营==========//
                case CustomRoles.Arsonist:
                    killer.SetKillCooldown(Options.ArsonistDouseTime.GetFloat());
                    if (!Main.isDoused[(killer.PlayerId, target.PlayerId)] && !Main.ArsonistTimer.ContainsKey(killer.PlayerId))
                    {
                        Main.ArsonistTimer.Add(killer.PlayerId, (target, 0f));
                        Utils.NotifyRoles(SpecifySeer: __instance);
                        RPC.SetCurrentDousingTarget(killer.PlayerId, target.PlayerId);
                    }
                    return false;
                case CustomRoles.Revolutionist:
                    killer.SetKillCooldown(Options.RevolutionistDrawTime.GetFloat());
                    if (!Main.isDraw[(killer.PlayerId, target.PlayerId)] && !Main.RevolutionistTimer.ContainsKey(killer.PlayerId))
                    {
                        Main.RevolutionistTimer.TryAdd(killer.PlayerId, (target, 0f));
                        Utils.NotifyRoles(SpecifySeer: __instance);
                        RPC.SetCurrentDrawTarget(killer.PlayerId, target.PlayerId);
                    }
                    return false;
                case CustomRoles.Innocent:
                    target.RpcMurderPlayerV3(killer);
                    return false;
                case CustomRoles.Pelican:
                    if (Pelican.CanEat(killer, target.PlayerId))
                    {
                        Utils.TP(killer.NetTransform, target.GetTruePosition());
                        Pelican.EatPlayer(killer, target);
                        killer.SetKillCooldownV2();
                        killer.RPCPlayCustomSound("Eat");
                        target.RPCPlayCustomSound("Eat");
                    }
                    return false;
                case CustomRoles.FFF:
                    if (!target.Is(CustomRoles.Lovers) && !target.Is(CustomRoles.Ntr) && !target.Is(CustomRoles.CrushLovers))
                    {
                        killer.Data.IsDead = true;
                        Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                        killer.RpcMurderPlayerV3(killer);
                        Main.PlayerStates[killer.PlayerId].SetDead();
                        Logger.Info($"{killer.GetRealName()} 击杀了非目标玩家，壮烈牺牲了（bushi）", "FFF");
                        return false;
                    }
                    break;
                case CustomRoles.Gamer:
                    Gamer.CheckGamerMurder(killer, target);
                    return false;
                case CustomRoles.DarkHide:
                    DarkHide.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Provocateur:
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                    killer.RpcMurderPlayerV3(target);
                    killer.RpcMurderPlayerV3(killer);
                    killer.SetRealKiller(target);
                    Main.Provoked.TryAdd(killer.PlayerId, target.PlayerId);
                    return false;
                case CustomRoles.Totocalcio:
                    Totocalcio.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Succubus:
                    Succubus.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Jackal:
                    if (Jackal.OnCheckMurder(killer, target))
                        return false;
                    break;

                case CustomRoles.Amnesiac:
                    foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                    {
                        var pos = target.transform.position;
                        var dis = Vector2.Distance(pos, pc.transform.position);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        return false;
                    }
                    return false;
                case CustomRoles.Swapper:
                    foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                    {
                        var pos = target.transform.position;
                        var dis = Vector2.Distance(pos, pc.transform.position);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        Swapper.OnCheckMurder(killer, target);
                        return false;
                    }

                    break;
                case CustomRoles.Solicited:
                    if (!killer.Is(CustomRoles.Captain))
                    {
                        return true;
                    }
                    break;
                case CustomRoles.SchrodingerCat:
                    if (killer.Is(CustomRoles.SchrodingerCat) || killer.Is(CustomRoles.CrewSchrodingerCat))
                    {
                        return true;
                    }
                    break;
                case CustomRoles.Exorcist:
                    
                    if (!Main.ForExorcist.Contains(target.PlayerId))
                    {
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Exorcist), GetString("NotExorcist")));
                        return true;
                    }
                    else
                    {
                        Main.ForExorcist.Remove(target.PlayerId);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        Main.ExorcistMax[killer.PlayerId]++;
                        if (Main.ExorcistMax[killer.PlayerId] >= Options.MaxExorcist.GetInt())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Exorcist);
                            CustomWinnerHolder.WinnerIds.Add(killer.PlayerId);
                        }
                        return true;
                    }                  
           
                case CustomRoles.YinLang:
                    //银狼升级
                    if (killer.Is(CustomRoles.YinLang))
                    {
                        if (YLdj >= 1 && YLdj <= 5)
                        {
                            if (killer.PlayerId != target.PlayerId)
                            {
                                foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                                {
                                    var pos = target.transform.position;
                                    var dis = Vector2.Distance(pos, pc.transform.position);
                                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                                    Logger.Info("银狼击杀开始", "YL");
                                    NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang1")));
                                    Logger.Info($"{target.GetNameWithRole()} |系统警告| => {target.GetNameWithRole()}", "YinLang");
                                    var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                                    Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                                    ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
                                    target.MarkDirtySettings();
                                    new LateTask(() =>
                                    {
                                        Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed;
                                        ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
                                        target.MarkDirtySettings();
                                        RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                                    }, 10, "Trapper BlockMove");
                                    YLLevel += 1;
                                    YLCS = YinLang.YLSJ.GetInt();
                                    if (YLLevel == YinLang.YLSJ.GetInt())
                                    {
                                        YLdj += 1;
                                        YLLevel = 0;
                                    }
                                    killer.Notify(string.Format(GetString("YinLangLevel"), YLdj, YLLevel, YLCS - YLLevel));
                                    return false;
                                }
                            }
                        }
                        else if (YLdj >= 6 && YLdj <= 10)
                        {
                            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                            {
                                var pos = target.transform.position;
                                var dis = Vector2.Distance(pos, pc.transform.position);
                                killer.SetKillCooldownV2(target: target, forceAnime: true);
                                Logger.Info("银狼击杀开始", "YL");
                                NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang2")));
                                Logger.Info($"{target.GetNameWithRole()} |是否允许更改| => {target.GetNameWithRole()}", "YinLang");
                                Main.AllPlayerSpeed[target.PlayerId] = 0.01f;
                                var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                                ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
                                target.MarkDirtySettings();
                                //    NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang1")));
                                Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed - 0.01f;
                                ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
                                target.MarkDirtySettings();
                                RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                                Main.AllPlayerSpeed[target.PlayerId] = 0.01f;
                                YLLevel += 1;
                                YLCS = YinLang.YLSJ.GetInt();
                                if (YLLevel == YinLang.YLSJ.GetInt())
                                {
                                    YLdj += 1;
                                    YLLevel = 0;
                                }
                                killer.Notify(string.Format(GetString("YinLangLevel"), YLdj, YLLevel, YLCS - YLLevel));
                                return false;
                            }
                        }
                        else
                        {
                            Utils.TP(killer.NetTransform, target.GetTruePosition());
                            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                            Utils.TP(target.NetTransform, Pelican.GetBlackRoomPS());
                            target.SetRealKiller(killer);
                            Main.PlayerStates[target.PlayerId].SetDead();
                            target.RpcMurderPlayerV3(target);
                            killer.SetKillCooldownV2();
                            NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang3")));
                            killer.Notify(GetString("YinLangNotMax"));
                            return false;
                        }
                    }
                    return false;
                //==========船员职业==========//
                case CustomRoles.Sheriff:
                    if (!Sheriff.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.SwordsMan:
                    if (!SwordsMan.OnCheckMurder(killer))
                        return false;
                    break;
                case CustomRoles.Medicaler:
                    Medicaler.OnCheckMurderFormedicaler(killer, target);
                    return false;
                case CustomRoles.Captain:
                    Captain.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Counterfeiter:
                    if (Counterfeiter.CanBeClient(target) && Counterfeiter.CanSeel(killer.PlayerId))
                        Counterfeiter.SeelToClient(killer, target);
                    return false;
                
                case CustomRoles.Scout:
                    Scout.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Deputy:
                    Deputy.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Prophet:
                    Prophet.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.QSR:
                    QSR.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.ET:
                    ET.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.DemonHunterm:
                    DemonHunterm.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.ElectOfficials:
                    ElectOfficials.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.BSR:
                    BSR.OnCheckMurder(killer,target);
                    break;
                case CustomRoles.SpeedUp:
                    SpeedUp.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.ChiefOfPolice:
                    ChiefOfPolice.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Knight:
                    Knight.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Merchant:
                    Merchant.OnCheckMurder(killer, target);
                    return false;
            }
        }

            // 击杀前检查
            if (!killer.RpcCheckAndMurder(target, true))
            return false;

        // 清道夫清理尸体
        if (killer.Is(CustomRoles.Scavenger))
        {
            Utils.TP(killer.NetTransform, target.GetTruePosition());
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
            Utils.TP(target.NetTransform, Pelican.GetBlackRoomPS());
            target.SetRealKiller(killer);
            Main.PlayerStates[target.PlayerId].SetDead();
            target.RpcMurderPlayerV3(target);
            killer.SetKillCooldownV2();
            NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("KilledByScavenger")));
            return false;
        }

        // 肢解者肢解受害者
        if (killer.Is(CustomRoles.OverKiller) && killer.PlayerId != target.PlayerId)
        {
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Dismembered;
            new LateTask(() =>
            {
                if (!Main.OverDeadPlayerList.Contains(target.PlayerId)) Main.OverDeadPlayerList.Add(target.PlayerId);
                var ops = target.GetTruePosition();
                var rd = IRandom.Instance;
                for (int i = 0; i < 20; i++)
                {
                    Vector2 location = new(ops.x + ((float)(rd.Next(0, 201) - 100) / 100), ops.y + ((float)(rd.Next(0, 201) - 100) / 100));
                    location += new Vector2(0, 0.3636f);

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(target.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.None, -1);
                    NetHelpers.WriteVector2(location, writer);
                    writer.Write(target.NetTransform.lastSequenceId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);

                    target.NetTransform.SnapTo(location);
                    killer.MurderPlayer(target);

                    if (target.Is(CustomRoles.Avanger))
                    {
                        var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId).ToList();
                        var rp = pcList[IRandom.Instance.Next(0, pcList.Count)];
                        Main.PlayerStates[rp.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
                        rp.SetRealKiller(target);
                        rp.RpcMurderPlayerV3(rp);
                    }

                    MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.None, -1);
                    messageWriter.WriteNetObject(target);
                    AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
                }
                Utils.TP(killer.NetTransform, ops);
            }, 0.05f, "OverKiller Murder");
        }
        //抑郁者赌命
        if (killer.Is(CustomRoles.Depressed))
        {
            if (killer.Is(CustomRoles.OldThousand))
            {
                killer.SetKillCooldownV2();
            }
                var rd = IRandom.Instance;
            if (rd.Next(0, 100) < Options.DepressedIdioctoniaProbability.GetInt())
            {
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Depression;
                killer.RpcMurderPlayerV3(killer);
                return false;
            }
        }
        //毁尸者毁尸
        if (killer.Is(CustomRoles.Destroyers))
        {
            var Dy = IRandom.Instance;
            int rndNum = Dy.Next(0, 100);
            if (rndNum >= 10 && rndNum < 20)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
            }
            if (rndNum >= 20 && rndNum < 30)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Misfire;
            }
            if (rndNum >= 30 && rndNum < 40)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
            }
            if (rndNum >= 40 && rndNum < 50)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.FollowingSuicide;
            }
            if (rndNum >= 50 && rndNum < 60)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Torched;
            }
            if (rndNum >= 60 && rndNum < 70)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
            }
            if (rndNum >= 70 && rndNum < 80)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Quantization;
            }
            if (rndNum >= 80 && rndNum < 90)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
            }
            if (rndNum >= 90 && rndNum < 100)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Trialed;
            }
        }
        //倒霉蛋倒霉
        if (killer.Is(CustomRoles.UnluckyEggs))
        {
            var Ue = IRandom.Instance;
            if (Ue.Next(0, 100) < Options.UnluckyEggsKIllUnluckyEggs.GetInt())
            {
                killer.RpcMurderPlayerV3(killer);
                return false;
            }
        }
        //自爆兵自爆
        if (killer.Is(CustomRoles.Bomber))
        {
            Logger.Info("炸弹爆炸了", "Boom");
            CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
            foreach (var player in Main.AllPlayerControls)
            {
                
                if (!player.IsModClient()) player.KillFlash();
                if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                if (Vector2.Distance(killer.transform.position, player.transform.position) <= Options.BomberRadius.GetFloat())
                {
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    player.SetRealKiller(killer);
                    player.RpcMurderPlayerV3(player);
               }
            }
        }
        //修道徒升级
        if (killer.Is(CustomRoles.Cultivator))
        {
            if (Main.CultivatorKillMax[killer.PlayerId] <= Options.CultivatorMax.GetInt())
            {
                Main.CultivatorKillMax[killer.PlayerId]++;
            }
            else
            {
                killer.Notify(GetString("CultivatorNotMax"));
            }
            if (Main.CultivatorKillMax[killer.PlayerId] == 1 && Options.CultivatorOneCanKillCooldown.GetBool())
            {
                Main.AllPlayerKillCooldown[killer.PlayerId] = Options.CultivatorOneKillCooldown.GetFloat();
            }
            if (Main.CultivatorKillMax[killer.PlayerId] == 2 && Options.CultivatorTwoCanScavenger.GetBool())
            {
                Utils.TP(killer.NetTransform, target.GetTruePosition());
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                Utils.TP(target.NetTransform, Pelican.GetBlackRoomPS());
                target.SetRealKiller(killer);
                Main.PlayerStates[target.PlayerId].SetDead();
                target.RpcMurderPlayerV3(target);
                killer.SetKillCooldownV2();
                NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cultivator), GetString("KilledByCultivator")));
                return false;
            }
            if (Main.CultivatorKillMax[killer.PlayerId] == 3 && Options.CultivatorThreeCanBomber.GetBool())
            {
                Logger.Info("炸弹爆炸了", "Boom");
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (var player in Main.AllPlayerControls)
                {
                    if (!player.IsModClient()) player.KillFlash();
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                    if (player == killer) continue;
                    if (Vector2.Distance(killer.transform.position, player.transform.position) <= Options.BomberRadius.GetFloat())
                    {
                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        player.SetRealKiller(killer);
                        player.RpcMurderPlayerV3(player);
                    }
                }
            }
            if (Main.CultivatorKillMax[killer.PlayerId] == 4 && Options.CultivatorFourCanFlash.GetBool())
            {
                Main.AllPlayerSpeed[killer.PlayerId] = Options.CultivatorSpeed.GetFloat();
            }
        }
        //紊乱者击杀
        if (killer.Is(CustomRoles.Disorder))
        {
            var Dd = IRandom.Instance;
            if (Dd.Next(0, 100) < Options.Disorderility.GetInt())
            {
                var Ie = IRandom.Instance;
                int Kl = Ie.Next(0, 100);
                if (killer.Is(CustomRoles.OldThousand))
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.DisorderKillCooldown.GetFloat();
                }
                if (Kl >= 10 && Kl < 20)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.CultivatorKillCooldown.GetFloat();
                }
                if (Kl >= 20 && Kl < 30)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = EvilGambler.EvilGamblerKillCooldown.GetFloat();
                }
                if (Kl >= 30 && Kl < 40)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.DepressedKillCooldown.GetFloat();
                }
                if (Kl >= 40 && Kl < 50)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.CleanerKillCooldown.GetFloat();
                }
                if (Kl >= 50 && Kl < 60)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.BomberKillCooldown.GetFloat();
                }
                if (Kl >= 60 && Kl < 70)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.CapitalismSkillCooldown.GetFloat();
                }
                if (Kl >= 70 && Kl < 80)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.ScavengerKillCooldown.GetFloat();
                }
                if (Kl >= 80 && Kl < 90)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.MNKillCooldown.GetFloat();
                }
                if (Kl >= 90 && Kl < 100)
                {
                    Main.AllPlayerKillCooldown[killer.PlayerId] = Options.RevolutionistCooldown.GetFloat();
                }
            }
        }
        //行刑师击杀
        if (killer.Is(CustomRoles.OldImpostor))
        {
            target.RpcMurderPlayerV3(target);
            target.SetRealKiller(killer);
                killer.SetKillCooldownV2();
                return false;
        }
        //执行者召开会议
        if (killer.Is(CustomRoles.Executor))
        {
            target?.ReportDeadBody(null);
        }
        //俄罗斯大转盘
        if (killer.Is(CustomRoles.Followers))
        {
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
            var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != killer.PlayerId).ToList();
            var Fr = pcList[IRandom.Instance.Next(0, pcList.Count)];
            Main.PlayerStates[Fr.PlayerId].deathReason = PlayerState.DeathReason.Execution;
            Fr.SetRealKiller(killer);
            Fr.RpcMurderPlayerV3(Fr);
            if (Fr.GetCustomRole().IsImpostor())
            {
                killer.RpcMurderPlayerV3(killer);
                return false;
            }
        }
        //爆破狂技能
        if (killer.Is(CustomRoles.DemolitionManiac))
        {
            if (Options.DemolitionManiacKillPlayerr.GetInt() == 0)
            {
                Main.DemolitionManiacKill.Add(target.PlayerId);
                killer.SetKillCooldownV2(target: target, forceAnime: true);
                return false;
            }
            if (Options.DemolitionManiacKillPlayerr.GetInt() == 1)
            {
                killer.SetKillCooldownV2(target: target, forceAnime: true);
                Main.InBoom.Remove(killer.PlayerId);
                Main.InBoom.Add(killer.PlayerId, Utils.GetTimeStamp());
                Main.ForDemolition.Add(target.PlayerId);
                new LateTask(() =>
                {
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);

                    CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                    foreach (var player in Main.AllPlayerControls)
                    {
                        
                        
                    }
                }, Options.DemolitionManiacWait.GetFloat(), "DemolitionManiacBoom!!!");
                return false;
            }
        }
        if (target.Is(CustomRoles.NiceMini) && NiceMini.Age != 18)
        {
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceMini), GetString("Cantkillkid")));
            return false;
        }
        if (target.Is(CustomRoles.EvilMini) && NiceMini.Age != 18)
        {
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceMini), GetString("Cantkillkid")));
            return false;
        }
        //猎人
        if (killer.Is(CustomRoles.Hunter))
        {
            Main.HunterMax[killer.PlayerId]++;
            if ((Main.HunterMax[killer.PlayerId] <= Options.HunterCanTargetMax.GetInt() || Main.HunterMax[killer.PlayerId] <= Options.HunterCanTargetMaxEveryMeeting.GetInt()) && !Main.HunterTarget.Contains(target))
            {
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                Main.HunterTarget.Add(target);
                NameColorManager.Add(killer.PlayerId, target.PlayerId, "#f7eea9");
                return false;
            }
            else if (Main.HunterTarget.Contains(target))
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Hunter), GetString("InList")));
                return false;
            }
            else if (Main.HunterMax[killer.PlayerId] > Options.HunterCanTargetMax.GetInt() || Main.HunterMax[killer.PlayerId] > Options.HunterCanTargetMaxEveryMeeting.GetInt())
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Hunter), GetString("HunterTargetMax")));
                return false;
            }
        }
        //暗恋者暗恋

        if (killer.Is(CustomRoles.Crush))
        {
            Main.CrushMax[killer.PlayerId]++;
            if (Main.CrushMax[killer.PlayerId] == 1 && !target.Is(CustomRoles.Captain) && !target.Is(CustomRoles.Believer) && !target.Is(CustomRoles.Lovers) && !target.Is(CustomRoles.CupidLovers) && !target.Is(CustomRoles.Cupid) && !target.Is(CustomRoles.Ntr) && !target.Is(CustomRoles.God))
            {
                Main.CrushLoversPlayers.Add(killer);
                Main.PlayerStates[killer.PlayerId].SetSubRole(CustomRoles.CrushLovers);
                Main.CrushLoversPlayers.Add(target);
                Main.PlayerStates[target.PlayerId].SetSubRole(CustomRoles.CrushLovers);
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                RPC.SyncCrushLoversPlayers();
                return false;
            }
            else if (target.Is(CustomRoles.Captain) && target.Is(CustomRoles.Believer) && target.Is(CustomRoles.Lovers) && target.Is(CustomRoles.CupidLovers) && target.Is(CustomRoles.Cupid) && target.Is(CustomRoles.Ntr) && target.Is(CustomRoles.God))
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Crush), GetString("CrushInvalidTarget")));
                return false;
            }
            else
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Crush), GetString("CrushInvalidTarget")));
                return false;
            }
        }
        //丘比特之箭！
        if (killer.Is(CustomRoles.Cupid))
        {
            Main.CupidMax[killer.PlayerId]++;
            if (Main.CupidMax[killer.PlayerId] < 2 && !target.Is(CustomRoles.Captain) && !target.Is(CustomRoles.Believer) && !target.Is(CustomRoles.Lovers) && !target.Is(CustomRoles.Crush) && !target.Is(CustomRoles.CrushLovers) && !target.Is(CustomRoles.Ntr) && !target.Is(CustomRoles.God))
            {
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                Main.CupidLoveList.Add(target);
                Main.CrushLoversPlayers.Add(target);
                NameColorManager.Add(killer.PlayerId, target.PlayerId, "#ff80c0");
                return false;
            }
            else if (Main.CupidMax[killer.PlayerId] == 2 && !target.Is(CustomRoles.Captain) && !target.Is(CustomRoles.Believer) && !target.Is(CustomRoles.Lovers) && !target.Is(CustomRoles.CrushLovers) && !target.Is(CustomRoles.Crush) && !target.Is(CustomRoles.Ntr) && !target.Is(CustomRoles.God))
            {
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                Main.CupidLoveList.Add(target);
                Main.CrushLoversPlayers.Add(target);
                NameColorManager.Add(killer.PlayerId, target.PlayerId, "#ff80c0");
                foreach (var cupidplayer in Main.CupidLoveList)
                {
                    Main.PlayerStates[cupidplayer.PlayerId].SetSubRole(CustomRoles.CupidLovers);
                }
                RPC.SyncCupidLoversPlayers();
                return false;
            }
            else if (target.Is(CustomRoles.Captain) && target.Is(CustomRoles.Believer) && target.Is(CustomRoles.Lovers) && target.Is(CustomRoles.CrushLovers) && target.Is(CustomRoles.Crush) && target.Is(CustomRoles.Ntr) && target.Is(CustomRoles.God))
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cupid), GetString("CupidInvalidTarget")));
                return false;
            }
            else
            {
                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cupid), GetString("CupidInvalidTarget")));
                return false;
            }
        }
        //奴隶主奴隶
        if (killer.Is(CustomRoles.Slaveowner))
        {
            if (target.GetCustomRole().IsCrewmate())
            {
                if (Main.SlaveownerMax[killer.PlayerId] >= Options.ForSlaveownerSlav.GetInt())
                {
                    return false;
                }
                if (Options.TargetcanSeeSlaveowner.GetBool())
                {
                    NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Slaveowner), GetString("SlavBySlaveowner")));
                    Main.ForSlaveowner.Add(target.PlayerId);
                    Main.SlaveownerMax[killer.PlayerId]++;
                    killer.ResetKillCooldown();
                    killer.SetKillCooldown();
                    killer.RpcGuardAndKill(target);
                    CustomWinnerHolder.WinnerIds.Remove(target.PlayerId);
                    return false;
                }
                else
                {
                    Main.ForSlaveowner.Add(target.PlayerId);
                    Main.SlaveownerMax[killer.PlayerId]++;
                    killer.ResetKillCooldown();
                    killer.SetKillCooldown();
                    killer.RpcGuardAndKill(target);
                    CustomWinnerHolder.WinnerIds.Remove(target.PlayerId);
                    return false;
                }
                
            }
            else
            {
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
            }
        }
        //咒术师选择替罪羊
        if (killer.Is(CustomRoles.Spellmaster))
        {
            if (Main.SpellmasterMax[killer.PlayerId] >= Options.SpellmasterKillMax.GetInt())
            {
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
            }
            else
            {
                Main.ForSpellmaster.Add(target.PlayerId);
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                Main.SpellmasterMax[killer.PlayerId]++;
                return false;
            }
        }
        //三角恋卷入
        int Lvt = 0;
        if (killer.Is(CustomRoles.Lovers))
        {
            Lvt++;
            if (Options.LoverThree.GetBool() && Lvt == 1)
            {
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                target.RpcSetCustomRole(CustomRoles.Lovers);
                return false;
            }
            else
            {
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
            }
        }
        //嫉妒狂击杀
        if (killer.Is(CustomRoles.Jealousy))
        {
            if (!Main.ForJealousy.Contains(target.PlayerId))
            {
                killer.RpcMurderPlayerV3(killer);
                return false;
            }
            else
            {
                Main.JealousyMax[killer.PlayerId]++;
            }
            if (Main.JealousyMax[killer.PlayerId] >= Options.JealousyKillMax.GetInt())
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jealousy);
                CustomWinnerHolder.WinnerIds.Add(killer.PlayerId);
            }
        }
        //瘟疫之源感染
        if (killer.Is(CustomRoles.SourcePlague))
        {
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            NameColorManager.Add(killer.PlayerId, target.PlayerId, "#999999");
            //是否目标已经被感染了
            if (!Main.ForSourcePlague.Contains(target.PlayerId))
            {
                Main.ForSourcePlague.Add(target.PlayerId);
                bool AllPlayerForWY = true;
                //看看所有玩家有没有有人没被感染
                foreach (var player in Main.AllPlayerControls)
                {
                    if (player == killer) continue;
                    if (Main.ForSourcePlague.Contains(player.PlayerId))
                    {
                        NameColorManager.Add(killer.PlayerId, player.PlayerId, "#999999");
                    }
                    if (!Main.ForSourcePlague.Contains(player.PlayerId))
                    {
                        AllPlayerForWY = false;
                        continue;
                    }
                }
                if (AllPlayerForWY)
                {
                    killer.RpcSetCustomRole(CustomRoles.PlaguesGod);
                }
            }
            else
            {
                return false;
            }
            return false;
        }
        if (killer.Is(CustomRoles.King))
        {
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            NameColorManager.Add(killer.PlayerId, target.PlayerId, "#FFCC00");
            if (Main.KingCanKill.Contains(killer.PlayerId))
            {
                foreach (var player in Main.AllPlayerControls)
                {
                    if (Main.ForKing.Contains(player.PlayerId))
                    {
                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Execution;
                        player.SetRealKiller(killer);
                        player.RpcMurderPlayerV3(player);
                    }
                }
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.King);
                CustomWinnerHolder.WinnerIds.Add(killer.PlayerId);
            }
            if (!Main.ForKing.Contains(target.PlayerId))
            {
                Main.ForKing.Add(target.PlayerId);
                bool AllPlayerForKing = true;
                foreach (var player in Main.AllPlayerControls)
                {
                    if (player == killer) continue;
                    if (Main.ForKing.Contains(player.PlayerId))
                    {
                        NameColorManager.Add(killer.PlayerId, player.PlayerId, "#FFCC00");
                    }
                    if (!Main.ForKing.Contains(player.PlayerId))
                    {
                        AllPlayerForKing = false;
                        continue;
                    }
                }
                if (AllPlayerForKing)
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.King), GetString("EnterVentToWinandKillPlayerToWin")));
                    Main.KingCanpc.Add(killer.PlayerId);
                    Main.KingCanKill.Add(killer.PlayerId);
                }
            }
            else
            {
                return false;
            }
            return false;
        }
        //选择你的命运
        if (killer.Is(CustomRoles.DestinyChooser))
        {
            killer.SetKillCooldownV2(target: target, forceAnime: true);
            var DC = IRandom.Instance;
                int Kl = DC.Next(0, 100);
                if (Kl >= 10 && Kl < 40)
                {
                NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("KilledByIll")));
                Main.ForDestinyChooser.Add(target.PlayerId);
                return false;
                 }
                if (Kl >= 40 && Kl < 60)
                {
                NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("KilledByForTask")));
                Main.ForTasksDestinyChooser.Add(target.PlayerId);
                return false;
                 }
                if (Kl >= 60 && Kl < 101)
                {
                NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("KilledTarget")));
                Main.ForLostDeadDestinyChooser.Add(target.PlayerId);
                return false;
                 }
                if (Main.ForDestinyChooser.Contains(target.PlayerId) || Main.ForTasksDestinyChooser.Contains(target.PlayerId) || Main.ForLostDeadDestinyChooser.Contains(target.PlayerId))
            {
                new LateTask(() =>
                {
                    target.RpcMurderPlayerV3(target);
                    Utils.NotifyRoles();
                }, Options.DestinyChooserSeconds.GetInt(), ("Killer"));
            }
            return false;
        }
        //压榨人工
        if (killer.Is(CustomRoles.Squeezers))
        {
            killer.SetKillCooldownV2(target: target, forceAnime: true);
            Main.ForSqueezers.Add(target.PlayerId);
            new LateTask(() =>
            {
                if (!Main.TasksSqueezers.Contains(target.PlayerId))
                {
                    NameNotifyManager.Notify(__instance, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("NotAssassin")));
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                }
                else
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Squeezers), GetString("NotSqueezers")));
                    Main.TasksSqueezers.Remove(target.PlayerId);
                }
                Utils.NotifyRoles();
            }, Options.SqueezersMaxSecond.GetInt(), ("Killer"));
            return false;
        }
        //被保护了
        if (Main.MerchantProject.Contains(target.PlayerId))
        {
            killer.SetKillCooldownV2(target: target, forceAnime: true);
            Main.MerchantProject.Remove(target.PlayerId);
            return false;
        }

        //==キル処理==
        __instance.RpcMurderPlayerV3(target);
            //============

            return false;
    }


        public static bool RpcCheckAndMurder(PlayerControl killer, PlayerControl target, bool check = false)
        {
        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return false;
            if (target == null) target = killer;

            //禁止内鬼刀叛徒
            if (killer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && !Options.ImpCanKillMadmate.GetBool())
                return false;

            //禁止叛徒刀内鬼
            if (killer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && !Options.MadmateCanKillImp.GetBool())
                return false;

            //医生护盾检查
            if (Medicaler.OnCheckMurder(killer, target))
                return false;

        //被捕快带上手铐
        if (Main.DeputyInProtect.Contains(killer.PlayerId))
            return false;

        //被起诉人上空包弹
        if (Main.QSRInProtect.Contains(killer.PlayerId))
            return false;

        //凶手被传染
        if (Main.ForSourcePlague.Contains(target.PlayerId))
        {
            Main.ForSourcePlague.Add(killer.PlayerId);
            foreach (var player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.SourcePlague))
                {
                    NameColorManager.Add(player.PlayerId, killer.PlayerId, "#999999");
                }
            }
        }
        //QX Skill
        Main.StrikersShields = Options.StrikersShields.GetInt();
        if (killer.Is(CustomRoles.Strikers) && Main.StrikersShields > 0)
            {
                Main.StrikersShields--;
            }
        if (target.Is(CustomRoles.Strikers) && Main.StrikersShields > 0)
            {
                killer.RpcGuardAndKill(killer);
                Main.StrikersShields--;
                return false;
            }
            if (killer.Is(CustomRoles.Strikers) && Main.StrikersShields == 0)
            {
                killer.RpcMurderPlayerV3(killer);
            }
            //被外星人干扰
            if (Main.ForET.Contains(killer.PlayerId))
        {
            killer.SetKillCooldownV2(target: target, forceAnime: true);
            return false;
        }
        //击杀濒死
        if (Main.ForLostDeadDestinyChooser.Contains(target.PlayerId))
        {
            target.RpcMurderPlayerV3(target);
            target?.ReportDeadBody(null);
        }
        //被守护者杀死
        if (Main.ForKnight.Contains(target.PlayerId))
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.Knight) && player.IsAlive())
                {
                    player.RpcMurderPlayerV3(killer);
                    Main.ForKnight.Remove(target.PlayerId);
                }
            }
            return false;
        }  
        //内鬼没啦
        if (target.Is(CustomRoleTypes.Impostor))
        {
            int DefectorInt = 0;
            int optImpNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors);
            int ImIntDead = 0;
            ImIntDead++;
            foreach (var player in Main.AllPlayerControls)
            {
                if (!player.IsAlive() && player.GetCustomRole().IsImpostor() && !Main.KillImpostor.Contains(player.PlayerId) && !player.Is(CustomRoles.Defector) && player.PlayerId != target.PlayerId)
                {
                    Main.KillImpostor.Add(player.PlayerId);
                    ImIntDead++;

                    foreach (var partnerPlayer in Main.AllPlayerControls)
                    {
                        if (ImIntDead != optImpNum) continue;
                        if (partnerPlayer.GetCustomRole().IsCrewmate() && partnerPlayer.CanUseKillButton() && DefectorInt == 0)
                        {
                            Logger.Info($"qwqwqwq", "Jackal");
                            DefectorInt++;
                            partnerPlayer.RpcSetCustomRole(CustomRoles.Defector);
                            partnerPlayer.ResetKillCooldown();
                            partnerPlayer.SetKillCooldown();
                            partnerPlayer.RpcGuardAndKill(partnerPlayer);
                        }
                    }
                }
            }
        }
        //击杀VIP
        if (target.Is(CustomRoles.VIP) || target.Is(CustomRoles.VIP) && killer.PlayerId == target.PlayerId)
        {
            foreach (var player in Main.AllPlayerControls)
            {
                player.KillFlash();
                if (target.Is(CustomRoleTypes.Impostor))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("ImpostorDaed")));
                }
                else if (target.Is(CustomRoleTypes.Crewmate))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Crewmate), GetString("CrewmateDead")));
                }
                else if (target.Is(CountTypes.Jackal))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("JackalDaed")));
                }
                else if (target.Is(CountTypes.Pelican))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pelican), GetString("PelicanDaed")));
                }
                else if (target.Is(CountTypes.Gamer))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gamer), GetString("GamerDaed")));
                }
                else if (target.Is(CountTypes.BloodKnight))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.BloodKnight), GetString("BloodKnightDaed")));
                }
                else if (target.Is(CountTypes.Succubus))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), GetString("SuccubusDaed")));
                }
                else if (target.Is(CountTypes.YinLang))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLangDaed")));
                }
                else if (target.Is(CountTypes.PlaguesGod))
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.PlaguesGod), GetString("PlaguesGodDaed")));
                }
                else
                {
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Crewmate), GetString("NotRoles")));
                }
            }
        }
            switch (target.GetCustomRole())
        {
            //击杀幸运儿
            case CustomRoles.Luckey:
                if (target.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    return false;
                }
                var rd = IRandom.Instance;
                if (rd.Next(0, 100) < Options.LuckeyProbability.GetInt())
                {
                    var Lc = IRandom.Instance;
                    if (Lc.Next(0, 100) < Options.LuckeyCanSeeKillility.GetInt())
                    {
                        Utils.TP(killer.NetTransform, target.GetTruePosition());
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                        target.RpcGuardAndKill(killer);
                        return false;
                    }
                    else
                    {
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        return false;
                    }
                }
                break;
            //mascot be killed
            case CustomRoles.Mascot:
                if (target.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    killer.RpcMurderPlayerV3(killer);
                    return false;
                }
                var rdmas = IRandom.Instance;
                if (rdmas.Next(0, 100) < Options.MascotPro.GetInt())
                {
                    var pcList1 = Main.AllAlivePlayerControls.Where(x => x.PlayerId != killer.PlayerId).ToList();
                    var Fr1 = pcList1[IRandom.Instance.Next(0, pcList1.Count)];

                    if (Options.MascotKiller.GetBool() == true)
                    {
                        Main.PlayerStates[Fr1.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                        killer.SetRealKiller(killer);
                        killer.RpcMurderPlayerV3(killer);
                        return false;
                    }
                    else if (Fr1.GetCustomRole().IsMascot())
                    {
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        Main.PlayerStates[Fr1.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                        killer.SetRealKiller(killer);
                        killer.RpcMurderPlayerV3(killer);
                        return false;
                    }
                    else
                    {
                        Main.PlayerStates[Fr1.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                        Fr1.SetRealKiller(Fr1);
                        Fr1.RpcMurderPlayerV3(Fr1);
                    }
                }
                break;

            //击杀呪狼
            case CustomRoles.CursedWolf:
                    if (Main.CursedWolfSpellCount[target.PlayerId] <= 0) break;
                    killer.RpcGuardAndKill(target);
                    target.RpcGuardAndKill(target);
                    Main.CursedWolfSpellCount[target.PlayerId] -= 1;
                    RPC.SendRPCCursedWolfSpellCount(target.PlayerId);
                    Logger.Info($"{target.GetNameWithRole()} : {Main.CursedWolfSpellCount[target.PlayerId]}回目", "CursedWolf");
                    Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Spell;
                    killer.RpcMurderPlayerV3(killer);
                    return false;
                //击杀老兵
                case CustomRoles.Veteran:
                    if (Main.VeteranInProtect.ContainsKey(target.PlayerId) && killer.PlayerId != target.PlayerId)
                        if (Main.VeteranInProtect[target.PlayerId] + Options.VeteranSkillDuration.GetInt() >= Utils.GetTimeStamp())
                        {
                            killer.SetRealKiller(target);
                            target.RpcMurderPlayerV3(killer);
                            Logger.Info($"{target.GetRealName()} 老兵反弹击杀：{killer.GetRealName()}", "Veteran Kill");
                            return false;
                        }
                    break;
                //检查明星附近是否有人
                case CustomRoles.SuperStar:
                    if (Main.AllAlivePlayerControls.Where(x =>
                        x.PlayerId != killer.PlayerId &&
                        x.PlayerId != target.PlayerId &&
                        Vector2.Distance(x.GetTruePosition(), target.GetTruePosition()) < 2f
                        ).ToList().Count >= 1) return false;
                    break;
                //玩家被击杀事件
                case CustomRoles.Gamer:
                    if (!Gamer.CheckMurder(killer, target))
                        return false;
                    break;
                //嗜血骑士技能生效中
                case CustomRoles.BloodKnight:
                    if (BloodKnight.InProtect(target.PlayerId))
                    {
                        killer.RpcGuardAndKill(target);
                        target.RpcGuardAndKill();
                        target.Notify(GetString("BKOffsetKill"));
                        return false;
                    }
                    break;
                //击杀挑衅者
                case CustomRoles.Rudepeople:
                    if (Main.RudepeopleInProtect.ContainsKey(target.PlayerId) && killer.PlayerId != target.PlayerId)
                        if (Main.RudepeopleInProtect[target.PlayerId] + Options.RudepeopleSkillDuration.GetInt() >= Utils.GetTimeStamp(DateTime.Now))
                        {
                            Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                            killer.RpcMurderPlayerV3(target);
                            killer.RpcMurderPlayerV3(killer);
                            killer.SetRealKiller(target);
                        CustomWinnerHolder.WinnerIds.Remove(killer.PlayerId);
                        return false;
                        }
                    break;
                //击杀银狼
                case CustomRoles.YinLang:
                    killer.RPCPlayCustomSound("ylkq");
                    Logger.Info("银狼被杀，开始执行114514", "YinLang");
                    Main.AllPlayerKillCooldown[killer.PlayerId] = 114514f;
                    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang4")));
                foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                {
                    var pos = target.transform.position;
                    var dis = Vector2.Distance(pos, pc.transform.position);
                    Logger.Info("执行减速", "YL");
                    //NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang2")));
                    Logger.Info($"{killer.GetNameWithRole()} |是否允许更改| => {killer.GetNameWithRole()}", "YinLang");
                    Main.AllPlayerSpeed[killer.PlayerId] = 0.01f;
                    var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
                    Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
                    ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
                    killer.MarkDirtySettings();
                    //    NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.YinLang), GetString("YinLang1")));
                    Main.AllPlayerSpeed[killer.PlayerId] = Main.AllPlayerSpeed[killer.PlayerId] - Main.MinSpeed + tmpSpeed - 0.01f;
                    ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
                    killer.MarkDirtySettings();
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                    Main.AllPlayerSpeed[killer.PlayerId] = 0.01f;
                    break;
                }
                break;
            //击杀失落的船员
            case CustomRoles.LostCrew:
                    new LateTask(() =>
                    {
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.RPCPlayCustomSound("SUS");
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.LostCrew), GetString("IAMSUS!!!")));
                        Utils.NotifyRoles();
                    }, 5f, ("LOST!!!!"));
                    new LateTask(() =>
                    {
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.RPCPlayCustomSound("LOST");
                        Utils.NotifyRoles();
                    }, 8f, ("SUS!!!!"));
                    new LateTask(() =>
                    {
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.RPCPlayCustomSound("LOST");
                        Utils.NotifyRoles();
                    }, 12f, ("SUS!!!!"));
                    new LateTask(() =>
                    {
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.RPCPlayCustomSound("LOST");
                        Utils.NotifyRoles();
                    }, 14f, ("SUS!!!!"));
                    new LateTask(() =>
                    {
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.RPCPlayCustomSound("LOST");
                        Utils.NotifyRoles();
                    }, 15f, ("SUS!!!!"));
                    new LateTask(() =>
                    {
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.KillFlash();
                        killer.RPCPlayCustomSound("LOST");
                        target.RpcMurderPlayerV3(killer);
                        Utils.NotifyRoles();
                    }, 17f, ("KILLER!!!!!!!!"));
                    break;
                //击杀特务
                case CustomRoles.SpecialAgent:
                if (killer.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    target.SetRealKiller(killer);
                    Main.PlayerStates[target.PlayerId].SetDead();
                    killer.SetKillCooldownV2();
                    target.RpcMurderPlayerV3(target);
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                    return true;
                }
                if (target.Is(CustomRoles.OldThousand))
                {
                    killer.SetRealKiller(target);
                    target.RpcMurderPlayerV3(killer);
                    Logger.Info($"{target.GetRealName()} 特务反杀：{killer.GetRealName()}", "SpecialAgent Kill");
                    return false;
                }
                var pg = IRandom.Instance;
                if (pg.Next(0, 100) < Options.SpecialAgentrobability.GetInt())
                    {
                        killer.SetRealKiller(target);
                        target.RpcMurderPlayerV3(killer);
                        Logger.Info($"{target.GetRealName()} 特务反杀：{killer.GetRealName()}", "SpecialAgent Kill");
                        return false;
                    }
                    break;
                //击杀任务工
                case CustomRoles.HatarakiMan:
                if (killer.Is(CustomRoles.OldThousand))
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    target.SetRealKiller(killer);
                    Main.PlayerStates[target.PlayerId].SetDead();
                    killer.SetKillCooldownV2();
                    target.RpcMurderPlayerV3(target);
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
                    return true;
                }
                if (target.Is(CustomRoles.OldThousand))
                {
                    killer.SetRealKiller(target);
                    target.RpcMurderPlayerV3(killer);
                    return false;
                }
                var pg1 = IRandom.Instance;
                    if (pg1.Next(0, 100) < Options.SpecialAgentrobability.GetInt())
                    {
                        killer.SetRealKiller(target);
                        target.RpcMurderPlayerV3(killer);
                        Logger.Info($"{target.GetRealName()} 任务工反杀：{killer.GetRealName()}", "HatarakiMan Kill");
                        return false;
                    }
                    break;
                //击杀萧暮
                case CustomRoles.XiaoMu:
                if (target.Is(CustomRoles.OldThousand))
                {
                    killer.SetRealKiller(target);
                    target.RpcMurderPlayerV3(killer);
                    return false;
                }
                var Fg = IRandom.Instance;
                int xiaomu = Fg.Next(1, 3);
                    if (xiaomu == 1)
                    {
                        if (killer.PlayerId != target.PlayerId || target.GetRealKiller()?.GetCustomRole() is CustomRoles.Swooper)
                        {
                            NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu1")));
                            killer.RPCPlayCustomSound("Congrats");
                            target.RPCPlayCustomSound("Congrats");
                            float delay;
                            if (Options.BaitDelayMax.GetFloat() < Options.BaitDelayMin.GetFloat()) delay = 0f;
                            else delay = IRandom.Instance.Next((int)Options.BaitDelayMin.GetFloat(), (int)Options.BaitDelayMax.GetFloat() + 1);
                            delay = Math.Max(delay, 0.15f);
                            if (delay > 0.15f && Options.BaitDelayNotify.GetBool()) killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), string.Format(GetString("KillBaitNotify"), (int)delay)), delay);
                            Logger.Info($"{killer.GetNameWithRole()} 击杀萧暮自动报告 => {target.GetNameWithRole()}", "XiaoMu");
                            new LateTask(() => { if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data); }, delay, "Bait Self Report");
                        }
                    } else if (xiaomu == 2)
                    {
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu2")));
                        Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发原地不能动 => {target.GetNameWithRole()}", "XiaoMu");
                        var tmpSpeed1 = Main.AllPlayerSpeed[killer.PlayerId];
                        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
                        ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
                        killer.MarkDirtySettings();
                        new LateTask(() =>
                        {
                            Main.AllPlayerSpeed[killer.PlayerId] = Main.AllPlayerSpeed[killer.PlayerId] - Main.MinSpeed + tmpSpeed1;
                            ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
                            killer.MarkDirtySettings();
                            RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                        }, Options.TrapperBlockMoveTime.GetFloat(), "Trapper BlockMove");
                    } else if (xiaomu == 3)
                    {
                        Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发cd114514 => {target.GetNameWithRole()}", "XiaoMu");
                        Main.AllPlayerKillCooldown[killer.PlayerId] = 114514f;
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu3")));
                    } else
                    {
                        Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发若报告尸体则报告人知道凶手是谁 => {target.GetNameWithRole()}", "XiaoMu");
                        NameNotifyManager.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.XiaoMu), GetString("YouKillXiaoMu4")));
                    }
                    break;
                
                //击杀孟姜女
                case CustomRoles.MengJiangGirl:
                    var Mg = IRandom.Instance;
                    int mengjiang = Mg.Next(0, 15);
                    PlayerControl mengjiangp = Utils.GetPlayerById(mengjiang);
                    if (Options.MengJiangGirlWinnerPlayerer.GetInt() == 0)
                    {
                        if (mengjiangp.GetCustomRole().IsCrewmate())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.MengJiangGirl);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                        Logger.Info($"孟姜女被击杀，抽取到船员，设置为船员", "MengJiang");
                            break;
                        }
                    }
                    else if (Options.MengJiangGirlWinnerPlayerer.GetInt() == 1)
                    {
                        if (mengjiangp.GetCustomRole().IsImpostor())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.MengJiangGirl);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                        Logger.Info($"孟姜女被击杀，抽取到内鬼，设置为内鬼", "MengJiang");
                            break;
                        }
                    }
                    else if (Options.MengJiangGirlWinnerPlayerer.GetInt() == 2)
                    {
                        if (mengjiangp.GetCustomRole().IsNeutral())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.MengJiangGirl);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                        Logger.Info($"孟姜女被击杀，抽取到中立，设置为中立", "MengJiang");
                            break;
                        }
                    }
                    else
                    {
                        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.cry;
                    }
                    break;
                //击杀不屈
                case CustomRoles.Indomitable:
                    Main.ShieldPlayer = byte.MaxValue;
                Utils.TP(killer.NetTransform, target.GetTruePosition());
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                killer.SetKillCooldownV2(target: target, forceAnime: true);
                    target.RpcGuardAndKill();
                        new LateTask(() =>
                        {
                            target?.NoCheckStartMeeting(target?.Data);
                        }, 10.0f, "Skill Remain Message");
                new LateTask(() =>
                {
                    target.RpcMurderPlayerV3(target);
                }, 23.0f, "Skill Remain Message");
                return false;
           

                //击杀公牛
                case CustomRoles.Bull:
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    foreach (var player in Main.AllPlayerControls)
                    {
                        if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                        if (player == target) continue;
                        if (Vector2.Distance(target.transform.position, player.transform.position) <= Options.BullRadius.GetFloat())
                        {
                            player.SetRealKiller(target);
                            player.RpcMurderPlayerV3(player);
                            Main.BullKillMax[target.PlayerId]++;
                        }
                    }
                    if (Main.BullKillMax[target.PlayerId] >= Options.BullKill.GetInt())
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Bull);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);

                }
                return false;
        
                //击杀受虐狂
                case CustomRoles.Masochism:
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    Main.MasochismKillMax[target.PlayerId]++;
                killer.RPCPlayCustomSound("DM");
                target.Notify(string.Format(GetString("MasochismKill"), Main.MasochismKillMax[target.PlayerId]));
                    if (Main.MasochismKillMax[target.PlayerId] >= Options.KillMasochismMax.GetInt())
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Masochism);
                        CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                    }
                return false;
      
            //击杀修道徒
            case CustomRoles.Cultivator:
                if (Main.CultivatorKillMax[killer.PlayerId] == 5 && Options.CultivatorFiveCanNotKill.GetBool())
                {
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    return false;
                }
                break;

            //击杀厄运儿
            case CustomRoles.BadLuck:
                var BL = IRandom.Instance;
                if (BL.Next(0, 100) < 10)
                {
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    return false;
                }
                else
                {
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    target.RpcMurderPlayerV3(target);
                    target.SetRealKiller(killer);
                    killer.RpcSetCustomRole(CustomRoles.UnluckyEggs);
                    return false;
                }
           
            //击杀豺狼
            case CustomRoles.Jackal:
                foreach (var player in Main.AllPlayerControls)
                {
                    Main.isjackalDead = true;
                    if (player.Is(CustomRoles.Sidekick))
                    {
                        player.RpcSetCustomRole(CustomRoles.Jackal);
                        Jackal.Add(player.PlayerId);
                        Jackal.Add(player.PlayerId);
                        player.ResetKillCooldown();
                        player.SetKillCooldown();
                       player.RpcGuardAndKill(player);
                    }
                }
                break;
            //击杀警长
            case CustomRoles.Sheriff:
                
                foreach (var player in Main.AllPlayerControls)
                {
                        Main.isSheriffDead = true;
                    if (Deputy.DeputyCanBeSheriff.GetBool())
                    {
                        if (player.Is(CustomRoles.Deputy))
                        {
                            player.RpcSetCustomRole(CustomRoles.Sheriff);
              
                                    Sheriff.Add(player.PlayerId);
                                    Sheriff.Add(player.PlayerId);
               
                            player.ResetKillCooldown();
                            player.SetKillCooldown();
                            player.RpcGuardAndKill(player);
                        }
                    }
                }
                break; 
            //击杀时间之主
            case CustomRoles.TimeMaster:
                if (Main.TimeMasterInProtect.ContainsKey(target.PlayerId) && killer.PlayerId != target.PlayerId)
                    if (Main.TimeMasterInProtect[target.PlayerId] + Options.TimeMasterSkillDuration.GetInt() >= Utils.GetTimeStamp(DateTime.Now))
                    {
                        foreach (var player in Main.AllPlayerControls)
                        {
                            if (Main.TimeMasterbacktrack.ContainsKey(player.PlayerId))
                            {
                                var position = Main.TimeMasterbacktrack[player.PlayerId];
                                Utils.TP(player.NetTransform, position);
                            }
                        }
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        return false;
                    }
                break;
            //击杀薛定谔的猫
            case CustomRoles.SchrodingerCat:
               if (killer.GetCustomRole().IsCrewmate())
                {
                    target.RpcSetCustomRole(CustomRoles.CrewSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    return false;
                 }
                if (killer.GetCustomRole().IsImpostor())
                {
                    target.RpcSetCustomRole(CustomRoles.ImpostorSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound); 
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#FF0000");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#FF0000");
                    foreach (var player in Main.AllPlayerControls)
                    {
                        if (player.GetCustomRole().IsImpostor())
                        {
                            NameColorManager.Add(player.PlayerId, target.PlayerId, "#FF0000");
                            NameColorManager.Add(target.PlayerId, player.PlayerId, "#FF0000");
                        }
                    }
                    
                    return false;
                }
                if (killer.Is(CustomRoles.BloodKnight))
                {
                    target.RpcSetCustomRole(CustomRoles.BloodSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#630000");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#630000");
                    return false;
                }
                if (killer.Is(CustomRoles.Gamer))
                {
                    target.RpcSetCustomRole(CustomRoles.GamerSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#68bc71");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#68bc71");
                    return false;
                }
                if (killer.Is(CustomRoles.Jackal))
                {
                    target.RpcSetCustomRole(CustomRoles.JSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#00b4eb");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#00b4eb");
                    return false;
                }
                if (killer.Is(CustomRoles.YinLang))
                {
                    target.RpcSetCustomRole(CustomRoles.YLSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#6A5ACD");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#6A5ACD");
                    return false;
                }
                if (killer.Is(CustomRoles.PlaguesGod))
                {
                    target.RpcSetCustomRole(CustomRoles.PGSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#101010");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#101010");
                    return false;
                }
                if (killer.Is(CustomRoles.DarkHide))
                {
                    target.RpcSetCustomRole(CustomRoles.DHSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#483d8b");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#483d8b");
                    return false;
                }
                if (killer.Is(CustomRoles.OpportunistKiller))
                {
                    target.RpcSetCustomRole(CustomRoles.OKSchrodingerCat);
                    Utils.TP(killer.NetTransform, target.GetTruePosition());
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
                    target.RpcGuardAndKill(killer);
                    NameColorManager.Add(killer.PlayerId, target.PlayerId, "#CC6600");
                    NameColorManager.Add(target.PlayerId, killer.PlayerId, "#CC6600");
                    return false;
                }
                break;
            //击杀奴隶主
            case CustomRoles.Slaveowner:
                foreach (var player in Main.AllPlayerControls)
                {
                    if (Main.ForSlaveowner.Contains(player.PlayerId))
                    {
                        Main.ForSlaveowner.Remove(target.PlayerId);
                        CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                    }
                }
                break;
            //击杀咒术师
            case CustomRoles.Spellmaster:
                int Fs = 0;
                foreach (var player in Main.AllPlayerControls)
                {
                    if (player == target) continue;
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                    if (Main.ForSpellmaster.Contains(player.PlayerId))
                    {
                        player.RpcMurderPlayerV3(player);
                        Main.ForSpellmaster.Remove(target.PlayerId);
                        Fs++;
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        return false;
                    }
                }
                if (Fs == 1)
                {
                    killer.SetKillCooldownV2(target: target, forceAnime: true);
                    return false;
                }
                break;
            //击杀万疫之神
            case CustomRoles.PlaguesGod:
                killer.SetRealKiller(target);
                target.RpcMurderPlayerV3(killer);
                Logger.Info($"{target.GetRealName()} 万疫之神反杀：{killer.GetRealName()}", "Veteran Kill");
                return false;
           
            //击杀逃犯
            case CustomRoles.Fugitive:
                var Fi = IRandom.Instance;
                int rndNum = Fi.Next(0, 100);
                if (rndNum >= 10 && rndNum < 20)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Masochism);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 20 && rndNum < 30)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Bull);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 30 && rndNum < 40)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jackal);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 40 && rndNum < 50)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jester);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 50 && rndNum < 60)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jealousy);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 60 && rndNum < 70)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.BloodKnight);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 70 && rndNum < 80)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.King);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 80 && rndNum < 90)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.God);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                if (rndNum >= 90 && rndNum < 100)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.DarkHide);
                    CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                }
                break;
        }



        //护士急救
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Nurse))
                {
                        if (target.PlayerId == pc.PlayerId || Main.NnurseHelepMax[pc.PlayerId] >= Options.NurseMax.GetInt() || !pc.IsAlive())
                            Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                    else
                    {
                        Main.NnurseHelepMax[pc.PlayerId]++;
                        Utils.TP(killer.NetTransform, target.GetTruePosition());
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        Utils.TP(target.NetTransform, Pelican.GetBlackRoomPS());
                        Main.ForNnurse.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.NnurseHelep.Add(target.PlayerId);
                        Main.NnurseHelep.Add(pc.PlayerId);
                        killer.SetKillCooldownV2();
                        NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Nurse), GetString("HelpByNurse")));
                        var tmpSpeed1 = Main.AllPlayerSpeed[pc.PlayerId];
                        Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                        var tmpSpeed2 = Main.AllPlayerSpeed[target.PlayerId];
                        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                        pc.MarkDirtySettings();
                        target.MarkDirtySettings();
                        new LateTask(() =>
                        {
                            Main.AllPlayerSpeed[pc.PlayerId] = Main.AllPlayerSpeed[pc.PlayerId] - Main.MinSpeed + tmpSpeed1;
                            pc.MarkDirtySettings();
                            Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed2;
                            target.MarkDirtySettings();
                            Utils.TP(target.NetTransform, pc.GetTruePosition());
                            Utils.TP(target.NetTransform, pc.GetTruePosition());
                            RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
                            RPC.PlaySoundRPC(target.PlayerId, Sounds.TaskComplete);
                            Main.NnurseHelep.Remove(target.PlayerId);
                            if (!pc.IsAlive())
                            {
                                target.RpcMurderPlayerV3(target);
                            }
                        }, Options.NurseSkillDuration.GetFloat(), "Trapper BlockMove");
                        return false;
                    }
                }
            }
        }
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                var pos = target.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > Options.GuideKillRadius.GetFloat()) continue;
                if (pc.Is(CustomRoles.Guide) && killer.GetCustomRole().IsImpostor() && Main.GuideMax[pc.PlayerId] <= Options.GuideKillMax.GetInt())
                {
                    Main.GuideMax[pc.PlayerId]++;
                    foreach (var player in Main.AllAlivePlayerControls)
                    {
                        if (!player.IsModClient()) player.KillFlash();
                        if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                        if (player == killer) continue;
                        if (player == pc) continue;
                        if (player == target) continue;
                        if (Vector2.Distance(pc.transform.position, player.transform.position) <= Options.GuideKillRadius.GetFloat())
                        {
                            Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.ForGuide;
                            player.SetRealKiller(pc);
                            killer.RpcMurderPlayerV3(player);
                        }
                    }
                }
            }
        }
            //灵能者操控灵魂
            if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                var Sr = IRandom.Instance;
                if (pc.Is(CustomRoles.Spiritualizer) && Sr.Next(0, 100) < Options.SpiritualizerProbability.GetInt())
                {
                    Main.ForSpiritualizerCrewmate.Add(target.PlayerId);
                    Main.ForSpiritualizerImpostor.Add(killer.PlayerId);
                }
            }
        }
        //雷达
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                
                if (Main.MerchantLeiDa.Contains(pc.PlayerId))
                {
                    Main.MerchantLeiDa.Remove(pc.PlayerId);
                    foreach (var player in Main.AllPlayerControls)
                    {
                        if (player == killer && Vector2.Distance(killer.transform.position, player.transform.position) <= 3)
                        {
                            Main.MerchantMax[pc.PlayerId]++;
                        }
                    }
                    pc.Notify(GetString("MerchantOnGuard"), Main.MerchantMax[pc.PlayerId]);
                }
            }
        }
       
        //被嫉妒狂看中
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Jealousy))
                {
                    if (killer.PlayerId == pc.PlayerId && Main.ForJealousy.Contains(target.PlayerId))
                    {
                        pc.ResetKillCooldown();
                    }         
                    else
                    {
                        Main.ForJealousy.Add(killer.PlayerId);
                        if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
                        var Jealousy = killer.PlayerId;
                        pc.Notify(string.Format(GetString("ForJealousy!!!"), Main.AllPlayerNames[Jealousy]));
                        NameColorManager.Add(pc.PlayerId, killer.PlayerId, "#996666");
                        pc.RPCPlayCustomSound("anagry");
                    }  
                }
            }
        }
        //啊啊啊是血！！！！！
        if (killer.PlayerId != target.PlayerId)
        {
            if (killer.Is(CustomRoles.Hemophobia) && !Main.ForHemophobia.Contains(killer.PlayerId)) return false;
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                var pos = target.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > Options.HemophobiaRadius.GetFloat()) continue;
                
                if (pc.Is(CustomRoles.Hemophobia))
                {
                    if (killer.PlayerId == pc.PlayerId)
                        Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                    else
                    {
                        Main.HemophobiaInKill.Remove(pc.PlayerId);
                        Main.HemophobiaInKill.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.ForHemophobia.Add(pc.PlayerId);
                        if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
                        pc.Notify(GetString("HemophobiaOnGuard"), Options.HemophobiaSeconds.GetFloat());
                    }                    
                }                   
            }
        }
        
        //妖魔鬼怪快离开
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Exorcist))
                {
                    Main.ForExorcist.Add(killer.PlayerId);
                    RPC.PlaySoundRPC(pc.PlayerId, Sounds.KillSound);
                    NameColorManager.Add(pc.PlayerId, killer.PlayerId, "#FF0000");
                }
            }
        }
        //牛仔套圈
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                if (pc.Is(CustomRoles.Cowboy))
                {
                    if (Main.MaxCowboy[pc.PlayerId] >= Options.CowboyMax.GetInt() || pc.Is(CustomRoles.Madmate) && killer.GetCustomRole().IsImpostorTeam())
                        Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                    else
                    {
                        Main.MaxCowboy[pc.PlayerId]++;
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                        killer.SetKillCooldownV2(target: target, forceAnime: true);
                        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);

                        new LateTask(() =>
                        {
                            Utils.TP(target.NetTransform, pc.GetTruePosition());
                            pc.RpcGuardAndKill();
                            Utils.NotifyRoles();
                        }, 0.2f, ("Come!"));
                        return false;
                    }                   
                }
            }
        }

        //护盾师保护
        if (killer.PlayerId != target.PlayerId)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
            {
                var pos = target.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > Options.NiceShieldsRadius.GetFloat()) continue;
                if (pc.Is(CustomRoles.NiceShields))
                {
                    if (Main.NiceShieldsInProtect.ContainsKey(pc.PlayerId) && killer.PlayerId != target.PlayerId)
                        if (Main.NiceShieldsInProtect[pc.PlayerId] + Options.NiceShieldsSkillDuration.GetInt() >= Utils.GetTimeStamp(DateTime.Now))
                        {
                            if (pc.Is(CustomRoles.Madmate) && killer.GetCustomRole().IsImpostorTeam())
                            {
                                Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                            }
                            else
                            {
                                killer.SetKillCooldownV2(target: target, forceAnime: true);
                                return false;
                            }
                        }
                }
            }
        }

        //保镖保护
        if (killer.PlayerId != target.PlayerId)
            {
                foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId))
                {
                    var pos = target.transform.position;
                    var dis = Vector2.Distance(pos, pc.transform.position);
                    if (dis > Options.BodyguardProtectRadius.GetFloat()) continue;
                    if (pc.Is(CustomRoles.Bodyguard))
                    {
                        if (pc.Is(CustomRoles.Madmate) && killer.GetCustomRole().IsImpostorTeam())
                            Logger.Info($"{pc.GetRealName()} 是个叛徒，所以他选择无视杀人现场", "Bodyguard");
                        else
                        {
                            Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                            pc.RpcMurderPlayerV3(killer);
                            pc.SetRealKiller(killer);
                            pc.RpcMurderPlayerV3(pc);
                            Logger.Info($"{pc.GetRealName()} 挺身而出与歹徒 {killer.GetRealName()} 同归于尽", "Bodyguard");
                            return false;
                        }
                    }
                }
            }

            //首刀保护
            if (Main.ShieldPlayer != byte.MaxValue && Main.ShieldPlayer == target.PlayerId && Utils.IsAllAlive)
            {
                Main.ShieldPlayer = byte.MaxValue;
                killer.SetKillCooldownV2(target: target, forceAnime: true);
                target.RpcGuardAndKill();
                return false;
            }
            //UP首刀保护
            if (Main.ShieldPlayer != byte.MaxValue && PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp && Utils.IsAllAlive && Options.EnableUpMode.GetBool() )
            {
                Main.ShieldPlayer = byte.MaxValue;
                killer.SetKillCooldownV2(target: target, forceAnime: true);
                target.RpcGuardAndKill();
                return false;
            }

        //首刀叛变
        if (Options.MadmateSpawnMode.GetInt() == 1 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && Utils.CanBeMadmate(target))
            {
                Main.MadmateNum++;
                target.RpcSetCustomRole(CustomRoles.Madmate);
                ExtendedPlayerControl.RpcSetCustomRole(target.PlayerId, CustomRoles.Madmate);
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Madmate), GetString("BecomeMadmateCuzMadmateMode")));
                killer.SetKillCooldown();
                killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);
                Logger.Info("设置职业:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Madmate.ToString(), "Assign " + CustomRoles.Madmate.ToString());
                return false;
            }

            if (!check) killer.RpcMurderPlayerV3(target);
            return true;
        }
        #endregion
}
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    class MurderPlayerPatch
    {
        public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            Logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()}{(target.protectedByGuardian ? "(Protected)" : "")}", "MurderPlayer");

            if (RandomSpawn.CustomNetworkTransformPatch.NumOfTP.TryGetValue(__instance.PlayerId, out var num) && num > 2) RandomSpawn.CustomNetworkTransformPatch.NumOfTP[__instance.PlayerId] = 3;
            if (!target.protectedByGuardian)
                Camouflage.RpcSetSkin(target, ForceRevert: true);
        }
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (target.AmOwner) RemoveDisableDevicesPatch.UpdateDisableDevices();
        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost) return;

            if (Main.OverDeadPlayerList.Contains(target.PlayerId)) return;

            PlayerControl killer = __instance; //読み替え変数

            //実際のキラーとkillerが違う場合の入れ替え処理
            if (Sniper.IsEnable)
            {
                if (Sniper.TryGetSniper(target.PlayerId, ref killer))
                {
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Sniped;
                }
            }
            if (killer != __instance)
            {
                Logger.Info($"Real Killer={killer.GetNameWithRole()}", "MurderPlayer");

            }
            if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.etc)
            {
                //死因が設定されていない場合は死亡判定
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
            }
            //看看UP是不是被首刀了
            if (Main.FirstDied == byte.MaxValue && target.Is(CustomRoles.Youtuber))
            {
                CustomSoundsManager.RPCPlayCustomSoundAll("Congrats");
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Youtuber); //UP主被首刀了，哈哈哈哈哈
                CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
            }
        if (Main.FirstDied == byte.MaxValue && target.GetCustomRole().IsCrewmate() && !target.CanUseKillButton() && Options.CanWronged.GetBool())
        {
            Main.WrongedList.Add(target.PlayerId);

        }

        //记录首刀
        if (Main.FirstDied == byte.MaxValue)
                Main.FirstDied = target.PlayerId;

            if (target.Is(CustomRoles.Bait))
            {
                if (killer.PlayerId != target.PlayerId || target.GetRealKiller()?.GetCustomRole() is CustomRoles.Swooper)
                {
                    killer.RPCPlayCustomSound("Congrats");
                    target.RPCPlayCustomSound("Congrats");
                    float delay;
                    if (Options.BaitDelayMax.GetFloat() < Options.BaitDelayMin.GetFloat()) delay = 0f;
                    else delay = IRandom.Instance.Next((int)Options.BaitDelayMin.GetFloat(), (int)Options.BaitDelayMax.GetFloat() + 1);
                    delay = Math.Max(delay, 0.15f);
                    if (delay > 0.15f && Options.BaitDelayNotify.GetBool()) killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), string.Format(GetString("KillBaitNotify"), (int)delay)), delay);
                    Logger.Info($"{killer.GetNameWithRole()} 击杀诱饵 => {target.GetNameWithRole()}", "MurderPlayer");
                    new LateTask(() => { if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data); }, delay, "Bait Self Report");
                }
            }

            if (target.Is(CustomRoles.Trapper) && killer != target)
                killer.TrapperKilled(target);

            switch (target.GetCustomRole())
            {
                case CustomRoles.BallLightning:
                    if (killer != target)
                        BallLightning.MurderPlayer(killer, target);
                    break;
            }

            switch (killer.GetCustomRole())
            {
                case CustomRoles.BoobyTrap:
                    if (killer != target)
                    {
                        if (!Main.BoobyTrapBody.Contains(target.PlayerId)) Main.BoobyTrapBody.Add(target.PlayerId);
                        if (!Main.KillerOfBoobyTrapBody.ContainsKey(target.PlayerId)) Main.KillerOfBoobyTrapBody.Add(target.PlayerId, killer.PlayerId);
                        Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Misfire;
                        killer.RpcMurderPlayerV3(killer);
                    }
                    break;
                case CustomRoles.SwordsMan:
                    if (killer != target)
                        SwordsMan.OnMurder(killer);
                    break;
                case CustomRoles.BloodKnight:
                    BloodKnight.OnMurderPlayer(killer, target);
                    break;
            }

            if (killer.Is(CustomRoles.TicketsStealer) && killer.PlayerId != target.PlayerId)
                killer.Notify(string.Format(GetString("TicketsStealerGetTicket"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Options.TicketsPerKill.GetFloat()).ToString("0.0#####")));

            if (target.Is(CustomRoles.Avanger))
            {
                var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId).ToList();
                var rp = pcList[IRandom.Instance.Next(0, pcList.Count)];
                Main.PlayerStates[rp.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
                rp.SetRealKiller(target);
                rp.RpcMurderPlayerV3(rp);
            }

            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Mediumshiper)))
                pc.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshiperKnowPlayerDead")));
          if (Lawyer.Target.ContainsValue(target.PlayerId))
             Lawyer.ChangeRoleByTarget(target);
             Hacker.AddDeadBody(target);
            Mortician.OnPlayerDead(target);
            Vulture.OnPlayerDead(target);

            Utils.AfterPlayerDeathTasks(target);

            Main.PlayerStates[target.PlayerId].SetDead();
            target.SetRealKiller(killer, true); //既に追加されてたらスキップ
            Utils.CountAlivePlayers(true);

            Utils.TargetDies(__instance, target);

            if (Options.LowLoadMode.GetBool())
            {
                __instance.MarkDirtySettings();
                target.MarkDirtySettings();
                Utils.NotifyRoles(killer);
                Utils.NotifyRoles(target);
            }
            else
            {
                Utils.SyncAllSettings();
                Utils.NotifyRoles();
            }
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
    class ShapeshiftPatch
    {
        public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            Logger.Info($"{__instance?.GetNameWithRole()} => {target?.GetNameWithRole()}", "Shapeshift");

            var shapeshifter = __instance;
            var shapeshifting = shapeshifter.PlayerId != target.PlayerId;

            if (Main.CheckShapeshift.TryGetValue(shapeshifter.PlayerId, out var last) && last == shapeshifting)
            {
                Logger.Info($"{__instance?.GetNameWithRole()}:Cancel Shapeshift.Prefix", "Shapeshift");
                return;
            }

            Main.CheckShapeshift[shapeshifter.PlayerId] = shapeshifting;
            Main.ShapeshiftTarget[shapeshifter.PlayerId] = target.PlayerId;

            Sniper.OnShapeshift(shapeshifter, shapeshifting);

        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return;
            if (!shapeshifting) Camouflage.RpcSetSkin(__instance);

            if (Pelican.IsEaten(shapeshifter.PlayerId) || GameStates.IsVoting)
                goto End;

            switch (shapeshifter.GetCustomRole())
            {
                case CustomRoles.EvilTracker:
                    EvilTracker.OnShapeshift(shapeshifter, target, shapeshifting);
                    break;
                case CustomRoles.FireWorks:
                    FireWorks.ShapeShiftState(shapeshifter, shapeshifting);
                    break;
                case CustomRoles.Warlock:
                    if (Main.CursedPlayers[shapeshifter.PlayerId] != null)//呪われた人がいるか確認
                    {
                        if (shapeshifting && !Main.CursedPlayers[shapeshifter.PlayerId].Data.IsDead)//変身解除の時に反応しない
                        {
                            var cp = Main.CursedPlayers[shapeshifter.PlayerId];
                            Vector2 cppos = cp.transform.position;//呪われた人の位置
                            Dictionary<PlayerControl, float> cpdistance = new();
                            float dis;
                            foreach (PlayerControl p in Main.AllAlivePlayerControls)
                            {
                                if (p.PlayerId == cp.PlayerId) continue;
                                if (!Options.WarlockCanKillSelf.GetBool() && p.PlayerId == shapeshifter.PlayerId) continue;
                                if (!Options.WarlockCanKillAllies.GetBool() && p.GetCustomRole().IsImpostor()) continue;
                                dis = Vector2.Distance(cppos, p.transform.position);
                                cpdistance.Add(p, dis);
                                Logger.Info($"{p?.Data?.PlayerName}の位置{dis}", "Warlock");
                            }
                            if (cpdistance.Count >= 1)
                            {
                                var min = cpdistance.OrderBy(c => c.Value).FirstOrDefault();//一番小さい値を取り出す
                                PlayerControl targetw = min.Key;
                                if (cp.RpcCheckAndMurder(targetw, true))
                                {
                                    targetw.SetRealKiller(shapeshifter);
                                    Logger.Info($"{targetw.GetNameWithRole()}was killed", "Warlock");
                                    cp.RpcMurderPlayerV3(targetw);//殺す
                                    shapeshifter.RpcGuardAndKill(shapeshifter);
                                    shapeshifter.Notify(GetString("WarlockControlKill"));
                                }
                            }
                            else
                            {
                                shapeshifter.Notify(GetString("WarlockNoTarget"));
                            }
                            Main.isCurseAndKill[shapeshifter.PlayerId] = false;
                            RPC.RpcSyncCurseAndKill();
                        }
                        Main.CursedPlayers[shapeshifter.PlayerId] = null;
                    }
                    break;
                case CustomRoles.Escapee:
                    if (shapeshifting)
                    {
                        if (Main.EscapeeLocation.ContainsKey(shapeshifter.PlayerId))
                        {
                            var position = Main.EscapeeLocation[shapeshifter.PlayerId];
                            Main.EscapeeLocation.Remove(shapeshifter.PlayerId);
                            Logger.Msg($"{shapeshifter.GetNameWithRole()}:{position}", "EscapeeTeleport");
                            Utils.TP(shapeshifter.NetTransform, position);
                            shapeshifter.RPCPlayCustomSound("Teleport");
                        }
                        else
                        {
                            Main.EscapeeLocation.Add(shapeshifter.PlayerId, shapeshifter.GetTruePosition());
                        }
                    }
                    break;
            case CustomRoles.Amnesiac:
                Main.isCursed = false;
                break;
            case CustomRoles.Swapper:
                Main.isCursed = false;
                break;
            case CustomRoles.Miner:
                    if (Main.LastEnteredVent.ContainsKey(shapeshifter.PlayerId))
                    {
                        int ventId = Main.LastEnteredVent[shapeshifter.PlayerId].Id;
                        var vent = Main.LastEnteredVent[shapeshifter.PlayerId];
                        var position = Main.LastEnteredVentLocation[shapeshifter.PlayerId];
                        Logger.Msg($"{shapeshifter.GetNameWithRole()}:{position}", "MinerTeleport");
                        Utils.TP(shapeshifter.NetTransform, new Vector2(position.x, position.y));
                    }
                    break;
                case CustomRoles.Assassin:
                     if (shapeshifting)
                {
                    if (!target.IsAlive())
                    {
                        NameNotifyManager.Notify(__instance, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("NotAssassin")));
                    }
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.AtAssassin;
                   target.SetRealKiller(__instance);
                    target.RpcMurderPlayerV3(target);
                }
                    break;
                case CustomRoles.ImperiusCurse:
                    if (shapeshifting)
                    {
                        new LateTask(() =>
                        {
                            if (!(!GameStates.IsInTask || !shapeshifter.IsAlive() || !target.IsAlive() || shapeshifter.inVent || target.inVent))
                            {
                                var originPs = target.GetTruePosition();
                                Utils.TP(target.NetTransform, shapeshifter.GetTruePosition());
                                Utils.TP(shapeshifter.NetTransform, originPs);
                            }
                        }, 1.5f, "ImperiusCurse TP");
                    }
                    break;
                case CustomRoles.QuickShooter:
                    QuickShooter.OnShapeshift(shapeshifter, shapeshifting);
                    break;
                case CustomRoles.Concealer:
                    Concealer.OnShapeshift(shapeshifting);
                    break;
                case CustomRoles.Hacker:
                    Hacker.OnShapeshift(shapeshifter, shapeshifting, target);
                break;
            case CustomRoles.Anglers:
                if (shapeshifting)
                {
                    Utils.TP(target.NetTransform, shapeshifter.GetTruePosition());
                }
                break;
        }

    End:

            //変身解除のタイミングがずれて名前が直せなかった時のために強制書き換え
            if (!shapeshifting)
            {
                new LateTask(() =>
                {
                    Utils.NotifyRoles(NoCache: true);
                },
                1.2f, "ShapeShiftNotify");
            }
        }
    }
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
class ReportDeadBodyPatch
{
    public static Dictionary<byte, bool> CanReport;
    public static Dictionary<byte, List<GameData.PlayerInfo>> WaitReport = new();
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
    {
        if (GameStates.IsMeeting) return false;
        if (Options.DisableMeeting.GetBool()) return false;
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat) return false;
        if (Options.CurrentGameMode == CustomGameMode.HotPotato) return false;
        if (!CanReport[__instance.PlayerId])
        {
            WaitReport[__instance.PlayerId].Add(target);
            Logger.Warn($"{__instance.GetNameWithRole()}:通報禁止中のため可能になるまで待機します", "ReportDeadBody");
            return false;
        }

        Logger.Info($"{__instance.GetNameWithRole()} => {target?.Object?.GetNameWithRole() ?? "null"}", "ReportDeadBody");

        foreach (var kvp in Main.PlayerStates)
        {
            var pc = Utils.GetPlayerById(kvp.Key);
            kvp.Value.LastRoom = pc.GetPlainShipRoom();
        }

        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return true;

        try
            {
                //通報者が死んでいる場合、本処理で会議がキャンセルされるのでここで止める
                if (__instance.Data.IsDead) return false;

                //=============================================
                //以下检查是否允许本次会议
                //=============================================

                var killer = target?.Object?.GetRealKiller();
                var killerRole = killer?.GetCustomRole();

                //杀戮机器无法报告或拍灯
                if (__instance.Is(CustomRoles.Minimalism)) return false;
                //禁止小黑人报告
                if (((Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Concealer.IsHidding) && Options.DisableReportWhenCC.GetBool()) return false;
          //  //扰乱技能中禁止报告
          //  if (Main.sabcatInProtect.ContainsKey(target.PlayerId))
            //    if (Main.sabcatInProtect[target.PlayerId] + Options.sabcatCooldown.GetInt() >= //Utils.GetTimeStamp(DateTime.Now))
              //  {
               //     Main.PlayerStates[killer.PlayerId].deathReason = //PlayerState.DeathReason.PissedOff;
               //     return false;
                //}

            if (target == null) //拍灯事件
                {
                    if (__instance.Is(CustomRoles.Jester) && !Options.JesterCanUseButton.GetBool()) return false;
                    if (__instance.Is(CustomRoles.EIReverso))
                     {
                    __instance?.NoCheckStartMeeting(__instance?.Data);
                    return false;
                    }
                }
                else //报告尸体事件
                {

                    // 清洁工来扫大街咯
                    if (__instance.Is(CustomRoles.Cleaner))
                    {
                        Main.CleanerBodies.Remove(target.PlayerId);
                        Main.CleanerBodies.Add(target.PlayerId);
                        __instance.RPCPlayCustomSound("Clear");
                        __instance.Notify(GetString("CleanerCleanBody"));
                        Logger.Info($"{__instance.GetRealName()} 清理了 {target.PlayerName} 的尸体", "Cleaner");
                        return false;
                    }
                    // 秃鹫吞噬尸体
                    if (__instance.Is(CustomRoles.Vulture))
                    {
                        Main.VultureBodies.Remove(target.PlayerId);
                        Main.VultureBodies.Add(target.PlayerId);
                        __instance.RPCPlayCustomSound("Eat");
                        __instance.Notify(GetString("VultureCleanBody"));
                        Logger.Info($"{__instance.GetRealName()} 吞噬了 {target.PlayerName} 的尸体", "Cleaner");
                        Main.VultureEatMax[__instance.PlayerId]++;
                        if (Main.VultureEatMax[__instance.PlayerId] >= Vulture.VultureEat.GetInt())
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vulture);
                            CustomWinnerHolder.WinnerIds.Add(__instance.PlayerId);
                        }
                        return false;
                    }
                // 失忆者无法报告尸体
                if (__instance.Is(CustomRoles.Amnesiac))
                {
                    Amnesiac.OnReportDeadBody(__instance, target);
                    Logger.Info("失忆者正常进入无法报告", "Amnesiac");
                    return false;
                }
                // 失忆者无法报告尸体第二次尝试阻止！！！
                if (__instance.Is(CustomRoles.Amnesiac))
                {
                    Amnesiac.OnReportDeadBody(__instance, target);
                    Logger.Info("失忆者正常进入无法报告--第二次尝试阻止", "Amnesiac");
                    return false;
                }
                //反转侠报告尸体
                if (__instance.Is(CustomRoles.EIReverso))
                {
                    __instance?.ReportDeadBody(null);
                    return false;
                }

                // 被赌杀的尸体无法被报告
                if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Gambled) return false;

                    // 清道夫的尸体无法被报告
                    if (killerRole == CustomRoles.Scavenger) return false;

                    // 银狼的尸体无法被报告
                    if (killerRole == CustomRoles.YinLang) return false;

                // 被清理的尸体无法报告
                if (Main.CleanerBodies.Contains(target.PlayerId))
                     {
                    NameNotifyManager.Notify(__instance, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cleaner), GetString("CleanerNot__instance")));
                    return false;
                     }

                //被吞噬的尸体无法报告
                if (Main.VultureBodies.Contains(target.PlayerId))
                {
                    NameNotifyManager.Notify(__instance, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Vulture), GetString("VultureNot__instance")));
                    return false;
                }

                    // 胆小鬼不敢报告
                    if (__instance.Is(CustomRoles.Oblivious) && (target?.Object == null || !target.Object.Is(CustomRoles.Bait))) return false;

                    // 报告了诡雷尸体
                    if (Main.BoobyTrapBody.Contains(target.PlayerId) && __instance.IsAlive())
                    {
                        var killerID = Main.KillerOfBoobyTrapBody[target.PlayerId];
                        Main.PlayerStates[__instance.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        __instance.SetRealKiller(Utils.GetPlayerById(killerID));

                        __instance.RpcMurderPlayerV3(__instance);
                        RPC.PlaySoundRPC(killerID, Sounds.KillSound);

                        if (!Main.BoobyTrapBody.Contains(__instance.PlayerId)) Main.BoobyTrapBody.Add(__instance.PlayerId);
                        if (!Main.KillerOfBoobyTrapBody.ContainsKey(__instance.PlayerId)) Main.KillerOfBoobyTrapBody.Add(__instance.PlayerId, killerID);
                        return false;
                    }
                }

            if (Options.SyncButtonMode.GetBool() && target == null)
            {
                Logger.Info("最大:" + Options.SyncedButtonCount.GetInt() + ", 現在:" + Options.UsedButtonCount, "ReportDeadBody");
                if (Options.SyncedButtonCount.GetFloat() <= Options.UsedButtonCount)
                {
                    Logger.Info("使用可能ボタン回数が最大数を超えているため、ボタンはキャンセルされました。", "ReportDeadBody");
                    return false;
                }
                else Options.UsedButtonCount++;
                if (Options.SyncedButtonCount.GetFloat() == Options.UsedButtonCount)
                {
                    Logger.Info("使用可能ボタン回数が最大数に達しました。", "ReportDeadBody");
                }
            }

            AfterReportTasks(__instance, target);

        }
        catch (Exception e)
        {
            Logger.Exception(e, "ReportDeadBodyPatch");
            Logger.SendInGame("Error: " + e.ToString());
        }

        return true;
    }
    public static void AfterReportTasks(PlayerControl player, GameData.PlayerInfo target)
        {
            //=============================================
            //以下、ボタンが押されることが確定したものとする。
            //=============================================

            if (target == null) //ボタン
            {
                if (player.Is(CustomRoles.Mayor))
                {
                    Main.MayorUsedButtonCount[player.PlayerId] += 1;
                }
            //操控者
            /*if (player.Is(CustomRoles.Manipulator) || !player.Is(CustomRoles.Manipulator))
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (!pc.IsAlive()) continue;
                    if (pc.GetCustomRole().IsImpostor())
                    {
                        var Im = IRandom.Instance;
                        int Ma = Im.Next(0, 100);
                        if (Ma < Options.DepressedIdioctoniaProbability.GetInt())
                        {
                            Main.ManipulatorNeutral[player.PlayerId] += Im.Next(0, 10);
                        }
                        else
                        {
                            Main.ManipulatorImpotors[player.PlayerId]++;
                        }
                    }
                    if (pc.GetCustomRole().IsCrewmate())
                    {
                        var Cr = IRandom.Instance;
                        int Mn = Cr.Next(0, 100);
                        if (Mn < Options.DepressedIdioctoniaProbability.GetInt())
                        {
                            Main.ManipulatorNeutral[player.PlayerId] += Cr.Next(0, 10);
                        }
                        else
                        {
                            Main.ManipulatorCrewmate[player.PlayerId]++;
                        }
                    }
                    if (pc.GetCustomRole().IsNeutral())
                    {
                        var Ne = IRandom.Instance;
                        int Mi = Ne.Next(0, 100);
                        if (Mi < Options.DepressedIdioctoniaProbability.GetInt())
                        {
                            Main.ManipulatorNeutral[player.PlayerId] += Ne.Next(0, 10);
                        }
                        else
                        {
                            Main.ManipulatorNeutral[player.PlayerId]++;
                        }
                    }
                }
                string msg;
                msg = string.Format(GetString("ManipulatorNoticeVictim"), Main.ManipulatorImpotors[player.PlayerId], Main.ManipulatorCrewmate[player.PlayerId], Main.ManipulatorNeutral[player.PlayerId]);
                Main.ManipulatorNotify.Add(player.PlayerId, msg);
                new LateTask(() =>
                {
                    Main.ManipulatorImpotors[player.PlayerId] = 0;
                    Main.ManipulatorNeutral[player.PlayerId] = 0;
                    Main.ManipulatorCrewmate[player.PlayerId] = 0;
                    Utils.NotifyRoles();
                }, 15f, ("清空"));
            }*/
        }
            else
            {
                var tpc = Utils.GetPlayerById(target.PlayerId);
                if (tpc != null && !tpc.IsAlive())
                {
                    // 撅暮报告
                    if (player.Is(CustomRoles.Detective))
                    {
                        string msg;
                        Logger.Info("即将进入循环，请稍后", "Detective");
                        msg = string.Format(GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetDisplayRoleName());
                        if (Options.DetectiveCanknowKiller.GetBool())
                        {
                            Logger.Info("正常进入循环", "Detective");
                            var realKiller = tpc.GetRealKiller();
                            if (realKiller == null) msg += "；" + GetString("DetectiveNoticeKillerNotFound");
                            else msg += "；" + string.Format(GetString("DetectiveNoticeKiller"), realKiller.GetDisplayRoleName());
                        }
                        Main.DetectiveNotify.Add(player.PlayerId, msg);
                    }                
            }
            }
            Main.LastVotedPlayerInfo = null;
            Main.ArsonistTimer.Clear();
            Main.PuppeteerList.Clear();
            Main.GuesserGuessed.Clear();
            Main.VeteranInProtect.Clear();
            Main.GrenadierBlinding.Clear();
            Main.MadGrenadierBlinding.Clear();
            Main.RudepeopleInProtect.Clear();
            Divinator.didVote.Clear();
            Main.NiceShieldsInProtect.Clear();
            Main.DeputyInProtect.Clear();
            Main.QSRInProtect.Clear();
            Main.InBoom.Clear();
        Main.ForNnurse.Clear();

            Main.GrenadiersInProtect.Clear();
            Concealer.OnReportDeadBody();
        //Concealer.OnReportDeadBody();
            Psychic.OnReportDeadBody();
            BountyHunter.OnReportDeadBody();
            SerialKiller.OnReportDeadBody();
            Sniper.OnReportDeadBody();
            Vampire.OnStartMeeting();
            Pelican.OnReportDeadBody();
            Counterfeiter.OnReportDeadBody();
            BallLightning.OnReportDeadBody();
            QuickShooter.OnReportDeadBody();
            Eraser.OnReportDeadBody();
            Hacker.OnReportDeadBody();
            Judge.OnReportDeadBody();
            Greedier.OnReportDeadBody();

            Mortician.OnReportDeadBody(player, target);
            Mediumshiper.OnReportDeadBody(target);
            Vulture.OnReportDeadBody(player,target);

        #region 革命家失败处理
        foreach (var x in Main.RevolutionistStart)
            {
                var tar = Utils.GetPlayerById(x.Key);
                if (tar == null) continue;
                tar.Data.IsDead = true;
                Main.PlayerStates[tar.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                tar.RpcExileV2();
                Main.PlayerStates[tar.PlayerId].SetDead();
                Logger.Info($"{tar.GetRealName()} 因会议革命失败", "Revolutionist");
            }
            Main.RevolutionistTimer.Clear();
            Main.RevolutionistStart.Clear();
            Main.RevolutionistLastTime.Clear();
            #endregion

            Main.AllPlayerControls
                .Where(pc => Main.CheckShapeshift.ContainsKey(pc.PlayerId))
                .Do(pc => Camouflage.RpcSetSkin(pc, RevertToDefault: true));

            MeetingTimeManager.OnReportDeadBody();

            Utils.NotifyRoles(isForMeeting: true, NoCache: true);

            Utils.SyncAllSettings();

            if (Concealer.IsHidding && !(Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()))
                Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, ForceRevert: true, RevertToDefault: true));

            if (Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool())
            { Utils.NotifyRoles(CamouflageisForMeeting: true, CamouflageIsActive: true); }
        }
        public static async void ChangeLocalNameAndRevert(string name, int time)
        {
            //async Taskじゃ警告出るから仕方ないよね。
            var revertName = PlayerControl.LocalPlayer.name;
            PlayerControl.LocalPlayer.RpcSetNameEx(name);
            await Task.Delay(time);
             PlayerControl.LocalPlayer.RpcSetNameEx(revertName);
        }
}
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        private static long LastFixedUpdate = new();
        private static StringBuilder Mark = new(20);
        private static StringBuilder Suffix = new(120);
        private static int LevelKickBufferTime = 10;
        private static Dictionary<byte, int> BufferTime = new();
        public static void Postfix(PlayerControl __instance)
        {
            var player = __instance;

            if (!GameStates.IsModHost) return;

            bool lowLoad = false;
            if (Options.LowLoadMode.GetBool())
            {
                BufferTime.TryAdd(player.PlayerId, 10);
                BufferTime[player.PlayerId]--;
                if (BufferTime[player.PlayerId] > 0) lowLoad = true;
                else BufferTime[player.PlayerId] = 10;
            }

            Sniper.OnFixedUpdate(player);
            Zoom.OnFixedUpdate();
            if (!lowLoad)
            {
                NameNotifyManager.OnFixedUpdate(player);
                TargetArrow.OnFixedUpdate(player);
                LocateArrow.OnFixedUpdate(player);
            }


            if (AmongUsClient.Instance.AmHost)
            {//実行クライアントがホストの場合のみ実行
                if (GameStates.IsLobby && ((ModUpdater.hasUpdate && ModUpdater.forceUpdate) || ModUpdater.isBroken || !Main.AllowPublicRoom) && AmongUsClient.Instance.IsGamePublic)
                    AmongUsClient.Instance.ChangeGamePublic(false);

                if (GameStates.IsInTask && ReportDeadBodyPatch.CanReport[__instance.PlayerId] && ReportDeadBodyPatch.WaitReport[__instance.PlayerId].Count > 0)
                {
                    var info = ReportDeadBodyPatch.WaitReport[__instance.PlayerId][0];
                    ReportDeadBodyPatch.WaitReport[__instance.PlayerId].Clear();
                    Logger.Info($"{__instance.GetNameWithRole()}:通報可能になったため通報処理を行います", "ReportDeadbody");
                    __instance.ReportDeadBody(info);
                }

                //踢出低等级的人
                if (!lowLoad && GameStates.IsLobby && !player.AmOwner && Options.KickLowLevelPlayer.GetInt() != 0 && (
                    (player.Data.PlayerLevel != 0 && player.Data.PlayerLevel < Options.KickLowLevelPlayer.GetInt()) ||
                    player.Data.FriendCode == ""
                    ))
                {
                    LevelKickBufferTime--;
                    if (LevelKickBufferTime <= 0)
                    {
                        LevelKickBufferTime = 20;
                        AmongUsClient.Instance.KickPlayer(player.GetClientId(), false);
                        string msg = string.Format(GetString("KickBecauseLowLevel"), player.GetRealName().RemoveHtmlTags());
                        Logger.SendInGame(msg);
                        Logger.Info(msg, "LowLevel Kick");
                    }
                }

                DoubleTrigger.OnFixedUpdate(player);
                Vampire.OnFixedUpdate(player);
                BountyHunter.FixedUpdate(player);
                SerialKiller.FixedUpdate(player);

                #region 女巫处理
                if (GameStates.IsInTask && Main.WarlockTimer.ContainsKey(player.PlayerId))//処理を1秒遅らせる
                {
                    if (player.IsAlive())
                    {
                        if (Main.WarlockTimer[player.PlayerId] >= 1f)
                        {
                            player.RpcResetAbilityCooldown();
                            Main.isCursed = false;//変身クールを１秒に変更
                            player.SyncSettings();
                            Main.WarlockTimer.Remove(player.PlayerId);
                        }
                        else Main.WarlockTimer[player.PlayerId] = Main.WarlockTimer[player.PlayerId] + Time.fixedDeltaTime;//時間をカウント
                    }
                    else
                    {
                        Main.WarlockTimer.Remove(player.PlayerId);
                    }
                }
                //ターゲットのリセット
                #endregion

                #region 纵火犯浇油处理
                if (GameStates.IsInTask && Main.ArsonistTimer.ContainsKey(player.PlayerId))//アーソニストが誰かを塗っているとき
                {
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                    {
                        Main.ArsonistTimer.Remove(player.PlayerId);
                        Utils.NotifyRoles(__instance);
                        RPC.ResetCurrentDousingTarget(player.PlayerId);
                    }
                    else
                    {
                        var ar_target = Main.ArsonistTimer[player.PlayerId].Item1;//塗られる人
                        var ar_time = Main.ArsonistTimer[player.PlayerId].Item2;//塗った時間
                        if (!ar_target.IsAlive())
                        {
                            Main.ArsonistTimer.Remove(player.PlayerId);
                        }
                        else if (ar_time >= Options.ArsonistDouseTime.GetFloat())//時間以上一緒にいて塗れた時
                        {
                            player.SetKillCooldown();
                            Main.ArsonistTimer.Remove(player.PlayerId);//塗が完了したのでDictionaryから削除
                            Main.isDoused[(player.PlayerId, ar_target.PlayerId)] = true;//塗り完了
                            player.RpcSetDousedPlayer(ar_target, true);
                            Utils.NotifyRoles(player);//名前変更
                            RPC.ResetCurrentDousingTarget(player.PlayerId);
                        }
                        else
                        {

                            float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                            float dis = Vector2.Distance(player.transform.position, ar_target.transform.position);//距離を出す
                            if (dis <= range)//一定の距離にターゲットがいるならば時間をカウント
                            {
                                Main.ArsonistTimer[player.PlayerId] = (ar_target, ar_time + Time.fixedDeltaTime);
                            }
                            else//それ以外は削除
                            {
                                Main.ArsonistTimer.Remove(player.PlayerId);
                                Utils.NotifyRoles(player);
                                RPC.ResetCurrentDousingTarget(player.PlayerId);

                                Logger.Info($"Canceled: {player.GetNameWithRole()}", "Arsonist");
                            }
                        }
                    }
                }
                #endregion

                #region 革命家拉人处理
                if (GameStates.IsInTask && Main.RevolutionistTimer.ContainsKey(player.PlayerId))//当革命家拉拢一个玩家时
                {
                    if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                    {
                        Main.RevolutionistTimer.Remove(player.PlayerId);
                        Utils.NotifyRoles(player);
                        RPC.ResetCurrentDrawTarget(player.PlayerId);
                    }
                    else
                    {
                        var rv_target = Main.RevolutionistTimer[player.PlayerId].Item1;//拉拢的人
                        var rv_time = Main.RevolutionistTimer[player.PlayerId].Item2;//拉拢时间
                        if (!rv_target.IsAlive())
                        {
                            Main.RevolutionistTimer.Remove(player.PlayerId);
                        }
                        else if (rv_time >= Options.RevolutionistDrawTime.GetFloat())//在一起时间超过多久
                        {
                            player.SetKillCooldown();
                            Main.RevolutionistTimer.Remove(player.PlayerId);//拉拢完成从字典中删除
                            Main.isDraw[(player.PlayerId, rv_target.PlayerId)] = true;//完成拉拢
                            player.RpcSetDrawPlayer(rv_target, true);
                            Utils.NotifyRoles(player);
                            RPC.ResetCurrentDrawTarget(player.PlayerId);
                            if (IRandom.Instance.Next(1, 100) <= Options.RevolutionistKillProbability.GetInt())
                            {
                                rv_target.SetRealKiller(player);
                                Main.PlayerStates[rv_target.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                                player.RpcMurderPlayerV3(rv_target);
                                Main.PlayerStates[rv_target.PlayerId].SetDead();
                                Logger.Info($"Revolutionist: {player.GetNameWithRole()} killed {rv_target.GetNameWithRole()}", "Revolutionist");
                            }
                        }
                        else
                        {
                            float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                            float dis = Vector2.Distance(player.transform.position, rv_target.transform.position);//超出距离
                            if (dis <= range)//在一定距离内则计算时间
                            {
                                Main.RevolutionistTimer[player.PlayerId] = (rv_target, rv_time + Time.fixedDeltaTime);
                            }
                            else//否则删除
                            {
                                Main.RevolutionistTimer.Remove(player.PlayerId);
                                Utils.NotifyRoles(__instance);
                                RPC.ResetCurrentDrawTarget(player.PlayerId);

                                Logger.Info($"Canceled: {__instance.GetNameWithRole()}", "Revolutionist");
                            }
                        }
                    }
                }
                if (GameStates.IsInTask && player.IsDrawDone() && player.IsAlive())
                {
                    if (Main.RevolutionistStart.ContainsKey(player.PlayerId)) //如果存在字典
                    {
                        if (Main.RevolutionistLastTime.ContainsKey(player.PlayerId))
                        {
                            long nowtime = Utils.GetTimeStamp();
                            if (Main.RevolutionistLastTime[player.PlayerId] != nowtime) Main.RevolutionistLastTime[player.PlayerId] = nowtime;
                            int time = (int)(Main.RevolutionistLastTime[player.PlayerId] - Main.RevolutionistStart[player.PlayerId]);
                            int countdown = Options.RevolutionistVentCountDown.GetInt() - time;
                            Main.RevolutionistCountdown.Clear();
                            if (countdown <= 0)//倒计时结束
                            {
                                Utils.GetDrawPlayerCount(player.PlayerId, out var y);
                                foreach (var pc in y.Where(x => x != null && x.IsAlive()))
                                {
                                    pc.Data.IsDead = true;
                                    Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                                    pc.RpcMurderPlayerV3(pc);
                                    Main.PlayerStates[pc.PlayerId].SetDead();
                                    Utils.NotifyRoles(pc);
                                }
                                player.Data.IsDead = true;
                                Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                                player.RpcMurderPlayerV3(player);
                                Main.PlayerStates[player.PlayerId].SetDead();
                            }
                            else
                            {
                                Main.RevolutionistCountdown.Add(player.PlayerId, countdown);
                            }
                        }
                        else
                        {
                            Main.RevolutionistLastTime.TryAdd(player.PlayerId, Main.RevolutionistStart[player.PlayerId]);
                        }
                    }
                    else //如果不存在字典
                    {
                        Main.RevolutionistStart.TryAdd(player.PlayerId, Utils.GetTimeStamp());
                    }
                }
                #endregion

                if (!lowLoad)
                {
                    //检查老兵技能是否失效
                    if (GameStates.IsInTask && player.Is(CustomRoles.Veteran))
                    {
                        if (Main.VeteranInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.VeteranSkillDuration.GetInt() < Utils.GetTimeStamp())
                        {
                            Main.VeteranInProtect.Remove(player.PlayerId);
                            player.RpcGuardAndKill();
                            player.Notify(string.Format(GetString("VeteranOffGuard"), Main.VeteranNumOfUsed[player.PlayerId]));
                        }
                    }
                //检查恐血者技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.Hemophobia))
                {
                    if (Main.HemophobiaInKill.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.HemophobiaSeconds.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.HemophobiaInKill.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("HemophobiaSkillStop"));
                        Main.ForHemophobia.Remove(player.PlayerId);
                    }
                }
                //检查小孩是否要增加年龄
                /*if (GameStates.IsInTask && (player.Is(CustomRoles.NiceMini) || player.Is(CustomRoles.EvilMini)))
                {
                    if (Main.NiceMiniTime.TryGetValue(player.PlayerId, out var vtime) && vtime + NiceMini.Up < Utils.GetTimeStamp() && NiceMini.Age != 18 && Main.GetUp.TryGetValue(player.PlayerId, out var utime) && utime + NiceMini.GrowUpTime > Utils.GetTimeStamp() || !Main.NiceMiniTime.ContainsKey(player.PlayerId))
                    {
                        Main.NiceMiniTime.Remove(player.PlayerId);
                        Main.NiceMiniTime.Add(player.PlayerId, Utils.GetTimeStamp());
                        NiceMini.Age += 1;
                        Logger.Info($"年龄", "ReportDeadbody");
                    }
                    if (!Main.GetUp.ContainsKey(player.PlayerId))
                    {
                        Main.GetUp.Add(player.PlayerId, Utils.GetTimeStamp());
                    }                 
                }//*/

                if (GameStates.IsInTask &&(player.Is(CustomRoles.NiceMini) || player.Is(CustomRoles.EvilMini)))
                {
                    if (NiceMini.Age < 18)
                    {
                        if (LastFixedUpdate == Utils.GetTimeStamp()) return;
                        LastFixedUpdate = Utils.GetTimeStamp();
                        NiceMini.GrowUpTime ++;
                        if (NiceMini.GrowUpTime >= NiceMini.GrowUpDuration.GetInt()/18)
                        {
                            NiceMini.Age += 1;
                            NiceMini.GrowUpTime = 0;
                        }
                    }
                }
                
                //检查双刀手的第二把叨是否已经到时间
                if (GameStates.IsInTask && player.Is(CustomRoles.DoubleKiller))
                {
                    if (Main.DoubleKillerKillSeacond.TryGetValue(player.PlayerId, out var vtime) && vtime + DoubleKiller.TwoDoubleKillerKillColldown.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.DoubleKillerKillSeacond.Remove(player.PlayerId);
                        Main.DoubleKillerMax.Add(player.PlayerId);
                        player.Notify(GetString("DoubleKillerKillColldownTure"));
                        Logger.Info($"aaaaaa", "ReportDeadbody");
                    }
                }
               

                //检查护士是否已经完成救治
                if (GameStates.IsInTask && player.Is(CustomRoles.Nurse))
                {
                    if (Main.ForNnurse.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.NurseSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.ForNnurse.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("ForNnurseCanHelp"));
                        Main.NnurseHelep.Remove(player.PlayerId);
                    }
                }
                //检查爆破狂的炸弹是否到了引爆时间
                if (GameStates.IsInTask && player.Is(CustomRoles.DemolitionManiac))
                {
                    if (Main.InBoom.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.DemolitionManiacWait.GetFloat() < Utils.GetTimeStamp())
                    {
                        
                        foreach (var pc in Main.AllPlayerControls)
                        {
                            if (!pc.IsModClient()) pc.KillFlash();
                            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) continue;
                            if (Main.ForDemolition.Contains(pc.PlayerId))
                            {
                                foreach (var BoomPlayer in Main.AllPlayerControls)
                                {
                                    if (Vector2.Distance(pc.transform.position, BoomPlayer.transform.position) <= Options.DemolitionManiacRadius.GetFloat())
                                    {
                                        Main.PlayerStates[BoomPlayer.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                                        BoomPlayer.SetRealKiller(player);
                                        BoomPlayer.RpcMurderPlayerV3(BoomPlayer);
                                    }
                                }
                                
                            }                           
                        }
                    }
                }

                //检查掷雷兵技能是否生效
                if (GameStates.IsInTask && player.Is(CustomRoles.Grenadier))
                    {
                        if (Main.GrenadierBlinding.TryGetValue(player.PlayerId, out var gtime) && gtime + Options.GrenadierSkillDuration.GetInt() < Utils.GetTimeStamp())
                        {
                            Main.GrenadierBlinding.Remove(player.PlayerId);
                            player.RpcGuardAndKill();
                            player.Notify(GetString("GrenadierSkillStop"));
                            Utils.MarkEveryoneDirtySettings();
                        }
                        if (Main.MadGrenadierBlinding.TryGetValue(player.PlayerId, out var mgtime) && mgtime + Options.GrenadierSkillDuration.GetInt() < Utils.GetTimeStamp())
                        {
                            Main.MadGrenadierBlinding.Remove(player.PlayerId);
                            player.RpcGuardAndKill();
                            player.Notify(GetString("GrenadierSkillStop"));
                            Utils.MarkEveryoneDirtySettings();
                        }
                    }
                //检查掷弹兵技能是否生效
                if (GameStates.IsInTask && player.Is(CustomRoles.Grenadiers))
                {
                    if (Main.GrenadiersInProtect.TryGetValue(player.PlayerId, out var gtime) && gtime + Options.GrenadiersDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.GrenadiersInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("GrenadierSkillStop"));
                        Utils.MarkEveryoneDirtySettings();
                        foreach (var pc in Main.AllPlayerControls)
                        {
                            Main.ForGrenadiers.Remove(pc.PlayerId);
                        }
                    }
                }
                //检查挑衅者技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.Rudepeople))
                    {
                        if (Main.RudepeopleInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.RudepeopleSkillDuration.GetInt() < Utils.GetTimeStamp())
                        {
                            Main.RudepeopleInProtect.Remove(player.PlayerId);
                            player.RpcGuardAndKill();
                        player.Notify(string.Format(GetString("RudepeopleOffGuard")));
                        }
                    }
                //检查扰乱者技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.sabcat))
                {
                    if (Main.sabcatInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.sabcatCooldown.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.sabcatInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(string.Format(GetString("sabcatOffGuard"), Main.sabcatNumOfUsed[player.PlayerId]));
                    }
                }
                //检查时间之主技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.TimeMaster))
                {
                    if (Main.TimeMasterInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.TimeMasterSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.TimeMasterInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("TimeMasterSkillStop"));
                    }
                }
                //检查正义的护盾师的技能是否失效
                if (GameStates.IsInTask && player.Is(CustomRoles.NiceShields))
                {
                    if (Main.NiceShieldsInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.NiceShieldsSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.NiceShieldsInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("NiceShieldsSkillStop"));
                    }
                }
                //检查时停者的技能是否生效
                if (GameStates.IsInTask && player.Is(CustomRoles.TimeStops))
                {
                    if (Main.TimeStopsInProtect.TryGetValue(player.PlayerId, out var vtime) && vtime + Options.TimeStopsSkillDuration.GetInt() < Utils.GetTimeStamp())
                    {
                        Main.TimeStopsInProtect.Remove(player.PlayerId);
                        player.RpcGuardAndKill();
                        player.Notify(GetString("TimeStopsSkillStop"));
                        foreach (var pc in Main.AllPlayerControls)
                        {
                            Main.TimeStopsstop.Remove(pc.PlayerId);
                        }
                    }
                }

                //检查马里奥是否完成
                if (GameStates.IsInTask && player.Is(CustomRoles.Mario) && Main.MarioVentCount[player.PlayerId] > Options.MarioVentNumWin.GetInt())
                    {
                        Main.MarioVentCount[player.PlayerId] = Options.MarioVentNumWin.GetInt();
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario); //马里奥这个多动症赢了
                        CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                    }

                    Pelican.OnFixedUpdate();
                    BallLightning.OnFixedUpdate();
                    Swooper.OnFixedUpdate(player);
                    BloodKnight.OnFixedUpdate(player);

                    if (GameStates.IsInTask && player.IsAlive() && Options.LadderDeath.GetBool()) FallFromLadder.FixedUpdate(player);

                    if (GameStates.IsInGame) LoversSuicide();
                if (GameStates.IsInGame) JackalSuicide();
                if (GameStates.IsInGame) CaptainSuicide();
                //if (GameStates.IsInGame) SheriffSuicide();
                if (GameStates.IsInGame) CrushLoversSuicide();
                if (GameStates.IsInGame) CupidLoversSuicide();
                if (GameStates.IsInGame) ImposotorSuicide();
                if (GameStates.IsInGame) HunterSuicide();

                #region 傀儡师处理
                if (GameStates.IsInTask && Main.PuppeteerList.ContainsKey(player.PlayerId))
                    {
                        if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                        {
                            Main.PuppeteerList.Remove(player.PlayerId);
                            RPC.RpcSyncPuppeteerList();
                        }
                        else
                        {
                            Vector2 puppeteerPos = player.transform.position;//PuppeteerListのKeyの位置
                            Dictionary<byte, float> targetDistance = new();
                            float dis;
                            foreach (var target in Main.AllAlivePlayerControls)
                            {
                                if (target.PlayerId != player.PlayerId && !target.Is(CountTypes.Impostor))
                                {
                                    dis = Vector2.Distance(puppeteerPos, target.transform.position);
                                    targetDistance.Add(target.PlayerId, dis);
                                }
                            }
                            if (targetDistance.Count() != 0)
                            {
                                var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();//一番値が小さい
                                PlayerControl target = Utils.GetPlayerById(min.Key);
                                var KillRange = NormalGameOptionsV07.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                                if (min.Value <= KillRange && player.CanMove && target.CanMove)
                                {
                                    if (player.RpcCheckAndMurder(target, true))
                                    {
                                        var puppeteerId = Main.PuppeteerList[player.PlayerId];
                                        RPC.PlaySoundRPC(puppeteerId, Sounds.KillSound);
                                        target.SetRealKiller(Utils.GetPlayerById(puppeteerId));
                                        player.RpcMurderPlayerV3(target);
                                        Utils.MarkEveryoneDirtySettings();
                                        Main.PuppeteerList.Remove(player.PlayerId);
                                        RPC.RpcSyncPuppeteerList();
                                        Utils.NotifyRoles();
                                    }
                                }
                            }
                        }
                    }
                    #endregion

                    if (GameStates.IsInTask && player == PlayerControl.LocalPlayer)
                        DisableDevice.FixedUpdate();
                    if (GameStates.IsInTask && player == PlayerControl.LocalPlayer)
                        AntiAdminer.FixedUpdate();

                    if (GameStates.IsInGame && Main.RefixCooldownDelay <= 0)
                        foreach (var pc in Main.AllPlayerControls)
                        {
                            if (pc.Is(CustomRoles.Vampire) || pc.Is(CustomRoles.Warlock) || pc.Is(CustomRoles.Assassin))
                                Main.AllPlayerKillCooldown[pc.PlayerId] = Options.DefaultKillCooldown * 2;
                        }

                    if (!Main.DoBlockNameChange && AmongUsClient.Instance.AmHost)
                        Utils.ApplySuffix(__instance);
                }
            }
            //LocalPlayer専用
            if (__instance.AmOwner)
            {
                //キルターゲットの上書き処理
                if (GameStates.IsInTask && !__instance.Is(CustomRoleTypes.Impostor) && __instance.CanUseKillButton() && !__instance.Data.IsDead)
                {
                    var players = __instance.GetPlayersInAbilityRangeSorted(false);
                    PlayerControl closest = players.Count <= 0 ? null : players[0];
                    HudManager.Instance.KillButton.SetTarget(closest);
                }
            }

            //役職テキストの表示
            var RoleTextTransform = __instance.cosmetics.nameText.transform.Find("RoleText");
            var RoleText = RoleTextTransform.GetComponent<TMPro.TextMeshPro>();
            if (RoleText != null && __instance != null && !lowLoad)
            {
                if (GameStates.IsLobby)
                {
                    if (Main.playerVersion.TryGetValue(__instance.PlayerId, out var ver))
                    {
                        if (Main.ForkId != ver.forkId) // フォークIDが違う場合
                            __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>{ver.forkId}</size>\n{__instance?.name}</color>";
                        else if (Main.version.CompareTo(ver.version) == 0)
                            __instance.cosmetics.nameText.text = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" ? $"<color=#87cefa>{__instance.name}</color>" : $"<color=#ffff00><size=1.2>{ver.tag}</size>\n{__instance?.name}</color>";
                        else __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>v{ver.version}</size>\n{__instance?.name}</color>";
                    }
                    else __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                }
                if (GameStates.IsInGame)
                {
                    var RoleTextData = Utils.GetRoleText(PlayerControl.LocalPlayer.PlayerId, __instance.PlayerId);
                    //if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                    //{
                    //    var hasRole = main.AllPlayerCustomRoles.TryGetValue(__instance.PlayerId, out var role);
                    //    if (hasRole) RoleTextData = Utils.GetRoleTextHideAndSeek(__instance.Data.Role.Role, role);
                    //}
                    RoleText.text = RoleTextData.Item1;
                    if (Options.CurrentGameMode == CustomGameMode.SoloKombat) RoleText.text = "";
                    RoleText.color = RoleTextData.Item2;
                    if (__instance.AmOwner) RoleText.enabled = true; //自分ならロールを表示
                    else if (Options.CurrentGameMode == CustomGameMode.SoloKombat) RoleText.enabled = true;
                    else if (Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) RoleText.enabled = true; //他プレイヤーでVisibleTasksCountが有効なおかつ自分が死んでいるならロールを表示
                    else if (__instance.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.CrushLovers) && PlayerControl.LocalPlayer.Is(CustomRoles.CrushLovers) && Options.CrushLoverKnowRoles.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.CupidLovers) && PlayerControl.LocalPlayer.Is(CustomRoles.CupidLovers) && Options.CupidLoverKnowRoles.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.CupidLovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Cupid) && Options.CanKnowCupid.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) RoleText.enabled = true;
                    else if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosImp.GetBool()) RoleText.enabled = true;
                    else if (__instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowWhosMadmate.GetBool()) RoleText.enabled = true;
                    else if (__instance.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) RoleText.enabled = true;
                    else if (Totocalcio.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                    else if (Succubus.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Jackal.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Corpse.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true; 
                else if (Captain.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                else if (Lawyer.KnowRole(PlayerControl.LocalPlayer, __instance)) RoleText.enabled = true;
                //喵喵队
                //内鬼
                else if (__instance.Is(CustomRoles.ImpostorSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.ImpostorSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //豺狼
                else if (__instance.Is(CustomRoles.JSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.Jackal) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Jackal) && PlayerControl.LocalPlayer.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.JSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.JSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.Whoops) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Whoops) && PlayerControl.LocalPlayer.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.JSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.Attendant) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Attendant) && PlayerControl.LocalPlayer.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //西风骑士团(bushi)
                else if (__instance.Is(CustomRoles.BloodSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.BloodKnight) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.BloodKnight) && PlayerControl.LocalPlayer.Is(CustomRoles.BloodSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //疫情的源头
                else if (__instance.Is(CustomRoles.PGSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.PlaguesGod) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.PlaguesGod) && PlayerControl.LocalPlayer.Is(CustomRoles.PGSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //玩家
                else if (__instance.Is(CustomRoles.GamerSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.Gamer) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.Gamer) && PlayerControl.LocalPlayer.Is(CustomRoles.GamerSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //穹P黑客(BUSHI)
                else if (__instance.Is(CustomRoles.YLSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.YinLang) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.YinLang) && PlayerControl.LocalPlayer.Is(CustomRoles.YLSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //黑，真tm黑
                else if (__instance.Is(CustomRoles.DHSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.DarkHide) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.DarkHide) && PlayerControl.LocalPlayer.Is(CustomRoles.DHSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                //雇佣
                else if (__instance.Is(CustomRoles.OKSchrodingerCat) && PlayerControl.LocalPlayer.Is(CustomRoles.OpportunistKiller) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (__instance.Is(CustomRoles.OpportunistKiller) && PlayerControl.LocalPlayer.Is(CustomRoles.OKSchrodingerCat) && Options.CanKnowKiller.GetBool()) RoleText.enabled = true;
                else if (PlayerControl.LocalPlayer.Is(CustomRoles.God)) RoleText.enabled = true;
                    else if (PlayerControl.LocalPlayer.Is(CustomRoles.GM)) RoleText.enabled = true;
                    else if (Main.GodMode.Value) RoleText.enabled = true;
                    else RoleText.enabled = false; //そうでなければロールを非表示
                    if (!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
                    {
                        RoleText.enabled = false; //ゲームが始まっておらずフリープレイでなければロールを非表示
                        if (!__instance.AmOwner) __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                    }
                    if (Main.VisibleTasksCount) //他プレイヤーでVisibleTasksCountは有効なら
                        RoleText.text += Utils.GetProgressText(__instance); //ロールの横にタスクなど進行状況表示


                    //変数定義
                    var seer = PlayerControl.LocalPlayer;
                    var target = __instance;

                    string RealName;
                    Mark.Clear();
                    Suffix.Clear();

                    //名前変更
                    RealName = target.GetRealName();

                    //名前色変更処理
                    //自分自身の名前の色を変更
                    if (target.AmOwner && GameStates.IsInTask)
                    { //targetが自分自身
                        if (target.Is(CustomRoles.Arsonist) && target.IsDouseDone())
                            RealName = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist), GetString("EnterVentToWin"));
                        if (target.Is(CustomRoles.Revolutionist) && target.IsDrawDone())
                            RealName = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Revolutionist), string.Format(GetString("EnterVentWinCountDown"), Main.RevolutionistCountdown.TryGetValue(seer.PlayerId, out var x) ? x : 10));
                        if (Pelican.IsEaten(seer.PlayerId))
                            RealName = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"));
                        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                            SoloKombatManager.GetNameNotify(target, ref RealName);
                        if (NameNotifyManager.GetNameNotify(target, out var name))
                            RealName = name;
                    }

                    //NameColorManager準拠の処理
                    RealName = RealName.ApplyNameColorData(seer, target, false);

                    if (seer.GetCustomRole().IsImpostor()) //seerがインポスター
                    {
                        if (target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetPlayerTaskState().IsTaskFinished) //targetがタスクを終わらせたマッドスニッチ
                            Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), "★")); //targetにマーク付与
                    }
                    //インポスター/キル可能なニュートラルがタスクが終わりそうなSnitchを確認できる
                    Mark.Append(Snitch.GetWarningMark(seer, target));

                    if (seer.Is(CustomRoles.Arsonist))
                    {
                        if (seer.IsDousedPlayer(target))
                        {
                            Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");
                        }
                        else if (
                            Main.currentDousingTarget != byte.MaxValue &&
                            Main.currentDousingTarget == target.PlayerId
                        )
                        {
                            Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");
                        }
                    }
                    if (seer.Is(CustomRoles.Revolutionist))
                    {
                        if (seer.IsDrawPlayer(target))
                        {
                            Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");
                        }
                        else if (
                            Main.currentDrawTarget != byte.MaxValue &&
                            Main.currentDrawTarget == target.PlayerId
                        )
                        {
                            Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");
                        }
                    }

                    Mark.Append(Executioner.TargetMark(seer, target));

                    Mark.Append(Gamer.TargetMark(seer, target));

                    Mark.Append(Medicaler.TargetMark(seer, target));

                    Mark.Append(Totocalcio.TargetMark(seer, target));

                    Mark.Append(Lawyer.TargetMark(seer, target));

                if (seer.Is(CustomRoles.Puppeteer))
                    {
                        if (seer.Is(CustomRoles.Puppeteer) &&
                        Main.PuppeteerList.ContainsValue(seer.PlayerId) &&
                        Main.PuppeteerList.ContainsKey(target.PlayerId))
                            Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                    }
                    if (Sniper.IsEnable && target.AmOwner)
                    {
                        //銃声が聞こえるかチェック
                        Mark.Append(Sniper.GetShotNotify(target.PlayerId));

                    }
                    if (seer.Is(CustomRoles.EvilTracker)) Mark.Append(EvilTracker.GetTargetMark(seer, target));
                    //タスクが終わりそうなSnitchがいるとき、インポスター/キル可能なニュートラルに警告が表示される
                    Mark.Append(Snitch.GetWarningArrow(seer, target));

                    if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SuperStar), "★"));

                //迷你船员
                if (target.Is(CustomRoles.NiceMini) && NiceMini.EveryoneCanKnowMini.GetBool())
                    Mark.Append(Utils.ColorString(Color.yellow, NiceMini.Age != 18 ? $"({NiceMini.Age})" : ""));
                //迷你船员
                if (target.Is(CustomRoles.EvilMini) && NiceMini.EveryoneCanKnowMini.GetBool())
                    Mark.Append(Utils.ColorString(Color.yellow, NiceMini.Age != 18 ? $"({NiceMini.Age})" : ""));

                if (target.Is(CustomRoles.Captain))
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Captain), " ★Cap★ "));
             
                   if (target.Is(CustomRoles.QL) && Options.EveryOneKnowQL.GetBool())
                    Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.QL), "♛"));

                if (target.Is(CustomRoles.Hotpotato))
                    Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Hotpotato), "●"));

                    if (BallLightning.IsGhost(target))
                        Mark.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.BallLightning), "■"));

                    //ハートマークを付ける(会議中MOD視点)
                    if (__instance.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Lovers))
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                    }
                    else if (__instance.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Data.IsDead)
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                    }
                    else if (__instance.Is(CustomRoles.Ntr) || PlayerControl.LocalPlayer.Is(CustomRoles.Ntr))
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                    }
                    else if (__instance == PlayerControl.LocalPlayer && CustomRolesHelper.RoleExist(CustomRoles.Ntr))
                    {
                        Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                    }
                else if (__instance.Is(CustomRoles.CrushLovers) && PlayerControl.LocalPlayer.Is(CustomRoles.CrushLovers))
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.CrushLovers)}>♡</color>");
                }
                else if (__instance.Is(CustomRoles.CrushLovers) && PlayerControl.LocalPlayer.Data.IsDead)
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.CrushLovers)}>♡</color>");
                }
                else if (__instance.Is(CustomRoles.CupidLovers) && PlayerControl.LocalPlayer.Is(CustomRoles.CupidLovers))
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.CupidLovers)}>♡</color>");
                }
                else if (__instance.Is(CustomRoles.CupidLovers) && PlayerControl.LocalPlayer.Data.IsDead)
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.CupidLovers)}>♡</color>");
                }
                //矢印オプションありならタスクが終わったスニッチはインポスター/キル可能なニュートラルの方角がわかる
                Suffix.Append(Snitch.GetSnitchArrow(seer, target));

                    Suffix.Append(BountyHunter.GetTargetArrow(seer, target));

                    Suffix.Append(Mortician.GetTargetArrow(seer, target));

                    Suffix.Append(Vulture.GetTargetArrow(seer, target));

                    Suffix.Append(EvilTracker.GetTargetArrow(seer, target));

               //     Suffix.Append(NiceTracker.GetTargetArrow(seer, target));

                    if (GameStates.IsInTask && seer.Is(CustomRoles.AntiAdminer))
                    {
                        AntiAdminer.FixedUpdate();
                        if (target.AmOwner)
                        {
                            if (AntiAdminer.IsAdminWatch) Suffix.Append("★" + GetString("AntiAdminerAD"));
                            if (AntiAdminer.IsVitalWatch) Suffix.Append("★" + GetString("AntiAdminerVI"));
                            if (AntiAdminer.IsDoorLogWatch) Suffix.Append("★" + GetString("AntiAdminerDL"));
                            if (AntiAdminer.IsCameraWatch) Suffix.Append("★" + GetString("AntiAdminerCA"));
                        }
                    }

                    if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                        Suffix.Append(SoloKombatManager.GetDisplayHealth(target));

                    /*if(main.AmDebugger.Value && main.BlockKilling.TryGetValue(target.PlayerId, out var isBlocked)) {
                        Mark = isBlocked ? "(true)" : "(false)";
                    }*/
                if ((Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Concealer.IsHidding)
                        RealName = $"<size=0>{RealName}</size> ";

                    string DeathReason = seer.Data.IsDead && seer.KnowDeathReason(target) ? $"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doctor), Utils.GetVitalText(target.PlayerId))})" : "";
                    //Mark・Suffixの適用
                    target.cosmetics.nameText.text = $"{RealName}{DeathReason}{Mark}";

                    if (Suffix.ToString() != "")
                    {
                        //名前が2行になると役職テキストを上にずらす必要がある
                        RoleText.transform.SetLocalY(0.35f);
                        target.cosmetics.nameText.text += "\r\n" + Suffix.ToString();

                    }
                    else
                    {
                        //役職テキストの座標を初期値に戻す
                        RoleText.transform.SetLocalY(0.2f);
                    }
                }
                else
                {
                    //役職テキストの座標を初期値に戻す
                    RoleText.transform.SetLocalY(0.2f);
                }
            }
        }
    public static void ImposotorSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
    {
        if (Options.CanDefector.GetBool())
        {
            int DefectorInt = 0;
            int optImpNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors);
            int ImIntDead = 0;
            int AlivePlayerRemain = 0;
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                AlivePlayerRemain++;
            }
            foreach (var player in Main.AllPlayerControls)
            {
                if (player.Data.IsDead && player.PlayerId == deathId && player.GetCustomRole().IsImpostor() && !Main.KillImpostor.Contains(player.PlayerId) && !player.Is(CustomRoles.Defector))
                {
                    Main.KillImpostor.Add(player.PlayerId);
                    ImIntDead++;
                   
                    foreach (var partnerPlayer in Main.AllPlayerControls)
                    {
                    if (ImIntDead != optImpNum) continue;
                    if (AlivePlayerRemain < Options.DefectorRemain.GetInt()) continue;
                    if (partnerPlayer.GetCustomRole().IsCrewmate() && partnerPlayer.CanUseKillButton() && DefectorInt == 0)
                    {
                        Logger.Info($"qwqwqwq", "Jackal");
                        DefectorInt++;
                        partnerPlayer.RpcSetCustomRole(CustomRoles.Defector);
                        partnerPlayer.ResetKillCooldown();
                        partnerPlayer.SetKillCooldown();
                        partnerPlayer.RpcGuardAndKill(partnerPlayer);
                        if (isExiled)
                        {
                            if (now) partnerPlayer?.RpcExileV2();
                            partnerPlayer.RpcSetCustomRole(CustomRoles.Defector);
                            partnerPlayer.ResetKillCooldown();
                            partnerPlayer.SetKillCooldown();
                            partnerPlayer.RpcGuardAndKill(partnerPlayer);
                            DefectorInt++;
                        }
                        else
                            DefectorInt++;
                        partnerPlayer.RpcSetCustomRole(CustomRoles.Defector);
                        partnerPlayer.ResetKillCooldown();
                        partnerPlayer.SetKillCooldown();
                        partnerPlayer.RpcGuardAndKill(partnerPlayer);
                    }
                }
                }
                else
                {
                }
            }
        }
    }
    public static void JackalSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
    {
        if (CustomRoles.Jackal.IsEnable() && Main.isjackalDead == false && Jackal.SidekickCanBeJackal.GetBool())
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (!player.Data.IsDead && player.PlayerId != deathId && !player.Is(CustomRoles.Jackal)) continue;
                if (!player.Is(CustomRoles.Jackal)) continue;
                Main.isjackalDead = true;
                foreach (var partnerPlayer in Main.AllPlayerControls)
                {
                    if (player.PlayerId != deathId &&  partnerPlayer.Is(CustomRoles.Sidekick))
                    {
                        partnerPlayer.RpcSetCustomRole(CustomRoles.Jackal);
                        Jackal.Add(partnerPlayer.PlayerId);
                        Jackal.Add(partnerPlayer.PlayerId);
                        partnerPlayer.ResetKillCooldown();
                        partnerPlayer.SetKillCooldown();
                        partnerPlayer.RpcGuardAndKill(partnerPlayer);
                        if (isExiled)
                        {
                            if (now) partnerPlayer?.RpcExileV2();
                            partnerPlayer.RpcSetCustomRole(CustomRoles.Jackal);
                            Jackal.Add(partnerPlayer.PlayerId);
                            Jackal.Add(partnerPlayer.PlayerId);
                            partnerPlayer.ResetKillCooldown();
                            partnerPlayer.SetKillCooldown();
                            partnerPlayer.RpcGuardAndKill(partnerPlayer);
                        }
                        else
                            partnerPlayer.RpcSetCustomRole(CustomRoles.Jackal);
                        Jackal.Add(partnerPlayer.PlayerId);
                        Jackal.Add(partnerPlayer.PlayerId);
                        partnerPlayer.ResetKillCooldown();
                        partnerPlayer.SetKillCooldown();
                        partnerPlayer.RpcGuardAndKill(partnerPlayer);
                    }
                }
            }
        }
    }//*/
    /* public static void SheriffSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
     {
         if (CustomRoles.Sheriff.IsEnable() && Main.isSheriffDead == false && Deputy.DeputyCanBeSheriff.GetBool())
         {
             foreach (var player in Main.AllPlayerControls)
             {
                 if (!player.Is(CustomRoles.Sheriff)) continue;
                 if (!player.Data.IsDead && player.PlayerId != deathId && !player.Is(CustomRoles.Sheriff)) continue;

                 Main.isSheriffDead = true;

                         foreach (var partnerPlayer in Main.AllPlayerControls)
                         {
                             if (player.PlayerId != deathId && partnerPlayer.Is(CustomRoles.Deputy))
                             {
                                 partnerPlayer.RpcSetCustomRole(CustomRoles.Sheriff);
                                 Sheriff.Add(partnerPlayer.PlayerId);
                                 Sheriff.Add(partnerPlayer.PlayerId);
                                 partnerPlayer.ResetKillCooldown();
                                 partnerPlayer.SetKillCooldown();
                                 partnerPlayer.RpcGuardAndKill(partnerPlayer);

                                 if (isExiled)
                                 {
                                     if (now) partnerPlayer?.RpcExileV2();
                                     partnerPlayer.RpcSetCustomRole(CustomRoles.Sheriff);
                                     Sheriff.Add(partnerPlayer.PlayerId);
                                     Sheriff.Add(partnerPlayer.PlayerId);
                                     partnerPlayer.ResetKillCooldown();
                                     partnerPlayer.SetKillCooldown();
                                     partnerPlayer.RpcGuardAndKill(partnerPlayer);
                                 }
                                 else
                                     partnerPlayer.RpcSetCustomRole(CustomRoles.Sheriff);
                                 Sheriff.Add(partnerPlayer.PlayerId);
                                 Sheriff.Add(partnerPlayer.PlayerId);
                                 partnerPlayer.ResetKillCooldown();
                                 partnerPlayer.SetKillCooldown();
                                 partnerPlayer.RpcGuardAndKill(partnerPlayer);
                             }
                         }


             }
         }
     }//*/


    public static void CaptainSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
    {
        if (CustomRoles.Captain.IsEnable() && Main.isseniormanagementDead == false)
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (!player.Data.IsDead && player.PlayerId != deathId && !player.Is(CustomRoles.Captain)) continue;
                if (!player.Is(CustomRoles.Captain)) continue;

                    Main.isseniormanagementDead = true;
                foreach (var partnerPlayer in Main.AllPlayerControls)
                {
                    if (player.PlayerId == partnerPlayer.PlayerId) continue;//恋人id=其对偶id
                    if (partnerPlayer.PlayerId != deathId && partnerPlayer.Is(CustomRoles.seniormanagement) && !partnerPlayer.Data.IsDead)
                    {
                        Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.Ownerless;
                        if (isExiled)
                        {
                            if (now) partnerPlayer?.RpcExileV2();
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Ownerless, partnerPlayer.PlayerId);
                        }
                        else
                        {
                            partnerPlayer.RpcMurderPlayerV3(partnerPlayer);
                        }
                    }
                }
            }
        }
    }
    public static void HunterSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
    {
        if (CustomRoles.Hunter.IsEnable() && Main.isseniormanagementDead == false)
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (!player.Data.IsDead && player.PlayerId != deathId && !player.Is(CustomRoles.Hunter)) continue;
                if (!player.Is(CustomRoles.Hunter)) continue;

                Main.isHunterDead = true;
                foreach (var partnerPlayer in Main.HunterTarget)
                {
                    if (player.PlayerId == partnerPlayer.PlayerId) continue;//恋人id=其对偶id
                    if (partnerPlayer.PlayerId != deathId && !partnerPlayer.Data.IsDead)
                    {
                        Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                        if (isExiled)
                        {
                            if (now) partnerPlayer?.RpcExileV2();
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, partnerPlayer.PlayerId);
                        }
                        else
                        {
                            partnerPlayer.RpcMurderPlayerV3(partnerPlayer);
                        }
                    }
                }
            }
        }
    }
    //FIXME: 役職クラス化のタイミングで、このメソッドは移動予定
    public static void LoversSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
        {
            if (Options.LoverSuicide.GetBool() && CustomRoles.Lovers.IsEnable() && Main.isLoversDead == false)
            {
                foreach (var loversPlayer in Main.LoversPlayers)
                {
                    if (!loversPlayer.Data.IsDead && loversPlayer.PlayerId != deathId) continue;//
                    Main.isLoversDead = true;//恋人死亡
                    foreach (var partnerPlayer in Main.LoversPlayers)//在恋人中循环搜索对偶玩家
                    {
                        //本人ならスキップ
                        if (loversPlayer.PlayerId == partnerPlayer.PlayerId) continue;//恋人id=其对偶id

                        //残った恋人を全て殺す(2人以上可)
                        //如果恋人死亡
                        if (partnerPlayer.PlayerId != deathId && !partnerPlayer.Data.IsDead) //检测恋人死亡(对偶玩家id=死亡id，则对偶玩家死亡）
                        {
                            Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.FollowingSuicide; //设置对偶玩家死因为殉情
                            if (isExiled) //如果被放逐
                            {
                                if (now) partnerPlayer?.RpcExileV2(); //放逐击杀恋人
                                CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.FollowingSuicide, partnerPlayer.PlayerId); //设置死因为殉情
                            }
                            else //其他情况
                                partnerPlayer.RpcMurderPlayerV3(partnerPlayer); //杀死恋人
                        }
                    }
                }
            }
        }

    public static void CrushLoversSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
    {
        if (Options.CrushLoverSuicide.GetBool() && CustomRoles.Crush.IsEnable() && Main.isCrushLoversDead == false)
        {
            foreach (var loversPlayer in Main.CrushLoversPlayers)
            {
                if (!loversPlayer.Data.IsDead && loversPlayer.PlayerId != deathId) continue;//
                Main.isLoversDead = true;//恋人死亡
                foreach (var partnerPlayer in Main.CrushLoversPlayers)//在恋人中循环搜索对偶玩家
                {
                    //本人ならスキップ
                    if (loversPlayer.PlayerId == partnerPlayer.PlayerId) continue;//恋人id=其对偶id

                    //残った恋人を全て殺す(2人以上可)
                    //如果恋人死亡
                    if (partnerPlayer.PlayerId != deathId && !partnerPlayer.Data.IsDead) //检测恋人死亡(对偶玩家id=死亡id，则对偶玩家死亡）
                    {
                        Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.FollowingSuicide; //设置对偶玩家死因为殉情
                        if (isExiled) //如果被放逐
                        {
                            if (now) partnerPlayer?.RpcExileV2(); //放逐击杀恋人
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.FollowingSuicide, partnerPlayer.PlayerId); //设置死因为殉情
                        }
                        else //其他情况
                            partnerPlayer.RpcMurderPlayerV3(partnerPlayer); //杀死恋人
                    }
                }
            }
        }
    }
    public static void CupidLoversSuicide(byte deathId = 0x7f, bool isExiled = false, bool now = false)
    {
        if (Options.CupidLoverSuicide.GetBool() && CustomRoles.Cupid.IsEnable() && Main.isCupidLoversDead == false)
        {
            foreach (var loversPlayer in Main.CupidLoversPlayers)
            {
                if (!loversPlayer.Data.IsDead && loversPlayer.PlayerId != deathId) continue;//
                Main.isLoversDead = true;//恋人死亡
                foreach (var partnerPlayer in Main.CupidLoversPlayers)//在恋人中循环搜索对偶玩家
                {
                    //本人ならスキップ
                    if (loversPlayer.PlayerId == partnerPlayer.PlayerId) continue;//恋人id=其对偶id

                    //残った恋人を全て殺す(2人以上可)
                    //如果恋人死亡
                    if (partnerPlayer.PlayerId != deathId && !partnerPlayer.Data.IsDead) //检测恋人死亡(对偶玩家id=死亡id，则对偶玩家死亡）
                    {
                        Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.FollowingSuicide; //设置对偶玩家死因为殉情
                        if (isExiled) //如果被放逐
                        {
                            if (now) partnerPlayer?.RpcExileV2(); //放逐击杀恋人
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.FollowingSuicide, partnerPlayer.PlayerId); //设置死因为殉情
                        }
                        else //其他情况
                            partnerPlayer.RpcMurderPlayerV3(partnerPlayer); //杀死恋人
                    }
                }
                foreach (var cupid in Main.AllPlayerControls)
                {
                    if (loversPlayer.PlayerId == cupid.PlayerId) continue;//恋人id=其对偶id
                    if (cupid.PlayerId != deathId && cupid.Is(CustomRoles.Cupid) && !cupid.Data.IsDead)
                    {
                        Main.PlayerStates[cupid.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                        if (isExiled)
                        {
                            if (now) cupid?.RpcExileV2();
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, cupid.PlayerId);
                        }
                        else
                        {
                            cupid.RpcMurderPlayerV3(cupid);
                        }
                    }
                }
            }
        }
    }
    
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    class PlayerStartPatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            var roleText = UnityEngine.Object.Instantiate(__instance.cosmetics.nameText);
            roleText.transform.SetParent(__instance.cosmetics.nameText.transform);
            roleText.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            roleText.fontSize -= 1.2f;
            roleText.text = "RoleText";
            roleText.gameObject.name = "RoleText";
            roleText.enabled = false;
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetColor))]
    class SetColorPatch
    {
        public static bool IsAntiGlitchDisabled = false;
        public static bool Prefix(PlayerControl __instance, int bodyColor)
        {
        //色変更バグ対策
        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost || __instance.CurrentOutfit.ColorId == bodyColor || IsAntiGlitchDisabled) return true;
            return true;
        }
    }

[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
class EnterVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {

        Witch.OnEnterVent(pc);

        if (pc.Is(CustomRoles.Mayor))
        {
            if (Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.MayorNumOfUseButton.GetInt())
            {
                pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc?.ReportDeadBody(null);
            }
        }

        if (pc.Is(CustomRoles.Paranoia))
        {
            if (Main.ParaUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.ParanoiaNumOfUseButton.GetInt())
            {
                Main.ParaUsedButtonCount[pc.PlayerId] += 1;
                if (AmongUsClient.Instance.AmHost)
                {
                    new LateTask(() =>
                    {
                        Utils.SendMessage(GetString("SkillUsedLeft") + (Options.ParanoiaNumOfUseButton.GetInt() - Main.ParaUsedButtonCount[pc.PlayerId]).ToString(), pc.PlayerId);
                    }, 4.0f, "Skill Remain Message");
                }
                pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc?.NoCheckStartMeeting(pc?.Data);
            }
        }

        if (pc.Is(CustomRoles.Mario))
        {
            Main.MarioVentCount.TryAdd(pc.PlayerId, 0);
            Main.MarioVentCount[pc.PlayerId]++;
            Utils.NotifyRoles(pc);
            if (pc.AmOwner)
            {
                if (Main.MarioVentCount[pc.PlayerId] % 5 == 0) CustomSoundsManager.Play("MarioCoin");
                else CustomSoundsManager.Play("MarioJump");
            }
            if (AmongUsClient.Instance.AmHost && Main.MarioVentCount[pc.PlayerId] >= Options.MarioVentNumWin.GetInt())
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario); //马里奥这个多动症赢了
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            }
        }

        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return;

        Logger.Info($"{pc.GetNameWithRole()} EnterVent: {__instance.Id}", "EnterVent");

        Main.LastEnteredVent.Remove(pc.PlayerId);
        Main.LastEnteredVent.Add(pc.PlayerId, __instance);
        Main.LastEnteredVentLocation.Remove(pc.PlayerId);
        Main.LastEnteredVentLocation.Add(pc.PlayerId, pc.GetTruePosition());

        Swooper.OnEnterVent(pc, __instance);

        if (pc.Is(CustomRoles.Veteran))
        {
            Main.VeteranInProtect.Remove(pc.PlayerId);
            Main.VeteranInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            Main.VeteranNumOfUsed[pc.PlayerId]--;
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("Gunload");
            pc.Notify(GetString("VeteranOnGuard"), Options.VeteranSkillDuration.GetFloat());
        }
        if (pc.Is(CustomRoles.Grenadier))
        {
            if (pc.Is(CustomRoles.Madmate))
            {
                Main.MadGrenadierBlinding.Remove(pc.PlayerId);
                Main.MadGrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => !x.GetCustomRole().IsImpostorTeam() && !x.Is(CustomRoles.Madmate)).Do(x => x.RPCPlayCustomSound("FlashBang"));
            }
            else
            {
                Main.GrenadierBlinding.Remove(pc.PlayerId);
                Main.GrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => x.GetCustomRole().IsImpostor() || (x.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool())).Do(x => x.RPCPlayCustomSound("FlashBang"));
            }
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("FlashBang");
            pc.Notify(GetString("GrenadierSkillInUse"), Options.GrenadierSkillDuration.GetFloat());
            Utils.MarkEveryoneDirtySettings();
        }
        if (pc.Is(CustomRoles.DovesOfNeace))
        {
            Main.DovesOfNeaceNumOfUsed[pc.PlayerId]--;
            pc.RpcGuardAndKill(pc);
            Main.AllAlivePlayerControls.Where(x =>
            pc.Is(CustomRoles.Madmate) ?
            (x.CanUseKillButton() && x.GetCustomRole().IsCrewmate()) :
            (x.CanUseKillButton())
            ).Do(x =>
            {
                x.RPCPlayCustomSound("Dove");
                x.ResetKillCooldown();
                x.SetKillCooldownV2();
                x.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DovesOfNeace), GetString("DovesOfNeaceSkillNotify")));
            });
            pc.RPCPlayCustomSound("Dove");
            pc.Notify(string.Format(GetString("DovesOfNeaceOnGuard"), Main.DovesOfNeaceNumOfUsed[pc.PlayerId]));
        }
        if (pc.Is(CustomRoles.Rudepeople))
        {
            Main.RudepeopleInProtect.Remove(pc.PlayerId);
            Main.RudepeopleInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            Main.RudepeopleNumOfUsed[pc.PlayerId]--;
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("RNM");
            pc.Notify(GetString("RudepeopleOnGuard"), Options.RudepeopleSkillDuration.GetFloat());
        }
        //扰乱技能启动！
        if (pc.Is(CustomRoles.sabcat))
        {
            Main.sabcatInProtect.Remove(pc.PlayerId);
            Main.sabcatInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            //Main.sabcatNumOfUsed[pc.PlayerId]--;
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("RNM");
            pc.Notify(GetString("sabcatOnGuard"), Options.sabcatCooldown.GetFloat());
        }
        if (pc.Is(CustomRoles.UnluckyEggs))
        {
            var Ue = IRandom.Instance;
            if (Ue.Next(0, 100) < Options.UnluckyEggsKIllUnluckyEggs.GetInt())
            {
                pc.RpcMurderPlayerV3(pc);
            }
        }
        /*
        if (pc.Is(CustomRoles.aTimeStops))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("THEWORLD");
            Main.TimeStopsInProtect.Remove(pc.PlayerId);
            Main.TimeStopsInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.RPCPlayCustomSound("THEWORLD");
            pc.Notify(GetString("TimeStopsOnGuard"), Options.TimeStopsSkillDuration.GetFloat());
            foreach (var player in Main.AllPlayerControls)
            {
                if (pc == player) continue;
                if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId)) continue;
                NameNotifyManager.Notify(player, Utils.ColorString(Utils.GetRoleColor(CustomRoles.TimeStops), GetString("ForTimeStops")));
                var tmpSpeed1 = Main.AllPlayerSpeed[player.PlayerId];
                Main.TimeStopsstop.Add(player.PlayerId);
                Main.AllPlayerSpeed[player.PlayerId] = Main.MinSpeed;
                player.MarkDirtySettings();
                new LateTask(() =>
                {
                    Main.AllPlayerSpeed[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId] - Main.MinSpeed + tmpSpeed1;
                    player.MarkDirtySettings();
                    Main.TimeStopsstop.Remove(player.PlayerId);
                    RPC.PlaySoundRPC(player.PlayerId, Sounds.TaskComplete);
                }, Options.TimeStopsSkillDuration.GetFloat(), "Trapper BlockMove");
            }
        }
        */
        if (pc.Is(CustomRoles.TimeMaster))
        {
            Main.TimeMasterInProtect.Remove(pc.PlayerId);
            Main.TimeMasterInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            pc.Notify(GetString("TimeMasterOnGuard"), Options.TimeMasterSkillDuration.GetFloat());
                foreach (var player in Main.AllPlayerControls)
                {
                    if (Main.TimeMasterbacktrack.ContainsKey(player.PlayerId))
                    {
                         var position = Main.TimeMasterbacktrack[player.PlayerId];
                    Utils.TP(player.NetTransform, position);
                    Main.TimeMasterbacktrack.Remove(player.PlayerId);
                    }
                    else
                    {
                        Main.TimeMasterbacktrack.Add(player.PlayerId, player.GetTruePosition());
                    }
                }
        }

        if (pc.Is(CustomRoles.DemolitionManiac))
        {
            foreach (var player in Main.AllPlayerControls)
            {
                if (Main.DemolitionManiacKill.Contains(player.PlayerId))
                {
                    Main.DemolitionManiacKill.Remove(player.PlayerId);
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    player.SetRealKiller(pc);
                    player.RpcMurderPlayerV3(player);
                }
            }
        }
        if (pc.Is(CustomRoles.GlennQuagmire))
        {
            List<PlayerControl> list = Main.AllAlivePlayerControls.Where(x => x.PlayerId != pc.PlayerId).ToList();
            if (list.Count < 1)
            {
                Logger.Info($"Q哥没有目标", "GlennQuagmire");
            }
            else
            {
                list = list.OrderBy(x => Vector2.Distance(pc.GetTruePosition(), x.GetTruePosition())).ToList();
                var target = list[0];
               if (target.GetCustomRole().IsImpostor())
                {
                    pc.RPCPlayCustomSound("giggity");
                    target.SetRealKiller(pc);
                    target.RpcCheckAndMurder(target);
                    pc.RpcGuardAndKill();
                        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Creation;
                }
               else
                {
                    if (Main.ForSourcePlague.Contains(pc.PlayerId))
                    {
                        new LateTask(() =>
                        {
                            Utils.TP(pc.NetTransform, target.GetTruePosition());
                            Main.ForSourcePlague.Add(target.PlayerId);
                            Utils.NotifyRoles();
                        }, 2.5f, ("NotRight!"));
                    }
                    new LateTask(() =>
                    {
                        Utils.TP(pc.NetTransform, target.GetTruePosition());
                        Utils.NotifyRoles();
                    }, 2.5f, ("NotRight!"));

                }
            }
        }
        if (pc.Is(CustomRoles.SoulSeeker))
        {
            if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
            foreach (var player in Main.AllPlayerControls)
            {
                if (Pelican.IsEaten(player.PlayerId) && Options.SoulSeekerCanSeeEat.GetBool())
                {
                    Main.SoulSeekerCanEat[pc.PlayerId]++;
                }
                if (!player.IsAlive())
                {
                    Main.SoulSeekerDead[pc.PlayerId]++;
                    if (player.CanUseKillButton())
                    {
                        Main.SoulSeekerCanKill[pc.PlayerId]++;
                    }
                    else
                    {
                        Main.SoulSeekerNotCanKill[pc.PlayerId]++;
                    } 
                }
            }
            pc.Notify(string.Format(GetString("SoulSeekerOffGuard"), Main.SoulSeekerDead[pc.PlayerId], Main.SoulSeekerNotCanKill[pc.PlayerId], Main.SoulSeekerCanKill[pc.PlayerId], Main.SoulSeekerCanEat[pc.PlayerId]));
            if (Main.SoulSeekerCanEat[pc.PlayerId] > 0)
            {
                Main.SoulSeekerCanEat[pc.PlayerId] = 0;
            }
            if (Main.SoulSeekerCanKill[pc.PlayerId] > 0)
            {
                Main.SoulSeekerCanKill[pc.PlayerId] = 0;
            }
            if (Main.SoulSeekerNotCanKill[pc.PlayerId] > 0)
            {
                Main.SoulSeekerNotCanKill[pc.PlayerId] = 0;
            }
            if (Main.SoulSeekerDead[pc.PlayerId] > 0)
            {
                Main.SoulSeekerDead[pc.PlayerId] = 0;
            }
        }
        if (pc.Is(CustomRoles.King) && Main.KingCanpc.Contains(pc.PlayerId))
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.King); 
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            foreach (var player in Main.AllPlayerControls)
            {
                if (Main.ForKing.Contains(player.PlayerId))
                {
                    CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                }
            }
        
        }
        if (Main.ForTasksDestinyChooser.Contains(pc.PlayerId))
        {
            pc.RpcMurderPlayerV3(pc);
        }
    }
        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
    class CoEnterVentPatch
    {
        public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id)
        {
            if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return true;

            Logger.Info($"{__instance.myPlayer.GetNameWithRole()} CoEnterVent: {id}", "CoEnterVent");

            if (AmongUsClient.Instance.IsGameStarted &&
                __instance.myPlayer.IsDouseDone())
            {
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (pc != __instance.myPlayer)
                    {
                        //生存者は焼殺
                        pc.SetRealKiller(__instance.myPlayer);
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Torched;
                        pc.RpcMurderPlayerV3(pc);
                        Main.PlayerStates[pc.PlayerId].SetDead();
                    }
                }
                foreach (var pc in Main.AllPlayerControls) pc.KillFlash();
                CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist); //焼殺で勝利した人も勝利させる
                CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
                return true;
            }

            if (AmongUsClient.Instance.IsGameStarted && __instance.myPlayer.IsDrawDone())//完成拉拢任务的玩家跳管后
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Revolutionist);//革命者胜利
                Utils.GetDrawPlayerCount(__instance.myPlayer.PlayerId, out var x);
                CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
                foreach (var apc in x) CustomWinnerHolder.WinnerIds.Add(apc.PlayerId);//胜利玩家
                return true;
            }

            //处理弹出管道的阻塞
            if ((__instance.myPlayer.Data.Role.Role != RoleTypes.Engineer && //不是工程师
            !__instance.myPlayer.CanUseImpostorVentButton()) || //不能使用内鬼的跳管按钮
            (__instance.myPlayer.Is(CustomRoles.sabcat)) ||
            (__instance.myPlayer.Is(CustomRoles.Mayor) && Main.MayorUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count) && count >= Options.MayorNumOfUseButton.GetInt()) ||
            (__instance.myPlayer.Is(CustomRoles.Paranoia) && Main.ParaUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count2) && count2 >= Options.ParanoiaNumOfUseButton.GetInt()) ||
            (__instance.myPlayer.Is(CustomRoles.Veteran) && Main.VeteranNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count3) && count3 < 1) ||
            (__instance.myPlayer.Is(CustomRoles.DovesOfNeace) && Main.DovesOfNeaceNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count4) && count4 < 1) ||
             (__instance.myPlayer.Is(CustomRoles.Rudepeople) && Main.RudepeopleNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count5) && count5 < 1)
            )
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, -1);
                writer.WritePacked(127);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                new LateTask(() =>
                {
                    int clientId = __instance.myPlayer.GetClientId();
                    MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, clientId);
                    writer2.Write(id);
                    AmongUsClient.Instance.FinishRpcImmediately(writer2);
                }, 0.5f, "Fix DesyncImpostor Stuck");

                if (__instance.myPlayer.Is(CustomRoles.DovesOfNeace)) __instance.myPlayer.Notify(GetString("DovesOfNeaceMaxUsage"));
                if (__instance.myPlayer.Is(CustomRoles.Veteran)) __instance.myPlayer.Notify(GetString("VeteranMaxUsage"));
                if (__instance.myPlayer.Is(CustomRoles.Rudepeople)) __instance.myPlayer.Notify(GetString("RudepeopleMaxUsage"));

                return false;
            }

            if (__instance.myPlayer.Is(CustomRoles.Swooper))
                Swooper.OnCoEnterVent(__instance, id);

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetName))]
    class SetNamePatch
    {
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] string name)
        {
        }
    }
    [HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
    class GameDataCompleteTaskPatch
    {
        public static void Postfix(PlayerControl pc)
        {
            Logger.Info($"TaskComplete:{pc.GetNameWithRole()}", "CompleteTask");
            Main.PlayerStates[pc.PlayerId].UpdateTask(pc);
            Utils.NotifyRoles();
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
    class PlayerControlCompleteTaskPatch
    {
        public static bool Prefix(PlayerControl __instance)
        {
            var player = __instance;

            if (Workhorse.OnCompleteTask(player)) //タスク勝利をキャンセル
                return false;

            //来自资本主义的任务
            if (Main.CapitalismAddTask.ContainsKey(player.PlayerId))
            {
                var taskState = player.GetPlayerTaskState();
                taskState.AllTasksCount += Main.CapitalismAddTask[player.PlayerId];
                Main.CapitalismAddTask.Remove(player.PlayerId);
                taskState.CompletedTasksCount++;
                GameData.Instance.RpcSetTasks(player.PlayerId, new byte[0]); //タスクを再配布
                player.SyncSettings();
                Utils.NotifyRoles(player);
                return false;
            }

            return true;
        }
        public static void Postfix(PlayerControl __instance)
        {
            var pc = __instance;
            Snitch.OnCompleteTask(pc);

            var isTaskFinish = pc.GetPlayerTaskState().IsTaskFinished;
            if (isTaskFinish && pc.Is(CustomRoles.Snitch) && pc.Is(CustomRoles.Madmate))
            {
                foreach (var impostor in Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)))
                    NameColorManager.Add(impostor.PlayerId, pc.PlayerId, "#ff1919");
                Utils.NotifyRoles(SpecifySeer: pc);
            }
            if ((isTaskFinish &&
                pc.GetCustomRole() is CustomRoles.Doctor or CustomRoles.Sunnyboy) ||
                pc.GetCustomRole() is CustomRoles.SpeedBooster)
            {
                //ライターもしくはスピードブースターもしくはドクターがいる試合のみタスク終了時にCustomSyncAllSettingsを実行する
                Utils.MarkEveryoneDirtySettings();
            }
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
    class PlayerControlProtectPlayerPatch
    {
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            Logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()}", "ProtectPlayer");
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RemoveProtection))]
    class PlayerControlRemoveProtectionPatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            Logger.Info($"{__instance.GetNameWithRole()}", "RemoveProtection");
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    class PlayerControlSetRolePatch
    {
        public static bool Prefix(PlayerControl __instance, ref RoleTypes roleType)
        {
            var target = __instance;
            var targetName = __instance.GetNameWithRole();
            Logger.Info($"{targetName} =>{roleType}", "PlayerControl.RpcSetRole");
            if (!ShipStatus.Instance.enabled) return true;
            if (roleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost)
            {
                var targetIsKiller = target.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(target.PlayerId);
                var ghostRoles = new Dictionary<PlayerControl, RoleTypes>();
                foreach (var seer in Main.AllPlayerControls)
                {
                    var self = seer.PlayerId == target.PlayerId;
                    var seerIsKiller = seer.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(seer.PlayerId);
                    if ((self && targetIsKiller) || (!seerIsKiller && target.Is(CustomRoleTypes.Impostor)))
                    {
                        ghostRoles[seer] = RoleTypes.ImpostorGhost;
                    }
                    else
                    {
                        ghostRoles[seer] = RoleTypes.CrewmateGhost;
                    }
                }
                if (ghostRoles.All(kvp => kvp.Value == RoleTypes.CrewmateGhost))
                {
                    roleType = RoleTypes.CrewmateGhost;
                }
                else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.ImpostorGhost))
                {
                    roleType = RoleTypes.ImpostorGhost;
                }
                else
                {
                    foreach ((var seer, var role) in ghostRoles)
                    {
                        Logger.Info($"Desync {targetName} =>{role} for{seer.GetNameWithRole()}", "PlayerControl.RpcSetRole");
                        target.RpcSetRoleDesync(role, seer.GetClientId());
                    }
                    return false;
                }
            }
            return true;
        }
    }
}
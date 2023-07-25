using HarmonyLib;
using Hazel;
using MS.Internal.Xml.XPath;
using Sentry;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.RandomSpawn;
using static UnityEngine.GraphicsBuffer;

namespace TOHE;

internal static class HotPotatoManager
{
    public static int RoundTime = new();
    public static int BoomTimes = new();
    public static int HotPotatoMax = new();
    public static int  IsAliveHot = new();
    public static int IsAliveCold = new();
    public static int  ListHotpotato = new();
    //设置

    public static OptionItem HotQuan;//热土豆数量
    public static OptionItem Boom; //爆炸时间;Remaining time of explosion
    public static OptionItem TD;//总时长;Totalduration


    public static void SetupCustomOption()
    {
        TD = IntegerOptionItem.Create(76_235_004, "KB_GameTime", new(30, 300, 5), 180, TabGroup.GameSettings, false)
           .SetGameMode(CustomGameMode.HotPotato)
           .SetColor(new Color32(245, 82, 82, byte.MaxValue))
           .SetValueFormat(OptionFormat.Seconds)
           .SetHeader(true);

        Boom = IntegerOptionItem.Create(62_293_008, "BoomTime", new(10, 60, 5), 15, TabGroup.GameSettings, false)
           .SetGameMode(CustomGameMode.HotPotato)
           .SetColor(new Color32(245, 82, 82, byte.MaxValue))
           .SetValueFormat(OptionFormat.Seconds);

        HotQuan = IntegerOptionItem.Create(147_45_47, "HotQuan", new(1, 4, 1), 2, TabGroup.GameSettings, false)
           .SetGameMode(CustomGameMode.HotPotato)
           .SetColor(new Color32(245, 82, 82, byte.MaxValue))
           .SetValueFormat(OptionFormat.Poeple);
           
    }
    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.HotPotato) return;
        RoundTime = TD.GetInt() + 8;
        BoomTimes = Boom.GetInt() + 8;
        HotPotatoMax = HotQuan.GetInt();
        IsAliveHot = 0;
        IsAliveCold = 0;
        ListHotpotato = 0;
    }
    public static string GetHudText()
    {
        return string.Format(Translator.GetString("HotPotatoTimeRemain"), RoundTime.ToString(), BoomTimes.ToString());
    }

    public static void OnPlayerAttack(PlayerControl killer, PlayerControl target)
    {
        if (!killer.Is(CustomRoles.Hotpotato)) return;
        if (target.Is(CustomRoles.Hotpotato)) return;           
                target.RpcSetCustomRole(CustomRoles.Hotpotato);
                killer.RpcSetCustomRole(CustomRoles.Coldpotato);
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
        killer.SetKillCooldownV2(target: target, forceAnime: true);
        Utils.NotifyRoles(killer);
                Utils.NotifyRoles(target);
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        private static long LastFixedUpdate = new();
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.HotPotato) return;

            if (AmongUsClient.Instance.AmHost)
            {
                foreach (var player in Main.AllPlayerControls)
                {
                    if (!player.IsAlive()) continue;
                    //一些巴拉巴拉的东西
                    var playerList = Main.AllAlivePlayerControls.ToList();
                    if (player.Is(CustomRoles.Hotpotato) && player.IsAlive() && !Main.ForHotPotato.Contains(player.PlayerId))
                    {
                        Main.ForHotPotato.Add(player.PlayerId);
                        IsAliveHot++;
                    }
                    if (playerList.Count == 1 && player.Is(CustomRoles.Coldpotato) && player.IsAlive())
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CP);
                        CustomWinnerHolder.WinnerIds.Add(player.PlayerId);
                    }
                    if (player.Is(CustomRoles.Coldpotato) && player.IsAlive() && !Main.ForHotCold.Contains(player.PlayerId))
                    {
                        Main.ForHotCold.Add(player.PlayerId);
                        IsAliveCold++;
                    }
                    //如果热土豆数量超出了则反向变为冷土豆
                    while (IsAliveCold <= HotPotatoMax)
                    {
                        foreach (var BoomPlayer in Main.AllPlayerControls)
                        {
                            if (BoomPlayer.Is(CustomRoles.Hotpotato))
                            {
                                BoomPlayer.RpcSetCustomRole(CustomRoles.Coldpotato);
                                IsAliveCold++;
                                IsAliveHot--;
                                continue;
                            }
                        }
                    }
                    if (IsAliveCold > HotPotatoMax)
                    {
                        //土豆数量检测
                        if (playerList.Count <= 4 && HotPotatoMax >= 2)
                        {
                            HotPotatoMax = 1;
                        }
                        if (playerList.Count <= 6 && playerList.Count > 4 && HotPotatoMax >= 3)
                        {
                            HotPotatoMax = 2;
                        }
                        if (playerList.Count <= 8 && playerList.Count > 6 && HotPotatoMax == 4)
                        {
                            HotPotatoMax = 3;
                        }
                    }
                        
                        //爆炸时间为0时
                        if (BoomTimes <= 0)
                        {
                            foreach (var pc in Main.AllPlayerControls)
                            {
                                 if (pc.Is(CustomRoles.Hotpotato) && pc.IsAlive() &&       Main.ForHotPotato.Contains(pc.PlayerId))
                                {
                                pc.RpcCheckAndMurder(pc);
                                IsAliveHot--;
                                Main.ForHotPotato.Remove(pc.PlayerId);
                                     }                                
                            }
                        new LateTask(() =>
                        {
                            var pcList = Main.AllAlivePlayerControls.ToList();
                            while (ListHotpotato == HotPotatoMax)
                            {
                                if (ListHotpotato >= HotPotatoMax)
                                {
                                    break;
                                }
                                if (pcList.Count > 0)
                                {
                                    var randomIndex = IRandom.Instance.Next(0, pcList.Count);
                                    var randomPlayer = pcList[randomIndex];
                                    randomPlayer.RpcSetCustomRole(CustomRoles.Hotpotato);
                                    IsAliveHot++;
                                    ListHotpotato++;
                                    IsAliveCold--;
                                    pcList.RemoveAt(randomIndex);
                                    Main.ForHotCold.Remove(randomPlayer.PlayerId);
                                    Main.ForHotPotato.Add(randomPlayer.PlayerId);
                                }
                            }
                        }, 2f);
                        BoomTimes = Boom.GetInt();
                        }                                           
                    
                }

                if (LastFixedUpdate == Utils.GetTimeStamp()) return;
                LastFixedUpdate = Utils.GetTimeStamp();

                // 减少全局倒计时
                RoundTime--;
                //减少爆炸冷却
                BoomTimes--;
            }
        }
    }
    public static Dictionary<byte, (string, long)> NameNotify = new();
    public static void SendRPCSyncNameNotify(PlayerControl pc)
    {
        if (pc.AmOwner || !pc.IsModClient()) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncHPNameNotify, SendOption.Reliable, pc.GetClientId());
        if (NameNotify.ContainsKey(pc.PlayerId))
            writer.Write(NameNotify[pc.PlayerId].Item1);
        else writer.Write("");
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPCSyncNameNotify(MessageReader reader)
    {
        var name = reader.ReadString();
        NameNotify.Remove(PlayerControl.LocalPlayer.PlayerId);
        if (name != null && name != "")
            NameNotify.Add(PlayerControl.LocalPlayer.PlayerId, (name, 0));
    }
    public static void AddNameNotify(PlayerControl pc, string text, int time = 5)
    {
        NameNotify.Remove(pc.PlayerId);
        NameNotify.Add(pc.PlayerId, (text, Utils.GetTimeStamp() + time));
        SendRPCSyncNameNotify(pc);
        SendRPCSyncHPPlayer(pc.PlayerId);
        Utils.NotifyRoles(pc);
    }
    private static void SendRPCSyncHPPlayer(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncKBPlayer, SendOption.Reliable, -1);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPCSyncHPPlayer(MessageReader reader)
    {
    }
}


using Hazel;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;

namespace TOHE;

public static class NameColorManager
{
    public static string ApplyNameColorData(this string name, PlayerControl seer, PlayerControl target, bool isMeeting)
    {
        if (!AmongUsClient.Instance.IsGameStarted) return name;

        if (!TryGetData(seer, target, out var colorCode))
        {
            if (KnowTargetRoleColor(seer, target, isMeeting, out var color))
                colorCode = color == "" ? target.GetRoleColorCode() : color;
        }
        string openTag = "", closeTag = "";
        if (colorCode != "")
        {
            if (!colorCode.StartsWith('#'))
                colorCode = "#" + colorCode;
            openTag = $"<color={colorCode}>";
            closeTag = "</color>";
        }
        return openTag + name + closeTag;
    }
    private static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, bool isMeeting, out string color)
    {
        color = "";

        // 内鬼叛徒互认
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor)) color = (target.Is(CustomRoles.Egoist) && Options.ImpEgoistVisibalToAllies.GetBool() && seer != target) ? Main.roleColors[CustomRoles.Egoist] : Main.roleColors[CustomRoles.Impostor];
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) color = Main.roleColors[CustomRoles.Impostor];
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool()) color = Main.roleColors[CustomRoles.Madmate];
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) color = Main.roleColors[CustomRoles.Madmate];
        if (seer.Is(CustomRoles.Gangster) && target.Is(CustomRoles.Madmate)) color = Main.roleColors[CustomRoles.Madmate];

        //魅魔小弟互认
        if (seer.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Succubus)) color = Main.roleColors[CustomRoles.Succubus];
        if (seer.Is(CustomRoles.Succubus) && target.Is(CustomRoles.Charmed)) color = Main.roleColors[CustomRoles.Charmed];
        if (seer.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Charmed) && Succubus.TargetKnowOtherTarget.GetBool()) color = Main.roleColors[CustomRoles.Charmed];

        //跟班互认
        if (seer.Is(CustomRoles.Attendant) && target.Is(CustomRoles.Jackal)) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Attendant)) color = Main.roleColors[CustomRoles.Attendant];
        if (seer.Is(CustomRoles.Attendant) && target.Is(CustomRoles.Attendant)) color = Main.roleColors[CustomRoles.Attendant];
        if (seer.Is(CustomRoles.Whoops) && target.Is(CustomRoles.Jackal)) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Whoops)) color = Main.roleColors[CustomRoles.Whoops];
        if (seer.Is(CustomRoles.Whoops) && target.Is(CustomRoles.Whoops)) color = Main.roleColors[CustomRoles.Whoops]; 
        if (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal)) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick)) color = Main.roleColors[CustomRoles.Sidekick];
        if (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick)) color = Main.roleColors[CustomRoles.Sidekick];

        // 警长捕快互认
        if (seer.Is(CustomRoles.Deputy) && target.Is(CustomRoles.Sheriff) && Deputy.DeputyKnowWhosSheriff.GetBool()) color = Main.roleColors[CustomRoles.Sheriff];
        if (seer.Is(CustomRoles.Sheriff) && target.Is(CustomRoles.Deputy) && Deputy.SheriffKnowWhosDeputy.GetBool()) color = Main.roleColors[CustomRoles.Deputy];

        //舰长干部互认
        if (seer.Is(CustomRoles.Solicited) && target.Is(CustomRoles.Captain)) color = Main.roleColors[CustomRoles.Captain];
        if (seer.Is(CustomRoles.Captain) && target.Is(CustomRoles.Solicited)) color = Main.roleColors[CustomRoles.Solicited];
        if (seer.Is(CustomRoles.Solicited) && target.Is(CustomRoles.Solicited) && Captain.TargetKnowOtherTarget.GetBool()) color = Main.roleColors[CustomRoles.Solicited];


        //抓捕互认
        if(target.Is(CustomRoles.captor)) color = Main.roleColors[CustomRoles.captor];
        // 卧底内鬼禁止互认
        //if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Undercover)) color = "#FFFFFF";
        //if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Undercover)) color ="#FFFFFF";
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Undercover)) color = "#FFFFFF";
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Undercover)) color = "#FFFFFF";
        //if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Undercover)) color = "#FFFFFF";
        //if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Undercover)) color = "#FFFFFF";
        //if (seer.Is(CustomRoles.Gangster) && target.Is(CustomRoles.Undercover)) color = "#FFFFFF";

        
        
                //累死Slok互认
                //红猫
                if (seer.Is(CustomRoles.ImpostorSchrodingerCat) && target.Is(CustomRoleTypes.Impostor) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.Impostor];
                if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.ImpostorSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.ImpostorSchrodingerCat];
                if (seer.Is(CustomRoles.ImpostorSchrodingerCat) && target.Is(CustomRoles.ImpostorSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.ImpostorSchrodingerCat];
                //蓝猫
                if (seer.Is(CustomRoles.Attendant) && target.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.JSchrodingerCat];
                if (seer.Is(CustomRoles.JSchrodingerCat) && target.Is(CustomRoles.Attendant) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.Attendant];
                if (seer.Is(CustomRoles.Whoops) && target.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.JSchrodingerCat];
                if (seer.Is(CustomRoles.JSchrodingerCat) && target.Is(CustomRoles.Whoops) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.Whoops];
                if (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.JSchrodingerCat];
                if (seer.Is(CustomRoles.JSchrodingerCat) && target.Is(CustomRoles.Sidekick) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.Sidekick];
                if (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.JSchrodingerCat];
                if (seer.Is(CustomRoles.JSchrodingerCat) && target.Is(CustomRoles.Jackal) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.Jackal];
                if (seer.Is(CustomRoles.JSchrodingerCat) && target.Is(CustomRoles.JSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.JSchrodingerCat];
                //潜藏
                if (seer.Is(CustomRoles.DHSchrodingerCat) && target.Is(CustomRoles.DarkHide) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.DarkHide];
                if (seer.Is(CustomRoles.DarkHide) && target.Is(CustomRoles.DHSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.DHSchrodingerCat];
                if (seer.Is(CustomRoles.DHSchrodingerCat) && target.Is(CustomRoles.DHSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.DHSchrodingerCat];
                //神
                if (seer.Is(CustomRoles.PGSchrodingerCat) && target.Is(CustomRoles.PlaguesGod) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.PlaguesGod];
                if (seer.Is(CustomRoles.PlaguesGod) && target.Is(CustomRoles.PGSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.PGSchrodingerCat];
                if (seer.Is(CustomRoles.PGSchrodingerCat) && target.Is(CustomRoles.PGSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.PGSchrodingerCat];
                //骑士
                if (seer.Is(CustomRoles.BloodSchrodingerCat) && target.Is(CustomRoles.BloodKnight) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.BloodKnight];
                if (seer.Is(CustomRoles.BloodKnight) && target.Is(CustomRoles.BloodSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.BloodSchrodingerCat];
                if (seer.Is(CustomRoles.BloodSchrodingerCat) && target.Is(CustomRoles.BloodSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.BloodSchrodingerCat];
                //银狼
                if (seer.Is(CustomRoles.YLSchrodingerCat) && target.Is(CustomRoles.YinLang) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.YinLang];
                if (seer.Is(CustomRoles.YinLang) && target.Is(CustomRoles.YLSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.YLSchrodingerCat];
                if (seer.Is(CustomRoles.YLSchrodingerCat) && target.Is(CustomRoles.YLSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.YLSchrodingerCat];
                //玩家
                if (seer.Is(CustomRoles.GamerSchrodingerCat) && target.Is(CustomRoles.Gamer) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.Gamer];
                if (seer.Is(CustomRoles.Gamer) && target.Is(CustomRoles.GamerSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.GamerSchrodingerCat];
                if (seer.Is(CustomRoles.GamerSchrodingerCat) && target.Is(CustomRoles.GamerSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.GamerSchrodingerCat];
                //雇佣
                if (seer.Is(CustomRoles.OKSchrodingerCat) && target.Is(CustomRoles.OpportunistKiller) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.OKSchrodingerCat];
                if (seer.Is(CustomRoles.OpportunistKiller) && target.Is(CustomRoles.OKSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.OKSchrodingerCat];
                if (seer.Is(CustomRoles.OKSchrodingerCat) && target.Is(CustomRoles.OKSchrodingerCat) && Options.CanKnowKiller.GetBool()) color = Main.roleColors[CustomRoles.OKSchrodingerCat];
                //恋人名称颜色优化
                if (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers)) color = Main.roleColors[CustomRoles.Lovers];
                if (seer.Is(CustomRoles.CrushLovers) && target.Is(CustomRoles.CrushLovers)) color = Main.roleColors[CustomRoles.CrushLovers];
                if (seer.Is(CustomRoles.CupidLovers) && target.Is(CustomRoles.CupidLovers)) color = Main.roleColors[CustomRoles.CupidLovers];
                if (seer.Is(CustomRoles.CupidLovers) && target.Is(CustomRoles.Cupid) && Options.CanKnowCupid.GetBool()) color = Main.roleColors[CustomRoles.CupidLovers];

        if (!seer.Is(CustomRoles.NiceMini) && target.Is(CustomRoles.NiceMini)) color = Main.roleColors[CustomRoles.NiceMini];
        if (!seer.Is(CustomRoles.EvilMini) && target.Is(CustomRoles.EvilMini)) color = Main.roleColors[CustomRoles.NiceMini];

        if (seer.Is(CustomRoles.NiceMini) && target.Is(CustomRoles.NiceMini)) color = Main.roleColors[CustomRoles.NiceMini];
        if (seer.Is(CustomRoles.EvilMini) && target.Is(CustomRoles.EvilMini)) color = Main.roleColors[CustomRoles.EvilMini];
        //可查看驱逐人职业
        //if (Options.Voteerroles.GetBool() && Options.CEMode.GetInt() == 2 && seer.GetRealName() == "") Mare.KnowTargetRoleColor(target, isMeeting);

        //if (Options.Voteerroles.GetBool()) color = Main.roleColors[CustomRoles.Attendant];

        if (color != "") return true;
        else return seer == target
            || (Main.GodMode.Value && seer.AmOwner)
            || target.Is(CustomRoles.GM)
            || seer.Is(CustomRoles.GM)
            || seer.Is(CustomRoles.God)
            || (target.Is(CustomRoles.QL) && Options.EveryOneKnowQL.GetBool())
            || (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
            || (target.Is(CustomRoles.Captain)
            || Mare.KnowTargetRoleColor(target, isMeeting));
    }
    public static bool TryGetData(PlayerControl seer, PlayerControl target, out string colorCode)
    {
        colorCode = "";
        var state = Main.PlayerStates[seer.PlayerId];
        if (!state.TargetColorData.TryGetValue(target.PlayerId, out var value)) return false;
        colorCode = value;
        return true;
    }

    public static void Add(byte seerId, byte targetId, string colorCode = "")
    {
        if (colorCode == "")
        {
            var target = Utils.GetPlayerById(targetId);
            if (target == null) return;
            colorCode = target.GetRoleColorCode();
        }

        var state = Main.PlayerStates[seerId];
        if (state.TargetColorData.TryGetValue(targetId, out var value) && colorCode == value) return;
        state.TargetColorData.Add(targetId, colorCode);

        SendRPC(seerId, targetId, colorCode);
    }
    public static void Remove(byte seerId, byte targetId)
    {
        var state = Main.PlayerStates[seerId];
        if (!state.TargetColorData.ContainsKey(targetId)) return;
        state.TargetColorData.Remove(targetId);

        SendRPC(seerId, targetId);
    }
    public static void RemoveAll(byte seerId)
    {
        Main.PlayerStates[seerId].TargetColorData.Clear();

        SendRPC(seerId);
    }
    private static void SendRPC(byte seerId, byte targetId = byte.MaxValue, string colorCode = "")
    {
        if (Options.CurrentGameMode != CustomGameMode.TOEX || Options.AllModMode.GetBool()) if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNameColorData, SendOption.Reliable, -1);
        writer.Write(seerId);
        writer.Write(targetId);
        writer.Write(colorCode);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte seerId = reader.ReadByte();
        byte targetId = reader.ReadByte();
        string colorCode = reader.ReadString();

        if (targetId == byte.MaxValue)
            RemoveAll(seerId);
        else if (colorCode == "")
            Remove(seerId, targetId);
        else
            Add(seerId, targetId, colorCode);
    }
}
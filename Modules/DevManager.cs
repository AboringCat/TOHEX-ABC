using System.Collections.Generic;
using System.Linq;

namespace TOHE;

public class DevUser
{
    public string Code { get; set; }
    public string Color { get; set; }
    public string Tag { get; set; }
    public bool IsUp { get; set; }
    public bool IsDev { get; set; }
    public bool DeBug { get; set; }
    public string UpName { get; set; }
    public DevUser(string code = "", string color = "null", string tag = "null", bool isUp = false, bool isDev = false, bool deBug = false, string upName = "未认证用户")
    {
        Code = code;
        Color = color;
        Tag = tag;
        IsUp = isUp;
        IsDev = isDev;
        DeBug = deBug;
        UpName = upName;
    }
    public bool HasTag() => Tag != "null";
    public string GetTag() => Color == "null" ? $"<size=1.7>{Tag}</size>\r\n" : $"<color={Color}><size=1.7>{(Tag == "#Dev" ? Translator.GetString("Developer") : Tag)}</size></color>\r\n";
}

public static class DevManager
{
    public static DevUser DefaultDevUser = new();
    public static List<DevUser> DevUser = new();
    public static void Init()
    {
        //if (!Main.Devtx.Value)
        //{
            // Dev
            DevUser.Add(new(code: "canneddrum#2370", color: "#fffcbe", tag: "开发者：喜", isUp: true, isDev: true, deBug: true, upName: "开发者：喜"));//TOHEXi开发者-喜
            DevUser.Add(new(code: "cleardress#6310", color: "#FFEFD5", tag: "Dev:A Boring Cat", isUp: true, isDev: true, deBug: true, upName: "A Boring Cat"));//TOHEXi开发者-喜
            DevUser.Add(new(code: "storeroan#0331", color: "#FF0066", tag: "开发者：Night_瓜", isUp: true, isDev: true, deBug: true, upName: "开发者：Night_瓜"));//Night_瓜
            DevUser.Add(new(code: "actorour#0029", color: "#ffc0cb", tag: "咔皮呆", isUp: true, isDev: true, deBug: true, upName: "KARPED1EM"));
            DevUser.Add(new(code: "pinklaze#1776", color: "#30548e", tag: "NCSIMON", isUp: true, isDev: true, deBug: true, upName: "NCSIMON"));
            DevUser.Add(new(code: "keepchirpy#6354", color: "#1FF3C6", tag: "Переводчик", isUp: false, isDev: true, deBug: false, upName: null)); //Tommy-XL
            DevUser.Add(new(code: "taskunsold#2701", color: "null", tag: "<color=#426798>Tem</color><color=#f6e509>mie</color>", isUp: false, isDev: true, deBug: false, upName: null)); //Tem
            DevUser.Add(new(code: "timedapper#9496", color: "#48FFFF", tag: "#阿能", isUp: false, isDev: true, deBug: false, upName: null)); //阿龍
            DevUser.Add(new(code: "sofaagile#3120", color: "null", tag: "null", isUp: false, isDev: true, deBug: true, upName: null)); //天寸
            DevUser.Add(new(code: "keyscreech#2151", color: "null", tag: "<color=#D3A4FF>美術</color><color=#5A5AAD>NotKomi</color>", isUp: false, isDev: true, deBug: false, upName: null)); //Endrmen40409

            // Up
            DevUser.Add(new(code: "truantwarm#9165", color: "#FFA500", tag: "帅比暮", isUp: true, isDev: false, deBug: true, upName: "萧暮不姓萧"));
            DevUser.Add(new(code: "drilldinky#1386", color: "#66CC00", tag: "河豚", isUp: true, isDev: false, deBug: false, upName: "爱玩AU的河豚"));
            DevUser.Add(new(code: "farardour#6818", color: "#CC33FF", tag: "提米", isUp: true, isDev: false, deBug: false, upName: "-提米SaMa-"));
            DevUser.Add(new(code: "vealused#8192", color: "#00CCFF", tag: "XY", isUp: true, isDev: false, deBug: false, upName: "lag丶xy"));
            DevUser.Add(new(code: "storyeager#0815", color: "#666633", tag: "航娜丽莎", isUp: true, isDev: false, deBug: false, upName: "航娜丽莎"));
            DevUser.Add(new(code: "versegame#3885", color: "#FF9900", tag: "柴唔cw", isUp: true, isDev: false, deBug: false, upName: "柴唔cw"));
            DevUser.Add(new(code: "closegrub#6217", color: "#fffcbe", tag: "警长不会玩", isUp: true, isDev: false, deBug: false, upName: "警长不会玩"));
            DevUser.Add(new(code: "frownnatty#7935", color: "#333333", tag: "鬼灵", isUp: true, isDev: false, deBug: false, upName: "鬼灵official"));
            DevUser.Add(new(code: "veryscarf#5368", color: "#333333", tag: "小武同学102", isUp: true, isDev: false, deBug: false, upName: "小武同学102"));
            DevUser.Add(new(code: "sparklybee#0275", color: "#FFFFFF", tag: "红包捏", isUp: true, isDev: false, deBug: false, upName: "--红包SaMa--"));
            DevUser.Add(new(code: "endingyon#3175", color: "#00FFFF", tag: "游侠", isUp: true, isDev: false, deBug: false, upName: "游侠开摆"));
            DevUser.Add(new(code: "storkfey#3570", color: "#00FF00", tag: "Calypso", isUp: true, isDev: false, deBug: false, upName: "Calypso"));
            DevUser.Add(new(code: "fellowsand#1003", color: "#00FF00", tag: "C-Faust", isUp: true, isDev: false, deBug: false, upName: "C-Faust"));
            DevUser.Add(new(code: "jetsafe#8512", color: "#FF9900", tag: "食好人", isUp: true, isDev: false, deBug: false, upName: "Hoream是好人"));
            DevUser.Add(new(code: "primether#5348", color: "#FF9900", tag: "Works!", isUp: true, isDev: false, deBug: false, upName: "AnonWorks"));
            DevUser.Add(new(code: "spoonkey#0792", color: "#00FF00", tag: "你在康什么", isUp: true, isDev: false, deBug: false, upName: "没好康的"));
            DevUser.Add(new(code: "beakedmire#6099", color: "#0000FF", tag: "茄-au", isUp: true, isDev: false, deBug: false, upName: "茄-au"));
        DevUser.Add(new(code: "wiseyellow#9613", color: "#0000FF", tag: "徐枣子", isUp: true, isDev: false, deBug: true, upName: "徐枣子"));
        DevUser.Add(new(code: "curlypace#0454", color: "#0033FF", tag: "天选之子", isUp: true, isDev: false, deBug: false, upName: "全员天选之子"));

            DevUser.Add(new(code: "neatnet#5851", color: "#FFFF00", tag: "The 200IQ guy", isUp: true, isDev: false, deBug: false, upName: "The 200IQ guy"));
            DevUser.Add(new(code: "contenthue#0404", color: "#FFFF00", tag: "The 200IQ guy", isUp: true, isDev: false, deBug: false, upName: "The 200IQ guy"));
        //DevUser.Add(new(code: "storeroan#0331", color: "#FF0066", tag: "Night_瓜", isUp: true, isDev: false, deBug: false, upName: "Night_瓜"));
        DevUser.Add(new(code: "heavyclod#2286", color: "#FFFF00", tag: " 小叨", isUp: true, isDev: false, deBug: true, upName: "小叨院长"));
        DevUser.Add(new(code: "firmine#0232", color: "#00FFFF", tag: "YH", isUp: true, isDev: false, deBug: false, upName: "YH永恒_"));
            DevUser.Add(new(code: "teamelder#5856", color: "#0089FF", tag: "全然无信Slok", isUp: true, isDev: true, deBug: true, upName: "Slok7565"));

            DevUser.Add(new(code: "radarright#2509", color: "#666666", tag: "我是谁？", isUp: false, isDev: false, deBug: true, upName: null));
        DevUser.Add(new(code: "creditpast#2783", color: "#666633", tag: "秋天的叶子", isUp: true, isDev: false, deBug: false, upName: "落叶不是屑"));
        DevUser.Add(new(code: "packedodds#5837", color: "#9999CC", tag: "网络管家", isUp: true, isDev: false, deBug: false, upName: "火狼网络"));
        DevUser.Add(new(code: "freepit#9942", color: "#33CCFF", tag: "C12H22O11", isUp: true, isDev: false, deBug: true, upName: "古明地白糖"));
        DevUser.Add(new(code: "actorcoy#7049", color: "#FF9900", tag: "橄榄汁", isUp: true, isDev: false, deBug: false, upName: "橄哥"));
        DevUser.Add(new(code: "elfalpha#5174", color: "#FF66CC", tag: "心语【喵子】", isUp: true, isDev: false, deBug: false, upName: "心语【喵子】"));
        DevUser.Add(new(code: "gridunable#5279", color: "#00BFFF", tag: "毒液", isUp: true, isDev: false, deBug: true, upName: "毒液"));
        DevUser.Add(new(code: "foodocular#9170", color: "#f6f657", tag: "帅哥闪亮登场", isUp: true, isDev: false, deBug: true, upName: "天空一声巨响"));
        //sb喜纯纯sb 纯脑残


        // Sponsor
        DevUser.Add(new(code: "recentduct#6068", color: "#FF00FF", tag: "高冷男模法师", isUp: false, isDev: false, deBug: true, upName: null));
            //DevUser.Add(new(code: "canneddrum#2370", color: "#fffcbe", tag: "我是喜唉awa", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "dovefitted#5329", color: "#1379bf", tag: "不要首刀我", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "luckylogo#7352", color: "#f30000", tag: "林@林", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "axefitful#8788", color: "#8e8171", tag: "寄才是真理", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "raftzonal#8893", color: "#8e8171", tag: "寄才是真理", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "twainrobin#8089", color: "#0000FF", tag: "啊哈修maker", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "mallcasual#6075", color: "#f89ccb", tag: "波奇酱", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "beamelfin#9478", color: "#6495ED", tag: "Amaster-1111", isUp: true, isDev: false, deBug: false, upName: "Amaster-1111"));
            DevUser.Add(new(code: "lordcosy#8966", color: "#FFD6EC", tag: "HostTOHE", isUp: false, isDev: false, deBug: false, upName: null)); //K
            DevUser.Add(new(code: "honestsofa#2870", color: "#D381D9", tag: "Sylveon", isUp: false, isDev: false, deBug: false, upName: null)); //SolarFlare
            DevUser.Add(new(code: "caseeast#7194", color: "#1c2451", tag: "disc.gg/maul", isUp: false, isDev: false, deBug: false, upName: null)); //laikrai
            DevUser.Add(new(code: "numbpile#3037", color: "#0000FF", tag: "叨叨叨", isUp: true, isDev: false, deBug: true, upName: "你敢叨我吗")); //叨叨
            DevUser.Add(new(code: "openswish#5297", color: "#00FFFF", tag: "羊蛋", isUp: true, isDev: false, deBug: true, upName: "杨带善人")); //羊蛋
            DevUser.Add(new(code: "offlinenil#5191", color: "#FFD700", tag: "无敌的只因", isUp: false, isDev: false, deBug: false, upName: null));
            DevUser.Add(new(code: "moonfar#7458", color: "#6495ED", tag: "Hi Adeli!!", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "dancewaxy#7730", color: "#0000FF", tag: "下批", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "tarrycask#3426", color: "#FFC0CB", tag: "B战AUAKA72", isUp: true, isDev: false, deBug: true, upName: "AUAKA72"));
            DevUser.Add(new(code: "wavymole#1886", color: "#800000", tag: "白嫖人", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "tabscaly#1622", color: "#DC143C", tag: "一块慕头", isUp: true, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "reputedgym#3551", color: "#F08080", tag: "黎川Aa", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "dotshort#5936", color: "#FF0000", tag: "想做UP", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "sinebovine#3307", color: "#4169E1", tag: "舰长", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "puppyfirm#0501", color: "#00BFFF", tag: "Fracsail今天也鸽", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "herbalogee#3060", color: "#FFFF00", tag: "乐", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "faxsoppy#3468", color: "#FF8000", tag: "一只可爱的助人为乐", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "dietwise#5145", color: "#0000FF", tag: "非常稳定", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "hingesmoky#1216", color: "#00FFFF", tag: "屑哦豁", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "loserown#0064", color: "#1E90FF", tag: "鸡你太美", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "midsnore#9133", color: "#E1FFFF", tag: "公认摆烂人", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "sweetbrown#5512", color: "#FF0000", tag: "四月与和纱", isUp: true, isDev: false, deBug: true, upName: "四月与和纱"));
            DevUser.Add(new(code: "pinsmolten#5349", color: "#0000FF", tag: "运气好得离谱awa", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "awashocean#9601", color: "#7B68EE", tag: "浅语QAQ", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "expirygrey#2519", color: "#FFB6C1", tag: "难q受(屑中之屑)", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "fibrekey#0994", color: "#F08080", tag: "心律失常患者", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "latedrama#3172", color: "#D2B48C", tag: "牛蛙种子.cpp未编译", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "spacesated#8096", color: "#FFF00", tag: "一个一个生草之人", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "wholespace#0544", color: "#FF9966", tag: "只因块", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "bioniczoo#7263", color: "#33CCFF", tag: "Galaxy", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "coinfluent#8964", color: "#FFCC00", tag: "发光的老灯儿", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "tenthchief#3380", color: "#333333", tag: "我真不是小黑子啊", isUp: false, isDev: false, deBug: true, upName: null));
            DevUser.Add(new(code: "nomadmoral#3920", color: "#3366ff", tag: "Korimizu", isUp: false, isDev: false, deBug: true, upName: null));
        DevUser.Add(new(code: "curlypace#0454", color: "#3366FF", tag: "工具人", isUp: true, isDev: false, deBug: true, upName: "TOH坏土"));
        DevUser.Add(new(code: "formeldest#7067", color: "#339900", tag: "拟 人 绿", isUp: false, isDev: false, deBug: true, upName: null));
        DevUser.Add(new(code: "wasppurply#7905", color: "#0000FF", tag: "嘉轩", isUp: true, isDev: false, deBug: true, upName: "嘉轩游戏解说"));
        DevUser.Add(new(code: "clayclayey#1311", color: "#00ffcc", tag: "我是bug者", isUp: true, isDev: true, deBug: true, upName: "Zeyan"));
        DevUser.Add(new(code: "nowtazure#8524", color: null, tag: "ShuiHei", isUp: true, isDev: false, deBug: true, upName: "ShuiHei"));
        DevUser.Add(new(code: "smartlatex#9383", color: "#FFA500", tag: "不是橙色", isUp: false, isDev: false, deBug: true, upName: null));
        DevUser.Add(new(code: "lionshrewd#4559", color: "#FFC0CB", tag: "可爱的偷吃", isUp: false, isDev: false, deBug: true, upName: null));
        DevUser.Add(new(code: "bothkeep#2634", color: null, tag: "<color=#4B0082>斯</color><color=#8A2BE2>卡</color><color=#9370DB>拉</color><color=#7B68EE>姆</color><color=#6A5ACD>齐</color>", isUp: true, isDev: false, deBug: true, upName: "流浪者"));
            DevUser.Add(new(code: "nowtazure#8254", color: "#FFFF00", tag: "鸽子区up主", isUp: false, isDev: false, deBug: true, upName: null));
             DevUser.Add(new(code: "stillchest#8146", color: "#FFCCFF", tag: "SctUyrK\r\n", isUp: true, isDev: false, deBug: true, upName: "SctUyrK"));
        DevUser.Add(new(code: "preysocial#6794", color: "#1379bf", tag: "宝石668", isUp: false, isDev: false, deBug: false, upName: null));
        //}
    }
    public static bool IsDevUser(this string code) => DevUser.Any(x => x.Code == code);
    public static DevUser GetDevUser(this string code) => code.IsDevUser() ? DevUser.Find(x => x.Code == code) : DefaultDevUser;
}

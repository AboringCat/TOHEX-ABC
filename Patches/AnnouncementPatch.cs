using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using AmongUs.Data.Player;
using Assets.InnerNet;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TOHE;

// ##https://github.com/Yumenopai/TownOfHost_Y
//来源：TOHY（谢谢！）
public class ModNews
{
    public int Number;
    public int BeforeNumber;
    public string Title;
    public string SubTitle;
    public string ShortTitle;
    public string Text;
    public string Date;

    public Announcement ToAnnouncement()
    {
        var result = new Announcement
        {
            Number = Number,
            Title = Title,
            SubTitle = SubTitle,
            ShortTitle = ShortTitle,
            Text = Text,
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Date = Date,
            Id = "ModNews"
        };

        return result;
    }
}
[HarmonyPatch]
public class ModNewsHistory
{
    public static List<ModNews> AllModNews = new();
    public static void Init()
    {
        {
            //当你创建新的公告时，你不能删除旧的公告
            /*
            *每当你创建新公告时，请按照以下格式：
            {
            var news = new ModNews
                {
                Number = xxx,         //（编号）
                Title = "",           //（标题）
                SubTitle = "",        //（副标题）
                ShortTitle = "",      //（短标题）
                Test = ""            //（文本）
            *换行时注意要
                + "\n"        
                ......
                + "\n",
            *注意，文本在最后一个+""以后添加逗号    
                Date = "",            //（日期）
                };
            AllModNews.Add(news);
            }
            *别忘了标点！！！！！
            *更新以后别忘了删var(var news)
            */
            var news = new ModNews
            {
                Number = 100001,
                Title = "TownOfHostEditedXi v1.5.1",
                SubTitle = "★★★★泰裤辣★★★★",
                ShortTitle = "★TOHEX v1.5.1",
                Text =
                "<size=150%>欢迎来到<color=fffcbe>TOHEX<color></size>"
                + "\n-----------------------------"
                + "\n修bug咯~~~"
                + "\n\n\n## 对应官方版本"
                + "\n- 基于TOH版本 v4.1.2"
                + "\n- 基于TOHE版本 v2.3.55"
                + "\n- 适配Among Us版本 v2023.7.11及以上版本"
                +"\n\n## 修复"
                + "\n- 修复了刀人职业的问题"
                + "\n- 修复了迷你船员被招募的问题"
                + "\n- 修复了还有一些问题"
                 + "\n\n## 新增职业"
                + "\n- 船员阵营：猎人"
                + "\n- 附加职业：VIP"
                 + "\n- 附加职业：通讯兵",
                Date = "2023-7-26T00:00:00Z"
            };
            AllModNews.Add(news);
        }
        { 
              var news = new ModNews
            {
                Number = 100000,
                Title = "TownOfHostEditedXi v1.5.0",
                SubTitle = "★★★★泰裤辣★★★★",
                ShortTitle = "★TOHEX v1.5.0",
                Text = 
                "-----------------------------" 
                + "\n芜湖，又是大版本~~~"
                + "\n\n\n## 对应官方版本"
                + "\n- 基于TOH版本 v4.1.2"
                + "\n- 基于TOHE版本 v2.3.55"
                + "\n- 适配Among Us版本 v2023.7.11及以上版本"
                + "\n\n## 新增职业"
                + "\n- 内鬼阵营：背叛者"
                + "\n- 中立阵营：丘比特"
                + "\n- 内鬼阵营：强袭者"
                 + "\n- 内鬼阵营：拾荒者"
                   + "\n- 内鬼阵营：压榨者"
                  + "\n- 船员/内鬼阵营：好/坏迷你船员"
                   + "\n- 赌怪附加：专业刺客"
                + "\n\n## 新增"
                + "\n- 暗恋者设置：恋人共生死&&可以得知对方职业"
                + "\n- 薛定谔的猫设置：可以得知凶手阵营玩家"
                 + "\n- 更改图标"
                 + "\n- 新的忍者！"
                + "\n\n## 修复"
                + "\n- 修复了薛定谔的猫不能胜利的问题"
                + "\n- 修复了暗恋者恋人不会共死的问题"
                + "\n- 修复了清廉之官不生成的问题"
                + "\n- 修复了更新日志无法打开的问题",
                Date = "2023-7-25T00:00:00Z"
            };
            AllModNews.Add(news);
        }
    }

    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)), HarmonyPrefix]
    public static bool SetModAnnouncements(PlayerAnnouncementData __instance, [HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {
        if (AllModNews.Count < 1)
        {
            Init();
            AllModNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });
        }

        List<Announcement> FinalAllNews = new();
        AllModNews.Do(n => FinalAllNews.Add(n.ToAnnouncement()));
        foreach (var news in aRange)
        {
            if (!AllModNews.Any(x => x.Number == news.Number))
                FinalAllNews.Add(news);
        }
        FinalAllNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });

        aRange = new(FinalAllNews.Count);
        for (int i = 0; i < FinalAllNews.Count; i++)
            aRange[i] = FinalAllNews[i];

        return true;
    }
}
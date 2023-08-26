using MS.Internal.Xml.XPath;
using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using Sentry;
using System.Linq;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.RandomSpawn;
using static UnityEngine.GraphicsBuffer;
using static TOHE.Options;

namespace TOHE;

public static class MrDesperate
{
    private static readonly int Id = 945920551;
    public static List<byte> playerIdList = new();

    public static OptionItem MrDesperateKillMeCooldown;
    public static OverrideTasksData MrDesperateTasks;
    public static int KillTime = new();


    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.MrDesperate);
        MrDesperateKillMeCooldown = FloatOptionItem.Create(Id + 10, "MrDesperateKillMeCooldown", new(0f, 180f, 2.5f), 65f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.MrDesperate])
            .SetValueFormat(OptionFormat.Seconds);
        SpecialAgentTasks = OverrideTasksData.Create(Id + 114, TabGroup.NeutralRoles, CustomRoles.MrDesperate);
    }
    public static void Init()
    {
        playerIdList = new();
        KillTime = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }
    public static string GetMrDesperate(byte playerId) => Utils.ColorString(Color.yellow, KillTime != 0 ? $"({KillTime})" : "");
}

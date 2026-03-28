using System.Collections.Generic;
using HarmonyLib;

namespace ValuableList;

[HarmonyPatch(typeof(ValuableObject))]
internal static class ValuableObjectPatch
{
    internal static readonly List<ValuableObject> Tracked = new();

    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    private static void TrackInstance(ValuableObject __instance)
    {
        Tracked.Add(__instance);
    }
}

using System.Reflection;
using HarmonyLib;

namespace ValuableList;

/// <summary>Private <see cref="MenuScrollBox"/> fields used for instant scroll sync (same names as MenuLib REPOReflection).</summary>
internal static class MenuScrollReflection
{
    internal static readonly FieldInfo ScrollerEndPosition = AccessTools.Field(typeof(MenuScrollBox), "scrollerEndPosition");
    internal static readonly FieldInfo ScrollerStartPosition = AccessTools.Field(typeof(MenuScrollBox), "scrollerStartPosition");
    internal static readonly FieldInfo ScrollHandleTargetPosition = AccessTools.Field(typeof(MenuScrollBox), "scrollHandleTargetPosition");
    internal static readonly FieldInfo ScrollAmount = AccessTools.Field(typeof(MenuScrollBox), "scrollAmount");
}

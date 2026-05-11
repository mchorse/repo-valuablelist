using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValuableList;

/// <summary>
/// Defensive guard for <see cref="SemiFunc.UIGetRectTransformPositionOnScreen"/>.
///
/// The vanilla implementation dereferences <c>MenuPage.rectTransform</c> without a null check.
/// When MenuLib instantiates a popup page from the <c>SettingsGraphics</c> template, the cloned
/// <see cref="MenuPage"/> has a null <c>rectTransform</c> field until its <c>Start()</c> runs (the
/// field is <c>internal</c> and not serialized, so it isn't copied during <c>Object.Instantiate</c>).
/// In the same frame, <c>MenuManager.Update</c> can iterate <c>allMenuButtons</c> — including
/// freshly-cloned template buttons whose <c>parentPage</c> points at the un-Started page — and call
/// <c>RegisterHover</c> → <c>UIMouseHover</c> → this method, producing a
/// <see cref="System.NullReferenceException"/>.
///
/// Patching here (rather than near <c>Start</c> or <c>OnEnable</c>) avoids interfering with
/// <see cref="MenuPage"/>'s position bookkeeping during the intro animation, and is narrower than
/// patching <c>UIMouseHover</c> (which MenuLib already IL-hooks).
/// </summary>
[HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIGetRectTransformPositionOnScreen))]
internal static class SemiFuncPatch
{
    private static readonly FieldInfo? MenuPageRectTransformField = AccessTools.Field(typeof(MenuPage), "rectTransform");

    /// <summary>Off-screen sentinel so any subsequent bounds check (e.g. in <c>UIMouseHover</c>) reliably fails.</summary>
    private static readonly Vector2 OffScreen = new(float.NegativeInfinity, float.NegativeInfinity);

    [HarmonyPrefix]
    private static bool GuardAgainstMissingMenuPageRectTransform(RectTransform rectTransform, ref Vector2 __result)
    {
        if (rectTransform == null)
        {
            __result = OffScreen;
            return false;
        }

        var menuPage = rectTransform.GetComponentInParent<MenuPage>();

        if (menuPage == null)
        {
            __result = OffScreen;
            return false;
        }

        if (MenuPageRectTransformField?.GetValue(menuPage) is RectTransform pageRect && pageRect != null)
        {
            return true;
        }

        __result = OffScreen;
        return false;
    }
}

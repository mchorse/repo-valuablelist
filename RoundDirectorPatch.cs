using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValuableList;

[HarmonyPatch(typeof(RoundDirector))]
internal static class RoundDirectorPatch
{
    private const float HudPrimaryFontSize = 16f;
    private const float HudLineStackSpacing = -6f;
    private static readonly float HudStatsFontSize = HudPrimaryFontSize * 1.5f;

    private static GameObject? labelObject;
    private static TextMeshProUGUI? roomLineText;
    private static TextMeshProUGUI? statsLineText;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RoundDirector.Update))]
    private static void UpdateHudLabel()
    {
        if (!SemiFunc.RunIsLevel())
        {
            SetLabelActive(false);
            
            return;
        }

        EnsureLabel();
        
        if (statsLineText == null)
        {
            return;
        }

        if (!ValuableList.Instance.ShowMostExpensiveHudLabelEnabled)
        {
            SetLabelActive(false);
            return;
        }

        var (entry, count) = ValuableUtils.GetMostExpensiveInCurrentRoom();
        var (collected, total, collectedValue, totalValue) = ValuableUtils.GetLevelCollectionStats();
        var statsPlain = $"{collected}/{total} - {ValuableUtils.FormatPrice(collectedValue)}/{ValuableUtils.FormatPrice(totalValue)}";

        statsLineText.text = statsPlain;
        statsLineText.color = total > 0 && collected == total ? ValuablesMenu.HighlightValueLabelColor : Color.white;

        if (roomLineText != null)
        {
            if (entry != null)
            {
                roomLineText.gameObject.SetActive(true);
                roomLineText.text = $"{entry.Value.Name} - {ValuableUtils.FormatPrice(entry.Value.Price)} ({count})";
            }
            else
            {
                roomLineText.gameObject.SetActive(false);
            }
        }

        SetLabelActive(true);
    }

    private static void EnsureLabel()
    {
        if (labelObject != null && (roomLineText == null || statsLineText == null))
        {
            Object.Destroy(labelObject);

            labelObject = null;
            roomLineText = null;
            statsLineText = null;
        }

        if (labelObject != null)
        {
            return;
        }

        var gameHud = GameObject.Find("Game Hud");

        if (gameHud == null)
        {
            return;
        }

        labelObject = new GameObject("ValuableList Most Expensive Valuable");
        labelObject.transform.SetParent(gameHud.transform, false);

        var vlg = labelObject.AddComponent<VerticalLayoutGroup>();

        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = HudLineStackSpacing;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        var fitter = labelObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_FontAsset? hudFont = null;
        GameObject? tax = GameObject.Find("Tax Haul");
        TMP_Text? taxText = tax != null ? tax.GetComponent<TMP_Text>() : null;
        
        if (taxText != null)
        {
            hudFont = taxText.font;
        }

        var roomGo = new GameObject("Room line");
        
        roomGo.transform.SetParent(labelObject.transform, false);
        roomLineText = roomGo.AddComponent<TextMeshProUGUI>();
        ConfigureHudLine(roomLineText, HudPrimaryFontSize, hudFont);

        var statsGo = new GameObject("Stats line");
        
        statsGo.transform.SetParent(labelObject.transform, false);
        statsLineText = statsGo.AddComponent<TextMeshProUGUI>();
        ConfigureHudLine(statsLineText, HudStatsFontSize, hudFont);

        var rect = labelObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, 10f);
    }

    private static void ConfigureHudLine(TextMeshProUGUI tmp, float fontSize, TMP_FontAsset? font)
    {
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.richText = false;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.lineSpacing = 0f;
        tmp.paragraphSpacing = 0f;
        tmp.margin = Vector4.zero;

        if (font != null)
        {
            tmp.font = font;
        }

        var lineFitter = tmp.gameObject.AddComponent<ContentSizeFitter>();

        lineFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        lineFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void SetLabelActive(bool active)
    {
        if (labelObject != null && labelObject.activeSelf != active)
        {
            labelObject.SetActive(active);
        }
    }
}
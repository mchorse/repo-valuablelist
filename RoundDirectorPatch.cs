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
    private static readonly Color HudCurrentRoomColor = new Color(1f, 0.5f, 0.08f, 1f);

    private static GameObject? labelObject;
    private static TextMeshProUGUI? currentRoomLineText;
    private static TextMeshProUGUI? mostExpensiveLineText;
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
        
        if (currentRoomLineText == null || statsLineText == null)
        {
            return;
        }

        if (!ValuableList.Instance.ShowMostExpensiveHudLabelEnabled)
        {
            SetLabelActive(false);
            return;
        }

        var formattedRoom = ValuableUtils.GetCurrentPlayerRoomName();
        currentRoomLineText.text = string.IsNullOrWhiteSpace(formattedRoom) ? "Unknown" : formattedRoom;
        currentRoomLineText.color = HudCurrentRoomColor;

        var (entry, count, distanceToBest) = ValuableUtils.GetMostExpensiveInCurrentRoom();
        var (collected, total, collectedValue, totalValue) = ValuableUtils.GetLevelCollectionStats();
        var statsPlain = $"{collected}/{total} - {ValuableUtils.FormatPrice(collectedValue)}/{ValuableUtils.FormatPrice(totalValue)}";

        statsLineText.text = statsPlain;
        statsLineText.color = total > 0 && collected == total ? ValuablesMenu.HighlightValueLabelColor : Color.white;

        if (mostExpensiveLineText != null)
        {
            if (entry != null)
            {
                mostExpensiveLineText.gameObject.SetActive(true);

                if (ValuableList.Instance.ShowDistanceInMenuEnabled)
                {
                    var dist = ValuableUtils.FormatDistanceMetersOneDecimal(distanceToBest);
                    
                    mostExpensiveLineText.text = $"{entry.Value.Name} - {ValuableUtils.FormatPrice(entry.Value.Price)}/{dist}m ({count})";
                }
                else
                {
                    mostExpensiveLineText.text = $"{entry.Value.Name} - {ValuableUtils.FormatPrice(entry.Value.Price)} ({count})";
                }
            }
            else
            {
                mostExpensiveLineText.gameObject.SetActive(false);
            }
        }

        SetLabelActive(true);
    }

    private static void EnsureLabel()
    {
        if (labelObject != null && (currentRoomLineText == null || mostExpensiveLineText == null || statsLineText == null))
        {
            Object.Destroy(labelObject);

            labelObject = null;
            currentRoomLineText = null;
            mostExpensiveLineText = null;
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

        labelObject = new GameObject("VL-ValuableList Most Expensive Valuable");
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

        var currentRoomGo = new GameObject("VL-Current room line");
        currentRoomGo.transform.SetParent(labelObject.transform, false);
        currentRoomLineText = currentRoomGo.AddComponent<TextMeshProUGUI>();
        ConfigureHudLine(currentRoomLineText, HudPrimaryFontSize, hudFont);
        currentRoomLineText.color = HudCurrentRoomColor;

        var expensiveGo = new GameObject("VL-Most expensive line");
        expensiveGo.transform.SetParent(labelObject.transform, false);
        mostExpensiveLineText = expensiveGo.AddComponent<TextMeshProUGUI>();
        ConfigureHudLine(mostExpensiveLineText, HudPrimaryFontSize, hudFont);

        var statsGo = new GameObject("VL-Stats line");
        
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
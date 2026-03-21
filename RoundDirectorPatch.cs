using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValuableList;

[HarmonyPatch(typeof(RoundDirector))]
internal static class RoundDirectorPatch
{
    private static GameObject? labelObject;
    private static TextMeshProUGUI? labelText;

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
        
        if (labelText == null)
        {
            return;
        }

        if (!ValuableList.Instance.ShowMostExpensiveHudLabelEnabled)
        {
            SetLabelActive(false);
            return;
        }

        var entry = ValuableUtils.GetMostExpensiveInCurrentRoom();
        
        if (entry == null)
        {
            SetLabelActive(false);
            return;
        }

        labelText.text = $"{entry.Value.Name} - {ValuableUtils.FormatPrice(entry.Value.Price)}";
        
        SetLabelActive(true);
    }

    private static void EnsureLabel()
    {
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

        labelText = labelObject.AddComponent<TextMeshProUGUI>();
        labelText.fontSize = 16f;
        labelText.color = Color.white;
        labelText.enableWordWrapping = false;
        labelText.alignment = TextAlignmentOptions.Center;

        GameObject? tax = GameObject.Find("Tax Haul");
        TMP_Text? taxText = tax != null ? tax.GetComponent<TMP_Text>() : null;
        
        if (taxText != null)
        {
            labelText.font = taxText.font;
        }

        ContentSizeFitter? fitter = labelObject.AddComponent<ContentSizeFitter>();
        
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform? rect = labelObject.GetComponent<RectTransform>();
        
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, 10f);
    }

    private static void SetLabelActive(bool active)
    {
        if (labelObject != null && labelObject.activeSelf != active)
        {
            labelObject.SetActive(active);
        }
    }
}

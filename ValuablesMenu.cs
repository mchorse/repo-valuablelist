using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;

namespace ValuableList;

internal sealed class ValuablesMenu
{
    private static readonly Color ModuleHeaderColor = new Color(68f / 255f, 136f / 255f, 1f, 1f);
    private static readonly Color ValueLabelColor = Color.white;
    internal static readonly Color HighlightValueLabelColor = new Color(0f, 1f, 68f / 255f, 1f);
    private static readonly Color ActiveRoomHeaderColor = Color.Lerp(ModuleHeaderColor, HighlightValueLabelColor, 0.75f);

    private readonly ManualLogSource logger;
    private REPOPopupPage? currentPage;
    private bool isOpen;
    private readonly List<RoomGroupVisual> roomGroupVisuals = new();
    private REPOLabel? noResultsLabel;
    private REPOButton? toggleCollectedButton;
    private REPOLabel? summaryLabel;
    private bool hideCollectedValuables;
    private string currentSearchQuery = string.Empty;

    internal ValuablesMenu(ManualLogSource logger)
    {
        this.logger = logger;
    }

    internal void Toggle()
    {
        if (isOpen)
        {
            CloseCurrentPage();
            return;
        }

        if (SemiFunc.MenuLevel() || IsAnotherMenuOpen())
        {
            return;
        }

        Open();
    }

    private void Open()
    {
        try
        {
            CloseCurrentPage();

            var page = MenuAPI.CreateREPOPopupPage("Valuables", REPOPopupPage.PresetSide.Left, false, true, 0f);
            page.scrollView.scrollSpeed = 3f;
            page.onEscapePressed += HandleEscapePressed;
            
            var maskPadding = page.maskPadding;
            maskPadding.top = 35f;
            page.maskPadding = maskPadding;

            currentSearchQuery = string.Empty;
            var grouped = ValuableUtils.GetGroupedValuables();
            AddSearchBox(page);
            AddGroupedValuables(page, grouped);
            AddCollectedButton(page);
            summaryLabel = AddCollectedSummaryLabel(page, grouped);

            currentPage = page;
            isOpen = true;
            page.OpenPage(false);
            ValuableList.Instance.StartCoroutine(ScrollToCurrentPlayerRoomAfterOpen(page));
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to open valuables menu: {ex}");
            isOpen = false;
            currentPage = null;
            summaryLabel = null;
        }
    }

    private bool HandleEscapePressed()
    {
        isOpen = false;
        currentPage = null;
        return true;
    }

    private void AddGroupedValuables(REPOPopupPage page, List<IGrouping<string, ValuableEntry>> grouped)
    {
        roomGroupVisuals.Clear();
        noResultsLabel = null;

        page.AddElementToScrollView(parent =>
        {
            noResultsLabel = MenuAPI.CreateREPOLabel("No matching valuables found.", parent, default);
            noResultsLabel.labelTMP.fontStyle = FontStyles.Normal;
            noResultsLabel.labelTMP.fontSize = 16f;
            noResultsLabel.labelTMP.color = ValueLabelColor;
            SetLabelVisibility(noResultsLabel, false);
            return noResultsLabel.rectTransform;
        }, 0f, 0f);

        var currentRoom = ValuableUtils.GetCurrentPlayerRoomName();
        
        if (grouped.Count == 0)
        {
            page.AddElementToScrollView(parent =>
            {
                var emptyLabel = MenuAPI.CreateREPOLabel("No valuables found.", parent, default);
                
                emptyLabel.labelTMP.fontStyle = FontStyles.Normal;
                emptyLabel.labelTMP.fontSize = 16f;
                emptyLabel.labelTMP.color = ValueLabelColor;
                
                return emptyLabel.rectTransform;
            }, 0f, 0f);
            return;
        }

        for (var groupIndex = 0; groupIndex < grouped.Count; groupIndex++)
        {
            var roomGroup = grouped[groupIndex];
            var roomTotal = roomGroup.Sum(entry => entry.Price);
            var roomHeaderText = $"{ValuableUtils.TruncateRoomNameForMenuDisplay(roomGroup.Key)} ({ValuableUtils.FormatPrice(roomTotal)})";
            REPOLabel? groupHeader = null;
            
            page.AddElementToScrollView(parent =>
            {
                groupHeader = MenuAPI.CreateREPOLabel(roomHeaderText, parent, default);
                
                groupHeader.labelTMP.fontStyle = FontStyles.Bold;
                groupHeader.labelTMP.fontSize = 24f;
                
                var isCurrentRoom = !string.IsNullOrWhiteSpace(currentRoom) && string.Equals(roomGroup.Key, currentRoom, StringComparison.OrdinalIgnoreCase);
                
                groupHeader.labelTMP.color = isCurrentRoom ? ActiveRoomHeaderColor : ModuleHeaderColor;
                
                return groupHeader.rectTransform;
            }, 0f, 0f);

            var entryVisuals = new List<ValuableRowVisual>();
            
            foreach (var entry in roomGroup)
            {
                REPOLabel? lineLabelRef = null;
                
                page.AddElementToScrollView(parent =>
                {
                    var lineLabel = MenuAPI.CreateREPOLabel(FormatValuableLine(entry), parent, default);
                    lineLabelRef = lineLabel;
                    
                    lineLabel.labelTMP.fontStyle = FontStyles.Normal;
                    lineLabel.labelTMP.fontSize = 16f;
                    lineLabel.labelTMP.color = ResolveRowColor(entry);
                    
                    var size = lineLabel.rectTransform.sizeDelta;
                    
                    lineLabel.rectTransform.sizeDelta = new Vector2(size.x, size.y * 0.625f);
                    
                    return lineLabel.rectTransform;
                }, 0f, 0f);

                if (lineLabelRef != null)
                {
                    entryVisuals.Add(new ValuableRowVisual(lineLabelRef, entry.Name, entry.IsInCartOrExtraction, entry.Price));
                }
            }

            REPOSpacer? spacer = null;
            
            if (groupIndex < grouped.Count - 1)
            {
                page.AddElementToScrollView(parent =>
                {
                    spacer = MenuAPI.CreateREPOSpacer(parent, default, new Vector2(0f, 8f));
                    
                    return spacer.rectTransform;
                }, 0f, 0f);
            }

            if (groupHeader != null)
            {
                roomGroupVisuals.Add(new RoomGroupVisual(roomGroup.Key, groupHeader, entryVisuals, spacer, 0f));
            }
        }
    }

    private static string FormatValuableLine(ValuableEntry entry)
    {
        var showDistance = ValuableList.Instance.ShowDistanceInMenuEnabled;
        var distancePart = showDistance
            ? $" ({ValuableUtils.FormatDistanceMetersOneDecimal(entry.DistanceFromPlayerMeters)}m)"
            : string.Empty;

        if (entry.IsCosmeticBox)
        {
            return $"{entry.Name}{distancePart}";
        }

        return $"{entry.Name} - {ValuableUtils.FormatPrice(entry.Price)}{distancePart}";
    }

    private static Color ResolveRowColor(ValuableEntry entry)
    {
        if (entry.IsCosmeticBox && entry.CosmeticBoxRarity.HasValue)
        {
            return ValuableUtils.GetCosmeticBoxRarityColor(entry.CosmeticBoxRarity.Value);
        }

        return entry.IsInCartOrExtraction ? HighlightValueLabelColor : ValueLabelColor;
    }

    private void AddSearchBox(REPOPopupPage page)
    {
        page.AddElement(parent =>
        {
            MenuAPI.CreateREPOInputField("Search", query =>
            {
                currentSearchQuery = query ?? string.Empty;
                ApplySearchFilter(page, currentSearchQuery);
            }, parent, new Vector2(83f, 272f)).transform.localScale = Vector3.one * 0.95f;
        });
    }

    private void ApplySearchFilter(REPOPopupPage page, string query)
    {
        try
        {
            var normalized = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim().ToLowerInvariant();
            var visibleGroupCount = 0;

            foreach (var group in roomGroupVisuals)
            {
                var hasAnyVisibleEntry = false;
                var visibleTotal = 0;
                
                foreach (var row in group.Rows)
                {
                    var visibleByQuery = string.IsNullOrEmpty(normalized) || row.NameLower.Contains(normalized);
                    var visibleByCollectedState = !hideCollectedValuables || !row.IsInCartOrExtraction;
                    var visible = visibleByQuery && visibleByCollectedState;
                    
                    SetLabelVisibility(row.Label, visible);
                    hasAnyVisibleEntry |= visible;
                    if (visible)
                    {
                        visibleTotal += row.Price;
                    }
                }

                SetLabelVisibility(group.Header, hasAnyVisibleEntry);
                group.Header.labelTMP.text = $"{ValuableUtils.TruncateRoomNameForMenuDisplay(group.RoomName)} ({ValuableUtils.FormatPrice(visibleTotal)})";
                
                if (hasAnyVisibleEntry)
                {
                    visibleGroupCount++;
                }
            }

            foreach (var group in roomGroupVisuals)
            {
                if (group.Spacer == null)
                {

                    continue;
                }

                SetSpacerVisibility(group.Spacer, IsLabelVisible(group.Header));
            }

            SetLabelVisibility(noResultsLabel, roomGroupVisuals.Count > 0 && visibleGroupCount == 0);
            UpdateToggleButtonVisual();
            UpdateSummaryLabelFromVisibleRows();

            page.scrollView.SetScrollPosition(0f);
        }
        catch (Exception ex)
        {
            logger.LogError($"Search filter update failed: {ex}");
        }
    }

    private void ScrollToCurrentPlayerRoom(REPOPopupPage page)
    {
        var msb = page.menuScrollBox;

        page.scrollView.UpdateElements();
        TryRecalculateMenuScrollHeight(msb);
        page.scrollView.UpdateElements();
        CacheRoomScrollTargets(page);

        if (msb == null || !msb.scrollBar.activeSelf || roomGroupVisuals.Count == 0)
        {
            SnapMenuScrollToScrollerY(msb, 0f);

            return;
        }

        var currentRoom = ValuableUtils.GetCurrentPlayerRoomName();

        if (string.IsNullOrWhiteSpace(currentRoom))
        {
            SnapMenuScrollToScrollerY(msb, 0f);

            return;
        }

        var roomIndex = roomGroupVisuals.FindIndex(group =>
            string.Equals(group.RoomName, currentRoom, StringComparison.OrdinalIgnoreCase));

        if (roomIndex < 0)
        {
            SnapMenuScrollToScrollerY(msb, 0f);

            return;
        }

        SnapMenuScrollToScrollerY(msb, roomGroupVisuals[roomIndex].CachedTargetScrollerY);
        page.scrollView.UpdateElements();
    }

    private IEnumerator ScrollToCurrentPlayerRoomAfterOpen(REPOPopupPage page)
    {
        yield return null;
        yield return null;

        if (!isOpen || currentPage != page)
        {
            yield break;
        }

        ApplySearchFilter(page, string.Empty);
        ScrollToCurrentPlayerRoom(page);
    }

    /// <summary>
    /// Fills <see cref="RoomGroupVisual.CachedTargetScrollerY"/> — scroller local Y that aligns each room header with the mask top.
    /// </summary>
    private void CacheRoomScrollTargets(REPOPopupPage page)
    {
        var msb = page.menuScrollBox;

        if (roomGroupVisuals.Count == 0 || msb == null)
        {
            return;
        }

        if (!msb.scrollBar.activeSelf)
        {
            foreach (var g in roomGroupVisuals)
            {
                g.CachedTargetScrollerY = 0f;
            }

            return;
        }

        foreach (var g in roomGroupVisuals)
        {
            g.Header.gameObject.SetActive(true);
        }

        var maskRt = page.maskRectTransform;
        var maskTop = GetWorldTopY(maskRt);

        SetScrollerLocalYRaw(msb, 0f);
        var headerTopsAt0 = new float[roomGroupVisuals.Count];

        for (var i = 0; i < roomGroupVisuals.Count; i++)
        {
            headerTopsAt0[i] = GetWorldTopY(roomGroupVisuals[i].Header.rectTransform);
        }

        var start = (float)MenuScrollReflection.ScrollerStartPosition.GetValue(msb)!;
        var probeY = Mathf.Abs(start) > 1f ? start * 0.5f : 100f;

        SetScrollerLocalYRaw(msb, probeY);
        var firstHeaderTopAtProbe = GetWorldTopY(roomGroupVisuals[0].Header.rectTransform);

        var worldDelta = firstHeaderTopAtProbe - headerTopsAt0[0];
        var perUnit = Mathf.Abs(probeY) < 0.0001f ? 0f : worldDelta / probeY;

        for (var i = 0; i < roomGroupVisuals.Count; i++)
        {
            if (Mathf.Abs(perUnit) < 0.0001f)
            {
                roomGroupVisuals[i].CachedTargetScrollerY = 0f;
                continue;
            }

            roomGroupVisuals[i].CachedTargetScrollerY = (maskTop - headerTopsAt0[i]) / perUnit;
        }

        SnapMenuScrollToScrollerY(msb, 0f);
    }

    private static void TryRecalculateMenuScrollHeight(MenuScrollBox? menuScrollBox)
    {
        if (menuScrollBox == null)
        {
            return;
        }

        AccessTools.Method(typeof(MenuScrollBox), "RecalculateScrollHeight")?.Invoke(menuScrollBox, null);
    }

    private static float GetWorldTopY(RectTransform rectTransform)
    {
        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        return Mathf.Max(Mathf.Max(corners[0].y, corners[1].y), Mathf.Max(corners[2].y, corners[3].y));
    }

    /// <summary>
    /// Instant scroll: set <c>MenuScrollBox.scroller</c> local Y, then sync handle / <c>scrollAmount</c> (MenuLib patched formula).
    /// </summary>
    internal static void SnapMenuScrollToScrollerY(MenuScrollBox? menuScrollBox, float scrollerLocalY)
    {
        SetScrollerLocalYRaw(menuScrollBox, scrollerLocalY);
        SyncScrollBox(menuScrollBox);
    }

    private static void SetScrollerLocalYRaw(MenuScrollBox? msb, float y)
    {
        if (msb?.scroller == null)
        {
            return;
        }

        var sc = msb.scroller;
        sc.localPosition = new Vector3(sc.localPosition.x, y, sc.localPosition.z);
    }

    private static void SyncScrollBox(MenuScrollBox? msb)
    {
        if (msb == null || !msb.scrollBar.activeSelf)
        {
            return;
        }

        var start = (float)MenuScrollReflection.ScrollerStartPosition.GetValue(msb)!;
        var end = (float)MenuScrollReflection.ScrollerEndPosition.GetValue(msb)!;
        var range = end - start;
        var y = msb.scroller.localPosition.y;
        var scrollAmount = Mathf.Approximately(range, 0f) ? 1f : Mathf.Clamp01((y - start) / range);

        var bgH = msb.scrollBarBackground.rect.height;
        var half = msb.scrollHandle.sizeDelta.y / 2f;
        var handleY = scrollAmount * bgH - half;
        handleY = Mathf.Clamp(handleY, half, bgH - half);
        var finalScrollAmount = Mathf.Clamp01((handleY + half) / bgH);

        msb.scrollHandle.localPosition =
            new Vector3(msb.scrollHandle.localPosition.x, handleY, msb.scrollHandle.localPosition.z);

        MenuScrollReflection.ScrollHandleTargetPosition.SetValue(msb, handleY);
        MenuScrollReflection.ScrollAmount.SetValue(msb, finalScrollAmount);

        var finalY = Mathf.Lerp(start, end, finalScrollAmount);
        var sc = msb.scroller;
        sc.localPosition = new Vector3(sc.localPosition.x, finalY, sc.localPosition.z);
    }

    private REPOLabel? AddCollectedSummaryLabel(REPOPopupPage page, List<IGrouping<string, ValuableEntry>> grouped)
    {
        REPOLabel? created = null;

        page.AddElement(parent =>
        {
            created = MenuAPI.CreateREPOLabel(BuildSummaryText(grouped), parent, new Vector2(120f, 20f));

            created.labelTMP.fontStyle = FontStyles.Normal;
            created.labelTMP.fontSize = 24f;
            created.labelTMP.color = SummaryColorForGrouped(grouped);
            created.labelTMP.alignment = TextAlignmentOptions.Right;
        });

        return created;
    }

    private static string BuildSummaryText(IEnumerable<IGrouping<string, ValuableEntry>> grouped)
    {
        var allCount = grouped.Sum(group => group.Count());
        var collectedCount = grouped.Sum(group => group.Count(entry => entry.IsInCartOrExtraction));
        var totalAcquirable = grouped.Sum(group => group.Where(entry => !entry.IsInCartOrExtraction).Sum(entry => entry.Price));

        return $"{collectedCount}/{allCount}, {ValuableUtils.FormatPrice(totalAcquirable)}";
    }

    private static Color SummaryColorForGrouped(IEnumerable<IGrouping<string, ValuableEntry>> grouped)
    {
        var allCount = grouped.Sum(group => group.Count());
        var collectedCount = grouped.Sum(group => group.Count(entry => entry.IsInCartOrExtraction));

        var isAllCollected = allCount > 0 && collectedCount == allCount;

        return isAllCollected ? HighlightValueLabelColor : ValueLabelColor;
    }

    private void UpdateSummaryLabelFromVisibleRows()
    {
        if (summaryLabel?.labelTMP == null)
        {
            return;
        }

        var collected = 0;
        var total = 0;
        var acquirableSum = 0;

        foreach (var group in roomGroupVisuals)
        {
            foreach (var row in group.Rows)
            {
                acquirableSum += row.Price;
                total++;

                if (row.IsInCartOrExtraction)
                {
                    collected++;
                }
            }
        }

        summaryLabel.labelTMP.text = $"{collected}/{total} - {ValuableUtils.FormatPrice(acquirableSum)}";
        summaryLabel.labelTMP.color = total > 0 && collected == total ? HighlightValueLabelColor : ValueLabelColor;
    }

    private void AddCollectedButton(REPOPopupPage page)
    {
        page.AddElement(parent =>
        {
            toggleCollectedButton = MenuAPI.CreateREPOButton("Collected", () =>
            {
                hideCollectedValuables = !hideCollectedValuables;
                
                ApplySearchFilter(page, currentSearchQuery);
            }, parent, new Vector2(66f, 18f));

            UpdateToggleButtonVisual();
        });
    }

    private void CloseCurrentPage()
    {
        if (currentPage != null)
        {
            currentPage.ClosePage(true);
        }

        currentPage = null;
        isOpen = false;
        summaryLabel = null;
    }

    private static bool IsAnotherMenuOpen()
    {
        return MenuManager.instance && MenuManager.instance.currentMenuPage;
    }

    private void UpdateToggleButtonVisual()
    {
        if (toggleCollectedButton?.labelTMP == null)
        {
            return;
        }

        toggleCollectedButton.labelTMP.text = hideCollectedValuables ? "Show Collected" : "Hide Collected";
    }

    private static void SetLabelVisibility(REPOLabel? label, bool visible)
    {
        if (label == null || label.repoScrollViewElement == null)
        {
            return;
        }

        label.repoScrollViewElement.visibility = visible;
    }

    private static bool IsLabelVisible(REPOLabel? label)
    {
        if (label == null || label.repoScrollViewElement == null)
        {
            return false;
        }

        return label.repoScrollViewElement.visibility;
    }

    private static void SetSpacerVisibility(REPOSpacer? spacer, bool visible)
    {
        if (spacer == null || spacer.repoScrollViewElement == null)
        {
            return;
        }

        spacer.repoScrollViewElement.visibility = visible;
    }

    private readonly struct ValuableRowVisual
    {
        internal ValuableRowVisual(REPOLabel label, string name, bool isInCartOrExtraction, int price)
        {
            Label = label;
            NameLower = name.ToLowerInvariant();
            IsInCartOrExtraction = isInCartOrExtraction;
            Price = price;
        }

        internal REPOLabel Label { get; }
        internal string NameLower { get; }
        internal bool IsInCartOrExtraction { get; }
        internal int Price { get; }
    }

    private sealed class RoomGroupVisual
    {
        internal RoomGroupVisual(string roomName, REPOLabel header, List<ValuableRowVisual> rows, REPOSpacer? spacer, float cachedTargetScrollerY)
        {
            RoomName = roomName;
            Header = header;
            Rows = rows;
            Spacer = spacer;
            CachedTargetScrollerY = cachedTargetScrollerY;
        }

        internal string RoomName { get; }
        internal REPOLabel Header { get; }
        internal List<ValuableRowVisual> Rows { get; }
        internal REPOSpacer? Spacer { get; }
        internal float CachedTargetScrollerY { get; set; }
    }
}
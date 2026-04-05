using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
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
            ValuableList.Instance.StartCoroutine(ScrollToCurrentPlayerRoomAfterOpen(page, grouped));
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
                    lineLabel.labelTMP.color = entry.IsInCartOrExtraction ? HighlightValueLabelColor : ValueLabelColor;
                    
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
                roomGroupVisuals.Add(new RoomGroupVisual(roomGroup.Key, groupHeader, entryVisuals, spacer));
            }
        }
    }

    private static string FormatValuableLine(ValuableEntry entry)
    {
        var priceText = ValuableUtils.FormatPrice(entry.Price);

        if (!ValuableList.Instance.ShowDistanceInMenuEnabled)
        {
            return $"{entry.Name} - {priceText}";
        }

        var dist = ValuableUtils.FormatDistanceMetersOneDecimal(entry.DistanceFromPlayerMeters);

        return $"{entry.Name} - {priceText} ({dist}m)";
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

    private static void ScrollToCurrentPlayerRoom(REPOPopupPage page, List<IGrouping<string, ValuableEntry>> grouped)
    {
        if (grouped.Count == 0)
        {
            return;
        }

        var currentRoom = ValuableUtils.GetCurrentPlayerRoomName();
        
        if (string.IsNullOrWhiteSpace(currentRoom))
        {
            page.scrollView.SetScrollPosition(0f);
            return;
        }

        var roomIndex = grouped.FindIndex(group => string.Equals(group.Key, currentRoom, StringComparison.OrdinalIgnoreCase));
        
        if (roomIndex < 0)
        {
            page.scrollView.SetScrollPosition(0f);
            return;
        }

        if (roomIndex == 0)
        {
            page.scrollView.SetScrollPosition(0f);
            return;
        }

        var normalizedPosition = (float)roomIndex / Math.Max(1, grouped.Count - 1);
        
        page.scrollView.SetScrollPosition(normalizedPosition);
    }

    private IEnumerator ScrollToCurrentPlayerRoomAfterOpen(REPOPopupPage page, List<IGrouping<string, ValuableEntry>> grouped)
    {
        // Wait for popup open/layout to complete before setting scroll.
        yield return null;
        yield return null;

        if (!isOpen || currentPage != page)
        {
            yield break;
        }

        // Ensure search-state visibility is initialized after scroll elements are fully wired.
        ApplySearchFilter(page, string.Empty);
        ScrollToCurrentPlayerRoom(page, grouped);
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

    private readonly struct RoomGroupVisual
    {
        internal RoomGroupVisual(string roomName, REPOLabel header, List<ValuableRowVisual> rows, REPOSpacer? spacer)
        {
            RoomName = roomName;
            Header = header;
            Rows = rows;
            Spacer = spacer;
        }

        internal string RoomName { get; }
        internal REPOLabel Header { get; }
        internal List<ValuableRowVisual> Rows { get; }
        internal REPOSpacer? Spacer { get; }
    }
}
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Globalization;
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
    private static readonly Color HighlightValueLabelColor = new Color(0f, 1f, 68f / 255f, 1f);
    private static readonly Color ActiveRoomHeaderColor = Color.Lerp(ModuleHeaderColor, HighlightValueLabelColor, 0.75f);

    private readonly ManualLogSource logger;
    private REPOPopupPage? currentPage;
    private bool isOpen;
    private readonly List<RoomGroupVisual> roomGroupVisuals = new();
    private REPOLabel? noResultsLabel;

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

            var grouped = ValuableUtils.GetGroupedValuables();
            AddSearchBox(page);
            AddGroupedValuables(page, grouped);
            AddCollectedSummaryLabel(page, grouped);

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
            REPOLabel? groupHeader = null;
            
            page.AddElementToScrollView(parent =>
            {
                groupHeader = MenuAPI.CreateREPOLabel(roomGroup.Key, parent, default);
                
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
                    var lineLabel = MenuAPI.CreateREPOLabel($"{entry.Name} - {FormatPrice(entry.Price)}", parent, default);
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
                    entryVisuals.Add(new ValuableRowVisual(lineLabelRef, entry.Name));
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
                roomGroupVisuals.Add(new RoomGroupVisual(groupHeader, entryVisuals, spacer));
            }
        }
    }

    private void AddSearchBox(REPOPopupPage page)
    {
        page.AddElement(parent =>
        {
            MenuAPI.CreateREPOInputField("Search", query =>
            {
                ApplySearchFilter(page, query);
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
                
                foreach (var row in group.Rows)
                {
                    var visible = string.IsNullOrEmpty(normalized) || row.NameLower.Contains(normalized);
                    
                    SetLabelVisibility(row.Label, visible);
                    hasAnyVisibleEntry |= visible;
                }

                SetLabelVisibility(group.Header, hasAnyVisibleEntry);
                
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

    private void AddCollectedSummaryLabel(REPOPopupPage page, List<IGrouping<string, ValuableEntry>> grouped)
    {
        var allCount = grouped.Sum(group => group.Count());
        var collectedCount = grouped.Sum(group => group.Count(entry => entry.IsInCartOrExtraction));
        var isAllCollected = allCount > 0 && collectedCount == allCount;
        var summaryColor = isAllCollected ? HighlightValueLabelColor : ValueLabelColor;

        page.AddElement(parent =>
        {
            var summaryLabel = MenuAPI.CreateREPOLabel($"{collectedCount}/{allCount}", parent, new Vector2(120f, 20f));

            summaryLabel.labelTMP.fontStyle = FontStyles.Normal;
            summaryLabel.labelTMP.fontSize = 24f;
            summaryLabel.labelTMP.color = summaryColor;
            summaryLabel.labelTMP.alignment = TextAlignmentOptions.Right;
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
    }

    private static bool IsAnotherMenuOpen()
    {
        return MenuManager.instance && MenuManager.instance.currentMenuPage;
    }

    private static string FormatPrice(int rawPrice)
    {
        if (ValuableList.Instance.RoundPricesEnabled)
        {
            return $"${(rawPrice / 1000f).ToString("0.0", CultureInfo.InvariantCulture)}K";
        }

        return $"${rawPrice}";
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
        internal ValuableRowVisual(REPOLabel label, string name)
        {
            Label = label;
            NameLower = name.ToLowerInvariant();
        }

        internal REPOLabel Label { get; }
        internal string NameLower { get; }
    }

    private readonly struct RoomGroupVisual
    {
        internal RoomGroupVisual(REPOLabel header, List<ValuableRowVisual> rows, REPOSpacer? spacer)
        {
            Header = header;
            Rows = rows;
            Spacer = spacer;
        }

        internal REPOLabel Header { get; }
        internal List<ValuableRowVisual> Rows { get; }
        internal REPOSpacer? Spacer { get; }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ValuableList;

internal static class ValuableUtils
{
    private static readonly Regex RoomMetadataPrefixRegex = new(@"^[^-]+?\s*-\s*[A-Z]+\s*-\s*\d+\s*-\s*(.+)$", RegexOptions.Compiled);
    private static float hudCacheTime;
    private static (ValuableEntry? Entry, int Count, float DistanceToBestMeters) mostExpensiveCachedResult;
    private static (int CollectedCount, int TotalCount, int CollectedValue, int TotalValue) levelCollectionCachedResult;
    private static (int InRoomCount, int TotalCount, SemiFunc.Rarity? RoomDominantRarity) cosmeticBoxesCachedResult;
    private const float HudCacheInterval = 0.5f;

    private static bool cosmeticBoxColorsResolved;
    private static Color cosmeticBoxColorCommon = new Color(0.31f, 0.85f, 0.31f, 1f);
    private static Color cosmeticBoxColorUncommon = new Color(0.4f, 0.7f, 1f, 1f);
    private static Color cosmeticBoxColorRare = new Color(1f, 0.08f, 0.58f, 1f);
    private static Color cosmeticBoxColorUltraRare = new Color(1f, 0.65f, 0.1f, 1f);

    internal static List<IGrouping<string, ValuableEntry>> GetGroupedValuables()
    {
        var orderedEntries = GetValuableEntries()
            .OrderBy(v => v.Room, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.IsCosmeticBox ? 0 : 1)
            .ThenBy(v => v.IsInCartOrExtraction)
            .ThenByDescending(v => v.CosmeticBoxRarity.HasValue ? (int)v.CosmeticBoxRarity.Value : -1)
            .ThenByDescending(v => v.Price)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roomGroups = orderedEntries
            .GroupBy(v => v.Room)
            .Select(g => new MaterializedRoomGroup(g.Key, g.ToList()))
            .ToList();

        roomGroups.Sort((a, b) =>
        {
            var totalA = SumPrices(a);
            var totalB = SumPrices(b);
            var cmp = totalB.CompareTo(totalA);

            if (cmp != 0)
            {
                return cmp;
            }

            return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
        });

        return roomGroups.ConvertAll(g => (IGrouping<string, ValuableEntry>)g);
    }

    private static long SumPrices(IEnumerable<ValuableEntry> entries)
    {
        long sum = 0;

        foreach (var e in entries)
        {
            sum += e.Price;
        }

        return sum;
    }

    private sealed class MaterializedRoomGroup : IGrouping<string, ValuableEntry>
    {
        private readonly List<ValuableEntry> _entries;

        internal MaterializedRoomGroup(string key, List<ValuableEntry> entries)
        {
            Key = key;
            _entries = entries;
        }

        public string Key { get; }

        public IEnumerator<ValuableEntry> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static (ValuableEntry? Entry, int Count, float DistanceToBestMeters) GetMostExpensiveInCurrentRoom()
    {
        EnsureHudCaches();
        
        return mostExpensiveCachedResult;
    }

    internal static (int CollectedCount, int TotalCount, int CollectedValue, int TotalValue) GetLevelCollectionStats()
    {
        EnsureHudCaches();

        return levelCollectionCachedResult;
    }

    internal static (int InRoomCount, int TotalCount, SemiFunc.Rarity? RoomDominantRarity) GetCosmeticBoxStats()
    {
        EnsureHudCaches();

        return cosmeticBoxesCachedResult;
    }

    internal static Color GetCosmeticBoxRarityColor(SemiFunc.Rarity rarity)
    {
        if (!cosmeticBoxColorsResolved)
        {
            TryResolveCosmeticBoxColors();
        }

        return rarity switch
        {
            SemiFunc.Rarity.Common => cosmeticBoxColorCommon,
            SemiFunc.Rarity.Uncommon => cosmeticBoxColorUncommon,
            SemiFunc.Rarity.Rare => cosmeticBoxColorRare,
            SemiFunc.Rarity.UltraRare => cosmeticBoxColorUltraRare,
            _ => Color.white,
        };
    }

    internal static string ToHtmlHex(Color color)
    {
        var r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
        var g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
        var b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static void TryResolveCosmeticBoxColors()
    {
        var ui = CosmeticWorldObjectUI.instance;

        if (ui == null || ui.elementPrefab == null)
        {
            return;
        }

        var element = ui.elementPrefab.GetComponent<CosmeticWorldObjectUIElement>();

        if (element == null)
        {
            return;
        }

        cosmeticBoxColorCommon = element.colorCommon;
        cosmeticBoxColorUncommon = element.colorUncommon;
        cosmeticBoxColorRare = element.colorRare;
        cosmeticBoxColorUltraRare = element.colorUltraRare;
        cosmeticBoxColorsResolved = true;
    }

    private static void EnsureHudCaches()
    {
        if (Time.time - hudCacheTime < HudCacheInterval)
        {
            return;
        }

        hudCacheTime = Time.time;

        ComputeHudCaches();
    }

    private static void ComputeHudCaches()
    {
        var currentRoom = GetCurrentPlayerRoomName();
        ValuableEntry? best = null;
        var distanceToBest = 0f;
        var roomCount = 0;
        var totalCount = 0;
        var collectedCount = 0;
        var totalValue = 0;
        var collectedValue = 0;

        foreach (var v in GetAllValuableObjects())
        {
            totalCount++;
            
            var price = FloorDollarValue(v.dollarValueCurrent);
            
            totalValue += price;

            var inCartOrExtraction = IsInCartOrExtraction(v);

            if (inCartOrExtraction)
            {
                collectedCount++;
                collectedValue += price;
            }

            if (!string.IsNullOrWhiteSpace(currentRoom) && !inCartOrExtraction)
            {
                var room = GetRoomName(v);

                if (string.Equals(room, currentRoom, StringComparison.OrdinalIgnoreCase))
                {
                    roomCount++;

                    if (best == null || price > best.Value.Price)
                    {
                        best = new ValuableEntry(CleanValuableName(v.gameObject.name), price, room, false, 0f);
                        distanceToBest = GetDistanceFromPlayerMeters(v.transform);
                    }
                }
            }
        }

        mostExpensiveCachedResult = (best, roomCount, distanceToBest);
        levelCollectionCachedResult = (collectedCount, totalCount, collectedValue, totalValue);

        ComputeCosmeticBoxCaches(currentRoom);
    }

    private static void ComputeCosmeticBoxCaches(string? currentRoom)
    {
        var inRoom = 0;
        var total = 0;
        SemiFunc.Rarity? roomDominantRarity = null;

        var roundDirector = RoundDirector.instance;

        if (roundDirector != null)
        {
            foreach (var cosmetic in roundDirector.cosmeticWorldObjects)
            {
                if (!cosmetic)
                {
                    continue;
                }

                total++;

                if (string.IsNullOrWhiteSpace(currentRoom))
                {
                    continue;
                }

                var room = GetCosmeticRoomName(cosmetic);

                if (!string.Equals(room, currentRoom, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                inRoom++;

                if (roomDominantRarity == null || cosmetic.rarity > roomDominantRarity.Value)
                {
                    roomDominantRarity = cosmetic.rarity;
                }
            }
        }

        cosmeticBoxesCachedResult = (inRoom, total, roomDominantRarity);
    }

    private static string GetCosmeticRoomName(CosmeticWorldObject cosmetic)
    {
        var room = cosmetic.roomVolumeCheck?.CurrentRooms?.FirstOrDefault(r => r != null && r.Module != null);

        if (room?.Module == null)
        {
            return "Unknown";
        }

        return CleanRoomName(room.Module.name);
    }

    internal static string? GetCurrentPlayerRoomName()
    {
        var room = PlayerAvatar.instance?.RoomVolumeCheck?.CurrentRooms?.FirstOrDefault(r => r != null && r.Module != null);

        if (room?.Module == null)
        {
            return null;
        }

        return CleanRoomName(room.Module.name);
    }

    private static string GetRoomName(ValuableObject valuable)
    {
        var room = valuable.roomVolumeCheck?.CurrentRooms?.FirstOrDefault(r => r != null && r.Module != null);

        if (room?.Module == null)
        {
            return "Unknown";
        }

        return CleanRoomName(room.Module.name);
    }

    private static bool IsInCartOrExtraction(ValuableObject valuable)
    {
        var inCart = valuable.physGrabObject != null 
            && valuable.physGrabObject.impactDetector != null 
            && valuable.physGrabObject.impactDetector.inCart;

        var inExtraction = valuable.roomVolumeCheck != null 
            && valuable.roomVolumeCheck.inExtractionPoint;

        return inCart || inExtraction;
    }

    private static string CleanValuableName(string source)
    {
        var cleaned = CleanBasicName(source);
        
        if (cleaned.StartsWith("Valuable ", StringComparison.OrdinalIgnoreCase))
        {
            var firstSpace = cleaned.IndexOf(' ');
            
            if (firstSpace >= 0)
            {
                var secondSpace = cleaned.IndexOf(' ', firstSpace + 1);
                
                if (secondSpace >= 0 && secondSpace + 1 < cleaned.Length)
                {
                    return cleaned.Substring(secondSpace + 1).Trim();
                }
            }
        }

        return cleaned.Replace(" Valuable ", " ").Trim();
    }

    private static string CleanRoomName(string source)
    {
        var cleaned = StripPrefix(CleanBasicName(source), "Module - ");
        
        if (cleaned.IndexOf("Start Room", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Start Room";
        }

        var match = RoomMetadataPrefixRegex.Match(cleaned);
        
        if (match.Success)
        {
            var roomName = match.Groups[1].Value.Trim();
            
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                return roomName;
            }
        }

        return cleaned;
    }

    private static string CleanBasicName(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "Unknown";
        }

        return source.Replace("(Clone)", string.Empty).Trim();
    }

    private static string StripPrefix(string source, string prefix)
    {
        if (source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(prefix.Length).Trim();
        }

        return source;
    }

    private static IEnumerable<ValuableObject> GetAllValuableObjects()
    {
        var tracked = ValuableObjectPatch.Tracked;

        tracked.RemoveAll(v => !v);

        return tracked.Where(v => v.gameObject.activeInHierarchy);
    }

    private static IEnumerable<ValuableEntry> GetValuableEntries()
    {
        var includeDistance = ValuableList.Instance.ShowDistanceInMenuEnabled;

        var valuableEntries = GetAllValuableObjects()
            .Select(v => new ValuableEntry(
                CleanValuableName(v.gameObject.name),
                FloorDollarValue(v.dollarValueCurrent),
                GetRoomName(v),
                IsInCartOrExtraction(v),
                includeDistance ? GetDistanceFromPlayerMeters(v.transform) : 0f
            ));

        var cosmeticEntries = GetAllCosmeticBoxes()
            .Select(c => new ValuableEntry(
                GetCosmeticBoxDisplayName(c),
                0,
                GetCosmeticRoomName(c),
                IsInCartOrExtraction(c),
                includeDistance ? GetDistanceFromPlayerMeters(c.transform) : 0f,
                c.rarity
            ));

        return valuableEntries.Concat(cosmeticEntries);
    }

    private static IEnumerable<CosmeticWorldObject> GetAllCosmeticBoxes()
    {
        var roundDirector = RoundDirector.instance;

        if (roundDirector == null)
        {
            return Enumerable.Empty<CosmeticWorldObject>();
        }

        return roundDirector.cosmeticWorldObjects.Where(c => c && c.gameObject.activeInHierarchy);
    }

    private static bool IsInCartOrExtraction(CosmeticWorldObject cosmetic)
    {
        var inCart = cosmetic.physGrabObject != null
            && cosmetic.physGrabObject.impactDetector != null
            && cosmetic.physGrabObject.impactDetector.inCart;

        var inExtraction = cosmetic.roomVolumeCheck != null
            && cosmetic.roomVolumeCheck.inExtractionPoint;

        return inCart || inExtraction;
    }

    private static string GetCosmeticBoxDisplayName(CosmeticWorldObject cosmetic)
    {
        var cleaned = CleanBasicName(cosmetic.gameObject.name);

        if (string.IsNullOrWhiteSpace(cleaned)
            || string.Equals(cleaned, "Unknown", StringComparison.OrdinalIgnoreCase)
            || cleaned.IndexOf("Cosmetic", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return GetFallbackCosmeticBoxName(cosmetic.rarity);
        }

        return cleaned;
    }

    private static string GetFallbackCosmeticBoxName(SemiFunc.Rarity rarity)
    {
        return rarity switch
        {
            SemiFunc.Rarity.Common => "Cosmetic box - Common",
            SemiFunc.Rarity.Uncommon => "Cosmetic box - Uncommon",
            SemiFunc.Rarity.Rare => "Cosmetic box - Rare",
            SemiFunc.Rarity.UltraRare => "Cosmetic box - Ultra-rare",
            _ => "Cosmetic box",
        };
    }

    private static float GetDistanceFromPlayerMeters(Transform target)
    {
        var player = PlayerAvatar.instance;

        if (player == null || target == null)
        {
            return 0f;
        }

        return Vector3.Distance(player.transform.position, target.position);
    }
    
    public static string FormatPrice(int rawPrice)
    {
        if (ValuableList.Instance.RoundPricesEnabled)
        {
            var kOneDecimalFloored = Mathf.Floor(rawPrice / 100f) / 10f;

            return $"${kOneDecimalFloored.ToString("0.0", CultureInfo.InvariantCulture)}K";
        }

        return $"${rawPrice}";
    }

    internal static string FormatDistanceMetersOneDecimal(float meters)
    {
        var flooredTenth = Mathf.Floor(Mathf.Max(0f, meters) * 10f) / 10f;

        return flooredTenth.ToString("0.0", CultureInfo.InvariantCulture);
    }

    internal static string TruncateRoomNameForMenuDisplay(string roomName)
    {
        if (string.IsNullOrEmpty(roomName) || roomName.Length <= 28)
        {
            return roomName;
        }

        return roomName.Substring(0, 25) + "...";
    }

    private static int FloorDollarValue(float dollarValueCurrent)
    {
        return Mathf.FloorToInt(dollarValueCurrent);
    }
}

internal readonly struct ValuableEntry
{
    public string Name { get; }
    public int Price { get; }
    public string Room { get; }
    public bool IsInCartOrExtraction { get; }
    public float DistanceFromPlayerMeters { get; }
    public SemiFunc.Rarity? CosmeticBoxRarity { get; }
    public bool IsCosmeticBox => CosmeticBoxRarity.HasValue;

    internal ValuableEntry(string name, int price, string room, bool isInCartOrExtraction, float distanceFromPlayerMeters = 0f, SemiFunc.Rarity? cosmeticBoxRarity = null)
    {
        Name = name;
        Price = price;
        Room = room;
        IsInCartOrExtraction = isInCartOrExtraction;
        DistanceFromPlayerMeters = distanceFromPlayerMeters;
        CosmeticBoxRarity = cosmeticBoxRarity;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ValuableList;

internal static class ValuableUtils
{
    private static readonly Regex RoomMetadataPrefixRegex = new(
        @"^[^-]+?\s*-\s*[A-Z]+\s*-\s*\d+\s*-\s*(.+)$",
        RegexOptions.Compiled);

    internal static List<IGrouping<string, ValuableEntry>> GetGroupedValuables()
    {
        return UnityEngine.Object.FindObjectsOfType<ValuableObject>()
            .Where(v => v && v.gameObject.activeInHierarchy)
            .Select(v => new ValuableEntry(
                CleanValuableName(v.gameObject.name),
                Mathf.RoundToInt(v.dollarValueCurrent),
                GetRoomName(v),
                IsInCartOrExtraction(v)))
            .OrderBy(v => v.Room, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.IsInCartOrExtraction)
            .ThenBy(v => v.Price)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .GroupBy(v => v.Room)
            .ToList();
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
        var room = valuable.roomVolumeCheck?.CurrentRooms?
            .FirstOrDefault(r => r != null && r.Module != null);

        if (room?.Module == null)
        {
            return "Unknown";
        }

        return CleanRoomName(room.Module.name);
    }

    private static bool IsInCartOrExtraction(ValuableObject valuable)
    {
        var inCart = valuable.physGrabObject != null &&
            valuable.physGrabObject.impactDetector != null &&
            valuable.physGrabObject.impactDetector.inCart;

        var inExtraction = valuable.roomVolumeCheck != null &&
            valuable.roomVolumeCheck.inExtractionPoint;

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
}

internal readonly struct ValuableEntry
{
    public string Name { get; }
    public int Price { get; }
    public string Room { get; }
    public bool IsInCartOrExtraction { get; }

    internal ValuableEntry(string name, int price, string room, bool isInCartOrExtraction)
    {
        Name = name;
        Price = price;
        Room = room;
        IsInCartOrExtraction = isInCartOrExtraction;
    }
}
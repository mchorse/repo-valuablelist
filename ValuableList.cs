using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ValuableList;

[BepInPlugin("McHorse.ValuableList", "ValuableList", "1.3")]
[BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.HardDependency)]
public class ValuableList : BaseUnityPlugin
{
    internal static ValuableList Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }
    private ValuablesMenu? valuablesMenu;
    private ConfigEntry<KeyCode>? openMenuKeybind;
    private ConfigEntry<bool>? roundPrices;
    private ConfigEntry<bool>? showMostExpensiveValuable;
    private ConfigEntry<bool>? showDistanceInMenu;
    internal bool RoundPricesEnabled => roundPrices?.Value ?? true;
    internal bool ShowMostExpensiveHudLabelEnabled => showMostExpensiveValuable?.Value ?? true;
    internal bool ShowDistanceInMenuEnabled => showDistanceInMenu?.Value ?? false;

    private void Awake()
    {
        Instance = this;
        
        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        openMenuKeybind = Config.Bind(
            "Controls", 
            "Open Menu", 
            KeyCode.K, 
            "Keyboard key used to open the valuables menu."
        );
        roundPrices = Config.Bind(
            "General",
            "RoundPrices",
            true,
            "Round list prices to thousands with one decimal place (e.g. 15.4k)."
        );
        showMostExpensiveValuable = Config.Bind(
            "General",
            "ShowMostExpensiveValuable",
            false,
            "Show a HUD label with the most expensive valuable in your current room."
        );
        showDistanceInMenu = Config.Bind(
            "General",
            "ShowDistanceInMenu",
            false,
            "When enabled, the valuables menu appends distance from your player in meters to each line (HUD unchanged)."
        );

        Patch();
        valuablesMenu = new ValuablesMenu(Logger);

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }

    private void Update()
    {
        if (SemiFunc.RunIsLevel() && SemiFunc.NoTextInputsActive() && openMenuKeybind != null && Input.GetKeyDown(openMenuKeybind.Value))
        {
            valuablesMenu?.Toggle();
        }
    }
}
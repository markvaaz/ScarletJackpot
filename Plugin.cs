using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ProjectM;
using ScarletCore.Data;
using ScarletCore.Events;
using ScarletCore.Systems;
using ScarletJackpot.Services;
using VampireCommandFramework;

namespace ScarletJackpot;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("markvaaz.ScarletCore")]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin {
  static Harmony _harmony;
  public static Harmony Harmony => _harmony;
  public static Plugin Instance { get; private set; }
  public static ManualLogSource LogInstance { get; private set; }
  public static Settings Settings { get; private set; }
  public static Database Database { get; private set; }

  public override void Load() {
    Instance = this;
    LogInstance = Log;

    Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

    _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
    _harmony.PatchAll(Assembly.GetExecutingAssembly());

    if (GameSystems.Initialized) {
      OnInitialized(null, null);
    } else {
      EventManager.OnInitialize += OnInitialized;
    }

    Settings = new Settings(MyPluginInfo.PLUGIN_GUID, Instance);
    Database = new Database(MyPluginInfo.PLUGIN_GUID);

    LoadSettings();
    CommandRegistry.RegisterAll();
  }

  public override bool Unload() {
    _harmony?.UnpatchSelf();
    ActionScheduler.UnregisterAssembly(Assembly.GetExecutingAssembly());
    EventManager.UnregisterAssembly(Assembly.GetExecutingAssembly());
    CommandRegistry.UnregisterAssembly();
    return true;
  }

  public static void OnInitialized(object _, object __) {
    EventManager.OnInitialize -= OnInitialized;
    SlotService.Initialize();
  }

  public static void ReloadSettings() {
    Settings.Dispose();
    LoadSettings();
  }
  public static void LoadSettings() {
    Settings.Section("Spin Cost")
      .Add("CostPrefabGUID", 862477668, "The PrefabGUID of the item to be consumed for each spin.")
      .Add("MinAmount", 100, "The minimum amount of the item to be consumed for each spin.")
      .Add("MaxAmount", 1000, "The maximum amount of the item to be consumed for each spin.");

    Settings.Section("RTP Control")
      .Add("RTPRate", 0.85f, "Return to Player rate (0.0 to 1.0). Higher values = more player-friendly.")
      .Add("BaseWinChance", 0.15f, "Base chance for winning lines (0.0 to 1.0). Higher values = more wins.")
      .Add("EnableRTPControl", true, "Enable sophisticated RTP control system for casino-like behavior.");

    // TODO: REVERT PRIZE POOL GUIDS TO 0
    // 862477668 just for testing
    Settings.Section("Prize Pool")
      .Add("Fish", 862477668, "The PrefabGUID of the prize item for a row of fish.")
      .Add("FishAmount", 1, "The amount of items given for a row of fish.")
      .Add("DuskCaller", 862477668, "The PrefabGUID of the prize item for a row of DuskCaller.")
      .Add("DuskCallerAmount", 2, "The amount of items given for a row of DuskCaller.")
      .Add("Gem", 862477668, "The PrefabGUID of the prize item for a row of gems.")
      .Add("GemAmount", 5, "The amount of items given for a row of gems.")
      .Add("Jewel", 862477668, "The PrefabGUID of the prize item for a row of jewels.")
      .Add("JewelAmount", 5, "The amount of items given for a row of jewels.")
      .Add("MagicStone", 862477668, "The PrefabGUID of the prize item for a row of magic stones.")
      .Add("MagicAmount", 10, "The amount of items given for a row of magic stones.")
      .Add("DemonFragment", 862477668, "The PrefabGUID of the prize item for a row of demon fragments (JACKPOT).")
      .Add("DemonAmount", 50, "The amount of items given for a row of demon fragments.");

    Settings.Section("Rugged Hands")
      .Add("EnableRuggedHands", true, "If enabled, the Rugged Hands item will steal the current prizes from the slot machine (if any). (1% chance)");
  }
}
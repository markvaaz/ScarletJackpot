using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
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
    LoadSettings();
  }
  public static void LoadSettings() {
    Settings.Section("General")
      .Add("EnableAnimation", true, "Enable slot machine lighting animations.")
      .Add("EnableSound", true, "Enable slot machine sound effects.")
      .Add("EnableWinVoiceLine", true, "Enable voice line when player wins.")
      .Add("EnableRuggedHands", true, "If enabled, the Rugged Hands item will steal the current prizes from the slot machine (if any). (1% chance)");

    Settings.Section("Spin Cost")
      .Add("CostPrefabGUID", 862477668, "The PrefabGUID of the item to be consumed for each spin.")
      .Add("MinAmount", 100, "The minimum amount of the item to be consumed for each spin.")
      .Add("MaxAmount", 1000, "The maximum amount of the item to be consumed for each spin.")
      .Add("MaxBetMultiplier", 3f, "Maximum prize multiplier. Min bet = 1x multiplier, Max bet = this value. Example: 3.0 means max bet gives 3x more prizes than min bet.");

    Settings.Section("RTP Control")
      .Add("RTPRate", 0.85f, "Return to Player rate (0.0 to 1.0). Higher values = more player-friendly.")
      .Add("BaseWinChance", 0.15f, "Base chance for winning lines (0.0 to 1.0). Higher values = more wins.")
      .Add("EnableRTPControl", true, "Enable sophisticated RTP control system for casino-like behavior.");

    Settings.Section("Prize Pool")
      .Add("Fish", 0, "The PrefabGUID of the prize item for a row of fish.")
      .Add("FishAmount", 0, "The amount of items given for a row of fish.")
      .Add("DuskCaller", 0, "The PrefabGUID of the prize item for a row of DuskCaller.")
      .Add("DuskCallerAmount", 0, "The amount of items given for a row of DuskCaller.")
      .Add("Gem", 0, "The PrefabGUID of the prize item for a row of gems.")
      .Add("GemAmount", 0, "The amount of items given for a row of gems.")
      .Add("Jewel", 0, "The PrefabGUID of the prize item for a row of jewels.")
      .Add("JewelAmount", 0, "The amount of items given for a row of jewels.")
      .Add("MagicStone", 0, "The PrefabGUID of the prize item for a row of magic stones.")
      .Add("MagicAmount", 0, "The amount of items given for a row of magic stones.")
      .Add("DemonFragment", 0, "The PrefabGUID of the prize item for a row of demon fragments (JACKPOT).")
      .Add("DemonAmount", 0, "The amount of items given for a row of demon fragments.");
  }
}
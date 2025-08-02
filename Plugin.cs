﻿using BepInEx;
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
    _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

    if (GameSystems.Initialized) {
      OnInitialized(null, null);
    } else {
      EventManager.OnInitialize += OnInitialized;
    }

    Settings = new Settings(MyPluginInfo.PLUGIN_GUID, Instance);
    Database = new Database(MyPluginInfo.PLUGIN_GUID);
    CommandRegistry.RegisterAll();
  }

  public override bool Unload() {
    _harmony?.UnpatchSelf();
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
    Settings.Section("General")
      .Add("Enable", true, "Enable or disable the plugin");
  }

  /*
    [CommandGroup("groupname")]
    public class CommandGroup
    {
      [Command("commandname", "Description of the command")]
      public static void CommandName(CommandContext context)
      {
        // Command implementation
        context.Reply("Command executed successfully!");
      }
    }
  */

}

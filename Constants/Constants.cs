using ScarletCore.Data;
using Stunlock.Core;

namespace ScarletJackpot.Constants;

internal static class Constants {
  public static readonly PrefabGUID SCT_PREFAB = new(-1404311249);
  public static readonly PrefabGUID INTERACT_INSPECT = new(222103866);
  public static readonly PrefabGUID WIN_INDICATOR = new(-113436752);
  public static readonly PrefabGUID RAGHANDS_WIN_INDICATOR = new(1216450741);
  public static readonly PrefabGUID RAGHANDS_PREFAB = new(1216450741);
  public static PrefabGUID SPIN_COST_PREFAB => new PrefabGUID(Plugin.Settings.Get<int>("CostPrefabGUID"));
  public const string SlotId = "__ScarletJackpot.Slot__";
  public static int SPIN_MIN_AMOUNT => Plugin.Settings.Get<int>("MinAmount");
  public static int SPIN_MAX_AMOUNT => Plugin.Settings.Get<int>("MaxAmount");
  public static float BASE_WIN_CHANCE => Plugin.Settings.Get<float>("BaseWinChance");
  public static float MAX_BET_MULTIPLIER => Plugin.Settings.Get<float>("MaxBetMultiplier");
  public static bool ANIMATION_ENABLED => Plugin.Settings.Get<bool>("EnableAnimation");
  public static bool SOUND_ENABLED => Plugin.Settings.Get<bool>("EnableSound");
  public static bool VOICE_LINE_ENABLED => Plugin.Settings.Get<bool>("EnableWinVoiceLine");
}

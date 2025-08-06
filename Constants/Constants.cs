using ScarletCore.Data;
using Stunlock.Core;

namespace ScarletJackpot.Constants;

internal static class Constants {
  public static readonly PrefabGUID SCT_PREFAB = new(-1404311249);
  public static readonly PrefabGUID INTERACT_INSPECT = new(222103866);
  public static readonly PrefabGUID WIN_INDICATOR = new(-113436752);
  public static readonly PrefabGUID SlotInteractBuff = new(1405487786);
  public static PrefabGUID SPIN_COST_PREFAB => new PrefabGUID(Plugin.Settings.Get<int>("CostPrefabGUID"));
  public static int SPIN_MIN_AMOUNT => Plugin.Settings.Get<int>("MinAmount");
  public static int SPIN_MAX_AMOUNT => Plugin.Settings.Get<int>("MaxAmount");
  public const string SlotId = "__ScarletJackpot.Slot__";
  public static float RTP_RATE => Plugin.Settings.Get<float>("RTPRate");
  public static float BASE_WIN_CHANCE => Plugin.Settings.Get<float>("BaseWinChance");
  public static bool ENABLE_RTP_CONTROL => Plugin.Settings.Get<bool>("EnableRTPControl");
}

global using static ScarletJackpot.Constants.Constants;
global using ScarletCore;
global using static GameData;
internal static class GameData {
  public static Unity.Collections.NativeParallelHashMap<Stunlock.Core.PrefabGUID, Unity.Entities.Entity> PrefabGuidToEntityMap => ScarletCore.Systems.GameSystems.PrefabCollectionSystem._PrefabGuidToEntityMap;
}
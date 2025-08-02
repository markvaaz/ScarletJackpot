using HarmonyLib;
using ProjectM;
using ScarletCore.Utils;
using Unity.Collections;

namespace ScarletMarket.Patches;

[HarmonyPatch(typeof(NameableInteractableSystem), nameof(NameableInteractableSystem.OnUpdate))]
internal static class InteractPatch {
  [HarmonyPrefix]
  public static void Prefix(NameableInteractableSystem __instance) {
    var query = __instance._RenameQuery.ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      Log.Components(entity);
    }
  }
}
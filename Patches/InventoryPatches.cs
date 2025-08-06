using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletJackpot.Services;
using ScarletCore.Services;

namespace ScarletJackpot.Patches;

[HarmonyPatch]
internal static class InventoryPatches {
  [HarmonyPatch(typeof(MoveItemBetweenInventoriesSystem), nameof(MoveItemBetweenInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(MoveItemBetweenInventoriesSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601321_0.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var moveItemEvent = entity.Read<MoveItemBetweenInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) ||
            !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        if (!SlotService.Has(toInv) && !SlotService.Has(fromInv)) continue;

        var toInvIsSlotMachine = SlotService.Has(toInv);
        var fromCharacter = entity.Read<FromCharacter>();

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) {
          entity.Destroy(true);
          continue;
        }

        var player = fromCharacter.Character.GetPlayerData();

        if (toInvIsSlotMachine && InventoryService.TryGetItemAtSlot(fromInv, moveItemEvent.FromSlot, out var item) && item.ItemType == SPIN_COST_PREFAB && item.Amount >= SPIN_MIN_AMOUNT && item.Amount <= SPIN_MAX_AMOUNT) {
          var slot = SlotService.Get(toInv);

          // Verificar se o jogador é o atual da slot machine
          if (slot.CurrentPlayer != fromCharacter.Character) {
            var playerData = fromCharacter.Character.GetPlayerData();
            if (playerData != null) {
              MessageService.Send(playerData, "You must be interacting with the slot machine to place bets!");
            }
            entity.Destroy(true);
            continue;
          }

          SlotService.SetBetAmount(player, item.Amount);
        }

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in MoveItemBetweenInventoriesSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(DropInventoryItemSystem), nameof(DropInventoryItemSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(DropInventoryItemSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_1470978904_0.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var dropItemEvent = entity.Read<DropInventoryItemEvent>();

        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(dropItemEvent.Inventory, out Entity inv)) continue;

        if (!SlotService.Has(inv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in DropInventoryItemSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SortSingleInventorySystem), nameof(SortSingleInventorySystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SortSingleInventorySystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance._EventQuery.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var sortEvent = entity.Read<SortSingleInventoryEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(sortEvent.Inventory, out Entity inv)) continue;

        if (!SlotService.Has(inv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in SortSingleInventorySystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SortAllInventoriesSystem), nameof(SortAllInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SortAllInventoriesSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601798_0.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var sortEvent = entity.Read<SortAllInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(sortEvent.Inventory, out Entity inv)) continue;

        if (!SlotService.Has(inv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in SortAllInventoriesSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(MoveAllItemsBetweenInventoriesSystem), nameof(MoveAllItemsBetweenInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(MoveAllItemsBetweenInventoriesSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601579_0.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var moveItemEvent = entity.Read<MoveAllItemsBetweenInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) || !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        if (!SlotService.Has(toInv) && !SlotService.Has(fromInv)) continue;

        if (toInv.IsPlayer()) {
          var player = toInv.GetPlayerData();

          if (SlotService.HasBet(player.PlatformId)) {
            var betAmount = SlotService.GetBetAmount(player.PlatformId);

            var slot = SlotService.Get(fromInv);

            // Verificar se a slot machine já está em execução
            if (slot.IsRunning) {
              Log.Info("Slot machine is already running - bet rejected!");
              entity.Destroy(true);
              continue;
            }

            // Verificar se este jogador é o atual da slot machine
            if (slot.CurrentPlayer != toInv) {
              var playerData = toInv.GetPlayerData();
              if (playerData != null) {
                MessageService.Send(playerData, "You must be interacting with the slot machine to spin!");
              }
              entity.Destroy(true);
              continue;
            }

            if (InventoryService.HasAmount(toInv, SPIN_COST_PREFAB, betAmount)) {
              InventoryService.RemoveItem(toInv, SPIN_COST_PREFAB, betAmount);
              slot.InitializeSlotAnimation(toInv);
            }
          }

        }

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in MoveAllItemsBetweenInventoriesSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(MoveAllItemsBetweenInventoriesV2System), nameof(MoveAllItemsBetweenInventoriesV2System.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(MoveAllItemsBetweenInventoriesV2System __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601631_0.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var moveItemEvent = entity.Read<MoveAllItemsBetweenInventoriesEventV2>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) || !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        if (!SlotService.Has(toInv) && !SlotService.Has(fromInv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in MoveAllItemsBetweenInventoriesV2SystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SplitItemSystem), nameof(SplitItemSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SplitItemSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var splitEvent = entity.Read<SplitItemEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(splitEvent.Inventory, out Entity inv)) continue;

        if (!SlotService.Has(inv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in SplitItemSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SplitItemV2System), nameof(SplitItemV2System.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SplitItemV2System __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var splitEvent = entity.Read<SplitItemEventV2>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(splitEvent.FromInventory, out Entity fromInv) || !niem.TryGetValue(splitEvent.ToInventory, out Entity toInv)) continue;

        if (!SlotService.Has(toInv) && !SlotService.Has(fromInv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in SplitItemV2SystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(SmartMergeItemsBetweenInventoriesSystem), nameof(SmartMergeItemsBetweenInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(SmartMergeItemsBetweenInventoriesSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601682_0.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var moveItemEvent = entity.Read<SmartMergeItemsBetweenInventoriesEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(moveItemEvent.ToInventory, out Entity toInv) || !niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInv)) continue;

        if (!SlotService.Has(toInv) && !SlotService.Has(fromInv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in SmartMergeItemsBetweenInventoriesSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(EquipItemFromInventorySystem), nameof(EquipItemFromInventorySystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(EquipItemFromInventorySystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var equipEvent = entity.Read<EquipItemFromInventoryEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(equipEvent.FromInventory, out Entity fromInv)) continue;

        if (!SlotService.Has(fromInv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in UnEquipItemSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }

  [HarmonyPatch(typeof(UnEquipItemSystem), nameof(UnEquipItemSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(UnEquipItemSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        var unequipEvent = entity.Read<UnequipItemEvent>();
        var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;

        if (!niem.TryGetValue(unequipEvent.ToInventory, out Entity toInv)) continue;

        if (!SlotService.Has(toInv)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in UnEquipItemSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }
}

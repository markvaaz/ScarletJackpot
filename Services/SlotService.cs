using System.Collections.Generic;
using ProjectM;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletJackpot.Models;
using Unity.Collections;
using Unity.Entities;

namespace ScarletJackpot.Services;

internal static class SlotService {
  public static Dictionary<Entity, SlotModel> FromSlot { get; set; } = [];
  public static Dictionary<Entity, SlotModel> FromSlotChest { get; set; } = [];

  public static void Initialize() {
    FromSlot.Clear();
    FromSlotChest.Clear();

    EntityQueryBuilder queryBuilder = new(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;
      if (entity.IdEquals(Ids.Slot)) {
        var slot = new SlotModel(entity);
        Register(slot);
      }
    }
  }

  public static void Register(SlotModel slot) {
    if (slot == null || slot.Slot == Entity.Null || slot.SlotChest == Entity.Null) return;

    FromSlot[slot.Slot] = slot;
    FromSlotChest[slot.SlotChest] = slot;
  }

  public static void Unregister(SlotModel slot) {
    if (slot == null || slot.Slot == Entity.Null || slot.SlotChest == Entity.Null) return;

    FromSlot.Remove(slot.Slot);
    FromSlotChest.Remove(slot.SlotChest);
  }

  public static SlotModel Get(Entity slot) {
    if (slot == Entity.Null) return null;

    if (FromSlot.TryGetValue(slot, out var model)) {
      return model;
    }

    if (FromSlotChest.TryGetValue(slot, out model)) {
      return model;
    }

    return null;
  }

  public static bool Has(Entity slot) {
    if (slot == Entity.Null) return false;

    return FromSlot.ContainsKey(slot) || FromSlotChest.ContainsKey(slot);
  }

  public static void ClearAll() {
    EntityQueryBuilder queryBuilder = new(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;
      if (entity.IdEquals(Ids.Slot)) {
        if (entity.Has<Follower>()) {
          var follower = entity.Read<Follower>().Followed._Value;
          follower.Destroy();
        }
        entity.Destroy();
      }
    }
  }
}
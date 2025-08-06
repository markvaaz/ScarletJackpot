using System.Collections.Generic;
using ProjectM;
using ScarletCore.Data;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletJackpot.Models;
using ScarletJackpot.Patches;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ScarletJackpot.Services;

internal static class SlotService {
  public static Dictionary<Entity, SlotModel> FromSlot { get; set; } = [];
  public static Dictionary<Entity, SlotModel> FromSlotChest { get; set; } = [];
  public static Dictionary<ulong, int> CurrentBetAmount { get; set; } = new();

  public static void Initialize() {
    FromSlot.Clear();
    FromSlotChest.Clear();

    EntityQueryBuilder queryBuilder = new(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (!entity.Has<NameableInteractable>()) continue;
      if (entity.IdEquals(SlotId)) {
        var slot = new SlotModel(entity);
        Register(slot);
      }
    }
  }

  public static void SetBetAmount(PlayerData player, int amount) {
    var playerId = player.PlatformId;
    var multiplier = SlotGameLogic.CalculateBetMultiplier(amount);
    player.SendMessage($"Bet set to ~{amount}~ (Prize multiplier: ~{multiplier:F2}x~)".FormatSuccess());
    CurrentBetAmount[playerId] = amount;
  }

  public static bool HasBet(ulong playerId) {
    return CurrentBetAmount.ContainsKey(playerId);
  }

  public static int GetBetAmount(ulong playerId) {
    if (CurrentBetAmount.TryGetValue(playerId, out var amount)) {
      return amount;
    }
    return 0;
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

  /// <summary>
  /// Finds the slot machine that the player is currently hovering over
  /// </summary>
  /// <param name="player">Player entity</param>
  /// <returns>SlotModel if found, null otherwise</returns>
  public static SlotModel GetSlotFromPlayerHover(Entity player) {
    if (player == Entity.Null || !player.Has<EntityInput>()) {
      return null;
    }

    var entityInput = player.Read<EntityInput>();
    var hoveredEntity = entityInput.HoveredEntity;

    if (hoveredEntity == Entity.Null) {
      return null;
    }

    return Get(hoveredEntity);
  }

  /// <summary>
  /// Finds the closest slot machine to the player's cursor position in the world
  /// </summary>
  /// <param name="player">Player entity</param>
  /// <param name="maxDistance">Maximum search distance from cursor</param>
  /// <returns>Closest SlotModel if found within range, null otherwise</returns>
  public static SlotModel GetSlotFromPlayerCursor(Entity player, float maxDistance = 5f) {
    if (player == Entity.Null || !player.Has<EntityInput>()) {
      return null;
    }

    var entityInput = player.Read<EntityInput>();
    var cursorWorldPosition = entityInput.AimPosition;

    SlotModel closestSlot = null;
    float closestDistance = float.MaxValue;

    foreach (var slot in FromSlot.Values) {
      if (slot == null || slot.Position.Equals(float3.zero)) continue;

      float distance = math.distance(cursorWorldPosition, slot.Position);
      if (distance <= maxDistance && distance < closestDistance) {
        closestDistance = distance;
        closestSlot = slot;
      }
    }

    return closestSlot;
  }

  /// <summary>
  /// Finds the closest slot machine to the player within a specified range
  /// </summary>
  /// <param name="playerPosition">Player position</param>
  /// <param name="maxDistance">Maximum search distance</param>
  /// <returns>Closest SlotModel if found within range, null otherwise</returns>
  public static SlotModel GetClosestSlot(float3 playerPosition, float maxDistance = 10f) {
    SlotModel closestSlot = null;
    float closestDistance = float.MaxValue;

    foreach (var slot in FromSlot.Values) {
      if (slot == null || slot.Position.Equals(float3.zero)) continue;

      float distance = math.distance(playerPosition, slot.Position);
      if (distance <= maxDistance && distance < closestDistance) {
        closestDistance = distance;
        closestSlot = slot;
      }
    }

    return closestSlot;
  }

  public static void ClearAll() {
    EntityQueryBuilder queryBuilder = new(Allocator.Temp);
    queryBuilder.AddAll(ComponentType.ReadOnly<NameableInteractable>());
    queryBuilder.WithOptions(EntityQueryOptions.IncludeDisabled);

    var query = GameSystems.EntityManager.CreateEntityQuery(ref queryBuilder).ToEntityArray(Allocator.Temp);

    foreach (var slot in query) {
      if (!slot.Has<NameableInteractable>()) continue;
      if (slot.IdEquals(SlotId)) {
        if (slot.Has<Follower>()) {
          var slotChest = slot.Read<Follower>().Followed._Value;

          if (slotChest.Has<Follower>()) {
            var lamp = slotChest.Read<Follower>().Followed._Value;
            lamp.Destroy();
          }
          slotChest.Destroy();
        }

        slot.Destroy();
      }
    }
  }

  public static void CheckAndClearInactivePlayers() {
    // Limpar jogadores inativos no InteractPatch
    InteractPatch.CleanupInactivePlayers();

    // Limpar jogadores inativos nos slots
    foreach (var slot in FromSlotChest.Values) {
      if (slot.HasCurrentPlayer()) {
        // Se o jogador atual não está mais interagindo, limpar
        if (!slot.IsPlayerInteracting(slot.CurrentPlayer)) {
          slot.ClearCurrentPlayer();
        }
      }
    }
  }
}
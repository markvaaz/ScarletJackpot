using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Shared;
using ScarletCore.Services;
using ScarletCore.Utils;
using ScarletJackpot.Constants;
using ScarletJackpot.Services;
using Unity.Collections;
using Unity.Entities;

namespace ScarletJackpot.Patches;

[HarmonyPatch(typeof(InteractValidateAndStopSystemServer), nameof(InteractValidateAndStopSystemServer.OnUpdate))]
public static class InteractPatch {
  private static readonly Dictionary<Entity, Entity> _lastKnownPlayer = new(); // Para rastrear mudanças

  [HarmonyPrefix]
  public static void Prefix(InteractValidateAndStopSystemServer __instance) {
    var query = __instance.__query_195794971_3.ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (entity.GetPrefabGuid() != Buffs.SlotInteractBuff) continue;

      var interactingPlayer = entity.Read<EntityOwner>().Owner;

      if (interactingPlayer == Entity.Null || !interactingPlayer.Has<PlayerCharacter>()) continue;

      var slot = interactingPlayer.Read<Interactor>().Target;

      if (!SlotService.FromSlotChest.TryGetValue(slot, out var slotModel)) continue;

      // Verificar se já processamos este jogador para este slot
      // if (_lastKnownPlayer.TryGetValue(slot, out var lastPlayer) && lastPlayer == interactingPlayer) {
      //   continue; // Mesmo jogador, não precisa reprocessar
      // }

      // Tentar definir este jogador como o atual
      bool canUseSlot = slotModel.SetCurrentPlayer(interactingPlayer);

      if (canUseSlot) {
        // Sucesso - atualizar o último jogador conhecido
        _lastKnownPlayer[slot] = interactingPlayer;
      } else {
        // Outro jogador já está usando - cancelar interação
        var playerData = interactingPlayer.GetPlayerData();
        if (playerData != null) {
          MessageService.Send(playerData, "Slot machine is being used by another player!".FormatError());
        }
        CancelInteraction(interactingPlayer);
      }
    }

    query.Dispose();
  }

  public static void CancelInteraction(Entity entity) {
    if (!entity.Exists()) return;

    BuffService.TryRemoveBuff(entity, Buffs.SlotInteractBuff);
  }

  // Limpar jogadores que não estão mais interagindo
  public static void CleanupInactivePlayers() {
    var slotsToRemove = new List<Entity>();

    foreach (var kvp in _lastKnownPlayer) {
      var slot = kvp.Key;
      var player = kvp.Value;

      if (!SlotService.FromSlotChest.TryGetValue(slot, out var slotModel)) {
        slotsToRemove.Add(slot);
        continue;
      }

      if (!slotModel.IsPlayerInteracting(player)) {
        slotsToRemove.Add(slot);
        slotModel.ClearCurrentPlayer();
      }
    }

    foreach (var slot in slotsToRemove) {
      _lastKnownPlayer.Remove(slot);
    }
  }
}
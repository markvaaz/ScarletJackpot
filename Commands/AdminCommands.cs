using System;
using System.Collections.Generic;
using ScarletCore.Services;
using ScarletCore.Utils;
using ScarletCore.Systems;
using ScarletCore.Data;
using ScarletJackpot.Models;
using ScarletJackpot.Services;
using VampireCommandFramework;
using Unity.Entities;
using Unity.Mathematics;
using ProjectM;

namespace ScarletJackpot.Commands;

[CommandGroup("slot")]
public static class AdminCommands {
  private static readonly Dictionary<PlayerData, ActionId> _selectedActions = new();

  [Command("create", adminOnly: true)]
  public static void CreateSlot(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.");
      return;
    }

    SlotService.Register(new SlotModel(player.Position));
    ctx.Reply("Slot machine created at your position.".FormatSuccess());
  }

  [Command("iwanttoremoveeverything", adminOnly: true)]
  public static void ClearAllSlots(ChatCommandContext ctx) {
    SlotService.ClearAll();
    ctx.Reply("All slot machines cleared.".FormatSuccess());
  }

  [Command("rotate", adminOnly: true)]
  public static void RotateSlotOnMouse(ChatCommandContext ctx, int steps = 1) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    // Get slot machine closest to player's cursor position
    var slot = SlotService.GetSlotFromPlayerCursor(player.CharacterEntity, 5f);
    if (slot == null) {
      ctx.Reply("No slot machine found near cursor. Please aim closer to a slot machine (within 5 units).".FormatError());
      return;
    }

    if (slot.IsRunning) {
      ctx.Reply("Cannot rotate slot machine while it's spinning!".FormatError());
      return;
    }

    // Normalize steps to 0-3 range
    steps = ((steps % 4) + 4) % 4;

    slot.RotateSlot(steps);

    string direction = steps switch {
      1 => "90° clockwise",
      2 => "180°",
      3 => "270° clockwise (90° counter-clockwise)",
      _ => "back to original position"
    };

    ctx.Reply($"Slot machine rotated {direction}.".FormatSuccess());
  }

  [Command("rotateclosest", adminOnly: true)]
  public static void RotateClosestSlot(ChatCommandContext ctx, int steps = 1) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    // Get closest slot machine to player
    var slot = SlotService.GetClosestSlot(player.Position, 15f);
    if (slot == null) {
      ctx.Reply("No slot machine found within 15 units of your position.".FormatError());
      return;
    }

    if (slot.IsRunning) {
      ctx.Reply("Cannot rotate slot machine while it's spinning!".FormatError());
      return;
    }

    // Normalize steps to 0-3 range
    steps = ((steps % 4) + 4) % 4;

    slot.RotateSlot(steps);

    string direction = steps switch {
      1 => "90° clockwise",
      2 => "180°",
      3 => "270° clockwise (90° counter-clockwise)",
      _ => "back to original position"
    };

    ctx.Reply($"Closest slot machine rotated {direction}.".FormatSuccess());
  }

  [Command("remove", adminOnly: true)]
  public static void RemoveSlotOnMouse(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    // First try to get slot from cursor position
    var slot = SlotService.GetSlotFromPlayerCursor(player.CharacterEntity, 5f);
    if (slot != null) {
      if (slot.IsRunning) {
        ctx.Reply("Cannot remove slot machine while it's spinning!".FormatError());
        return;
      }

      SlotService.Unregister(slot);

      // Destroy both entities
      if (slot.SlotChest.Exists()) {
        slot.SlotChest.Destroy();
      }
      if (slot.Slot.Exists()) {
        slot.Slot.Destroy();
      }

      ctx.Reply("Slot machine removed successfully.".FormatSuccess());
      return;
    }

    // Fallback to hovered entity if no slot found near cursor
    player.CharacterEntity.With((ref EntityInput entityInput) => {
      var hoveredEntity = entityInput.HoveredEntity;

      if (hoveredEntity == Entity.Null) {
        ctx.Reply("No slot machine found near cursor and no entity found under cursor.".FormatError());
        return;
      }

      hoveredEntity.Destroy();
      ctx.Reply("Entity under cursor removed.".FormatSuccess());
    });
  }

  [Command("move", shortHand: "m", adminOnly: true)]
  public static void MoveSlotCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".FormatError());
      return;
    }

    if (_selectedActions.ContainsKey(playerData)) {
      ctx.Reply($"You are already moving a slot machine.".FormatError());
      return;
    }

    // Try to get slot from cursor position first
    var slot = SlotService.GetSlotFromPlayerCursor(playerData.CharacterEntity, 5f);
    if (slot == null) {
      // Fallback to hovered entity
      var hoveredEntity = playerData.CharacterEntity.Read<EntityInput>().HoveredEntity;
      if (!hoveredEntity.Exists()) {
        ctx.Reply($"Please aim at the slot machine you want to move.".FormatError());
        return;
      }

      slot = SlotService.Get(hoveredEntity);
      if (slot == null) {
        ctx.Reply($"The targeted entity is not a slot machine.".FormatError());
        return;
      }
    }

    if (slot.IsRunning) {
      ctx.Reply($"Cannot move slot machine while it's spinning!".FormatError());
      return;
    }

    _selectedActions[playerData] = ActionScheduler.OncePerFrame((end) => {
      var inp = playerData.CharacterEntity.Read<EntityInput>();

      if (inp.State.InputsDown == SyncedButtonInputAction.Primary) {
        end();
        _selectedActions.Remove(playerData);

        // Update slot position and move entities
        var newPosition = inp.AimPosition;
        slot.MoveSlot(newPosition);

        ctx.Reply("Slot machine moved.".FormatSuccess());
        return;
      }

      // Preview position - move entities to cursor position
      var previewPosition = inp.AimPosition;
      if (slot.Slot.Exists()) {
        slot.Slot.SetPosition(previewPosition);
      }
      if (slot.SlotChest.Exists()) {
        var rotatedChestPos = previewPosition + math.mul(slot.Rotation, slot.SlotChestOffset);
        slot.SlotChest.SetPosition(rotatedChestPos);
      }
    });

    // Auto-cancel after 3 minutes
    ActionScheduler.Delayed(() => {
      if (!_selectedActions.ContainsKey(playerData)) return;
      ActionScheduler.CancelAction(_selectedActions[playerData]);
      _selectedActions.Remove(playerData);
      ctx.Reply("Move operation cancelled due to timeout.".FormatError());
    }, 180);

    ctx.Reply($"You are now moving the slot machine. ~Click to place it~.".FormatSuccess());
  }
}

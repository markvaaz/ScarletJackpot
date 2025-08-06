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
using System.Collections.Generic;
using Stunlock.Core;
using Unity.Transforms;
using ProjectM.CastleBuilding;

namespace ScarletJackpot.Commands;

[CommandGroup("slot")]
public static class Commands {
  private static readonly Dictionary<PlayerData, ActionId> _selectedActions = new();
  [Command("create")]
  public static void Test(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.");
      return;
    }

    SlotService.Register(new SlotModel(player.Position));
  }

  [Command("setbet")]
  public static void SetBet(ChatCommandContext ctx, int amount) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.");
      return;
    }

    if (amount < SPIN_MIN_AMOUNT) {
      ctx.Reply($"Bet amount too low. Minimum: {SPIN_MIN_AMOUNT}");
      return;
    }

    if (amount > SPIN_MAX_AMOUNT) {
      ctx.Reply($"Bet amount too high. Maximum: {SPIN_MAX_AMOUNT}");
      return;
    }

    SlotService.SetBetAmount(player.PlatformId, amount);
    ctx.Reply($"Bet amount set to {amount} for player {player.Name}.");
  }

  [Command("clear")]
  public static void ClearSlots(ChatCommandContext ctx) {
    SlotService.ClearAll();
  }

  [Command("rotate")]
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

  [Command("rotateclosest")]
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

  [Command("removeOnMouse")]
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

  [Command("debug")]
  public static void DebugCursorPosition(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.".FormatError());
      return;
    }

    if (!player.CharacterEntity.Has<EntityInput>()) {
      ctx.Reply("Player does not have EntityInput component.".FormatError());
      return;
    }

    var entityInput = player.CharacterEntity.Read<EntityInput>();
    var cursorPos = entityInput.AimPosition;
    var playerPos = player.Position;

    ctx.Reply($"Player Position: ({playerPos.x:F2}, {playerPos.y:F2}, {playerPos.z:F2})".FormatSuccess());
    ctx.Reply($"Cursor Position: ({cursorPos.x:F2}, {cursorPos.y:F2}, {cursorPos.z:F2})".FormatSuccess());

    // Find closest slot to cursor
    var slot = SlotService.GetSlotFromPlayerCursor(player.CharacterEntity, 10f);
    if (slot != null) {
      var distance = math.distance(cursorPos, slot.Position);
      ctx.Reply($"Closest slot at: ({slot.Position.x:F2}, {slot.Position.y:F2}, {slot.Position.z:F2}), Distance: {distance:F2}".FormatSuccess());
    } else {
      ctx.Reply("No slot found within 10 units of cursor.".FormatError());
    }

    // Check hovered entity
    var hoveredEntity = entityInput.HoveredEntity;
    if (hoveredEntity != Entity.Null) {
      ctx.Reply($"Hovered Entity: {hoveredEntity}".FormatSuccess());
    } else {
      ctx.Reply("No hovered entity.".FormatError());
    }
  }

  [Command("move", shortHand: "m")]
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

public static class Testando {
  private static readonly Dictionary<PlayerData, ActionId> _selectedActions = [];

  [Command("spawnentity", shortHand: "se")]
  public static void SpawnEntityCommand(ChatCommandContext ctx, string prefabGuid) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".FormatError());
      return;
    }

    if (!PrefabGUID.TryParse(prefabGuid, out var guid)) {
      ctx.Reply($"Invalid Prefab GUID: {prefabGuid}".FormatError());
      return;
    }

    UnitSpawnerService.ImmediateSpawn(guid, playerData.Position, 0f, 0f, -1f);

    ctx.Reply($"Entity spawned successfully".FormatSuccess());
  }

  [Command("spawnmoving", shortHand: "sm")]
  public static void SpawnMovingEntityCommand(ChatCommandContext ctx, string prefabGuid) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".FormatError());
      return;
    }

    if (_selectedActions.ContainsKey(playerData)) {
      ctx.Reply($"You are already moving an entity.".FormatError());
      return;
    }

    if (!PrefabGUID.TryParse(prefabGuid, out var guid)) {
      ctx.Reply($"Invalid Prefab GUID: {prefabGuid}".FormatError());
      return;
    }

    var entity = UnitSpawnerService.ImmediateSpawn(guid, playerData.Position, 0f, 0f, -1f);
    var currentRotationStep = 0;

    _selectedActions[playerData] = ActionScheduler.OncePerFrame((end) => {
      var inp = playerData.CharacterEntity.Read<EntityInput>();

      if (inp.State.InputsDown == SyncedButtonInputAction.Primary) {
        end();
        _selectedActions.Remove(playerData);
        ctx.Reply($"Entity moved.".FormatSuccess());
        return;
      }

      // Check for R key press to rotate
      if (inp.State.InputsDown == SyncedButtonInputAction.OffensiveSpell) {
        currentRotationStep = (currentRotationStep + 1) % 4;

        var quaternions = new quaternion[] {
          quaternion.identity,
          quaternion.RotateY(math.radians(90f)),
          quaternion.RotateY(math.radians(180f)),
          quaternion.RotateY(math.radians(270f))
        };

        var newRotation = quaternions[currentRotationStep];

        entity.With((ref Rotation rot) => {
          rot.Value = newRotation;
        });
      }

      entity.SetPosition(inp.AimPosition);
    });

    // Auto-cancel after 3 minutes
    ActionScheduler.Delayed(() => {
      if (!_selectedActions.ContainsKey(playerData)) return;
      ActionScheduler.CancelAction(_selectedActions[playerData]);
      _selectedActions.Remove(playerData);
      ctx.Reply("Move operation cancelled due to timeout.".FormatError());
    }, 180);

    ctx.Reply($"You are now moving an entity. ~Click to place it~. Press ~R to rotate~.".FormatSuccess());
  }

  [Command("moveentity", shortHand: "me")]
  public static void MoveEntityCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".FormatError());
      return;
    }

    if (_selectedActions.ContainsKey(playerData)) {
      ctx.Reply($"You are already moving an entity.".FormatError());
      return;
    }

    var hoveredEntity = playerData.CharacterEntity.Read<EntityInput>().HoveredEntity;

    if (!hoveredEntity.Exists()) {
      ctx.Reply($"Please aim at the entity you want to move.".FormatError());
      return;
    }

    _selectedActions[playerData] = ActionScheduler.OncePerFrame((end) => {
      var inp = playerData.CharacterEntity.Read<EntityInput>();

      if (inp.State.InputsDown == SyncedButtonInputAction.Primary) {
        end();
        _selectedActions.Remove(playerData);
        ctx.Reply($"Entity moved.".FormatSuccess());
        return;
      }

      hoveredEntity.SetPosition(inp.AimPosition);
    });

    // Auto-cancel after 3 minutes
    ActionScheduler.Delayed(() => {
      if (!_selectedActions.ContainsKey(playerData)) return;
      ActionScheduler.CancelAction(_selectedActions[playerData]);
      _selectedActions.Remove(playerData);
      ctx.Reply("Move operation cancelled due to timeout.".FormatError());
    }, 180);

    ctx.Reply($"You are now moving an entity. ~Click to place it~.".FormatSuccess());
  }

  [Command("lamp")]
  public static void LampCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".FormatError());
      return;
    }

    var lampEntity = UnitSpawnerService.ImmediateSpawn(new PrefabGUID(-1023837449), playerData.Position, 0f, 0f, -1f);
    byte index = 0;

    ActionScheduler.RepeatingFrames(() => {
      // 0 to 9 % module
      index = (byte)((index + 1) % 10);

      lampEntity.With((ref DyeableCastleObject dyeable) => {
        dyeable.PrevColorIndex = dyeable.ActiveColorIndex;
        dyeable.ActiveColorIndex = index;
      });
    }, 5);

    ctx.Reply($"Lamp spawned successfully at your position.".FormatSuccess());
  }
}
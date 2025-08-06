using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Shared;
using ProjectM.Tiles;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletJackpot.Constants;
using ScarletJackpot.Services;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ScarletJackpot.Models;

internal class SlotModel {
  private const int SPIN_TIMEOUT_SECONDS = 60;
  private const int DELAYED_FRAMES = 10;

  public int[] ItemColumns { get; } = [1, 3, 5];
  public float3 SlotChestOffset => new(0f, 0f, 0.5f);
  public Entity SlotChest { get; private set; }
  public Entity Slot { get; private set; }
  public Entity Lamp { get; private set; }
  public Entity Dummy { get; private set; }
  public bool IsRunning { get; private set; } = false;
  public Entity CurrentPlayer { get; private set; } = Entity.Null;
  private ActionId _spinTimeoutActionId;

  public float3 Position { get; private set; }
  public quaternion Rotation => Slot.Read<Rotation>().Value;

  private readonly SlotGameLogic _gameLogic;

  private static Entity _defaultStandEntity;
  public static Entity DefaultSlotEntity {
    get {
      if (_defaultStandEntity == Entity.Null) {
        if (!PrefabGuidToEntityMap.TryGetValue(Spawnable.SlotChest, out var defaultStand)) {
          return Entity.Null;
        }
        _defaultStandEntity = defaultStand;
      }
      return _defaultStandEntity;
    }
  }

  public SlotModel(float3 position) {
    var adjustedPosition = AdjustPosition(position);
    Position = adjustedPosition;

    CreateSlotEntity(adjustedPosition);
    CreateSlotChest(adjustedPosition);
    CreateLampEntity(adjustedPosition);
    CreateDummyEntity(adjustedPosition);
    BindAll();

    _gameLogic = new SlotGameLogic(this);
    ActionScheduler.DelayedFrames(() => _gameLogic.PopulateSlots(), DELAYED_FRAMES);
  }

  public SlotModel(Entity slotEntity) {
    if (slotEntity == Entity.Null || !slotEntity.Has<NameableInteractable>()) {
      return;
    }

    LoadAll(slotEntity);

    if (Slot.Has<LocalToWorld>()) {
      Position = Slot.Read<LocalToWorld>().Position;
    }

    _gameLogic = new SlotGameLogic(this);
  }

  public bool HasCurrentPlayer() => CurrentPlayer != Entity.Null;

  public void LoadAll(Entity slotEntity) {
    Slot = slotEntity;
    if (Slot.Has<Follower>()) {
      SlotChest = slotEntity.Read<Follower>().Followed._Value;
      if (SlotChest.Has<Follower>()) {
        Lamp = SlotChest.Read<Follower>().Followed._Value;
        if (Lamp.Has<Follower>()) {
          Dummy = Lamp.Read<Follower>().Followed._Value;
        }
      }
    }
  }

  private void BindAll() {
    if (Slot == Entity.Null || SlotChest == Entity.Null) {
      return;
    }

    if (!Slot.Has<Follower>()) {
      Slot.Add<Follower>();
    }

    if (!SlotChest.Has<Follower>()) {
      SlotChest.Add<Follower>();
    }

    if (!Lamp.Has<Follower>()) {
      Lamp.Add<Follower>();
    }

    Slot.With((ref Follower follower) => {
      follower.Followed._Value = SlotChest;
    });

    SlotChest.AddWith((ref Follower follower) => {
      follower.Followed._Value = Lamp;
    });

    Lamp.With((ref Follower follower) => {
      follower.Followed._Value = Dummy;
    });
  }

  public bool IsPlayerInteracting(Entity player) {
    if (player == Entity.Null || !player.Has<PlayerCharacter>()) return false;

    var interactor = player.Read<Interactor>();
    return BuffService.HasBuff(player, SlotInteractBuff) && interactor.Target != Entity.Null && interactor.Target == SlotChest;
  }

  public bool SetCurrentPlayer(Entity player) {
    if (IsRunning && player != CurrentPlayer) {
      Log.Info(1);
      return false;
    }

    if (CurrentPlayer == Entity.Null) {
      Log.Info(2);
      CurrentPlayer = player;
      StartSpinTimeout();
      return true;
    }

    if (!IsPlayerInteracting(CurrentPlayer)) {
      Log.Info(3);
      CurrentPlayer = player;
      StartSpinTimeout();
      return true;
    }

    if (CurrentPlayer == player) {
      if (_spinTimeoutActionId == default) {
        StartSpinTimeout();
      }
      return true;
    }

    Log.Info(5);

    return false;
  }

  public void ClearCurrentPlayer() {
    if (!IsRunning) {
      CurrentPlayer = Entity.Null;
      CancelSpinTimeout();
    }
  }

  private void StartSpinTimeout() {
    CancelSpinTimeout();

    _spinTimeoutActionId = ActionScheduler.Delayed(HandleSpinTimeout, SPIN_TIMEOUT_SECONDS);
  }

  private void CancelSpinTimeout() {
    if (_spinTimeoutActionId != default) {
      ActionScheduler.CancelAction(_spinTimeoutActionId);
      _spinTimeoutActionId = default;
    }
  }

  private void HandleSpinTimeout() {
    if (CurrentPlayer != Entity.Null) {
      var playerData = CurrentPlayer.GetPlayerData();

      if (!IsPlayerInteracting(CurrentPlayer)) {
        return;
      }

      playerData.SendMessage($"Slot machine timedout.".FormatError());
      BuffService.TryRemoveBuff(CurrentPlayer, SlotInteractBuff);
    }

    ClearCurrentPlayer();
  }

  internal void OnAnimationFinished() {
    IsRunning = false;
  }

  private static float3 AdjustPosition(float3 position) {
    var offset = 0;
    return new float3(
      math.round(position.x * 2f) / 2f + offset,
      position.y,
      math.round(position.z * 2f) / 2f + offset
    );
  }

  private void CreateLampEntity(float3 position) {
    Lamp = UnitSpawnerService.ImmediateSpawn(Spawnable.Lamp, position, 0f, 0f);

    if (Lamp != Entity.Null) {
      Lamp.With((ref EditableTileModel editableTileModel) => {
        editableTileModel.CanDismantle = false;
        editableTileModel.CanMoveAfterBuild = false;
        editableTileModel.CanRotateAfterBuild = false;
      });

      BuffService.TryApplyBuff(Lamp, Buffs.Invulnerable, -1);
      BuffService.TryApplyBuff(Lamp, Buffs.Immaterial, -1);
    } else {
      Log.Error("Failed to spawn Lamp entity for Slot machine.");
    }
  }

  public void ChangeLampColor(byte colorIndex) {
    Lamp.With((ref DyeableCastleObject dyeable) => {
      dyeable.PrevColorIndex = dyeable.ActiveColorIndex;
      dyeable.ActiveColorIndex = colorIndex;
    });
  }

  private void CreateDummyEntity(float3 position) {
    Dummy = UnitSpawnerService.ImmediateSpawn(Spawnable.Dummy, position, 0f, 0f);

    BuffService.TryApplyBuff(Dummy, Buffs.Invulnerable, -1);
    BuffService.TryApplyBuff(Dummy, Buffs.Invisibility, -1);
    BuffService.TryApplyBuff(Dummy, Buffs.Immaterial, -1);
  }

  private void CreateSlotEntity(float3 position) {
    Slot = UnitSpawnerService.ImmediateSpawn(Spawnable.Slot, position, 0f, 0f);
    ConfigureSlotEntity();
  }

  private void ConfigureSlotEntity() {
    Slot.With((ref Interactable interactable) => interactable.Disabled = true);
    Slot.SetId(SlotId);
    BuffService.TryApplyBuff(Slot, Buffs.Invulnerable, -1);

    Slot.With((ref EditableTileModel editableTileModel) => {
      editableTileModel.CanDismantle = false;
      editableTileModel.CanMoveAfterBuild = false;
      editableTileModel.CanRotateAfterBuild = false;
    });
  }

  private void CreateSlotChest(float3 position) {
    var oldSize = GetContainerSize(Spawnable.SlotChest);
    SetContainerSize(Spawnable.SlotChest, 21);
    SlotChest = UnitSpawnerService.ImmediateSpawn(Spawnable.SlotChest, position + SlotChestOffset, 0f, 0f);
    ConfigureSlotChest();
    SetContainerSize(Spawnable.SlotChest, oldSize);
  }

  private void ConfigureSlotChest() {
    SlotChest.With((ref EditableTileModel editableTileModel) => {
      editableTileModel.CanDismantle = false;
      editableTileModel.CanMoveAfterBuild = false;
      editableTileModel.CanRotateAfterBuild = false;
    });

    BuffService.TryApplyBuff(SlotChest, Buffs.Immaterial, -1);
    BuffService.TryApplyBuff(SlotChest, Buffs.Invulnerable, -1);
    BuffService.TryApplyBuff(SlotChest, Buffs.Invisibility, -1);
  }

  public void InitializeSlotAnimation() {
    if (IsRunning) {
      return;
    }

    IsRunning = true;

    StartSpinTimeout();

    _gameLogic.StartAnimation();
  }

  public void InitializeSlotAnimation(Entity player) {
    if (IsRunning) {
      if (player != Entity.Null && player.Has<PlayerCharacter>()) {
        var playerData = player.GetPlayerData();
        if (playerData != null) {
          MessageService.Send(playerData, "Slot machine is spinning... wait!");
        }
      }
      return;
    }

    CurrentPlayer = player;
    InitializeSlotAnimation();
  }

  public void AnimateAllColumnsSync() {
    _gameLogic.AnimateAllColumnsSync();
  }

  public void PopulateSlotsColumns() {
    _gameLogic.PopulateSlots();
  }

  public void StopColumnAnimation(int columnIndex) {
    if (columnIndex >= 0 && columnIndex < ItemColumns.Length) {

    }
  }

  public void StartManualColumnAnimation(int columnIndex, int iterations = 10) {
    if (columnIndex >= 0 && columnIndex < ItemColumns.Length) {

    }
  }

  public static void SetAllItemsMaxAmount(Entity entity, int maxAmount) {
    var inventoryBuffer = InventoryService.GetInventoryItems(entity);

    for (int i = 0; i < inventoryBuffer.Length; i++) {
      var item = inventoryBuffer[i];
      item.MaxAmountOverride = maxAmount;
      inventoryBuffer[i] = item;
    }
  }

  public static void SetContainerSize(PrefabGUID prefabGUID, int slots) {
    if (!PrefabGuidToEntityMap.TryGetValue(prefabGUID, out var prefabEntity)) {
      return;
    }

    if (!prefabEntity.TryGetBuffer<InventoryInstanceElement>(out var instanceBuffer))
      return;

    var inventoryInstanceElement = instanceBuffer[0];
    inventoryInstanceElement.RestrictedCategory = (long)ItemCategory.ALL;
    inventoryInstanceElement.Slots = slots;
    inventoryInstanceElement.MaxSlots = slots;
    instanceBuffer[0] = inventoryInstanceElement;
  }

  public static int GetContainerSize(PrefabGUID prefabGUID) {
    if (!PrefabGuidToEntityMap.TryGetValue(prefabGUID, out var prefabEntity)) {
      return -1;
    }

    if (prefabEntity.TryGetBuffer<InventoryInstanceElement>(out var instanceBuffer)) {
      return instanceBuffer[0].MaxSlots;
    }

    return -1;
  }

  public static void RotateTile(Entity tileEntity, int rotationStep) {
    var (tileRotation, newRotation) = GetRotationValues(rotationStep);
    var tilePosition = tileEntity.Read<TilePosition>();

    ApplyTileRotation(tileEntity, tileRotation, tilePosition);
    ApplyTransformRotation(tileEntity, newRotation);
  }

  private static (TileRotation tileRotation, quaternion newRotation) GetRotationValues(int rotationStep) {
    var tileRotations = new[] {
      TileRotation.None,
      TileRotation.Clockwise_90,
      TileRotation.Clockwise_180,
      TileRotation.Clockwise_270
    };

    var quaternions = new quaternion[] {
      quaternion.identity,
      quaternion.RotateY(math.radians(90f)),
      quaternion.RotateY(math.radians(180f)),
      quaternion.RotateY(math.radians(270f))
    };

    return (tileRotations[rotationStep], quaternions[rotationStep]);
  }

  private static void ApplyTileRotation(Entity tileEntity, TileRotation tileRotation, TilePosition tilePosition) {
    tileEntity.With((ref TilePosition tilePos) => tilePos.TileRotation = tileRotation);

    tileEntity.With((ref TileModelSpatialData spatialData) =>
        spatialData.LastTilePosition = tilePosition);

    tileEntity.With((ref StaticTransformCompatible compatible) =>
        compatible.NonStaticTransform_Rotation = tileRotation);
  }

  private static void ApplyTransformRotation(Entity tileEntity, quaternion newRotation) {
    tileEntity.Write(new Rotation { Value = newRotation });

    tileEntity.With((ref LocalTransform localTransform) =>
        localTransform.Rotation = newRotation);

    tileEntity.With((ref LocalToWorld localToWorld) => {
      var (right, forward, up) = CalculateRotationVectors(newRotation);
      var currentPosition = localToWorld.Position;

      localToWorld.Value = new float4x4(
        new float4(right.x, up.x, forward.x, currentPosition.x),
        new float4(right.y, up.y, forward.y, currentPosition.y),
        new float4(right.z, up.z, forward.z, currentPosition.z),
        new float4(0f, 0f, 0f, 1f)
      );
    });
  }

  private static (float3 right, float3 forward, float3 up) CalculateRotationVectors(quaternion rotation) {
    var right = math.mul(rotation, new float3(1f, 0f, 0f));
    var forward = math.mul(rotation, new float3(0f, 0f, 1f));
    var up = new float3(0f, 1f, 0f);

    return (right, forward, up);
  }

  public void Destroy() {
    if (SlotChest.Exists()) {
      SlotChest.Destroy();
    }
    if (Slot.Exists()) {
      Slot.Destroy();
    }
    if (Lamp.Exists()) {
      Lamp.Destroy();
    }
    if (Dummy.Exists()) {
      Dummy.Destroy();
    }
  }

  public void RotateSlot(int rotationSteps) {
    if (IsRunning) {
      return;
    }

    rotationSteps = ((rotationSteps % 4) + 4) % 4;

    var quaternions = new quaternion[] {
      quaternion.identity,
      quaternion.RotateY(math.radians(90f)),
      quaternion.RotateY(math.radians(180f)),
      quaternion.RotateY(math.radians(270f))
    };

    var targetRotation = quaternions[rotationSteps];
    ApplyRotationToEntities(rotationSteps, targetRotation);
  }

  public void AlignToRotation(quaternion targetRotation) {
    if (IsRunning) {
      return;
    }

    var quaternions = new quaternion[] {
      quaternion.identity,
      quaternion.RotateY(math.radians(90f)),
      quaternion.RotateY(math.radians(180f)),
      quaternion.RotateY(math.radians(270f))
    };

    var forward = math.mul(targetRotation, new float3(0, 0, 1));
    var threshold = 0.4f;

    int rotationStep = 0;
    if (forward.z > threshold) rotationStep = 0;
    else if (forward.x > threshold) rotationStep = 1;
    else if (forward.z < -threshold) rotationStep = 2;
    else if (forward.x < -threshold) rotationStep = 3;

    var finalRotation = quaternions[rotationStep];
    ApplyRotationToEntities(rotationStep, finalRotation);
  }

  private void ApplyRotationToEntities(int rotationStep, quaternion rotation) {
    var center = Position;

    if (Slot.Exists()) {
      Slot.SetPosition(center);
      RotateTile(Slot, rotationStep);
    }

    if (SlotChest.Exists()) {
      var rotatedChestPos = center + math.mul(rotation, SlotChestOffset);
      SlotChest.SetPosition(rotatedChestPos);
      RotateTile(SlotChest, rotationStep);
    }

    if (Lamp.Exists()) {
      Lamp.SetPosition(center);
      RotateTile(Lamp, rotationStep);
    }
  }

  /// <summary>
  /// Gets the current rotation step (0-3) of the slot machine
  /// </summary>
  /// <returns>Current rotation step</returns>
  public int GetRotationStep() {
    var forward = math.mul(Rotation, new float3(0, 0, 1));
    var threshold = 0.4f;

    if (forward.z > threshold) return 0;      // North
    else if (forward.x > threshold) return 1;  // East
    else if (forward.z < -threshold) return 2; // South
    else if (forward.x < -threshold) return 3; // West

    return 0; // Default to north
  }

  /// <summary>
  /// Moves the slot machine to a new position
  /// </summary>
  /// <param name="newPosition">New position for the slot machine</param>
  public void MoveSlot(float3 newPosition) {
    if (IsRunning) {
      return;
    }

    var adjustedPosition = AdjustPosition(newPosition);
    Position = adjustedPosition;

    // Move the main slot entity
    if (Slot.Exists()) {
      Slot.SetPosition(adjustedPosition);
    }

    // Move and reposition the slot chest with current rotation
    if (SlotChest.Exists()) {
      var rotatedChestPos = adjustedPosition + math.mul(Rotation, SlotChestOffset);
      SlotChest.SetPosition(rotatedChestPos);
    }

    // Move the lamp entity (positioned at same location as slot)
    if (Lamp.Exists()) {
      Lamp.SetPosition(adjustedPosition);
    }
  }
}
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

namespace ScarletJackpot.Models;

internal class SlotModel {
  private const int SPIN_TIMEOUT_SECONDS = 30;
  private const int DELAYED_FRAMES = 10;

  public int[] ItemColumns { get; } = [1, 3, 5];
  public float3 SlotChestOffset => new(0f, 0f, 0.5f);
  public Entity SlotChest { get; private set; }
  public Entity Slot { get; private set; }
  public Entity Lamp { get; private set; }
  public bool IsRunning { get; private set; } = false;
  public Entity CurrentPlayer { get; private set; } = Entity.Null;
  private ActionId _spinTimeoutActionId;

  // Rotation properties
  public float3 Position { get; private set; }
  public quaternion Rotation => Slot.Read<Rotation>().Value;

  // Game logic handler
  private SlotGameLogic _gameLogic;

  private static Entity _defaultStandEntity;
  public static Entity DefaultSlotEntity {
    get {
      if (_defaultStandEntity == Entity.Null) {
        if (!PrefabGuidToEntityMap.TryGetValue(Spawnable.SlotChest, out var defaultStand)) {
          Log.Error($"Failed to find prefab for GUID: {Spawnable.SlotChest.GuidHash}");
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
    BindSlotWithChest();

    // Initialize game logic
    _gameLogic = new SlotGameLogic(this);
    ActionScheduler.DelayedFrames(() => _gameLogic.PopulateSlots(), DELAYED_FRAMES);
  }

  public SlotModel(Entity slotEntity) {
    if (slotEntity == Entity.Null || !slotEntity.Has<NameableInteractable>()) {
      Log.Error("Invalid slot entity provided.");
      return;
    }

    Slot = slotEntity;
    SlotChest = slotEntity.Read<Follower>().Followed._Value;
    Lamp = SlotChest.Read<Follower>().Followed._Value;

    // Store position from existing entity
    if (Slot.Has<LocalToWorld>()) {
      Position = Slot.Read<LocalToWorld>().Position;
    }

    // Initialize game logic
    _gameLogic = new SlotGameLogic(this);
  }

  public bool HasCurrentPlayer() => CurrentPlayer != Entity.Null;

  public bool IsPlayerInteracting(Entity player) {
    if (player == Entity.Null || !player.Has<PlayerCharacter>()) return false;

    var interactor = player.Read<Interactor>();
    return BuffService.HasBuff(player, SlotInteractBuff) && interactor.Target != Entity.Null && interactor.Target == SlotChest;
  }

  public bool SetCurrentPlayer(Entity player) {
    if (IsRunning) return false; // Não pode trocar jogador durante spin

    // Se não há jogador atual, define este
    if (CurrentPlayer == Entity.Null) {
      CurrentPlayer = player;
      return true;
    }

    // Se o jogador atual não está mais interagindo, pode trocar
    if (!IsPlayerInteracting(CurrentPlayer)) {
      CurrentPlayer = player;
      return true;
    }

    // Se é o mesmo jogador, mantém
    if (CurrentPlayer == player) {
      return true;
    }

    // Outro jogador já está usando
    return false;
  }

  public void ClearCurrentPlayer() {
    if (!IsRunning) { // Só limpa se não estiver rodando
      CurrentPlayer = Entity.Null;
      CancelSpinTimeout();
    }
  }

  private void StartSpinTimeout() {
    CancelSpinTimeout(); // Cancelar timeout anterior se existir

    // Agendar timeout usando ActionScheduler.Delayed (em segundos)
    _spinTimeoutActionId = ActionScheduler.Delayed(HandleSpinTimeout, SPIN_TIMEOUT_SECONDS);
  }

  private void CancelSpinTimeout() {
    if (_spinTimeoutActionId != default) {
      ActionScheduler.CancelAction(_spinTimeoutActionId);
      _spinTimeoutActionId = default;
    }
  }

  private void HandleSpinTimeout() {
    if (!IsRunning) return;

    // Parar a animação e limpar estado
    IsRunning = false;

    // Enviar mensagem ao jogador
    if (CurrentPlayer != Entity.Null && CurrentPlayer.Has<PlayerCharacter>()) {
      var playerData = CurrentPlayer.GetPlayerData();
      if (playerData != null) {
        MessageService.Send(playerData, $"Slot machine spin timed out after {SPIN_TIMEOUT_SECONDS} seconds. Animation stopped.".FormatError());
      }
    }

    // Limpar jogador atual
    CurrentPlayer = Entity.Null;

    // Resetar slot machine para estado inicial
    _gameLogic.PopulateSlots();
  }

  /// <summary>
  /// Called by SlotGameLogic when animation finishes
  /// </summary>
  internal void OnAnimationFinished() {
    IsRunning = false;
    CancelSpinTimeout();
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

    SlotChest.AddWith((ref Follower follower) => {
      follower.Followed._Value = Lamp;
    });

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

  private void CreateSlotEntity(float3 position) {
    Slot = UnitSpawnerService.ImmediateSpawn(Spawnable.Slot, position, 0f, 0f);
    ConfigureSlotEntity();
  }

  private void BindSlotWithChest() {
    if (Slot == Entity.Null || SlotChest == Entity.Null) {
      Log.Error("Slot or SlotChest is not initialized properly.");
      return;
    }

    if (!Slot.Has<Follower>()) {
      Slot.Add<Follower>();
    }

    Slot.With((ref Follower follower) => {
      follower.Followed._Value = SlotChest;
    });
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

    // Iniciar timeout para cancelar se o spin não completar
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
      // This functionality could be delegated to SlotGameLogic if needed
    }
  }

  #region Static Entity Utilities

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
      Log.Error($"Failed to find prefab for GUID: {prefabGUID.GuidHash}");
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
      Log.Error($"Failed to find prefab for GUID: {prefabGUID.GuidHash}");
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

  #region Rotation Methods

  /// <summary>
  /// Rotates the slot machine by the specified number of 90-degree steps
  /// </summary>
  /// <param name="rotationSteps">Number of 90-degree steps (0-3)</param>
  public void RotateSlot(int rotationSteps) {
    if (IsRunning) {
      Log.Warning("Cannot rotate slot machine while spinning!");
      return;
    }

    // Normalize rotation steps to 0-3 range
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

  /// <summary>
  /// Rotates the slot machine to align with a specific quaternion rotation
  /// </summary>
  /// <param name="targetRotation">Target rotation quaternion</param>
  public void AlignToRotation(quaternion targetRotation) {
    if (IsRunning) {
      Log.Warning("Cannot rotate slot machine while spinning!");
      return;
    }

    var quaternions = new quaternion[] {
      quaternion.identity,
      quaternion.RotateY(math.radians(90f)),
      quaternion.RotateY(math.radians(180f)),
      quaternion.RotateY(math.radians(270f))
    };

    // Find closest rotation step
    var forward = math.mul(targetRotation, new float3(0, 0, 1));
    var threshold = 0.4f;

    int rotationStep = 0;
    if (forward.z > threshold) rotationStep = 0;      // North
    else if (forward.x > threshold) rotationStep = 1;  // East
    else if (forward.z < -threshold) rotationStep = 2; // South
    else if (forward.x < -threshold) rotationStep = 3; // West

    var finalRotation = quaternions[rotationStep];
    ApplyRotationToEntities(rotationStep, finalRotation);
  }

  /// <summary>
  /// Applies rotation to Slot, SlotChest, and Lamp entities
  /// </summary>
  /// <param name="rotationStep">Rotation step (0-3)</param>
  /// <param name="rotation">Target quaternion rotation</param>
  private void ApplyRotationToEntities(int rotationStep, quaternion rotation) {
    var center = Position;

    // Rotate the main slot entity
    if (Slot.Exists()) {
      Slot.SetPosition(center);
      RotateTile(Slot, rotationStep);
    }

    // Rotate and reposition the slot chest
    if (SlotChest.Exists()) {
      var rotatedChestPos = center + math.mul(rotation, SlotChestOffset);
      SlotChest.SetPosition(rotatedChestPos);
      RotateTile(SlotChest, rotationStep);
    }

    // Rotate the lamp entity (positioned at same location as slot)
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
      Log.Warning("Cannot move slot machine while spinning!");
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

  #endregion
  #endregion
}
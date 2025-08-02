using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using ProjectM.Network;
using ProjectM.Tiles;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ScarletJackpot.Models;

internal class SlotModel {
  private const int TOTAL_COLUMNS = 7;
  private const int TOTAL_ROWS = 3;
  private const int ANIMATION_INITIAL_FRAME_SPEED = 2;
  private const int ANIMATION_STOP_ITERATIONS = 75;
  private const int DELAYED_FRAMES = 10;
  private const int COLUMN_START_DELAY = 5;
  private const int COLUMN_SPEED_OFFSET = 1;

  public int[] ItemColumns { get; } = [1, 3, 5];
  public float3 SlotChestOffset => new(0f, 0f, 0.5f);
  public Entity SlotChest { get; private set; }
  public Entity Slot { get; private set; }
  public bool IsRunning { get; private set; } = false;
  public Entity CurrentPlayer { get; private set; } = Entity.Null;

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

    CreateSlotEntity(adjustedPosition);
    CreateSlotChest(adjustedPosition);
    BindSlotWithChest();
    ActionScheduler.DelayedFrames(PopulateSlotsColumns, DELAYED_FRAMES);
  }

  public SlotModel(Entity slotEntity) {
    if (slotEntity == Entity.Null || !slotEntity.Has<NameableInteractable>()) {
      Log.Error("Invalid slot entity provided.");
      return;
    }

    Slot = slotEntity;
    SlotChest = slotEntity.Read<Follower>().Followed._Value;
  }

  private static float3 AdjustPosition(float3 position) {
    var offset = 0;
    return new float3(
      math.round(position.x * 2f) / 2f + offset,
      position.y,
      math.round(position.z * 2f) / 2f + offset
    );
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
    Slot.SetId(Ids.Slot);
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
    IsRunning = true;
    ActionScheduler.DelayedFrames(() => StartStaggeredAnimation(), DELAYED_FRAMES);
  }

  public void InitializeSlotAnimation(Entity player) {
    CurrentPlayer = player;
    InitializeSlotAnimation();
  }

  private void StartStaggeredAnimation() {
    for (int i = 0; i < ItemColumns.Length; i++) {
      int columnIndex = i;
      int column = ItemColumns[i];
      int startDelay = i * COLUMN_START_DELAY;
      int columnSpeed = ANIMATION_INITIAL_FRAME_SPEED;

      ActionScheduler.DelayedFrames(() => {
        StartColumnAnimation(column, columnSpeed, columnIndex);
      }, startDelay);
    }
  }

  private void StartColumnAnimation(int column, int initialSpeed, int columnIndex) {
    AnimateColumnWithDelay(column, 0, initialSpeed, columnIndex);
  }

  private void AnimateColumnWithDelay(int column, int iteration, int frameSpeed, int columnIndex) {
    if (iteration >= ANIMATION_STOP_ITERATIONS) {
      // Check if this is the last column to finish animating
      if (columnIndex == ItemColumns.Length - 1) {
        ProcessSlotResults();
      }
      IsRunning = false;
      return;
    }

    AnimateSingleColumn(column);

    int nextFrameSpeed = frameSpeed;

    if (iteration >= 60) {
      nextFrameSpeed = frameSpeed + 1;
    }

    ActionScheduler.DelayedFrames(() => {
      AnimateColumnWithDelay(column, iteration + 1, nextFrameSpeed, columnIndex);
    }, nextFrameSpeed);
  }

  private void AnimateSingleColumn(int column) {
    var random = new System.Random();
    ProcessColumnAnimation(column, random);
  }

  [System.Obsolete("Use AnimateSingleColumn para animações individuais por coluna")]
  public void AnimateSlotColumns() {
    var random = new System.Random();

    foreach (var col in ItemColumns) {
      ProcessColumnAnimation(col, random);
    }
  }

  public void AnimateAllColumnsSync() {
    var random = new System.Random();

    foreach (var col in ItemColumns) {
      ProcessColumnAnimation(col, random);
    }
  }

  private void ProcessColumnAnimation(int col, System.Random random) {
    var currentItems = GetCurrentColumnItems(col);
    RemoveBottomItem(col);
    MoveItemsDown(col);
    AddNewTopItem(col, currentItems, random);
  }

  private List<PrefabGUID> GetCurrentColumnItems(int col) {
    var currentItems = new List<PrefabGUID>();

    for (int row = 0; row < TOTAL_ROWS; row++) {
      int slotIndex = row * TOTAL_COLUMNS + col;
      InventoryService.TryGetItemAtSlot(SlotChest, slotIndex, out var item);
      currentItems.Add(item.ItemType);
    }

    return currentItems;
  }

  private void RemoveBottomItem(int col) {
    int lastSlotIndex = (TOTAL_ROWS - 1) * TOTAL_COLUMNS + col;
    InventoryService.RemoveItemAtSlot(SlotChest, lastSlotIndex);
  }

  private void MoveItemsDown(int col) {
    for (int row = TOTAL_ROWS - 1; row > 0; row--) {
      int fromIndex = (row - 1) * TOTAL_COLUMNS + col;
      int toIndex = row * TOTAL_COLUMNS + col;

      InventoryService.RemoveItemAtSlot(SlotChest, toIndex);

      if (InventoryService.TryGetItemAtSlot(SlotChest, fromIndex, out var item)) {
        InventoryService.AddWithMaxAmount(SlotChest, toIndex, item.ItemType, 1, 1);
      }
    }
  }

  private void AddNewTopItem(int col, List<PrefabGUID> currentItems, System.Random random) {
    var newItem = SelectNewItemWithWeight(currentItems, random);
    int topSlotIndex = 0 * TOTAL_COLUMNS + col;

    InventoryService.RemoveItemAtSlot(SlotChest, topSlotIndex);
    InventoryService.AddWithMaxAmount(SlotChest, topSlotIndex, newItem, 1, 1);
    SetAllItemsMaxAmount(SlotChest, 1);
  }

  private static PrefabGUID SelectNewItemWithWeight(List<PrefabGUID> currentItems, System.Random random) {
    var used = new HashSet<PrefabGUID>(currentItems);
    used.Remove(default);

    // Try to get a weighted item that's not already in the column
    var availableItems = SlotItems.WeightedItems.Where(kvp => !used.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    if (availableItems.Count == 0) {
      // If all items are used, use the full weighted list
      availableItems = SlotItems.WeightedItems;
    }

    return GetWeightedRandomItem(availableItems, random);
  }

  // Novo método para controlar chances de vitória como cassino real
  private static PrefabGUID SelectNewItemWithWinControl(List<PrefabGUID> currentItems, System.Random random, bool allowWinningCombination = true) {
    var used = new HashSet<PrefabGUID>(currentItems);
    used.Remove(default);

    // Se não queremos permitir combinação vencedora, evitar itens que já estão na coluna
    if (!allowWinningCombination && used.Count > 0) {
      var availableItems = SlotItems.WeightedItems.Where(kvp => !used.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

      if (availableItems.Count > 0) {
        return GetWeightedRandomItem(availableItems, random);
      }
    }

    // Caso contrário, usar seleção normal com peso
    var allAvailableItems = SlotItems.WeightedItems.Where(kvp => !used.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    if (allAvailableItems.Count == 0) {
      allAvailableItems = SlotItems.WeightedItems;
    }

    return GetWeightedRandomItem(allAvailableItems, random);
  }

  private static PrefabGUID GetWeightedRandomItem(Dictionary<PrefabGUID, int> weightedItems, System.Random random) {
    int totalWeight = weightedItems.Values.Sum();
    int randomValue = random.Next(totalWeight);
    int currentWeight = 0;

    foreach (var item in weightedItems) {
      currentWeight += item.Value;
      if (randomValue < currentWeight) {
        return item.Key;
      }
    }

    return weightedItems.Keys.First();
  }

  public void StopColumnAnimation(int columnIndex) {
    if (columnIndex >= 0 && columnIndex < ItemColumns.Length) {

    }
  }

  public void StartManualColumnAnimation(int columnIndex, int iterations = 10) {
    if (columnIndex >= 0 && columnIndex < ItemColumns.Length) {
      int column = ItemColumns[columnIndex];
      int speed = ANIMATION_INITIAL_FRAME_SPEED + (columnIndex * COLUMN_SPEED_OFFSET);

      AnimateColumnWithDelay(column, 0, speed, columnIndex, iterations);
    }
  }

  private void AnimateColumnWithDelay(int column, int iteration, int frameSpeed, int columnIndex, int maxIterations) {
    if (iteration >= maxIterations) return;

    AnimateSingleColumn(column);

    int nextFrameSpeed = frameSpeed + (iteration / 3);

    ActionScheduler.DelayedFrames(() => {
      AnimateColumnWithDelay(column, iteration + 1, nextFrameSpeed, columnIndex, maxIterations);
    }, nextFrameSpeed);
  }

  #region Win Detection and Rewards
  private void ProcessSlotResults() {
    var wins = DetectWins();
    var raghandsWin = wins.ContainsValue(new PrefabGUID(1216450741)); // raghands GUID

    if (raghandsWin) {
      // Raghands steals all wins - no rewards
      Log.Info("Raghands appeared! All wins stolen!");
      return;
    }

    if (wins.Count > 0) {
      Log.Info($"Player won on {wins.Count} line(s)!");
      DeliverWinRewards(wins);
    } else {
      Log.Info("No wins this spin.");
    }
  }

  private Dictionary<int, PrefabGUID> DetectWins() {
    var wins = new Dictionary<int, PrefabGUID>();
    var random = new System.Random();

    for (int row = 0; row < TOTAL_ROWS; row++) {
      var rowItems = GetRowItems(row);

      // Check if all 3 items in the row are the same and not null
      if (rowItems[0] != default && rowItems[0] == rowItems[1] && rowItems[1] == rowItems[2]) {

        // Aplicar controle de probabilidade de vitória (como cassino real)
        if (SlotItems.ShouldFormWinningLine(rowItems[0], random)) {
          wins[row] = rowItems[0];
          Log.Info($"Win detected on row {row}: {rowItems[0].GuidHash}");
        } else {
          Log.Info($"Potential win on row {row} blocked by RTP control: {rowItems[0].GuidHash}");
          // Opcional: substituir um item para quebrar a linha
          BreakWinningLine(row, random);
        }
      }
    }

    return wins;
  }

  // Método para quebrar linhas vencedoras (controle de RTP)
  private void BreakWinningLine(int row, System.Random random) {
    // Escolher uma coluna aleatória para substituir
    int columnToBreak = random.Next(ItemColumns.Length);
    int col = ItemColumns[columnToBreak];
    int slotIndex = row * TOTAL_COLUMNS + col;

    // Remover o item atual
    InventoryService.RemoveItemAtSlot(SlotChest, slotIndex);

    // Adicionar um item diferente
    var currentRowItems = GetRowItems(row).ToList();
    var newItem = SelectDifferentItem(currentRowItems[0], random);

    InventoryService.AddWithMaxAmount(SlotChest, slotIndex, newItem, 1, 1);
    SetAllItemsMaxAmount(SlotChest, 1);
  }

  private PrefabGUID SelectDifferentItem(PrefabGUID avoidItem, System.Random random) {
    var availableItems = SlotItems.WeightedItems.Where(kvp => kvp.Key != avoidItem).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    return GetWeightedRandomItem(availableItems, random);
  }

  private PrefabGUID[] GetRowItems(int row) {
    var items = new PrefabGUID[3];

    for (int i = 0; i < ItemColumns.Length; i++) {
      int col = ItemColumns[i];
      int slotIndex = row * TOTAL_COLUMNS + col;

      if (InventoryService.TryGetItemAtSlot(SlotChest, slotIndex, out var item)) {
        items[i] = item.ItemType;
      }
    }

    return items;
  }

  private void DeliverWinRewards(Dictionary<int, PrefabGUID> wins) {
    // Find the player who triggered the slot
    var player = GetSlotPlayer();
    if (player == Entity.Null) return;

    foreach (var win in wins) {
      var winningItem = win.Value;
      var prize = GetPrizeForItem(winningItem);

      if (prize.Prefab != 0 && prize.Amount > 0) {
        // Deliver the prize to the player with correct amount from config
        var prizeGuid = new PrefabGUID(prize.Prefab);
        InventoryService.AddWithMaxAmount(player, 0, prizeGuid, prize.Amount, prize.Amount);

        // Log the win for debugging
        Log.Info($"Player won {prize.Amount}x {prize.Prefab} from {winningItem.GuidHash}");
      }
    }
  }

  private Entity GetSlotPlayer() {
    return CurrentPlayer;
  }

  private Prize GetPrizeForItem(PrefabGUID item) {
    // Map the winning item directly to its corresponding prize
    return PrizeItemMap.Prizes.GetValueOrDefault(item, new Prize(0, 0));
  }
  #endregion

  #region Population Methods
  public void PopulateSlotsColumns() {
    var random = new System.Random();
    var usedPerColumn = InitializeUsedItemsPerColumn();

    for (int row = 0; row < TOTAL_ROWS; row++) {
      foreach (var col in ItemColumns) {
        var prefabguid = SelectUniqueWeightedItemForColumn(col, usedPerColumn, random);
        AddItemToSlot(row, col, prefabguid);
      }
    }
  }

  private Dictionary<int, HashSet<PrefabGUID>> InitializeUsedItemsPerColumn() {
    var usedPerColumn = new Dictionary<int, HashSet<PrefabGUID>>();
    foreach (var col in ItemColumns)
      usedPerColumn[col] = [];
    return usedPerColumn;
  }

  private static PrefabGUID SelectUniqueWeightedItemForColumn(int col, Dictionary<int, HashSet<PrefabGUID>> usedPerColumn, System.Random random) {
    var availableItems = SlotItems.WeightedItems.Where(kvp => !usedPerColumn[col].Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    if (availableItems.Count == 0) {
      usedPerColumn[col].Clear();
      availableItems = SlotItems.WeightedItems;
    }

    var prefabguid = GetWeightedRandomItem(availableItems, random);
    usedPerColumn[col].Add(prefabguid);

    return prefabguid;
  }

  private void AddItemToSlot(int row, int col, PrefabGUID prefabguid) {
    int slotIndex = row * TOTAL_COLUMNS + col;
    InventoryService.AddWithMaxAmount(SlotChest, slotIndex, prefabguid, 1, 1);
    SetAllItemsMaxAmount(SlotChest, 1);
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
  #endregion
}
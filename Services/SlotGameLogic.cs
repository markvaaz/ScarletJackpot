using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletJackpot.Models;
using ScarletJackpot.Utils;
using ScarletJackpot.Data;
using Stunlock.Core;
using Unity.Entities;
using ScarletJackpot.Constants;

namespace ScarletJackpot.Services;

internal class SlotGameLogic {
  private const int TOTAL_COLUMNS = 7;
  private const int TOTAL_ROWS = 3;
  private const int ANIMATION_INITIAL_FRAME_SPEED = 2;
  private const int ANIMATION_STOP_ITERATIONS = 75;
  private const int DELAYED_FRAMES = 10;
  private const int COLUMN_START_DELAY = 5;
  private const int LAMP_COLOR_COUNT = 10; // Cores de 0 a 9
  private const int LAMP_COLOR_CHANGE_FREQUENCY = 3; // Mudar cor a cada 3 iterações

  private readonly int[] _itemColumns = [1, 3, 5];
  private readonly SlotModel _slotModel;

  // Game state
  private Dictionary<int, PrefabGUID> _plannedWins = new();
  private PrefabGUID[,] _finalResults = new PrefabGUID[3, 3]; // [row, column] - matriz 3x3 final
  private readonly Dictionary<int, int> _columnPlacementCounter = []; // Rastrear qual linha estamos preenchendo por coluna
  private bool _hasPlannedResults = false;

  // Lamp color animation state
  private byte _currentLampColor = 0;

  public SlotGameLogic(SlotModel slotModel) {
    _slotModel = slotModel ?? throw new ArgumentNullException(nameof(slotModel));
  }

  #region Public Game Control Methods

  public void StartAnimation() {
    ClearWinIndicators();
    PrepareSlotResults();

    _currentLampColor = 0;

    ActionScheduler.DelayedFrames(StartStaggeredAnimation, DELAYED_FRAMES);
  }

  public void AnimateAllColumnsSync() {
    var random = new Random();

    foreach (var col in _itemColumns) {
      ProcessColumnAnimation(col, random);
    }
  }

  public void PopulateSlots() {
    // Parar animação das cores da lâmpada se estiver rodando
    StopLampColorAnimation();

    var random = new Random();
    var usedPerColumn = InitializeUsedItemsPerColumn();

    ClearWinIndicators();

    for (int row = 0; row < TOTAL_ROWS; row++) {
      foreach (var col in _itemColumns) {
        var prefabguid = SelectUniqueWeightedItemForColumn(col, usedPerColumn, random);
        AddItemToSlot(row, col, prefabguid);
      }
    }
  }

  #endregion

  #region Animation Logic

  private void PrepareSlotResults() {
    _plannedWins.Clear();
    _columnPlacementCounter.Clear();
    _hasPlannedResults = true;
    var random = new Random();

    foreach (var col in _itemColumns) {
      _columnPlacementCounter[col] = 0;
    }

    FillMatrixWithUniqueColumns(random);
    ApplyWinningLines(random);
  }

  private void FillMatrixWithUniqueColumns(Random random) {
    for (int col = 0; col < 3; col++) {
      var usedItems = new HashSet<PrefabGUID>();

      for (int row = 0; row < 3; row++) {
        var availableItems = SlotItems.WeightedItems.Where(kvp => !usedItems.Contains(kvp.Key))
                                                   .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (availableItems.Count == 0) {
          usedItems.Clear();
          availableItems = SlotItems.WeightedItems;
        }

        var selectedItem = WeightedRandomSelector.SelectItem(availableItems, random);
        _finalResults[row, col] = selectedItem;
        usedItems.Add(selectedItem);
      }
    }
  }

  private void ApplyWinningLines(Random random) {
    for (int row = 0; row < TOTAL_ROWS; row++) {
      if (ShouldCreateWinForRow(random)) {
        var winningItem = SelectWinningItem(random);
        _plannedWins[row] = winningItem;

        for (int col = 0; col < 3; col++) {
          _finalResults[row, col] = winningItem;
        }
      }
    }

    EnsureNoColumnRepeatsAfterWins(random);
  }

  private void EnsureNoColumnRepeatsAfterWins(Random random) {
    for (int col = 0; col < 3; col++) {
      FixColumnRepeatsSmartly(col, random);
    }
  }

  private void FixColumnRepeatsSmartly(int col, Random random) {
    var columnItems = new List<PrefabGUID>();
    for (int row = 0; row < 3; row++) {
      columnItems.Add(_finalResults[row, col]);
    }

    if (columnItems.Distinct().Count() == columnItems.Count) {
      return;
    }

    var usedItems = new HashSet<PrefabGUID>();

    for (int row = 0; row < 3; row++) {
      var currentItem = _finalResults[row, col];

      if (usedItems.Contains(currentItem)) {
        if (_plannedWins.ContainsKey(row)) {
          FixColumnKeepingWinningLines(col, random);
          return;
        } else {
          var availableItems = SlotItems.WeightedItems.Where(kvp => !usedItems.Contains(kvp.Key))
                                                     .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

          if (availableItems.Count > 0) {
            var newItem = WeightedRandomSelector.SelectItem(availableItems, random);
            _finalResults[row, col] = newItem;
            usedItems.Add(newItem);
          }
        }
      } else {
        usedItems.Add(currentItem);
      }
    }
  }

  private void FixColumnKeepingWinningLines(int col, Random random) {
    var winningRows = new HashSet<int>();
    var nonWinningRows = new List<int>();

    for (int row = 0; row < 3; row++) {
      if (_plannedWins.ContainsKey(row)) {
        winningRows.Add(row);
      } else {
        nonWinningRows.Add(row);
      }
    }

    var reservedItems = new HashSet<PrefabGUID>();
    foreach (var winRow in winningRows) {
      reservedItems.Add(_finalResults[winRow, col]);
    }

    var usedItems = new HashSet<PrefabGUID>(reservedItems);

    foreach (var row in nonWinningRows) {
      var currentItem = _finalResults[row, col];

      if (usedItems.Contains(currentItem)) {
        var availableItems = SlotItems.WeightedItems.Where(kvp => !usedItems.Contains(kvp.Key))
                                                   .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (availableItems.Count > 0) {
          var newItem = WeightedRandomSelector.SelectItem(availableItems, random);
          _finalResults[row, col] = newItem;
          usedItems.Add(newItem);
        }
      } else {
        usedItems.Add(currentItem);
      }
    }
  }

  private bool ShouldCreateWinForRow(Random random) {
    return random.NextDouble() < BASE_WIN_CHANCE;
  }

  private PrefabGUID SelectWinningItem(Random random) {
    var weightedWinItems = new Dictionary<PrefabGUID, float>();

    foreach (var item in SlotItems.WeightedItems.Keys) {
      var baseWeight = SlotItems.WeightedItems[item];
      var winMultiplier = SlotItems.WinMultipliers.GetValueOrDefault(item, 1.0f);
      weightedWinItems[item] = baseWeight * winMultiplier;
    }

    return WeightedRandomSelector.SelectItem(weightedWinItems, random);
  }

  #region Lamp Color Animation

  private void StopLampColorAnimation() {
    _currentLampColor = 0;
    _slotModel.ChangeLampColor(_currentLampColor);
  }

  #endregion

  private void StartStaggeredAnimation() {
    for (int i = 0; i < _itemColumns.Length; i++) {
      int columnIndex = i;
      int column = _itemColumns[i];
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
      if (columnIndex == _itemColumns.Length - 1) {
        ProcessSlotResults();
      }
      return;
    }

    if (_hasPlannedResults && ShouldPlaceWinningItems(iteration, columnIndex)) {
      AnimateSingleColumnWithPlannedResults(column, columnIndex);
    } else {
      AnimateSingleColumn(column);
    }

    int nextFrameSpeed = frameSpeed;

    if (iteration >= 60) {
      nextFrameSpeed = frameSpeed + 1;
    }

    BuffService.TryRemoveBuff(_slotModel.Dummy, Buffs.SlotGameBuff);
    BuffService.TryApplyBuff(_slotModel.Dummy, Buffs.SlotGameBuff);

    _currentLampColor = (byte)((_currentLampColor + 1) % LAMP_COLOR_COUNT);
    _slotModel.ChangeLampColor(_currentLampColor);

    ActionScheduler.DelayedFrames(() => {
      AnimateColumnWithDelay(column, iteration + 1, nextFrameSpeed, columnIndex);
    }, nextFrameSpeed);
  }

  private bool ShouldPlaceWinningItems(int iteration, int columnIndex) {
    int remainingIterations = ANIMATION_STOP_ITERATIONS - iteration;
    return remainingIterations <= 3 && remainingIterations > 0;
  }

  private void AnimateSingleColumnWithPlannedResults(int column, int columnIndex) {
    var random = new Random();
    var currentItems = GetCurrentColumnItems(column);

    RemoveBottomItem(column);
    MoveItemsDown(column);

    var newItem = SelectItemFromPlannedMatrix(column, columnIndex, random);

    int topSlotIndex = 0 * TOTAL_COLUMNS + column;
    InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, topSlotIndex);
    InventoryService.AddWithMaxAmount(_slotModel.SlotChest, newItem, topSlotIndex, 1, 1);
    SlotModel.SetAllItemsMaxAmount(_slotModel.SlotChest, 1);
  }

  private PrefabGUID SelectItemFromPlannedMatrix(int column, int columnIndex, Random random) {
    int matrixColumnIndex = Array.IndexOf(_itemColumns, column);

    if (matrixColumnIndex >= 0 && matrixColumnIndex < 3) {
      int targetRow = _columnPlacementCounter[column];

      if (targetRow >= 0 && targetRow < 3) {
        var plannedItem = _finalResults[targetRow, matrixColumnIndex];

        _columnPlacementCounter[column]++;

        return plannedItem;
      }
    }

    return SelectNewItemWithWeight(GetCurrentColumnItems(column), random);
  }

  private void AnimateSingleColumn(int column) {
    var random = new Random();
    ProcessColumnAnimation(column, random);
  }

  private void ProcessColumnAnimation(int col, Random random) {
    var currentItems = GetCurrentColumnItems(col);
    RemoveBottomItem(col);
    MoveItemsDown(col);
    AddNewTopItem(col, currentItems, random);
  }

  private List<PrefabGUID> GetCurrentColumnItems(int col) {
    var currentItems = new List<PrefabGUID>();

    for (int row = 0; row < TOTAL_ROWS; row++) {
      int slotIndex = row * TOTAL_COLUMNS + col;
      InventoryService.TryGetItemAtSlot(_slotModel.SlotChest, slotIndex, out var item);
      currentItems.Add(item.ItemType);
    }

    return currentItems;
  }

  private void RemoveBottomItem(int col) {
    int lastSlotIndex = (TOTAL_ROWS - 1) * TOTAL_COLUMNS + col;
    InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, lastSlotIndex);
  }

  private void MoveItemsDown(int col) {
    for (int row = TOTAL_ROWS - 1; row > 0; row--) {
      int fromIndex = (row - 1) * TOTAL_COLUMNS + col;
      int toIndex = row * TOTAL_COLUMNS + col;

      InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, toIndex);

      if (InventoryService.TryGetItemAtSlot(_slotModel.SlotChest, fromIndex, out var item)) {
        InventoryService.AddWithMaxAmount(_slotModel.SlotChest, item.ItemType, toIndex, 1, 1);
      }
    }
  }

  private void AddNewTopItem(int col, List<PrefabGUID> currentItems, Random random) {
    var newItem = SelectNewItemWithWeight(currentItems, random);
    int topSlotIndex = 0 * TOTAL_COLUMNS + col;

    InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, topSlotIndex);
    InventoryService.AddWithMaxAmount(_slotModel.SlotChest, newItem, topSlotIndex, 1, 1);
    SlotModel.SetAllItemsMaxAmount(_slotModel.SlotChest, 1);
  }

  private static PrefabGUID SelectNewItemWithWeight(List<PrefabGUID> currentItems, Random random) {
    var used = new HashSet<PrefabGUID>(currentItems);
    used.Remove(default);

    var availableItems = SlotItems.WeightedItems.Where(kvp => !used.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    if (availableItems.Count == 0) {
      availableItems = SlotItems.WeightedItems;
    }

    return WeightedRandomSelector.SelectItem(availableItems, random);
  }

  #endregion

  #region Win Detection and Rewards

  private void ProcessSlotResults() {
    // Parar animação das cores da lâmpada
    StopLampColorAnimation();

    var wins = DetectWins();
    var raghandsWin = wins.ContainsValue(new PrefabGUID(1216450741));

    if (raghandsWin) {
      ClearWinIndicators();
    } else if (wins.Count > 0) {
      AddWinIndicators(wins);
      DeliverWinRewards(wins);
    } else {
      ClearWinIndicators();
    }

    _plannedWins.Clear();
    _columnPlacementCounter.Clear();
    _hasPlannedResults = false;

    // Notify slot model that animation finished
    _slotModel.OnAnimationFinished();
  }

  private void AddWinIndicators(Dictionary<int, PrefabGUID> wins) {
    foreach (var winRow in wins.Keys) {
      int leftSlotIndex = winRow * TOTAL_COLUMNS + 0;
      int rightSlotIndex = winRow * TOTAL_COLUMNS + 6;

      InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, leftSlotIndex);
      InventoryService.AddWithMaxAmount(_slotModel.SlotChest, WIN_INDICATOR, leftSlotIndex, 1, 1);

      InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, rightSlotIndex);
      InventoryService.AddWithMaxAmount(_slotModel.SlotChest, WIN_INDICATOR, rightSlotIndex, 1, 1);
    }

    SlotModel.SetAllItemsMaxAmount(_slotModel.SlotChest, 1);
  }

  private void ClearWinIndicators() {
    for (int row = 0; row < TOTAL_ROWS; row++) {
      int leftSlotIndex = row * TOTAL_COLUMNS + 0;
      int rightSlotIndex = row * TOTAL_COLUMNS + 6;

      // Forçar remoção completa dos slots de indicadores
      InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, leftSlotIndex);
      InventoryService.RemoveItemAtSlot(_slotModel.SlotChest, rightSlotIndex);

      // Garantir que os slots ficam vazios
      var inventoryBuffer = InventoryService.GetInventoryItems(_slotModel.SlotChest);
      if (leftSlotIndex < inventoryBuffer.Length) {
        var leftItem = inventoryBuffer[leftSlotIndex];
        leftItem.ItemType = new PrefabGUID(0);
        leftItem.Amount = 0;
        inventoryBuffer[leftSlotIndex] = leftItem;
      }

      if (rightSlotIndex < inventoryBuffer.Length) {
        var rightItem = inventoryBuffer[rightSlotIndex];
        rightItem.ItemType = new PrefabGUID(0);
        rightItem.Amount = 0;
        inventoryBuffer[rightSlotIndex] = rightItem;
      }
    }
  }

  private Dictionary<int, PrefabGUID> DetectWins() {
    var wins = new Dictionary<int, PrefabGUID>();

    for (int row = 0; row < TOTAL_ROWS; row++) {
      var rowItems = GetRowItems(row);

      if (rowItems[0] != default && rowItems[0] == rowItems[1] && rowItems[1] == rowItems[2]) {
        wins[row] = rowItems[0];
      }
    }

    return wins;
  }

  private PrefabGUID[] GetRowItems(int row) {
    var items = new PrefabGUID[3];

    for (int i = 0; i < _itemColumns.Length; i++) {
      int col = _itemColumns[i];
      int slotIndex = row * TOTAL_COLUMNS + col;

      if (InventoryService.TryGetItemAtSlot(_slotModel.SlotChest, slotIndex, out var item)) {
        items[i] = item.ItemType;
      }
    }

    return items;
  }

  private void DeliverWinRewards(Dictionary<int, PrefabGUID> wins) {
    var player = _slotModel.CurrentPlayer;
    if (player == Entity.Null || !player.Exists() || !player.Has<PlayerCharacter>()) {
      return;
    }

    BuffService.TryApplyBuff(player, Buffs.VictoryVoiceLineBuff); // 5 segundos de duração

    BuffService.TryApplyBuff(_slotModel.Dummy, Buffs.VictorySlotBuff); // Efeito visual no slot
    BuffService.TryRemoveBuff(_slotModel.Dummy, Buffs.VictorySlotBuff); // Efeito visual no slot

    // Obter valor da aposta do jogador para calcular multiplicador
    var playerData = player.GetPlayerData();
    var betAmount = SlotService.GetBetAmount(playerData.PlatformId);
    var betMultiplier = CalculateBetMultiplier(betAmount);

    foreach (var win in wins) {
      var winningItem = win.Value;
      var prize = GetPrizeForItem(winningItem);

      if (prize.Prefab != 0 && prize.Amount > 0) {
        var prizeGuid = new PrefabGUID(prize.Prefab);

        // Aplicar multiplicador e arredondar para cima
        var multipliedAmount = prize.Amount * betMultiplier;
        var finalAmount = (int)Math.Ceiling(multipliedAmount);

        try {
          // Usar AddItem para entregar ao jogador (não precisa de slot específico)
          InventoryService.AddItem(player, prizeGuid, finalAmount);
        } catch (Exception ex) {
          Log.Error($"Error delivering prize to player: {ex.Message}");
        }
      }
    }
  }

  private Prize GetPrizeForItem(PrefabGUID item) {
    return PrizeItemMap.Prizes.GetValueOrDefault(item, new Prize(0, 0));
  }

  public static float CalculateBetMultiplier(int betAmount) {
    var minBet = SPIN_MIN_AMOUNT;
    var maxBet = SPIN_MAX_AMOUNT;
    var maxMultiplier = MAX_BET_MULTIPLIER;

    // Evitar divisão por zero se min == max
    if (minBet >= maxBet) {
      return 1.0f;
    }

    // Escala linear: 1x no mínimo, maxMultiplier no máximo
    var ratio = (float)(betAmount - minBet) / (maxBet - minBet);
    ratio = Math.Max(0f, Math.Min(1f, ratio)); // Clamp entre 0 e 1

    return 1.0f + (ratio * (maxMultiplier - 1.0f));
  }

  #endregion

  #region Population Methods

  private Dictionary<int, HashSet<PrefabGUID>> InitializeUsedItemsPerColumn() {
    var usedPerColumn = new Dictionary<int, HashSet<PrefabGUID>>();
    foreach (var col in _itemColumns)
      usedPerColumn[col] = [];
    return usedPerColumn;
  }

  private static PrefabGUID SelectUniqueWeightedItemForColumn(int col, Dictionary<int, HashSet<PrefabGUID>> usedPerColumn, Random random) {
    var availableItems = SlotItems.WeightedItems.Where(kvp => !usedPerColumn[col].Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    if (availableItems.Count == 0) {
      usedPerColumn[col].Clear();
      availableItems = SlotItems.WeightedItems;
    }

    var prefabguid = WeightedRandomSelector.SelectItem(availableItems, random);
    usedPerColumn[col].Add(prefabguid);

    return prefabguid;
  }

  private void AddItemToSlot(int row, int col, PrefabGUID prefabguid) {
    int slotIndex = row * TOTAL_COLUMNS + col;
    InventoryService.AddWithMaxAmount(_slotModel.SlotChest, prefabguid, slotIndex, 1, 1);
    SlotModel.SetAllItemsMaxAmount(_slotModel.SlotChest, 1);
  }

  #endregion
}

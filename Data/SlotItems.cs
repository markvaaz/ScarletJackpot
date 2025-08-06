using System;
using System.Collections.Generic;
using System.Linq;
using Stunlock.Core;

namespace ScarletJackpot.Data;

internal static class SlotItems {
  public static readonly Dictionary<PrefabGUID, int> WeightedItems = new() {
    { new(193249843), 35 },   // fish - mais comum
    { new(301051123), 25 },   // gem - comum
    { new(1075994038), 20 },  // jewel - moderadamente comum
    { new(1128262258), 15 },  // DuskCaller - raro
    { new(1488205677), 10 },  // magicstone - muito raro
    { new(1216450741), 6 },   // raghands - muito raro (steal wins)
    { new(-77477508), 4 },    // demon fragment - extremamente raro (jackpot)
  };

  public static readonly Dictionary<PrefabGUID, float> WinMultipliers = new() {
    { new(193249843), 1.0f },    // fish - chance normal de formar linha (mais comum)
    { new(301051123), 0.9f },    // gem - 10% menos chance de formar linha
    { new(1075994038), 0.7f },   // jewel - 30% menos chance de formar linha
    { new(1128262258), 0.5f },   // DuskCaller - 50% menos chance de formar linha
    { new(1488205677), 0.3f },   // magicstone - 70% menos chance
    { new(1216450741), 0.1f },   // raghands - 90% menos chance (mas rouba tudo)
    { new(-77477508), 0.05f },   // demon fragment - 95% menos chance (jackpot)
  };

  public static readonly List<PrefabGUID> All = WeightedItems.Keys.ToList();

  public static PrefabGUID GetRandomWeightedItem(Random random) {
    int totalWeight = WeightedItems.Values.Sum();
    int randomValue = random.Next(totalWeight);
    int currentWeight = 0;

    foreach (var item in WeightedItems) {
      currentWeight += item.Value;
      if (randomValue < currentWeight) {
        return item.Key;
      }
    }

    return WeightedItems.Keys.First();
  }

  // Nova função para controlar probabilidade de vitória
  public static bool ShouldFormWinningLine(PrefabGUID item, Random random) {
    if (!ENABLE_RTP_CONTROL) {
      return true; // Se RTP está desabilitado, sempre permitir vitórias
    }

    if (!WinMultipliers.TryGetValue(item, out float multiplier)) {
      multiplier = 1.0f; // Default
    }

    // Usar chance base das configurações
    float baseChance = BASE_WIN_CHANCE;
    float finalChance = baseChance * multiplier;

    return random.NextDouble() < finalChance;
  }
}

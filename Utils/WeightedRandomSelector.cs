using System;
using System.Collections.Generic;
using System.Linq;
using Stunlock.Core;

namespace ScarletJackpot.Utils;

internal static class WeightedRandomSelector {
  /// <summary>
  /// Selects a random item from a weighted collection using integer weights
  /// </summary>
  /// <param name="weightedItems">Dictionary with items and their integer weights</param>
  /// <param name="random">Random instance to use</param>
  /// <returns>Selected item</returns>
  public static PrefabGUID SelectItem(Dictionary<PrefabGUID, int> weightedItems, Random random) {
    if (weightedItems == null || weightedItems.Count == 0) {
      throw new ArgumentException("Weighted items cannot be null or empty");
    }

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

  /// <summary>
  /// Selects a random item from a weighted collection using float weights
  /// </summary>
  /// <param name="weightedItems">Dictionary with items and their float weights</param>
  /// <param name="random">Random instance to use</param>
  /// <returns>Selected item</returns>
  public static PrefabGUID SelectItem(Dictionary<PrefabGUID, float> weightedItems, Random random) {
    if (weightedItems == null || weightedItems.Count == 0) {
      throw new ArgumentException("Weighted items cannot be null or empty");
    }

    float totalWeight = weightedItems.Values.Sum();
    float randomValue = (float)random.NextDouble() * totalWeight;
    float currentWeight = 0;

    foreach (var item in weightedItems) {
      currentWeight += item.Value;
      if (randomValue < currentWeight) {
        return item.Key;
      }
    }

    return weightedItems.Keys.First();
  }
}

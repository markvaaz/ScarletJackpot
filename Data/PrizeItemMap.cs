using System;
using System.Collections.Generic;
using ScarletCore.Data;
using ScarletJackpot.Models;
using Stunlock.Core;

namespace ScarletJackpot.Data;

internal static class PrizeItemMap {
  private static Settings Settings => Plugin.Settings;

  // Método auxiliar para obter configurações com valores padrão
  private static int GetConfigWithDefault(string key, int defaultValue) {
    try {
      return Settings.Get<int>(key);
    } catch (KeyNotFoundException) {
      // Usar valor padrão quando a configuração não existir
      return defaultValue;
    }
  }

  public static readonly Dictionary<PrefabGUID, Prize> Prizes = new() {
    { new(193249843), new(GetConfigWithDefault("Fish", 193249843), GetConfigWithDefault("FishAmount", 1)) },           // fish -> fish reward
    { new(1128262258), new(GetConfigWithDefault("DuskCaller", 1128262258), GetConfigWithDefault("DuskCallerAmount", 2)) }, // DuskCaller -> DuskCaller reward
    { new(301051123), new(GetConfigWithDefault("Gem", 301051123), GetConfigWithDefault("GemAmount", 5)) },             // gem -> gem reward
    { new(1075994038), new(GetConfigWithDefault("Jewel", 1075994038), GetConfigWithDefault("JewelAmount", 5)) },       // jewel -> jewel reward
    { new(1488205677), new(GetConfigWithDefault("MagicStone", 1488205677), GetConfigWithDefault("MagicAmount", 10)) }, // magicstone -> magicstone reward
    { new(-77477508), new(GetConfigWithDefault("DemonFragment", -77477508), GetConfigWithDefault("DemonAmount", 50)) } // demon fragment -> demon fragment reward
  };
}

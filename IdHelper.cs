using System;
using ProjectM;
using System.Text;
using Unity.Entities;

namespace ScarletJackpot;

internal static class IdHelper {
  public static string GetId(this Entity entity) {
    if (!entity.HasId()) return null;

    var mapZoneData = entity.ReadBuffer<UserMapZonePackedRevealElement>();

    if (mapZoneData.Length == 0) return null;

    byte[] stringBytes = new byte[mapZoneData.Length];

    for (int i = 0; i < mapZoneData.Length; i++) {
      stringBytes[i] = mapZoneData[i].PackedPixel;
    }

    return Encoding.UTF8.GetString(stringBytes).TrimEnd('\0');
  }

  public static void SetId(this Entity entity, string id) {
    if (!entity.Exists() || entity.Has<PlayerCharacter>()) return;

    if (!entity.Has<UserMapZonePackedRevealElement>()) {
      entity.AddBuffer<UserMapZonePackedRevealElement>();
    }

    byte[] stringBytes = Encoding.UTF8.GetBytes(id);

    var umzpBuffer = entity.ReadBuffer<UserMapZonePackedRevealElement>();
    umzpBuffer.Clear();

    for (int i = 0; i < stringBytes.Length; i++) {
      var byteToAdd = stringBytes[i];
      umzpBuffer.Add(new UserMapZonePackedRevealElement { PackedPixel = byteToAdd });
    }
  }

  public static bool HasId(this Entity entity) {
    if (!entity.Exists() || !entity.Has<UserMapZonePackedRevealElement>() || entity.Has<PlayerCharacter>()) return false;

    var mapZoneData = entity.ReadBuffer<UserMapZonePackedRevealElement>();
    return mapZoneData.Length > 0;
  }

  public static bool IdEquals(this Entity entity, string id) {
    if (!entity.HasId()) return false;
    return entity.GetId() == id;
  }

  public static bool IdEqualsAny(this Entity entity, params string[] ids) {
    if (!entity.HasId()) return false;

    foreach (var singleId in ids) {
      if (entity.GetId() == singleId) return true;
    }

    return false;
  }
}
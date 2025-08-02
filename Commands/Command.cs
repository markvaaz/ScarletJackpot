using ScarletCore.Services;
using ScarletJackpot.Models;
using ScarletJackpot.Services;
using VampireCommandFramework;

namespace ScarletJackpot.Commands;

public static class Commands {
  [Command("create slot")]
  public static void Test(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var player)) {
      ctx.Reply("Player not found.");
      return;
    }
    var slot = new SlotModel(player.Position);
    SlotService.Register(slot);
  }

  [Command("clear slots")]
  public static void ClearSlots(ChatCommandContext ctx) {
    SlotService.ClearAll();
  }
}
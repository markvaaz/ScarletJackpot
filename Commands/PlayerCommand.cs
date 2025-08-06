using ScarletCore.Services;
using ScarletJackpot.Services;
using VampireCommandFramework;

namespace ScarletJackpot.Commands;

[CommandGroup("slot")]
public static class PlayerCommands {

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

    SlotService.SetBetAmount(player, amount);
  }
}
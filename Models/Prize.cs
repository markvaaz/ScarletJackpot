namespace ScarletJackpot.Models;

internal struct Prize(int prefab, int amount) {
  public int Prefab = prefab;
  public int Amount = amount;
}
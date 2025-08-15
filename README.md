# ScarletJackpot

**ScarletJackpot** is a V Rising mod that adds a slot machine system to the game.

## Support & Donations

<a href="https://www.patreon.com/bePatron?u=30093731" data-patreon-widget-type="become-patron-button"><img height='36' style='border:0px;height:36px;' src='https://i.imgur.com/o12xEqi.png' alt='Become a Patron' /></a>  <a href='https://ko-fi.com/F2F21EWEM7' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' alt='Buy Me a Coffee at ko-fi.com' /></a>

---

## Real Money Trading (RMT) Notice

ScarletJackpot is designed only for fun with in-game items.
Please don’t use it for real money trades or connect it to external gambling systems, as this could break the V Rising EULA and community rules.
Server owners are responsible for ensuring their use of the mod follows the game’s terms.

## How It Works

- Slot machines are created by admins and placed in the world.
- Players can set their bet amount using `.slot bet <amount>` or simply drag the desired quantity of items into the slot machine's inventory.
- Each spin consumes items (configurable) and can award prizes based on the result.
- Prize items and amounts for each winning line are fully configurable.
- The prize received is multiplied according to the bet amount and the configured maximum multiplier (`MaxBetMultiplier`). Higher bets yield higher rewards, up to the defined maximum.
- The mod supports RTP (Return to Player) control, allowing server owners to balance win rates.
- Optional animations and sound effects for spins and wins.
- The rare "Rag Hands" event (if enabled) can steal the current winnings from the slot machine when triggered (it does not steal all previous winnings, only the current spin's prizes).

## Features

- Slot machine creation, movement, rotation, and removal by admins
- Configurable bet amount and spin cost for players
- Prize pool and win multipliers are fully customizable
- RTP (Return to Player) control for balancing
- Optional animations and sound effects
- Rag Hands event: rare chance to lose the current spin's prizes
- Reload and clear all slot machines with admin commands

## Commands

### Player Commands

- `.slot bet <amount>` — Sets your bet amount for the slot machine

### Admin Commands

- `.slot create` — Creates a slot machine at your position
- `.slot reload` — Reloads slot machine settings
- `.slot iwanttoremoveeverything` — Removes all slot machines
- `.slot rotate <steps>` — Rotates the slot machine near your cursor (steps: 1, 2, 3, 4)
- `.slot rotateclosest <steps>` — Rotates the closest slot machine to you (steps: 1, 2, 3, 4)
- `.slot remove` — Removes the slot machine near your cursor
- `.slot move` — Allows you to move a slot machine by aiming and clicking


## Configuration

All prize items, amounts, spin cost, bet limits, RTP, and special events are configurable via the mod's settings file.
**Important:** The mod does not include default configuration values for bet items or prizes. You must manually define all required settings before using the slot machine, otherwise it will not function.

## Installation

### Requirements

* **[BepInEx](https://wiki.vrisingmods.com/user/bepinex_install.html)**
* **[ScarletCore](https://thunderstore.io/c/v-rising/p/ScarletMods/ScarletCore/)**
* **[VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)**

Make sure BepInEx is installed before installing ScarletJackpot.

### Manual Installation

1. Download the latest release of **ScarletJackpot**.
2. Extract the contents into `BepInEx/plugins`:
   `<V Rising Server Directory>/BepInEx/plugins/`
   The directory should contain:
   `BepInEx/plugins/ScarletJackpot.dll`
3. Ensure **ScarletCore** and **VampireCommandFramework** are also installed.
4. Restart your server.

## Credits

- **cheesasaurus, EduardoG, Helskog, Mitch, SirSaia, Odjit** & the [V Rising Mod Community on Discord](https://vrisingmods.com/discord)

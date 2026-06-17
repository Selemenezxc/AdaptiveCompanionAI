# AdaptiveCompanionAI

`tModLoader` mod source project for the graduation-work topic:

**Разработка алгоритма динамического изменения сложности в игре**.

The project adds a co-op-style AI companion for Terraria. The companion is not a scripted damage turret: it uses only weapons and ammunition placed in its own inventory, keeps a separate armor/accessory set, tracks player-performance metrics, adapts its support coefficient, and can be tested in a structured duel mode.

## Main features

- Companion NPC that follows the player, fights nearby enemies and can be recalled.
- Unified companion inventory: 40 storage slots, armor slots, accessory slots and visibility toggles.
- Weapon-only combat model:
  - no fallback spheres;
  - no custom attack projectile classes;
  - weapon projectiles are spawned from the companion position;
  - ammunition is selected and consumed from the companion storage;
  - projectile AI/drawing is proxied so weapons that depend on the owning player behave from the companion position.
- Adaptive difficulty model based on player metrics: combat, survival, mobility, exploration, gear, input intensity and progression.
- Separate support and duel coefficients: ordinary gameplay helps weak players more; duel mode is balanced independently.
- Duel mode with automatic UI closing, starting separation, visible companion HP and stored duel history.
- Minimal draggable UI with Inventory, Metrics, Battle and Settings tabs.
- Inventory button near the vanilla trash area, plus hotkey and right-click access.

## Folder structure

- `AdaptiveCompanionAI.cs` - mod entry point, hotkey and network packets.
- `Common/Data` - metric catalog, snapshots, power model, inventory and duel records.
- `Common/Players` - player telemetry and persistence.
- `Common/Systems` - companion spawn/recall and UI state management.
- `Common/UI` - draggable UI, item slots and tab content.
- `Content/NPCs` - companion AI, movement, combat and duel logic.
- `Content/Projectiles` - global projectile adapter for companion-owned weapon projectiles.
- `Content/Items` - companion bell item.
- `docs` - architecture, metrics, test plan and VQR mapping.

## Installation

1. Install tModLoader through Steam.
2. Open `Documents/My Games/Terraria/tModLoader/ModSources/`.
3. Copy the `AdaptiveCompanionAI` folder into `ModSources`.
4. Start tModLoader.
5. Open `Workshop -> Develop Mods -> Build + Reload`.
6. Enable the mod and enter a world.

## Controls

- `O` - open the companion interface.
- `AI/R` button near the player inventory trash area - open the companion interface or restricted recall mode.
- Right-click the companion - open the interface when nearby.
- `/adl spawn` - spawn or recall the companion.
- `/adl ui` - open the interface.
- `/adl power 1.25` - set manual power coefficient.
- `/adl duel on` / `/adl duel off` - start or stop duel mode.
- Companion Bell:
  - left click - spawn/recall;
  - right click - open interface.

## Combat model

The companion chooses a weapon from its storage every time it needs to attack. The selector evaluates distance, target life, boss status, weapon DPS, weapon range, safety and the selected combat style. If the Settings tab uses a manual combat style, the selector first tries to pick a matching weapon class and only falls back to other weapons when no matching weapon is available.

Ranged weapons require suitable ammunition in the companion storage. Magic, melee, summon and projectile-based melee weapons use their own item parameters. The project intentionally avoids extra non-item attacks so the companion feels like a second player in co-op rather than a separate artificial turret.

## Dynamic difficulty model

The power model calculates:

- player skill score;
- assistance need;
- automatic support coefficient;
- manual coefficient;
- effective support coefficient;
- separate duel coefficient.

Weak players receive stronger ordinary support. Strong players still receive useful support, but the companion does not take over the game. Duel mode is balanced separately so the encounter remains useful as a calibration test.

## Documentation for VQR

See:

- `docs/THESIS_STRUCTURE.md`
- `docs/ARCHITECTURE.md`
- `docs/METRICS.md`
- `docs/TEST_PLAN.md`
- `docs/RELEASE_CHECKLIST.md`

## Current release

Version `0.7.1` completes the weapon-only companion combat rewrite and removes all legacy non-weapon attack mechanics.

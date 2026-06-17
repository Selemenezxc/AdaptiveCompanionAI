# Architecture

## Design goal

AdaptiveCompanionAI is structured as a research-grade tModLoader mod for dynamic difficulty adjustment. The implementation separates telemetry, adaptation, UI, inventory, combat and duel validation so each part can be described and tested independently in a graduation project.

## Runtime layers

### 1. Mod entry point

`AdaptiveCompanionAI.cs`

- registers the hotkey;
- handles lightweight multiplayer packets;
- routes spawn, recall, duel and settings actions.

### 2. Telemetry and persistence

`Common/Players/AdaptiveDifficultyPlayer.cs`

- collects player metrics each tick and from gameplay hooks;
- stores companion metrics;
- stores companion inventory, duel history and manual settings;
- updates the current power snapshot.

### 3. Data model

`Common/Data`

- `MetricCatalog.cs` - names, labels and formatting for player/companion metrics;
- `MetricSnapshot.cs` - metric storage and save/load logic;
- `PlayerProgressSnapshot.cs` - current game progression and character state;
- `CompanionPowerModel.cs` - adaptive coefficient calculation;
- `CompanionInventory.cs` - storage, armor, accessories, weapon and ammo counters;
- `DuelRecord.cs` - saved duel result rows;
- `CompanionControlEnums.cs` - skill simulation, combat style and power profile enums.

### 4. Companion world entity

`Content/NPCs/AdaptiveCompanionNPC.cs`

Responsibilities:

- NPC ownership and persistence;
- right-click interface access without Terraria dialogue;
- free follow movement and tactical combat positioning;
- interface-radius leash while the UI is open;
- scaled life, damage and defense;
- weapon selection from companion storage;
- ammunition lookup and consumption;
- melee hitbox validation;
- duel start, countdown, outcome and HP display.

### 5. Weapon projectile adapter

`Content/Projectiles/CompanionProjectileGlobal.cs`

This is not a custom attack. It is a global adapter attached to projectiles created from real inventory weapons. It marks companion-created weapon projectiles, assigns duel/normal hit rules, records companion damage metrics and temporarily proxies the projectile owner's position during AI/draw hooks. The proxy is necessary for weapons whose projectile logic uses `Main.player[projectile.owner]` as the origin.

### 6. UI layer

`Common/UI`

- `CompanionUIState.cs` - main tabbed UI;
- `CompanionItemSlot.cs` - isolated companion item slots;
- `UIDragPanel.cs` - movable panel.

Tabs:

- Inventory: storage, armor, accessories, recall action;
- Metrics: scrollable player and companion metrics;
- Battle: duel start and duel history;
- Settings: skill summary, manual coefficient, skill simulation, combat style and power profile.

### 7. System integration

`Common/Systems/AdaptiveCompanionSystem.cs`

- spawns or recalls the companion;
- opens and closes UI;
- tracks bound companion index for interface leash behavior;
- draws the inventory-side access button.

## Combat algorithm

1. Find a valid enemy target near the companion/player.
2. Build a candidate list from `CompanionInventory.Storage`.
3. Reject non-weapons and weapons that require unavailable ammunition.
4. Classify weapon as melee, ranged, magic, summon or adaptive/other.
5. If manual combat style is enabled, first score only matching weapons.
6. Score candidates by DPS, preferred distance, target type, player safety and style preference.
7. Use true melee only when the companion hitbox reaches the target.
8. Use projectile weapons through `EntitySource_ItemUse_WithAmmo`, `ItemLoader.ModifyShootStats`, `ItemLoader.Shoot` and `Projectile.NewProjectile` when the default shot is allowed.
9. Consume ammunition from companion storage when tModLoader ammo hooks permit it.
10. Record damage, kills, projectile count and selected-weapon metrics.

## Duel algorithm

1. Start command closes the UI and separates player and companion.
2. Countdown prevents pre-fight damage.
3. Companion selects weapons through the same inventory-only selector.
4. Companion melee attacks require contact.
5. Companion projectile attacks are visible hostile weapon projectiles that can hit only the owner player.
6. Player melee attacks against the companion are accepted only through Terraria's melee hitbox collision path.
7. Result is stored as duration, player damage, companion damage and winner.

## Public-release boundaries

The project is prepared as a source mod for tModLoader `ModSources`. It avoids invasive patches of Terraria internals and keeps the core algorithm readable for diploma review. Full public multiplayer synchronization of the companion inventory can be expanded as a separate production milestone.

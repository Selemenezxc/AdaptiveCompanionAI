# Test plan

## 1. Build and smoke test

1. Copy the project to `ModSources/AdaptiveCompanionAI`.
2. Run `Workshop -> Develop Mods -> Build + Reload`.
3. Enter a single-player world.
4. Confirm that the companion spawns or can be recalled with `/adl spawn`.
5. Open the UI with `O`, right-click and the inventory-side button.

Expected result: no compile errors, no Terraria dialogue window, UI opens correctly.

## 2. Inventory isolation

1. Put armor into companion head/body/leg slots.
2. Put accessories into companion accessory slots.
3. Equip and unequip items on the real player.
4. Move items in companion storage.

Expected result: companion items never duplicate into the player equipment slots and player equipment remains usable.

## 3. Weapon-only combat

1. Empty the companion storage.
2. Spawn a hostile NPC.
3. Confirm that the companion does not use any fallback attack.
4. Add a melee weapon and repeat.
5. Add a bow without arrows and repeat.
6. Add arrows and repeat.
7. Add a magic/projectile melee weapon such as Zenith and repeat.

Expected result: the companion attacks only when a usable weapon exists. Ammunition weapons require ammunition. Projectiles originate from the companion, not the player.

## 4. Weapon selection

1. Add several weapon types to companion storage.
2. Switch Settings style to Auto.
3. Observe decisions at close, medium and long range.
4. Force Melee, Ranged, Magic and Summon styles manually.

Expected result: automatic mode chooses situational weapons; manual style prioritizes matching weapons and falls back only if none are available.

## 5. Duel correctness

1. Put at least one melee weapon and one ranged weapon with ammunition into companion storage.
2. Start a duel from the Battle tab.
3. Confirm that the UI closes immediately.
4. Confirm that duelists are separated.
5. Try hitting the companion with a sword or pickaxe from too far away.
6. Try hitting with actual contact.
7. Let the companion shoot.

Expected result: player melee damage only works on real hitbox contact. Companion ranged attacks are visible and come from the companion. Companion HP is visible. Duel result is added to history.

## 6. Adaptation checks

1. Use real skill mode and record current skill/assistance values.
2. Switch simulated skill to Low, Medium and High.
3. Observe support and duel coefficients.
4. Adjust manual coefficient.

Expected result: support/duel coefficients change immediately and remain visible in Settings and Metrics.

## 7. Interface leash

1. Open the companion interface while the companion is nearby.
2. Move around the working radius.
3. Spawn enemies outside and inside the radius.

Expected result: while the UI is open, the companion does not leave the interface radius; it can defend against nearby enemies but does not chase distant targets.

## 8. Regression checklist

- No player inventory corruption.
- No accessory duplication.
- No extra non-weapon projectiles.
- No projectile origin from the player during companion attacks.
- No distant melee damage in duel.
- No standard NPC dialogue window.
- Duel history persists through save/load.

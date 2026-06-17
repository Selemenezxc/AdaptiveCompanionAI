# Release checklist

## Source quality

- [ ] Build succeeds in tModLoader `Workshop -> Develop Mods -> Build + Reload`.
- [ ] No `Player.netUpdate` usage.
- [ ] No legacy non-weapon attack projectiles.
- [ ] No TODO markers in public-facing logic.
- [ ] Version in `build.txt` matches release notes.

## Gameplay

- [ ] Companion opens UI by hotkey, inventory button and right-click.
- [ ] Restricted UI mode works when companion is far away.
- [ ] Recall button works.
- [ ] Companion remains in UI radius while the interface is open.
- [ ] Companion uses only weapons from storage.
- [ ] Ammunition is required and consumed for ammo weapons.
- [ ] Weapon projectiles originate from the companion.
- [ ] Manual combat style changes weapon preference.

## Duel

- [ ] Start button closes UI.
- [ ] Countdown prevents early damage.
- [ ] Player melee hits require actual collision.
- [ ] Companion melee hits require actual collision.
- [ ] Companion ranged attacks are visible hostile weapon projectiles.
- [ ] Companion HP is visible.
- [ ] Duel result is stored in history.

## VQR evidence

- [ ] Architecture diagram prepared from `docs/ARCHITECTURE.md`.
- [ ] Metric table prepared from `docs/METRICS.md`.
- [ ] Test protocol prepared from `docs/TEST_PLAN.md`.
- [ ] Source file hierarchy screenshot prepared.
- [ ] UI screenshots prepared.
- [ ] Duel test screenshots prepared.

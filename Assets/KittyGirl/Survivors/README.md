# Kitty Girl survivor systems

The survivor runtime installs itself after `main` loads. No scene edit is required. It integrates
directly with the restored `GameManager`, `PlayerController`, `Enemy`, and `Projectile` classes.

## Runtime flow

1. `SurvivorRuntimeBootstrap` finds the player and disables the old `EnemySpawner` component.
2. `ProceduralWaveDirector` discovers scene transforms named `EnemySpawn` / `Enemy Spawn` and uses them as spawn anchors. It falls back to a configurable area in front of the player.
3. Each wave receives a growing point budget. Enemy types have a cost, weight, and unlock wave, so every wave is generated rather than read from `waveList`.
4. Enemies receive wave-based health, speed, and damage scaling. Later waves can roll elite enemies.
5. Kills grant experience. Level-ups pause gameplay and offer distinct weighted upgrade choices.

## Tuning

Edit `Assets/Resources/SurvivorGameConfig.asset` in the Inspector. It contains run timing,
budget growth, spawn limits, scaling, elite settings, XP curve, and the enemy roster.

The built-in upgrades are in `SurvivorStats.UpgradeCatalog`. The compatibility bridge applies
them to the original weapon recharge fields, while the player and enemy classes directly handle
damage, projectile speed/area, movement, bonus health, recovery, and kill experience. Starting
or retrying a game resets upgrades and regenerates the run seed.

## Source layout

The old `Assets/_Scripts` gitlink has been replaced with the restored source files so subsequent
gameplay changes are tracked by this repository.

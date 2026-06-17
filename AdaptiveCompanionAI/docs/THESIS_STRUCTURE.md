# VQR structure mapping

Topic: **Разработка алгоритма динамического изменения сложности в игре**.

The project is prepared so its source code and documentation can be used as the practical result of the graduation work.

## Introduction

Recommended content:

- relevance of dynamic difficulty adjustment in games;
- object: Terraria gameplay process with modifiable NPC behavior;
- subject: an algorithm for adaptive companion strength and behavior control;
- goal: design and implement a dynamic difficulty algorithm through an AI companion;
- tasks:
  1. analyze adaptive difficulty and companion systems;
  2. define player-skill metrics;
  3. design the adaptive coefficient model;
  4. implement the tModLoader mod;
  5. test the model through ordinary gameplay and duel calibration;
- novelty: combining telemetry-based support coefficient, manual override, weapon-only co-op-style companion and duel-based validation;
- practical value: a working source mod and testable UI.

## Section 1. Analysis of the subject area

Use this section to describe:

- why Terraria and tModLoader are suitable for experiments;
- limitations of static difficulty;
- existing DDA approaches;
- behavioral telemetry as a basis for adaptation;
- requirements for a public-ready companion system.

Expected output: requirements for the algorithm and software product.

## Section 2. Theoretical model

Use this section to formalize:

- the metric set;
- normalized player-skill components;
- assistance need;
- automatic support coefficient;
- manual coefficient;
- duel coefficient;
- combat-style selection rules.

Expected output: formulas and criteria that justify the implementation.

## Section 3. Algorithm and implementation method

Use this section to describe:

- metric collection in `AdaptiveDifficultyPlayer`;
- coefficient calculation in `CompanionPowerModel`;
- weapon selection in `AdaptiveCompanionNPC`;
- ammunition selection and consumption;
- projectile-origin proxying in `CompanionProjectileGlobal`;
- duel procedure;
- UI workflow.

Expected output: an implementation algorithm and data-flow diagram.

## Section 4. Practical implementation

Use this section to show:

- project file hierarchy;
- UI screenshots;
- metric examples;
- ordinary combat tests;
- duel tests;
- manual style/profile tests;
- limitations and planned improvements.

Expected output: evidence that the product works and satisfies the stated requirements.

## Conclusion

State that:

- the goal was achieved;
- the algorithm was implemented in a tModLoader mod;
- metrics and coefficients are observable;
- the companion uses inventory weapons only;
- duel mode provides a repeatable validation scenario;
- further work can include advanced multiplayer synchronization, more accurate accessory simulation and ML-based policy tuning.

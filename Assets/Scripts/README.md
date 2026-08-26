# Run Crab Run — Script Package & Unity Setup Guide

This package is the code half of the MVP checklist in your design doc (section 25).
It implements the architecture from section 28 almost exactly:

```
Creature (abstract)
├── HarmlessAnimal        (Deer, Sheep)
└── Predator (abstract)
    ├── Wolf
    ├── Fox
    └── Bird (optional/stretch)

ImposterComponent          — modifier that hides a Predator inside a HarmlessAnimal

PlayerController / PlayerSurvival / HidingSpot
ResourceNode
GameManager / DifficultyManager / SpawnManager / UIManager
```

Copy the `Assets/Scripts` folder straight into your Unity project's `Assets` folder.
Everything below assumes a **2D URP or Built-in project** (top-down, no gravity).

---

## 1. Project-wide setup (do this first)

### Tags
Edit ▸ Project Settings ▸ Tags and Layers ▸ Tags, add:
- `Player`
- `Animal` (harmless animals — Deer, Sheep, and Imposters *before* they reveal)
- `Predator` (real predators, and Imposters *after* they reveal — this one is mostly
  set automatically by code, you don't need to hand-tag prefabs)

### Physics 2D
No special layers are strictly required for the MVP — everything uses trigger
colliders + tag checks rather than layer collision matrices, to keep the 4-day build
simple. If animals start pushing each other around annoyingly, give `Animal` and
`Predator` their own physics layers and turn off collision between them in
Edit ▸ Project Settings ▸ Physics 2D.

### Input
Movement uses `Input.GetAxisRaw("Horizontal"/"Vertical")`, which exists by default in
every new Unity project (WASD + arrow keys) — no Input Manager changes needed. Sprint
is hardcoded to Left Shift in `PlayerController`.

---

## 2. CreatureData assets (data-driven stats, doc section 29)

Right-click in the Project window ▸ Create ▸ RunCrabRun ▸ Creature Data.
Make one per creature type and tune values to match the doc's examples:

| Asset          | moveSpeed | chaseSpeed | detectionRange | attackRange | loseInterestTime |
|----------------|-----------|------------|-----------------|-------------|-------------------|
| Data_Deer      | 2.5       | –          | 5               | –           | –                 |
| Data_Sheep     | 2.3       | –          | 5               | –           | –                 |
| Data_Wolf      | 2.0       | 3.2        | 8               | 0.6         | 5                 |
| Data_Fox       | 2.8       | 5.5        | 6               | 0.5         | 2                 |

(`fleeSpeed`, `wanderRadius`, `minWanderPause`/`maxWanderPause` are used by every
creature type — tune to taste, defaults are reasonable.)

---

## 3. Building the Player

1. Create GameObject **Player**. Tag = `Player`.
2. Add: `Rigidbody2D` (Gravity Scale 0, Constraints ▸ Freeze Rotation Z), a
   `Collider2D` (e.g. CircleCollider2D, not a trigger), a `SpriteRenderer`.
3. Add `PlayerController.cs` and `PlayerSurvival.cs`.
4. That's it — hiding and dying are wired automatically via `HidingSpot` and `Predator`.

---

## 4. Building a Harmless Animal (Deer / Sheep)

1. Create the prefab GameObject, Tag = `Animal`.
2. Add `Rigidbody2D` (Gravity Scale 0, Freeze Rotation Z) + a physical `Collider2D`.
3. Add an empty **child** GameObject `DetectionZone`, with a `CircleCollider2D`
   (Is Trigger ✔, Radius ≈ matches `detectionRange`) and the `DetectionZone.cs` script.
4. Add `HarmlessAnimal.cs` to the root, assign the `Data` field (e.g. `Data_Deer`) and
   drag the child `DetectionZone` into the root's `Detection Zone` field.
5. Save as a prefab (e.g. `Deer.prefab`, `Sheep.prefab`).

## 5. Building a standalone Predator (Wolf / Fox)

Same steps as above, but:
- Add `Wolf.cs` or `Fox.cs` instead of `HarmlessAnimal.cs`.
- You do **not** need to set the Tag by hand — `Predator.OnEnable()` sets it to
  `Predator` automatically at runtime.
- Assign `Data_Wolf` / `Data_Fox`.

## 6. Building an Imposter (the core mechanic, doc section 8)

Full steps live as comments at the top of `ImposterComponent.cs`; summary:

1. Duplicate a finished Sheep/Deer prefab → rename `Sheep_Imposter`.
2. On the **same root GameObject**, add a `Wolf` (or `Fox`) component too. Give it its
   own `CreatureData` (its *true* stats) and its own `DetectionZone` child — leave that
   child **deactivated** in the Inspector.
3. **Untick** the Wolf component's enabled checkbox (leave it disabled).
4. Add a second child GameObject `RevealZone` with a small `CircleCollider2D`
   (Is Trigger ✔, smaller radius than the normal DetectionZone) + `DetectionZone.cs`.
5. Add `ImposterComponent.cs` to the root. Drag the Wolf component into
   `Hidden Predator`, and the `RevealZone` child's DetectionZone into `Reveal Zone`.
6. Leave the GameObject's Tag as `Animal` — it flips to `Predator` automatically on reveal.

From spawn it wanders and flees like a normal sheep. The instant the player enters the
`RevealZone`, the disguise drops and it immediately starts chasing.

---

## 7. World objects

**HidingSpot** (bush/rock): add `HidingSpot.cs` + a trigger `Collider2D` to any
prefab you want the crab to be able to hide in.

**ResourceNode** (berry bush / fruit / stream): add `ResourceNode.cs` + a trigger
`Collider2D`. Suggested values:
- Berry Bush: Type=Food, Restore=30, Uses=3
- Fruit: Type=Food, Restore=15, Uses=1
- Stream: Type=Water, Restore=40, Uses=0 (infinite)

Lay these out in resource hotspots per doc section 21 (a few clusters of food/water,
not evenly scattered).

---

## 8. Core game loop objects

1. Create empty GameObject **GameManager**, add `GameManager.cs` and
   `DifficultyManager.cs` (tune the `tiers` array, or leave the section-19 defaults).
2. Create empty GameObject **SpawnManager**, add `SpawnManager.cs`:
   - Drag the GameManager's `DifficultyManager` into `Difficulty`.
   - Create several empty `SpawnPoint` GameObjects around the map edges, drag them
     all into `Spawn Points`.
   - Drag your Deer/Sheep prefabs into `Harmless Animal Prefabs`, your
     `Sheep_Imposter`/`Deer_Imposter` prefabs into `Imposter Animal Prefabs`, and your
     Wolf/Fox prefabs into `Predator Prefabs`.

---

## 9. UI

1. Add a `Canvas` + `EventSystem` if you don't have one.
2. Add two `Slider`s: `HungerBar`, `ThirstBar` (Min 0 / Max 1).
3. Add a `Panel` (`DeathScreenPanel`, inactive by default) containing a survival-time
   `Text`, a best-time `Text`, and a `Restart` `Button`.
4. Create empty GameObject **UIManager**, add `UIManager.cs`, and wire:
   - `Hunger Bar` / `Thirst Bar` → the two Sliders
   - `Player Survival` → the Player's `PlayerSurvival` component
   - `Death Screen Panel`, `Survival Time Text`, `Best Time Text` → the panel pieces
5. Select the Restart Button ▸ On Click () ▸ drag in the `UIManager` GameObject ▸
   pick `UIManager.OnRestartButton()`.

---

## 10. World boundary (doc section 20)

Surround the finite tilemap with a border of solid Tilemap collider tiles (thick
trees) so the player physically cannot walk off the map — no invisible walls needed.

---

## 11. Testing checklist (matches doc section 25 MVP list)

- [ ] Crab moves in 8 directions, sprint works and visibly drains hunger faster
- [ ] Hunger/thirst bars tick down and hitting 0 kills the player
- [ ] Standing in a bush turns off predator chase (walk a Wolf toward you, then hide)
- [ ] Wolf: wanders → notices player → chases → gives up if you hide/outrun it
- [ ] Fox: same as Wolf but faster and breaks off chase sooner
- [ ] Deer/Sheep: wander, flee when a real Wolf/Fox/revealed-Imposter gets close
- [ ] Imposter sheep: behaves exactly like a normal sheep until you get close, then
      reveals and chases — and other nearby sheep correctly flee it once revealed
- [ ] Eating a berry bush/fruit restores hunger; drinking at a stream restores thirst
- [ ] Death screen shows survival time + best time, Restart button reloads the scene
- [ ] SpawnManager produces more predators/imposters as SurvivalTime climbs through
      the DifficultyManager tiers

Once all of these pass, you have the full MVP — everything in doc section 27
(group behavior, predator-hunts-deer, animal panic, imposter groups, resource
depletion, weather) is a stretch goal layered on top of this foundation, not a
rewrite of it.

# AGENTS.md

Unity 6 (6000.0.45f1) 2D top-down game — Brackeys Game Jam submission. All gameplay code is in `Assets/Scripts/` (17 C# files). There are no CLI build/test/lint commands; compilation and testing happen inside the Unity Editor.

## Architecture

Class hierarchy in `Assets/Scripts/`:

- `Creature` (abstract) — base for all animals. Rigidbody2D movement, wander coroutine, player detection.
  - `HarmlessAnimal` — Deer/Sheep. Flees from predators.
  - `Predator` (abstract) — Wolf/Fox/Bird. Wander → Chase → Search state machine. OnEnable sets tag to `"Predator"`.
    - `Wolf`, `Fox`, `Bird` — concrete predators with tuned stats.
- `ImposterComponent` — modifier placed on a HarmlessAnimal prefab alongside a *disabled* Predator component. Swaps active behavior on player proximity. This is the core mechanic.

Data-driven stats live in `CreatureData` ScriptableObjects (`Assets/Creature Data/`). Create new ones via Create → RunCrabRun → Creature Data.

## Tags are load-bearing

Tags drive gameplay logic via `CompareTag()` and `OnTriggerEnter2D` checks:
- `"Player"` — set on the Player GameObject.
- `"Animal"` — harmless animals and imposters *before* reveal.
- `"Predator"` — set automatically by `Predator.OnEnable()` at runtime; do not manually tag predator prefabs.

## Input

Uses legacy Input Manager axes (`Input.GetAxisRaw("Horizontal"/"Vertical")`). No Input System package changes needed. Sprint is hardcoded to `KeyCode.LeftShift` in `PlayerController.cs:37`.

## Key gotchas

- **Predator.OnEnable vs Awake**: `Predator` components on Imposter prefabs start disabled. `OnEnable` sets the tag and starts the state machine — anything that should only run when the predator is "active" must go in `OnEnable`, not `Awake`.
- **Imposter prefab setup** is error-prone. Full instructions are in `Assets/Scripts/ImposterComponent.cs:10-31` and `Assets/Scripts/README.md`. The hidden Predator component must start *disabled* in the Inspector; `ImposterComponent.Awake()` force-disables it as a safety net.
- **DetectionZone children**: Each creature needs a child `DetectionZone` with a `CircleCollider2D` (Is Trigger). Imposter prefabs need *two* DetectionZone children — one for the HarmlessAnimal behavior and one for the hidden Predator — plus a third `RevealZone` for the reveal trigger.
- **GameManager sets `Time.timeScale = 0`** on death. Any time-sensitive code must account for this (e.g. `Time.deltaTime` becomes 0).
- **Creature data assets exist** at `Assets/Creature Data/` (Data_Deer, Data_Sheep, Data_Wolf, Data_Fox). Prefabs exist at `Assets/Prefabs/Creatures/`.

## What not to do

- Do not manually set the `"Predator"` tag on prefab GameObjects — `Predator.OnEnable()` handles it.
- Do not put gameplay logic in `Awake` for Predator subclasses that will be used on Imposter prefabs — it runs once at load while the component is disabled and never fires again.
- Do not assume Layer collision settings are configured — MVP uses trigger colliders + tag checks, not physics layers.

## Reference docs

`Assets/Scripts/README.md` is the authoritative setup guide (184 lines). It covers prefab construction, CreatureData asset creation, UI wiring, and the MVP testing checklist. Read it before making structural changes.

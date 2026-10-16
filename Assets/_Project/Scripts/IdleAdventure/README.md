# IdleAdventure Module Structure

Idle Adventure mode is a Unity-side prototype for the idle stage-clearing combat mode. The module is split by responsibility so later backend sync, prefab work, and battle-rule changes have clear homes.

## Folder Layout

```text
Assets/_Project
|-- Scenes
|   `-- Idle Adventure mode.unity
|-- Configs
|   `-- IdleAdventure
|       `-- IdleAdventureDefaultModeConfig.asset
|-- Prefabs
|   `-- IdleAdventure
|       |-- FriendlyCombatant.prefab
|       `-- EnemyCombatant.prefab
`-- Scripts
    `-- IdleAdventure
        |-- Core
        |-- Config
        |-- Combat
        |-- UI
        |-- Formation
        |-- Progress
        `-- Rewards
```

## Script Responsibilities

| Folder | Files | Responsibility |
| --- | --- | --- |
| `Core` | `IdleAdventureModeController.cs` | Scene bootstrap, start/end flow, battle loop, party creation, damage calculation, stage progression, reward settlement trigger |
| `Config` | `IdleAdventureModeConfig.cs` | Inspector-facing mode config, combatant definitions, buff presets, field buffs, attribute restraints, stage rewards |
| `Combat` | `IdleAdventureCombatant.cs`, `IdleAdventureBattleSkill.cs`, `IdleAdventureBattleBuff.cs` | Runtime combatant state, speed order inputs, skills, cooldowns, buff state and damage multipliers |
| `UI` | `IdleAdventureActorView.cs`, `IdleAdventureBattleHud.cs`, `IdleAdventureStartPanel.cs`, `IdleAdventureStageEndPanel.cs` | Battle HUD, actor slots, start screen, stage end screen, buff icon display |
| `Formation` | `IdleAdventureFormationPanel.cs`, `IdleAdventureFormationDragItem.cs`, `IdleAdventureFormationSlotDropTarget.cs` | Formation window, draggable NPC entries, slot drop targets |
| `Progress` | `IdleAdventureStageProgress.cs` | Highest cleared stage persistence, next uncleared stage calculation, progress events |
| `Rewards` | `IdleAdventureRewardLedger.cs`, `IdleAdventureAfkRewardSource.cs` | Final reward grant ledger, idle resource production, and Inspector events for inventory or backend integration |

## Data Flow

1. `IdleAdventureModeController` loads `IdleAdventureModeConfig`.
2. Start screen is shown first; battle does not begin until the start button is clicked.
3. Formation data is held as six fixed slots. Empty slots remain empty instead of shrinking the party list.
4. Friendly and enemy runtime combatants are created from config definitions.
5. Each round builds initiative by effective speed, then resolves attacks one by one.
6. Damage uses the module formula:

```text
baseDamage = max(0, attacker.effectiveAttack - target.effectiveDefense)
bonusMultiplier = 1 + normalBuffMultiplier + attributeBuffMultiplier + fieldBuffMultiplier + attributeRestraintMultiplier
basicDamage = floor(baseDamage * bonusMultiplier)
skillDamage = floor(baseDamage * skillMultiplier * bonusMultiplier)
```

7. Attribute restraint is fixed at 20% when attacker attribute restrains defender attribute.
8. Buff durations and skill cooldowns tick down after each full initiative round.
9. Stage rewards are banked after each cleared stage and granted together when the player exits the mode.
10. If the party is defeated after clearing earlier stages, exiting still grants the pending rewards from those cleared stages; retrying the failed stage does not grant the failed stage reward.
11. AFK rewards are displayed in the lower-left overlay. Production is calculated from server time snapshots, with local time used only to preview progress between server syncs.
12. AFK reward output per cycle scales with the player's highest cleared stage and is capped by a maximum stored quantity.
13. The highest cleared stage is stored by the `IdleAdventureStageProgress` scene component; a new run starts at `highestClearedStage + 1`, so cleared stages are skipped on the next start.

## Inspector Configuration Notes

`IdleAdventureDefaultModeConfig.asset` should be the first place to configure mode data:

| Config Area | Purpose |
| --- | --- |
| Party Size | Default friendly count, base enemy count, enemy growth cadence |
| Friendly Stats / Enemy Stats | Fallback stats when no definition is available |
| Timing | Turn interval, attack movement time, hit flash time |
| Default Lineups | Friendly and enemy definitions, attributes, skill slots |
| Buff Presets | Skill or other buff presets for normal damage and attribute damage |
| Field Buffs | Attribute-limited stage or battlefield multipliers |
| Attribute Restraints | Attribute relationship table; multiplier is fixed in code at 20% |
| Stage Rewards | Per-stage reward items. Cleared stages add these into the current run's pending reward pool |

`IdleAdventureRewardLedger` is created as a persistent scene component under the mode controller. It stores the granted reward totals for the current scene run and exposes Inspector events:

| Event | Payload | Purpose |
| --- | --- | --- |
| `RewardGranted` | `itemKey`, `displayName`, `quantity` | Called once per item when the player exits the mode and pending rewards are granted |
| `SettlementCompleted` | `summary` | Called after a full settlement finishes |

`IdleAdventureAfkRewardSource` is created with a small lower-left overlay canvas. Its default values are `Gold x10` every `30` seconds, `+2` gold per highest cleared stage, and `1000` max stored gold. These can be adjusted in the generated component Inspector.

When `BackendGameService` is authenticated, the AFK widget calls:

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `LoadIdleAdventureAfkReward` | `GET /api/idle-adventure/players/{playerId}/afk-reward` | Load server time, last settlement time, highest cleared stage, and stored reward quantity |
| `CollectIdleAdventureAfkReward` | `POST /api/idle-adventure/players/{playerId}/afk-reward/collect` | Collect according to server settlement time and refresh inventory snapshot |

## Current Module Boundary

The Unity module currently owns local combat simulation. Backend integration should later replace or sync these areas:

| Unity Area | Backend Contract |
| --- | --- |
| Formation slots | Save/load player formation |
| Available NPC list | Load player-owned idle adventure NPCs |
| Enemy lineup | Load stage enemy definitions |
| Buff/skill config | Load server-authored battle config |
| Round resolution | Optional backend-authoritative combat settlement |
| Stage progress | Persist highest cleared stage and running session. The current Unity prototype mirrors highest cleared stage locally with player-scoped `PlayerPrefs` |
| Reward settlement | Grant all pending stage rewards when the player ends the mode |
| AFK reward collection | Persist server time, stored output, and collection grants |

The detailed backend contract is maintained at the project root in `IdleAdventure模式接口文档.md`.

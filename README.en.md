# FunRandomRounds

[中文](README.md) | [English](README.en.md)

A CS2 dedicated-server plugin that picks a random fun rule for every round in a **MatchZy** match. Rules do not repeat within the same match. The pool resets when warmup ends or a new match starts.

Current version: **1.5.8**  
Framework: [CounterStrikeSharp](https://docs.cssharp.dev/)

Companion plugin: [Fun1v5](https://github.com/IsNotSunal/Fun1v5) (do not enable both at the same time)

---

## Features

- Picks one rule from the pool each round and announces it in chat and on-screen
- **No duplicate rules in the same match**; leftover rounds play as Normal after the pool is empty
- No rule is applied during warmup; randomization starts after the live restart
- Restores CVars, buy state, and player attributes when a rule ends
- Admins can force a specific rule for testing
- Do not enable together with [Fun1v5](https://github.com/IsNotSunal/Fun1v5) `.fun1v5`

---

## Requirements

| Dependency | Notes |
|------|------|
| CS2 Dedicated Server | Windows / Linux |
| [Metamod:Source](https://www.sourcemm.net/) | CS2 build |
| [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) | API `1.0.342` or a compatible newer build |
| [MatchZy](https://github.com/shobhit-pathak/MatchZy) | Ready, start, and BO flow stay on MatchZy |

This plugin only rolls rules during live MatchZy rounds. It does not replace `.ready` / `.start`.

---

## Install

1. Download `FunRandomRounds.dll` from [Releases](../../releases), or build it yourself below.
2. Copy it to:

```text
csgo/addons/counterstrikesharp/plugins/FunRandomRounds/FunRandomRounds.dll
```

3. Restart the CS2 server, or run:

```text
css_plugins load FunRandomRounds
```

4. Confirm the log shows `FunRandomRounds` loaded.

Admin checks match MatchZy:

- CounterStrikeSharp flags: `@css/generic` or `@css/root`
- Or a SteamID listed in `csgo/cfg/MatchZy/admins.json`

---

## Usage

Admins type a dotted chat command, or use `css_funrand` in the server console.

| Chat | Console | Action |
|------|--------|------|
| `.funrand` | `css_funrand` | Enable random-rule mode |
| `.funrand off` | `css_funrand off` | Disable and restore pre-mode server settings |
| `.funrand list` | `css_funrand list` | List all rule numbers |
| `.funrand set 3` | `css_funrand set 3` | Force rule 3 for this round (testing; may repeat) |

Chat aliases: `.rand`, `.random`.

After enabling:

1. Start the match with MatchZy: players `.ready`, admin `.start`
2. After warmup and the live restart, each round gets an unused random rule
3. A center banner shows the rule name and description for about 10 seconds
4. Closing the mode or finishing the match resets the used-rule pool

`.funrand set` can still force a rule that already appeared.

---

## Rules

Numbers match `.funrand list` / `.funrand set`. In-game names stay in Chinese.

| # | Name | Effect |
|------|------|------|
| 1 | 正常 | Normal competitive round |
| 2 | 玉面手雷王 | Infinite HE grenades, buying disabled |
| 3 | 击杀传送 | Teleport to the victim on kill |
| 4 | 跳狙飞人 | Lower gravity, no gun spread |
| 5 | 吸血鬼 | Heal for damage dealt |
| 6 | 黑客来袭 | Wallhack for everyone |
| 7 | 身法大王 | Auto bunny hop, speed cap removed |
| 8 | 雷电法王 | Infinite taser, buying disabled |
| 9 | 脆皮大学生 | 1 HP, starting decoy, buying disabled |
| 10 | 无限制下包 | Plant C4 anywhere after freeze time |
| 11 | 豪气冲天 | Drop the held gun on a missed shot |
| 12 | 无限道具 | Infinite utility |
| 13 | 献祭队友 | Shooting teammates heals you |
| 14 | Hide | Invisible until you make noise |
| 15 | 连锁反应 | Dropped nades can be shot and detonated |
| 16 | CS2但是CF | Much less moving spread and recoil |
| 17 | 我是卡莎！ | Players are scaled down |
| 18 | 身法雷电法王 | Infinite bunny hop, no speed cap, taser only |
| 19 | 连锁反应大王 | Infinite utility; dropped nades can explode |
| 20 | 马了 | Forced spread on all guns |
| 21 | 火车头 | Knife only, movement speed × 5 |
| 22 | 内鬼？！！！ | Shuffle all player spawn positions |
| 23 | 大洗牌 | Shuffle teams while keeping side counts |
| 24 | 夺舍的来 | Killer inherits the victim's weapons and utility |
| 25 | 随机武器 | On kill, rifles swap to a random rifle, pistols to a random pistol |

---

## Build from source

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer that can target `net8.0`)
- Windows, Linux, or macOS

### Build

```bash
git clone https://github.com/IsNotSunal/FunRandomRounds.git
cd FunRandomRounds
dotnet restore
dotnet build -c Release
```

Output:

```text
bin/FunRandomRounds.dll
```

Copy only that DLL into the server plugin folder. Do not copy `CounterStrikeSharp.API.dll`; CounterStrikeSharp provides it at runtime.

### Project settings

Important entries in `FunRandomRounds.csproj`:

| Property | Value | Meaning |
|------|----|------|
| `TargetFramework` | `net8.0` | Matches the CounterStrikeSharp runtime |
| `AllowUnsafeBlocks` | `true` | Some rules touch schema / memory |
| `OutputPath` | `bin/` | No extra `net8.0` output folder |
| `CounterStrikeSharp.API` | `1.0.342` | Compile-time reference; `PrivateAssets=all` so it is not copied out |

When upgrading CSS, bump the API package version and run a matching CounterStrikeSharp build on the server.

---

## Project layout

```text
FunRandomRounds/
├── FunRandomRounds.cs          # Plugin entry: commands, round scheduler, rule pool
├── FunRandomRounds.csproj      # .NET 8 project
├── CvarSnapshot.cs             # Capture / restore CVars when the mode toggles
├── WeaponGuard.cs              # CanAcquire hook for restricted loadouts
├── Rules/
│   ├── RoundRule.cs            # Rule base class
│   ├── RuleRegistry.cs         # Registration order (rule numbers)
│   ├── WeaponUtil.cs           # Give / drop / equip helpers
│   ├── RestrictedLoadout.cs    # Restricted kits + buy lock / unlock
│   └── *Rule.cs                # Individual rules
├── README.md
├── README.en.md
└── LICENSE
```

Scheduling lives in `FunRandomRounds.cs`:

- `round_end` / `round_prestart`: stop the current rule and unlock buying
- `round_start`: wait about 2.5s before picking a rule, to skip MatchZy `mp_restartgame` bursts
- `warmup_end`: clear the used-rule pool for the new match
- Picks randomly from unused rules; `.funrand set` can still force a repeat

---

## Adding a rule

1. Create a class under `Rules/` that extends `RoundRule`.
2. Implement `Name` and `Description`.
3. Override lifecycle methods as needed:

| Method | When |
|------|------|
| `Start()` | Rule becomes active this round |
| `Stop()` | Round ends or the rule changes; restore anything you mutated |
| `OnPlayerSpawn` / `OnPlayerDeath` / `OnPlayerHurt` | Player events |
| `OnGrenadeThrown` / `OnWeaponFire` | Throw / fire |
| `OnFreezeEnd` | Freeze time ends |
| `OnTick` / `OnPostEntityThink` / `OnCheckTransmit` | Per-tick or visibility |

Optional properties:

- `BlocksBuy`: block the buy menu
- `BlocksBuyAndPickup`: also intercept `CanAcquire` pickups
- `AllowsWeapon(name)`: items still allowed under a restricted loadout

4. Append a factory in `RuleRegistry.cs`:

```csharp
plugin => new YourNewRule(plugin)
```

The number is the list order, starting at 1.

5. If the rule changes CVars, add their names to `CvarSnapshot.cs` `Names`, and restore them in `Stop()`.
6. `dotnet build -c Release`, deploy the DLL, then confirm with `.funrand list`.

Composite rules can hold existing rule instances and call `Start` / `Stop` on them (see `BhopTaserRule` and `ChainReactionKingRule`).

---

## Notes

- Do not enable [Fun1v5](https://github.com/IsNotSunal/Fun1v5) `.fun1v5` and `.funrand` at the same time
- Some rules change buy settings, grenade limits, friendly fire, gravity, and similar CVars; they should restore when the rule or plugin stops
- Rules such as 黑客来袭 depend on client visibility and may differ across CSS / game versions
- Hot reload (`css_plugins reload FunRandomRounds`) turns the mode off

---

## License

MIT. Plugin code is MIT-licensed.

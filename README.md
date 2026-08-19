# FunRandomRounds

[中文](README.md) | [English](README.en.md)

CS2 专用服务器插件：在 **MatchZy** 正式比赛中，每一小局随机启用一条趣味规则。同一大局内规则不会重复，热身结束 / 新比赛开始时重置规则池。

当前版本：**1.5.8**  
框架：[CounterStrikeSharp](https://docs.cssharp.dev/)

配套插件：[Fun1v5](https://github.com/IsNotSunal/Fun1v5)（不要和本插件同时开启）

---

## 功能概览

- 每回合从规则池中随机抽取一条规则，并在聊天和屏幕中央播报
- **同一大局内规则不重复**；全部用完后本回合按「正常」规则进行
- 热身期间不抽规则；热身结束、开赛重启后开始
- 规则结束时尽量还原 CVar、购买状态和玩家属性
- 管理员可强制指定本回合规则，方便测试
- 不要和 [Fun1v5](https://github.com/IsNotSunal/Fun1v5) 的 `.fun1v5` 同时开启

---

## 运行环境

| 依赖 | 说明 |
|------|------|
| CS2 Dedicated Server | Windows / Linux |
| [Metamod:Source](https://www.sourcemm.net/) | CS2 版本 |
| [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) | 建议与 API `1.0.342` 同代或更新 |
| [MatchZy](https://github.com/shobhit-pathak/MatchZy) | 开赛、准备、BO 流程仍由 MatchZy 负责 |

本插件只在 MatchZy 开赛后的正式回合里抽规则，不替代 `.ready` / `.start`。

---

## 安装

1. 在本仓库 [Releases](../../releases) 下载 `FunRandomRounds.dll`，或按下方说明自行构建。
2. 复制到服务器插件目录：

```text
csgo/addons/counterstrikesharp/plugins/FunRandomRounds/FunRandomRounds.dll
```

3. 重启 CS2 服务器，或在服务器控制台执行：

```text
css_plugins load FunRandomRounds
```

4. 确认日志出现 `FunRandomRounds` 加载成功。

管理员判定与 MatchZy 一致：

- CounterStrikeSharp 权限：`@css/generic` 或 `@css/root`
- 或 `csgo/cfg/MatchZy/admins.json` 中登记的 SteamID

---

## 使用方式

管理员在聊天输入（点号命令），或在服务器控制台使用 `css_funrand`。

| 聊天 | 控制台 | 作用 |
|------|--------|------|
| `.funrand` | `css_funrand` | 开启随机规则模式 |
| `.funrand off` | `css_funrand off` | 关闭并还原开启前的服务器设置 |
| `.funrand list` | `css_funrand list` | 查看全部规则编号 |
| `.funrand set 3` | `css_funrand set 3` | 强制本回合为第 3 条（测试用，可重复） |

聊天别名：`.rand`、`.random`。

开启后：

1. 仍用 MatchZy 开赛：玩家 `.ready`，管理员 `.start`
2. 热身结束、开赛重启后，每小局随机一条未使用过的规则
3. 屏幕中央约 10 秒显示规则名和描述
4. 关闭模式或整场比赛结束后，规则池重置

手动 `.funrand set` 仍可强制抽到已经出现过的规则。

---

## 规则列表

编号与 `.funrand list` / `.funrand set` 一致。

| 编号 | 名字 | 效果 |
|------|------|------|
| 1 | 正常 | 正常比赛 |
| 2 | 玉面手雷王 | 开局无限手雷，不能购买 |
| 3 | 击杀传送 | 击杀敌人时传送到敌人位置 |
| 4 | 跳狙飞人 | 重力减小，枪械无扩散 |
| 5 | 吸血鬼 | 造成多少伤害获得多少血量 |
| 6 | 黑客来袭 | 所有人获得透视 |
| 7 | 身法大王 | Auto BunnyHop，取消限速 |
| 8 | 雷电法王 | 开局电击枪且无限充能，不能购买 |
| 9 | 脆皮大学生 | 所有人 1 血，开局诱饵弹，不能购买 |
| 10 | 无限制下包 | 冻结倒计时结束后，C4 可安装在任何地方 |
| 11 | 豪气冲天 | 空枪立刻丢掉手中枪械 |
| 12 | 无限道具 | 无限道具 |
| 13 | 献祭队友 | 打队友回血 |
| 14 | Hide | 隐身，发出声音会短暂现形 |
| 15 | 连锁反应 | 地上的道具可以受到伤害并引爆 |
| 16 | CS2但是CF | 移动散射和后坐力大幅度减小 |
| 17 | 我是卡莎！ | 人物缩小 |
| 18 | 身法雷电法王 | 无限连跳取消限速，仅无限电击枪 |
| 19 | 连锁反应大王 | 无限道具，地上道具可受到伤害并引爆 |
| 20 | 马了 | 所有枪械强制扩散 |
| 21 | 火车头 | 只有刀，移动速度 × 5 |
| 22 | 内鬼？！！！ | 出生位置玩家位置随机调换 |
| 23 | 大洗牌 | 阵营玩家随机互换（保持两边人数平衡） |
| 24 | 夺舍的来 | 击杀人继承对方武器和道具 |
| 25 | 随机武器 | 击杀时步枪换随机步枪，手枪换随机手枪 |

---

## 从源码构建

### 环境

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（或更高，能编译 `net8.0` 即可）
- Windows / Linux / macOS 均可编译

### 编译

```bash
git clone https://github.com/IsNotSunal/FunRandomRounds.git
cd FunRandomRounds
dotnet restore
dotnet build -c Release
```

成功后输出：

```text
bin/FunRandomRounds.dll
```

把该 DLL 复制到服务器插件目录即可。不要把 `CounterStrikeSharp.API.dll` 一起拷进去，运行时由 CounterStrikeSharp 提供。

### 项目文件说明

`FunRandomRounds.csproj` 关键项：

| 属性 | 值 | 含义 |
|------|----|------|
| `TargetFramework` | `net8.0` | 与 CounterStrikeSharp 运行时一致 |
| `AllowUnsafeBlocks` | `true` | 部分规则需要 schema / 内存操作 |
| `OutputPath` | `bin/` | 输出目录不带 `net8.0` 子目录 |
| `CounterStrikeSharp.API` | `1.0.342` | 编译引用；`PrivateAssets=all`，不复制到输出 |

升级 CSS 时，同步修改 csproj 里的 API 版本，并在目标服务器上使用对应的 CounterStrikeSharp 构建。

---

## 项目结构

```text
FunRandomRounds/
├── FunRandomRounds.cs          # 插件入口：命令、回合调度、规则池
├── FunRandomRounds.csproj      # .NET 8 工程
├── CvarSnapshot.cs             # 开启模式时保存 / 关闭时还原 CVar
├── WeaponGuard.cs              # CanAcquire 拦截，限制禁购规则的拾取
├── Rules/
│   ├── RoundRule.cs            # 规则基类
│   ├── RuleRegistry.cs         # 规则登记表（编号顺序）
│   ├── WeaponUtil.cs           # 给枪 / 丢枪 / 装备辅助
│   ├── RestrictedLoadout.cs    # 限制配装 + 购买锁定 / 解锁
│   └── *Rule.cs                # 各条具体规则
├── README.md
├── README.en.md
└── LICENSE
```

调度逻辑在 `FunRandomRounds.cs`：

- `round_end` / `round_prestart`：停止当前规则并解锁购买
- `round_start`：延迟约 2.5 秒再抽规则，避开 MatchZy 多次 `mp_restartgame`
- `warmup_end`：清空本场已用规则池
- 抽取时从未使用过的规则中随机；`.funrand set` 不占用「禁止重复」限制以外的强制指定能力

---

## 添加新规则

1. 在 `Rules/` 新建类，继承 `RoundRule`。
2. 实现 `Name`、`Description`。
3. 按需要覆盖生命周期：

| 方法 | 时机 |
|------|------|
| `Start()` | 本回合规则生效 |
| `Stop()` | 回合结束或切换规则，必须还原自己改过的状态 |
| `OnPlayerSpawn` / `OnPlayerDeath` / `OnPlayerHurt` | 玩家事件 |
| `OnGrenadeThrown` / `OnWeaponFire` | 投掷 / 开火 |
| `OnFreezeEnd` | 冻结时间结束 |
| `OnTick` / `OnPostEntityThink` / `OnCheckTransmit` | 每帧或可见性 |

可选属性：

- `BlocksBuy`：禁止购买菜单
- `BlocksBuyAndPickup`：同时拦截 `CanAcquire` 拾取
- `AllowsWeapon(name)`：禁购规则下仍允许的物品

4. 在 `RuleRegistry.cs` 的 `Factories` 末尾加一行：

```csharp
plugin => new YourNewRule(plugin)
```

编号就是列表中的顺序（从 1 开始）。

5. 若规则会改 CVar，把名字加进 `CvarSnapshot.cs` 的 `Names`，并在 `Stop()` 里还原。
6. `dotnet build -c Release`，部署 DLL 后 `.funrand list` 确认新规则出现。

组合规则可以直接在新类里持有已有规则实例，分别调用 `Start` / `Stop`（参见 `BhopTaserRule`、`ChainReactionKingRule`）。

---

## 注意事项

- 不要同时开启 [Fun1v5](https://github.com/IsNotSunal/Fun1v5) 的 `.fun1v5` 和 `.funrand`
- 部分规则会改购买、手雷上限、友伤、重力等 CVar；关闭插件或规则结束时应自动还原
- 「黑客来袭」等规则依赖客户端可见性，效果因 CSS / 游戏版本可能有差异
- 热重载（`css_plugins reload FunRandomRounds`）会关闭当前模式

---

## License

MIT。插件代码按 MIT 许可使用。

# HanAirDropS2 2.4.1 更新与风险检查

检查日期：2026-10-11。使用本地 SwiftlyS2-Toolkit 的编辑、GameHooks、生命周期及审计指南，并核对 SwiftlyS2 官方接口与实现。随后按用户授权，仅修复第 1 项的循环提前退出；其他风险未修复。

最终交付仅包含空投 2.4.1。武器系统迁移候选在 ZR＋RPG 实服对比中改变了百分比加成顺序（旧版 120、候选 102），因此恢复原版，不交付候选；RPG、ZR 源码没有改动。详见 `SERVER-TEST-2.4.1.md`。

## 本次改动

- `src/HanAirDrop.Event.cs`：`HookEvents` 将旧实体事件迁移到 `GameHooks.Entities.StartTouch.Pre` / `TakeDamage.Pre`，回调改为对应的 `ref` context。
- 原事件同样在原调用之前触发，继续使用 Pre 保持时机。触碰的玩家、机器人、存活状态、箱子索引及名称筛选保持原样。
- 伤害仍按攻击者名称 `华仔空投_` 筛选，直接通过 `ctx.Params.Info.Damage = 0` 写回原生伤害信息，继续原调用；未增加取消伤害调用的行为，也未读取可空的 `DamageResult`。
- 同一事件服务负责新 Hook 的订阅与 `UnhookGameHooks` 退订；`Unload` 在释放服务容器之前调用退订。新 Hook 的订阅与退订有状态保护。
- `src/HanAirDropS2.cs`：插件元数据版本从 `2.4.0` 升到 `2.4.1`。
- `src/HanAirDrop.Event.cs`、`src/HanAirDrop.Helpers.cs`：移除没有被使用的 `using Mono.Cecil.Cil`。原源码在当前依赖下因这两行无法编译，删除不涉及运行逻辑。
- 更新 `net10.0/HanAirDropS2.dll`、`.pdb` 和 `.deps.json`。配置和翻译保持原样。
- 后续修复：`OnRoundStart` 玩家循环内的四处 `return HookResult.Continue` 改为 `continue`，遇到无效 pawn/controller、机器人或未存活玩家时继续处理其他玩家及定时器。版本仍为本次待交付的 2.4.1。

## 验证结果

本次实际还原的编译依赖为 `SwiftlyS2.CS2 1.4.14`，目标为 `.NET 10 / x64`。项目原有的浮动依赖 `Version="*"` 保持原样。

| 检查 | 实际结果 | 结论 |
| --- | --- | --- |
| Release 构建 | 0 警告、0 错误 | PASS |
| SwiftlyS2 自身 `ObsoleteApiScanner.Scan` 扫描旧 DLL | 复现用户日志中的 5 条过时 API 使用 | PASS |
| 同一扫描器检查新构建及发布目录 DLL | 过时 API 使用均为 0 | PASS |
| 发布 DLL 与构建 DLL 比较 | 字节一致，插件元数据版本为 2.4.1 | PASS |
| 新 Hook 注册、重复注册、重复退订、退订后重新注册 | 仅订阅 Pre，委托数量及移除符合预期 | PASS |
| 空触碰实体、无效攻击者回调 | 安全返回；无效攻击者保持原伤害，空伤害结果无需读取 | PASS |
| 本地回合回归 | 无效玩家、缺少 pawn 后仍继续遍历并更新定时器 | PASS |
| 本地 CS2 服务器验证 | 按用户指定文档实际执行，详细结果见 `SERVER-TEST-2.4.1.md` | 见测试报告 |

本地回归使用真实编译 DLL、真实 SwiftlyS2 扫描器及 context 类型，Hook 注册入口使用替身；它不等于游戏引擎内的碰撞与拾取验证。回归源码和结果留在被 Git 忽略的 `codex-obj/gamehooks-verification/`；旧发布文件留在 `codex-obj/baseline-2.4.0/`。

发布 DLL SHA256：`7BFB41D4FBBAB5FFF68772A7ED764B751D9B5521065C1AB00589810E7F8368D6`。

## 风险处理状态：仅修复第 1 项的循环提前退出

以下为静态检查结论，不表示已在当前服务器复现。P1 表示应优先评估的功能或权限问题，P2 表示特定状态或配置下的问题，P3 表示较低影响的问题。

### 1. P1：回合初始化可能提前结束（循环提前退出已修复）

- 定位：`src/HanAirDrop.Event.cs`，`OnRoundStart`，约 148–161 行。
- 原问题证据：遍历玩家时，遇到无效 pawn/controller、机器人或未存活玩家使用 `return HookResult.Continue`，会退出整个事件处理。
- 影响：后续玩家的拾取额度可能不重置，后面的定时器更新也不会执行；首个回合可能不启动自动空投，已有定时器可能沿用旧设置。
- 已修复：这四个分支改为 `continue`，不再退出整个事件处理。
- 回归：本地回归确认无效玩家、缺少 pawn 时仍更新定时器；本地实服有 16 个机器人时定时器正常启动，真人额度归零后新回合恢复为 2。
- 中途加入玩家的额度初始化属于原审计附带建议，本次没有修改。

### 2. P1（仅使用附带示例插件时）：发奖命令可被玩家直接调用

- 定位：`Example of creating command-based items for airdrop support/HanAirDropS2Command.cs`，`Ak` / `M4` / `Negev`。
- 证据：三个命令仅验证玩家对象并发放武器，没有空投授权状态或权限检查；命令名称和默认配置在仓库公开。
- 影响：若部署这些示例命令，知晓名称的玩家可绕过拾取条件直接领取武器。随机命令名不能作为授权机制。
- 修复方向：在发奖入口校验由空投生成的一次性授权，或改用受控共享接口发奖。
- 回归建议：普通玩家直接执行配置中的命令应被拒绝；合法拾取仍能正常发奖。未部署该示例时，此项不代表主插件存在同样的发奖漏洞。

### 3. P2：关闭自动空投后旧定时器仍可能运行

- 定位：`src/HanAirDrop.Event.cs`，`OnRoundStart` 约 177–190 行；`src/HanAirDrop.Services.cs`，`CreateDrop`。
- 证据：判断 `AirDropEnble == false` 或 `AirDropMode == 1` 后提前返回，发生在取消旧定时器之前；定时器回调及 `CreateDrop` 没有检查这两个开关。
- 影响：热重载关闭空投或改成仅死亡掉落后，之前已启动的定时生成可能继续，到下一回合也不一定停止。
- 修复方向：停用时显式取消定时器，回调复核自动生成条件；同时保留管理员手动生成的既有行为。
- 回归建议：定时模式运行中改为关闭或死亡模式，等待超过一个间隔及跨回合后检查生成数量。

### 4. P2：没有可领取道具时也会消耗箱子和额度

- 定位：`src/HanAirDrop.Services.cs`，`BoxTouch`，约 433–483 行。
- 证据：先删除箱子并扣除额度，再筛选启用状态、名称和玩家道具权限；`validItems.Count == 0` 时直接返回。
- 影响：配置名称不匹配、所有道具禁用或玩家无任何道具权限时，会出现箱子消失、额度扣除、没有奖励。发奖命令未部署或未成功执行时也缺少发放成功反馈。
- 修复方向：确定可用道具及发放路径后再消费箱子和额度；是否需要失败恢复应根据发奖接口设计决定。
- 回归建议：所有道具禁用、无道具权限、道具名称不匹配、外部发奖插件未加载。

### 5. P2：跨回合存活的箱子可能无法拾取

- 定位：`src/HanAirDrop.Event.cs`，`OnRoundStart` 的 `BoxData.Clear`；`src/HanAirDrop.Services.cs`，箱子寿命计时与 `BoxTouch`。
- 证据：回合开始清空所有箱子数据，但没有在该处删除箱子实体；触碰需要查到 `BoxData` 才会拾取。
- 影响：如果实体被游戏或其他插件保留到下一回合，且寿命尚未到期，玩家可能看到箱子却无法拾取。默认寿命较短及引擎回合清理可能使此条件不出现。
- 修复方向：回合重置时同步清理对应实体，或明确允许跨回合箱子并保留对应数据。
- 回归建议：延长箱子寿命，在回合结束前生成，观察下一回合实体和数据是否同时存在。

### 6. P2：延迟音效回调持有旧玩家对象

- 定位：`src/HanAirDrop.Helpers.cs`，`PlayBlockSound` 约 339–342 行；`src/HanAirDrop.Services.cs`，`BoxTouch` 约 491–494 行。
- 证据：`NextTick` 闭包捕获 `IPlayer`，在下一 tick 读取 `player.PlayerID`，没有重新确认会话有效性。
- 影响：玩家在两 tick 之间断开时，读取可能失效；槽位被复用时音效接收者也可能不再对应原玩家。未在实服复现。
- 修复方向：保存会话标识，在回调中重取并验证玩家；单纯移除接收者时只使用预先复制的槽位值。
- 回归建议：触碰后立即断线、快速重连及槽位复用。

### 7. P2：浮动 SDK 依赖使构建结果随时间变化

- 定位：`src/HanAirDropS2.csproj` 的 `SwiftlyS2.CS2 Version="*"`。
- 证据：原发布依赖为 1.2.2，本次还原为 1.4.14；两处多余 Mono 引用已经导致原源码无法用当前依赖直接编译。
- 影响：后续同一份源码可能还原到不同 API 或依赖集，难以保证重建一致性，也需要核对服务器框架版本。
- 修复方向：在单独升级中确定服务器目标 SDK，再固定依赖或使用锁文件；本次未修改依赖策略。
- 回归建议：干净环境还原、核对目标服务器框架、加载新 DLL。

### 8. P3：权重缺少有效性约束

- 定位：`src/HanAirDrop.Helpers.cs`，`SelectRandomBoxConfig` / `SelectByProbability`。
- 证据：权重直接求和并用于随机选择；全部为 0 时 `randomPoint` 为 0，第一个候选满足 `randomPoint <= cumulative`。
- 影响：权重全 0 时仍选择首个候选，负数等异常值会使选择结果偏离配置预期。
- 修复方向：明确权重允许范围及总和为 0 的处理方式，在配置加载时校验。
- 回归建议：全 0、负数及正常权重比例。

已检查主线程同步阻塞与 Hook 内 JSON/IO：目标源码未发现 `.Wait()`、`.Result` 或 Hook 内 JSON/磁盘/网络操作；本次没有增加后台线程或让 `ref` context 逃逸。

## 上服验证建议

替换插件目录中的 DLL、PDB 和 deps 文件，配置沿用现有文件。确认加载版本为 2.4.1、五条过时 API 告警消失，再核对实服自定义资源、队伍与额度限制。已执行用户指定的本地服务器测试，未更新真实生产服务器。

官方接口依据：[实体触碰 GameHooks](https://github.com/swiftly-solution/swiftlys2/blob/master/managed/src/SwiftlyS2.Shared/Modules/GameHooks/HooksCategories/Entities/ITouch.cs)、[实体伤害 GameHooks](https://github.com/swiftly-solution/swiftlys2/blob/master/managed/src/SwiftlyS2.Shared/Modules/GameHooks/HooksCategories/Entities/ITakeDamage.cs)。

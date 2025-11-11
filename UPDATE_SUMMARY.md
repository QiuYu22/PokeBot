# 更新摘要

## 1. Showdown 翻译支持
- 新增 `ShowdownTranslator<T>` 与 `ShowdownTranslatorDictionary`，识别中文宝可梦描述、性别、形态、持有物、努力值/个体值、太晶属性、证章、技能等信息并生成英文 Showdown 文本。
- `TranslateIfChinese` 通过 `ContainsCjk` 判断是否含中文，仅在必要时执行完整翻译，避免纯英文场景的额外扫描。
- 支持代际特性：`PK8` 的超极巨化、`PA8/PA9` 的头目标记、`PK9` 的太晶属性、奖章和规模标记等。

## 2. 各平台中文输入兼容
- Discord：在 `TradeModule`, `Helpers.ProcessShowdownSetAsync`, `AutoLegalityExtensionsDiscord` 中统一调用 `TranslateIfChinese`，合法化命令、批量交易、蛋交易均可使用中文。
- Twitch：`TwitchCommandsHelper` 在加入队列前先尝试中文翻译，确保直播指令解析一致。
- 合法化命令会根据世代选择适配的翻译器，避免太晶/奖章等 9 代特有字段污染旧世代配置。

## 3. 非法道具阻止名单本地化
- `NonTradableItemsPLZA` 将禁止交易的道具分为英文 (`BlockedItemNamesEn`) 与中文 (`BlockedItemNamesZh`) 两个列表，分别对照 `GameInfo.GetStrings("en")` 与 `GameInfo.GetStrings("zh-Hans")`。
- 为所有条目补充中文注释，方便维护与查阅。

## 4. 证章标题双语输出
- `TradeExtensions` 中将 `MarkTitle` 拆分为 `MarkTitleEn/MarkTitleZh`，并新增头目、巨人等特殊称号的中英文常量。
- `HasMark` 增加 `preferChinese` 参数，可根据调用需求返回中文或英文称号，默认保持原有英文输出。

## 5. 其他改进
- `BatchHelpers` 与相关流程自动复用翻译逻辑，批量交易也支持中文 Showdown 文本。
- 多处新增中文注释，提升代码可读性。
- 每次修改后执行 `dotnet build SysBot.NET.sln` 确认零警告零错误，保持解决方案可编译。

> 本文档用于概述当前分支新增/更新功能，便于快速了解改动范围与影响。


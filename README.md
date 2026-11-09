<p align="center">
  <img src="assets/icon.png" width="160" alt="日常助手原创厨师帽地鼠图标">
</p>

# 时光餐厅 · 日常助手

《时光餐厅》的非官方辅助模组，提供今日待答题与答案提示、答题辅助、地鼠自动击打和收益统计。支持独立悬浮答案。新增服务员与客人移动速度微调及悬浮调整框。

**当前版本：1.2.1** · [Steam创意工坊订阅（推荐）](https://steamcommunity.com/sharedfiles/filedetails/?id=3834109830) · [GitHub手动下载（备选）](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases/tag/v1.2.1) · [历史发行版](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases) · [游戏Steam商店](https://store.steampowered.com/app/4399360/?l=schinese) · [MIT许可证中文对照](LICENSE.zh-CN.md)

1.2.1字体修复版已在Steam创意工坊发布，日常使用优先订阅工坊版；GitHub下载作为备选。

## 功能

| 功能 | 使用方式 |
| --- | --- |
| 今日答题 | 查看当天尚未回答的题目及正确答案，支持自己答题或确认后自动完成剩余题目。 |
| 一键完成地鼠 | 二次确认后连续完成当天剩余次数，汇总本次实际获得的板牙和面具等奖励。 |
| 自动打地鼠 | 在自己的餐厅等待自然刷新，出现2秒后自动击打。 |
| 收益记录 | 按存档和日期记录实际结算收益，包含启用后手动击败的记录。 |
| 悬浮答案 | 可独立拖动、关闭；自己答题时同步显示，答题后自动刷新。 |
| 移动速度 | 服务员、普通及包房客人分组调整，1.0至10.0倍、步长0.1倍，支持滑块、输入和悬浮框。 |

奖励和每日额度由原版流程处理。仅处理当天剩余题目或次数；今日收益无法追溯模组启用前缺失的记录。

## 1.2.1字体修复

修复界面偶发整页粗黑：固定中文正文字体，使用独立材质清除异常扩张、描边和阴影，保留原有按钮颜色状态。详情见[1.2.1更新说明](docs/versions/v1.2.1.md)。

## 安装与更新

### 首选：Steam创意工坊订阅安装（推荐）

1. 完整退出游戏，打开[日常助手创意工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3834109830)，登录拥有《时光餐厅》的Steam账号并点击“订阅”。
2. 等待Steam下载完成，再启动游戏。
3. 在游戏主菜单的“模组”中勾选“日常助手：答题与打地鼠”；首次启用时阅读游戏显示的模组提示。
4. 进入自己的餐厅，点击右上角、“我的餐厅”左侧的厨师帽地鼠入口。

工坊订阅版由Steam管理下载与后续更新。更新完成后，请完整重启游戏以加载新版代码。

如果此前手动安装过，请先退出游戏，将`游戏目录\Mods\DailyAssistant`移到`Mods`目录之外备份，再使用工坊版，避免同一模组从两个渠道重复加载。模组设置文件可保留。

### 备选：GitHub手动下载安装

需要指定历史版本，或无法通过工坊安装时，可使用[GitHub发行版](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases)。

1. 完整退出游戏；若已订阅工坊版，先取消订阅并确认游戏仅保留一个安装来源。
2. 下载所需版本的`DailyAssistant-v版本号.zip`安装包并解压；日常使用无需下载平台自动生成的源码压缩包。
3. 将包内整个`DailyAssistant`文件夹放入游戏目录的`Mods`文件夹，最终结构应为`游戏目录\Mods\DailyAssistant\DailyAssistant.dll`。
4. 启动游戏，在主菜单的“模组”中勾选“日常助手：答题与打地鼠”。

手动更新时先退出游戏，备份旧模组目录，再用新版完整替换`DailyAssistant`文件夹；备份放在`Mods`目录之外。用户设置独立保存在`UserData\Current\DailyAssistant\state.json`，正常更新或切换安装渠道无需删除该文件。

停用时在模组列表取消勾选并完整重启游戏。更换动态库后也需完整重启，避免仍运行旧代码。回退旧版前建议备份模组自己的设置文件。


## 快速使用

打开右上角、“我的餐厅”左侧的厨师帽地鼠入口，在对应页面手动启用需要的功能。

- 答题：开启“答题辅助”，选择“自己答题”或“完成剩余答题”。可点击“悬浮答案”独立查看。
- 地鼠：选择“一键完成”并确认，或开启“自动打地鼠”等待自然刷新。
- 速度：在“移动速度”页开启“速度微调”，分别调整服务员与客人倍率；右侧输入后按回车或离开输入框应用。非法值会拒绝并提示修改；关闭功能开关立即恢复原速。

仅初次安装且没有保存设置时，所有功能默认关闭；之后保存并恢复开关及倍率。关闭浮窗只收起窗口，停止功能需关闭对应开关。升级保留旧版已有设置，新增开关默认关闭；浮窗和批量操作不会在重启后自动打开或执行。

完整操作见[使用说明](DailyAssistant/使用说明.md)。

## 版本记录

| 版本 | 主要变化 |
| --- | --- |
| [1.2.1](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases/tag/v1.2.1) | 修复字体整页粗黑，固定正文字体与独立材质 |
| [1.2.0](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases/tag/v1.2.0) | 移动速度微调与设置记忆 |
| [1.1.1](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases/tag/v1.1.1) | 修复答题提示的颜色标签与缺字 |
| [1.1.0](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases/tag/v1.1.0) | 悬浮答案与2秒击打等待 |
| [1.0.1](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases/tag/v1.0.1) | 原创厨师帽地鼠图标 |
| [1.0](https://github.com/SiH-zh/shiguangcanting-helper-mod/releases/tag/v1.0.0) | 首次发布：答题与打地鼠 |

详见[更新记录](CHANGELOG.md)及[各版本说明](docs/versions)。1.0对应规范化标签`v1.0.0`；所有历史版本均由本地已交付文件归档，提交历史是本次按版本重建的快照，非原始开发过程记录。

## 适用范围与已知限制

本项目最近于2026年11月9日（北京时间）针对Windows试玩版程序检查，游戏本地构建编号为`26431062`。游戏更新后相关接口可能变化，不保证兼容所有后续版本或同时修改相同逻辑的其他模组。

已确认工坊下载文件与本次修复程序一致，游戏日志显示1.2.1加载成功并使用固定中文正文及独立材质；这不等于全部场景和按钮状态都已完成目视复测。游戏更新相关接口后可能需要适配。

游戏需保持运行。地鼠自动处理需满足自己的餐厅营业及原版场景条件，电脑休眠或游戏退出后不工作。游戏启用模组对成就或排行榜的处理以游戏自身提示为准。

构建及隔离验证记录见[本版本详情](docs/versions/v1.2.1.md)。验证结果不等同于完整实机覆盖；遇到问题可提交[问题反馈](https://github.com/SiH-zh/shiguangcanting-helper-mod/issues)，附游戏版本、模组版本、操作步骤及相关日志，注意隐藏个人信息。

## 源码与编译

```text
DailyAssistant/
  DailyAssistant.dll   模组动态库
  icon.png             模组封面与入口图标
  info.ini             原生模组描述
  使用说明.md
  Source/              对应版本完整源码、编译脚本及验证源码
docs/versions/         历史版本详情
assets/icon.png        仓库展示图标
```

详见[编译说明](docs/编译说明.md)。编译引用自己已安装游戏提供的程序集，仓库及发行包不包含游戏程序或用户存档。日常使用优先通过Steam创意工坊订阅安装，也可从GitHub发行版手动下载安装；无需自行编译或安装额外加载器。

## 许可证与相关项目

本仓库自行编写的代码、文档和原创厨师帽地鼠图标使用[MIT许可证](LICENSE)，附[中文对照](LICENSE.zh-CN.md)。标准英文正文用于平台识别，中文对照便于阅读。游戏本体及第三方内容不属于本许可证授权范围，详见[第三方说明](第三方说明.md)。

这是玩家制作的非官方模组。[《时光餐厅》游戏商店](https://store.steampowered.com/app/4399360/?l=schinese)由游戏开发者维护。日常安装推荐[Steam创意工坊订阅](https://steamcommunity.com/sharedfiles/filedetails/?id=3834109830)；本仓库提供源码、版本记录和备选手动安装包。

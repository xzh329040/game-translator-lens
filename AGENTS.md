# Game Translator Lens — 维护指南

本文档写给后续接手本项目的 AI agent/维护者。目标是避免重复摸索，保留当前阶段的关键约束。

## 项目定位

- 本项目是面向国际服游戏的轻量 Windows OCR 翻译 overlay 工具。
- 默认目标语言为简体中文。
- 支持多语言游戏聊天翻译，不局限于单一游戏。
- 语音、TTS、漫画模式等非核心功能已裁剪，扩展前需确认需求。

## 当前技术栈

- UI：WPF / .NET 9 / Windows x64。
- 项目文件：`Game-Translator-Lens.csproj`。
- OCR：当前固定 OneOCR，WinOCR 不可用，不要作为默认路径恢复。
- 翻译：`DeepSeek` 和 `OpenAI Compatible` 走 OpenAI-compatible chat completions。
- Overlay：独立 WPF 窗口，支持透明背景、鼠标穿透、拖动、调整大小、滚动历史。
- 回话助手：overlay 底部可输入中文，翻译为目标语言并复制到剪贴板；不自动发送游戏聊天。
- 用户数据目录：`%AppData%\GameTranslatorLens`。

## 重要目录

- `Core/`：设置、术语表、消息解析、去重、翻译协调逻辑。
- `Ocr/`：截图、OCR 引擎和游戏聊天图像预处理。
- `Overlay/`：翻译 overlay 窗口。
- `Translation/`：OpenAI-compatible API 请求、模型列表获取。
- `Resources/GameGlossary.zh-CN.json`：游戏术语表。
- `Docs/`：架构、测试说明、历史决策。
- `Tools/OcrPreprocessLab/`：本地 OCR 预处理对比实验工具。
- `Tools/GlossaryValidator/`：词库 JSON、重复 alias、短 alias 风险检查工具。
- `captured-screenshots/`：本地 OCR 样本采集输出，已在 `.gitignore`，不要提交。
- `dist/`：发布产物，已在 `.gitignore`，不要提交。
- `app/`：本地 build 输出，已在 `.gitignore`，不要提交。

## 核心逻辑

### OCR 与消息解析

- 框选区域应完整包含游戏聊天文本框内容。
- 系统提示通常没有 `[player]：` 格式，应和玩家消息分开处理。
- 当前 OneOCR 使用自动识别。
- 当前主线 OCR 预处理是单一路径：保留颜色、2x 放大、轻微对比/亮度/gamma 增强、轻锐化。
- `Tools/OcrPreprocessLab` 可用于比较预处理方案；实验报告输出到 `Docs/ocr-lab-output/`，不要提交。

### 去重策略

- 维护有顺序的聊天消息列表，严格以玩家单条消息为翻译单元。
- 以 `[玩家名]：text` 作为玩家消息边界，不把同一个玩家连续多句话合成一条。
- 通过有序锚点和相似文本判断新增消息，抵抗 OCR 把同一行切块或轻微识别错误。
- 翻译请求有异步队列和上限，网络高延迟时会丢弃过旧队列，避免无限堆积。
- Overlay 最多保留最近 50 条翻译记录，方便用户滚动查看，不永久保存。

### Overlay 行为

- 点击"暂停"应隐藏 overlay，但不清空历史。
- OCR 没文字时，overlay 可在翻译完成后继续显示约 5-6 秒，然后隐藏；隐藏不等于清空。
- 用户拖动或调整 overlay 大小后，应自动保存位置和尺寸。
- 透明度控制的是黑色背景透明度，文字必须保持清晰。
- 鼠标穿透关闭后，用户应能拖动、调整大小和滚动 overlay。

## 诊断工具

当前正式版程序内保留"诊断工具"入口：

- 打开数据目录。
- 打开日志文件夹。
- 导出反馈包。
- 清除本机数据。
- 检查更新。
- 诊断模式。

导出反馈包必须脱敏 API Key，只能记录是否已配置。诊断模式默认关闭，只有用户主动开启时才写入高级 debug 日志。

相关文件：

- `settings.json`：用户设置；API Key 通过 Windows DPAPI 保存为 `apiKeyProtected`，不要记录或分发明文。
- `runtime.log`：程序内运行日志。
- `crash.log`：未捕获异常日志。
- `diagnostics/feedback-*.zip`：用户点击导出反馈包生成的脱敏诊断压缩包。

## API 与模型

- DeepSeek 默认 API URL：`https://api.deepseek.com`。
- 当前默认模型：`deepseek-v4-flash`。
- 模型下拉框应优先通过"获取模型"向 `/models` 获取，降低用户手填错误。
- API Key 不应写入诊断文件、日志或崩溃日志。

## 构建与发布

使用 .NET 9 SDK 构建：

```powershell
dotnet build Game-Translator-Lens.csproj -c Release
```

发布正式包：

```powershell
powershell -ExecutionPolicy Bypass -File Tools\PackageRelease.ps1
```

注意：

- 不要每次小修都打包，用户要求确认测试完成后再最终打包时再做。
- 自包含发布会带很多 .NET runtime DLL，这是正常的；不要手删不认识的 DLL。
- 发布正式版本时必须写清楚用户可读的更新概略、重要修复和注意事项。

## Git 维护约定

- 修改前先 `git status --short`。
- 不要 push、pull、加 remote，除非用户明确要求。
- `dist/`、`app/`、`obj/` 不应提交。
- `captured-screenshots/`、`game-screenshot/`、`Docs/ocr-lab-output/` 是本地实验数据，不应提交。

## 常见风险

- 新机器选择语言或模型闪退：优先看 `crash.log` 和 overlay 坐标是否为 NaN/Infinity。
- 翻译重复：优先检查 OCR 是否把同一行切块、玩家名是否被识别变化、锚点匹配是否失效。
- 翻译不动：检查 API URL、API Key、模型、请求超时、网络延迟和队列是否积压。
- Overlay 位置不保存：检查 `OverlayLeft/Top/Width/Height` 是否写入 `settings.json`。

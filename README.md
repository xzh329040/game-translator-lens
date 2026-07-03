# Game Translator Lens

<p align="center">
  <strong>面向国际服游戏的实时 OCR 翻译 overlay</strong><br>
  <span>A real-time OCR translation overlay for international game chat</span>
</p>

<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square">
  <img alt="Platform" src="https://img.shields.io/badge/Windows-x64-0078D4?style=flat-square">
  <img alt="OCR" src="https://img.shields.io/badge/OCR-OneOCR-00A8E8?style=flat-square">
  <img alt="Status" src="https://img.shields.io/badge/status-v1.1.0-34C759?style=flat-square">
</p>

<p align="center">
  <a href="#中文">中文</a> · <a href="#english">English</a>
</p>

---

## 中文

Game Translator Lens 是一个 Windows 桌面工具，用于在国际服游戏中实时翻译聊天内容。它通过本地 OneOCR 识别你框选的聊天区域，把外文玩家消息发送到 DeepSeek 或 OpenAI-compatible API 翻译为简体中文，并按原聊天顺序显示在置顶翻译框中。

项目目标很明确：在不打断游戏视野和操作节奏的前提下，让中文玩家能快速理解队友和对手的文字沟通。

### 功能亮点

- **游戏聊天识别**：以 `[玩家名]: 正文` 这类玩家消息为翻译单元，过滤系统提示和 UI 噪声。
- **本地 OneOCR**：OCR 在本机完成，固定自动识别，不依赖云 OCR。
- **OpenAI-compatible 翻译**：支持 DeepSeek 和 OpenAI-compatible chat completions API。
- **稳定顺序显示**：用 Timeline Seq 对齐消息身份，降低 OCR 抖动、多人连续发言时的重复和乱序。
- **低存在感 overlay**：置顶透明翻译框，支持常态显示、鼠标穿透、拖动缩放和历史滚动。
- **回话助手**：在 overlay 底部输入中文，翻译为目标语言并复制到剪贴板，由用户自行粘贴发送。
- **更新与反馈**：程序可检查 GitHub Releases 更新；主窗口可导出脱敏反馈包。
- **深浅外观**：主窗口支持深色/浅色主题切换，overlay 保持游戏内深色 HUD。
- **本地隐私保护**：API Key 使用 Windows DPAPI 存储，诊断导出会脱敏。

### 快速使用

1. 从 GitHub Releases 下载最新的 `GameTranslatorLens-*-portable-win-x64.zip`。
2. 解压整个压缩包，运行外层 `GameTranslatorLens.exe`。
3. 建议解压到英文路径，例如 `C:\GameTranslatorLens\` 或 `D:\Tools\GameTranslatorLens\`。
4. 在翻译 API 中选择 `DeepSeek` 或 `OpenAI Compatible`。
5. 填写 API URL 和 API Key；DeepSeek 默认 URL 为 `https://api.deepseek.com`。
6. 点击"获取模型"，选择模型，例如 `deepseek-v4-flash`。
7. 点击"选择翻译区域"，框选游戏聊天区域，尽量完整包含玩家名和正文。
8. 点击"开始"，译文会显示在翻译框中。

### 更新与反馈

便携包内包含 `GameTranslatorLensUpdater.exe` 和 `GameTranslatorLensUninstall.exe`。自动更新会下载最新发布包，校验后替换 `app/` 目录。更新过程保留旧版备份和 `%AppData%\GameTranslatorLens` 中的设置。

如需卸载，运行外层 `GameTranslatorLensUninstall.exe`。

### 回话输入

打开"显示回话输入条"后，overlay 底部会出现输入栏。输入中文，选择目标语言，按 Enter 后程序会翻译回话。默认会自动复制译文；如果关闭自动复制，或剪贴板被其他程序占用，译文会留在回话输入框中。

程序不会自动发送游戏聊天。翻译完成后，请在游戏聊天框中粘贴，再由你自己按 Enter 发送。

### 实现逻辑

```text
selected chat region
  -> pixel diff patrol
  -> text presence gate
  -> burst OCR
  -> chat parser
  -> Timeline alignment
  -> multi-frame consensus
  -> translation queue
  -> Seq-sorted overlay
```

### 开发者命令

```powershell
dotnet build Game-Translator-Lens.csproj -c Release
```

发布包：

```powershell
powershell -ExecutionPolicy Bypass -File Tools/PackageRelease.ps1
```

回归测试：

```powershell
dotnet run --project Tools/ReplayLab/ReplayLab.csproj -c Release -- --timeline-smoke
dotnet run --project Tools/ReplayLab/ReplayLab.csproj -c Release -- --similarity Tools/ReplayLab/similarity/korean-jamo-regression.json
dotnet run --project Tools/ReplayLab/ReplayLab.csproj -c Release -- Tools/ReplayLab/fixtures/smoke-korean-short Tools/ReplayLab/fixtures/smoke-korean-short/expected.json
```

### 项目结构

```text
Core/                  设置、Timeline、解析、诊断、翻译协调
Ocr/                   OneOCR 封装与图像预处理
Overlay/               置顶翻译框
Translation/           OpenAI-compatible / DeepSeek 请求
Resources/             术语表、主题资源、UI 图标
Updater/               外层便携更新器源码
Tools/ReplayLab/       离线回放与回归断言
Tools/OcrPreprocessLab/ OCR 预处理实验工具
Docs/                  架构、测试指南
```

---

## English

Game Translator Lens is a Windows desktop app for real-time international game chat translation. It captures a user-selected chat region, recognizes text locally with OneOCR, sends foreign-language player messages to DeepSeek or any OpenAI-compatible chat completions API, and renders Simplified Chinese translations in a topmost overlay.

### Highlights

- **Game chat parsing**: player chat lines such as `[player]: message` are treated as translation units, while system/UI noise is filtered.
- **Local OneOCR**: OCR runs locally with automatic recognition and no cloud OCR dependency.
- **OpenAI-compatible translation**: DeepSeek and OpenAI-compatible chat completions APIs are supported.
- **Stable ordering**: Timeline Seq identity reduces duplicates and out-of-order display when chat history is opened or OCR jitters.
- **Low-profile overlay**: topmost transparent translation box with always-show mode, click-through, dragging, resizing, and history scrolling.
- **Reply helper**: type Chinese in the overlay input bar, translate to the target language, and copy the result to the clipboard.
- **Updates and feedback**: the app can check GitHub Releases, export a redacted feedback package, and open a bug-report entry.
- **Local secret protection**: API keys are protected with Windows DPAPI, and diagnostics redact secrets.

### Quick Start

1. Download the latest `GameTranslatorLens-*-portable-win-x64.zip` from GitHub Releases.
2. Extract the whole archive and run the outer `GameTranslatorLens.exe`.
3. An English-only extract path is recommended, such as `C:\GameTranslatorLens\` or `D:\Tools\GameTranslatorLens\`.
4. Choose `DeepSeek` or `OpenAI Compatible`.
5. Enter the API URL and API key. DeepSeek defaults to `https://api.deepseek.com`.
6. Click Fetch Models and choose a model such as `deepseek-v4-flash`.
7. Select the game chat region. Include complete player names and message text.
8. Click Start. Translations will appear in the overlay.

### Updates and Feedback

Portable packages include `GameTranslatorLensUpdater.exe` and `GameTranslatorLensUninstall.exe`. Automatic update downloads the latest release, verifies the sha256, and replaces the `app/` directory. The updater keeps a backup and preserves `%AppData%\GameTranslatorLens` settings.

To uninstall, run `GameTranslatorLensUninstall.exe`.

### Developer Commands

```powershell
dotnet build Game-Translator-Lens.csproj -c Release
```

Publish:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/PackageRelease.ps1
```

Regression:

```powershell
dotnet run --project Tools/ReplayLab/ReplayLab.csproj -c Release -- --timeline-smoke
dotnet run --project Tools/ReplayLab/ReplayLab.csproj -c Release -- --similarity Tools/ReplayLab/similarity/korean-jamo-regression.json
dotnet run --project Tools/ReplayLab/ReplayLab.csproj -c Release -- Tools/ReplayLab/fixtures/smoke-korean-short Tools/ReplayLab/fixtures/smoke-korean-short/expected.json
```

### License

This project is released under the [MIT License](LICENSE).

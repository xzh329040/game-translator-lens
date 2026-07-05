# Game Translator Lens v1.2.0

## 更新要点

- 自定义翻译搭配新增 ✎ 编辑按钮，可随时修改已添加的翻译映射。
- 自定义翻译搭配新增 × 删除按钮，方便清理不再需要的条目。
- 快速上手手册新增第 7 步「自定义翻译搭配」图文说明。
- 修复快速上手手册提示文字在暗色主题下白底白字不可见的问题。
- 启动器、更新器、卸载器统一使用 game-translator-lens-icon 图标。
- 修复 GitHub 仓库无 Release 时检查更新报错的问题，改为提示"当前已是最新版本"。

## 快速开始

1. 解压整个压缩包。
2. 建议解压到英文路径，例如 `C:\GameTranslatorLens\`。
3. 运行外层 `GameTranslatorLens.exe`。
4. 首次启动会显示快速上手指南，共 7 步。
5. 配置 DeepSeek 或 OpenAI Compatible API，点击「获取模型」并选择模型。
6. 点击「选择翻译区域」，框选游戏聊天区域。
7. 点击「开始」，译文会显示在翻译框中。
8. 可选：打开「自定义翻译搭配」手动添加常用短语翻译。

DeepSeek API 需要充值余额并按量计费，聊天翻译用量通常很小，实际费用很低。

## 更新方式

程序会检查 GitHub Releases 中的新版本。自动更新会下载最新 zip，校验 sha256 后替换 `app/` 目录，保留旧版备份和用户设置。

## 发布包结构

```text
GameTranslatorLens/
  GameTranslatorLens.exe             ← 启动器
  GameTranslatorLensUpdater.exe      ← 更新器
  GameTranslatorLensUninstall.exe    ← 卸载器
  README.md
  app/
    GameTranslatorLens.exe
    *.dll
    OneOcr/
    Resources/
    ...
```

## 反馈

遇到问题请在主窗口「诊断工具」中点击「导出反馈包」，并在 GitHub Issues 中附上生成的 zip。API Key 不会写入反馈包。

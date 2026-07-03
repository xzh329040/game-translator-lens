# Game Translator Lens v0.2.0-beta.1 测试说明

## 推荐配置

- OCR 引擎：OneOCR
- OCR 源语言：韩语、日语、英语，按当前对局主要语言选择；韩语测试优先选“韩语”，不要先用“自动”。
- 翻译：DeepSeek 或 OpenAI Compatible API。
- DeepSeek 默认 API URL：`https://api.deepseek.com`
- DeepSeek 默认模型：`deepseek-v4-flash`

## 重点测试

- 韩语聊天是否重复刷屏。
- OCR 是否把同一条消息切成多条。
- API 高延迟时 OCR 是否继续识别新消息。
- 暂停后 overlay 是否隐藏且保留历史。
- OCR 无文字后 overlay 是否延迟隐藏，下一条新消息是否追加到历史末尾。
- overlay 最多保留最近 50 条记录，主窗口日志最多保留 200 条。

## 已知限制

- API Key 当前保存在本机 AppData 配置文件中，仍是明文；请勿把自己的配置文件一起分发。
- 本 beta 不包含语音翻译。
- Windows OCR 已从界面中移除，当前固定使用 OneOCR。

## 分发提醒

解压后运行 `GameTranslatorLens.exe`。不要删除 `OneOcr`、`Resources`、语言资源目录或随包 DLL。

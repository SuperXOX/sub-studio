<p align="center"><img src="assets/sub.svg" width="112" alt="SUB Studio logo"></p>

# SUB Studio

Windows 便携字幕工具：一键转繁体，或将中文 SRT 翻译为自然口语化的多国语言。苹果极简界面、粉紫渐变与圆润卡片。

**目标语言：** 繁体中文、英语、日语、韩语、泰语、印地语（Hindi）、西班牙语、意大利语。

## 功能

- **一键繁体：** 内置 OpenCC，使用本机词典，无需联网、账号或 AI 模型。
- **离线翻译：** Qwen3-4B-Instruct-2507 本地模型；支持 CPU 和 Vulkan。首次构建下载模型后，可断网使用。
- **联网 AI：** 使用随包的官方 Codex CLI，登录自己的 Codex/ChatGPT 账号，使用该账号可用额度。需要网络与有效账号；无需在软件中填写 API Key。
- **时间轴保护：** 程序只替换字幕正文。保存前校验序号、时间码、顺序、正文行数和格式标签；源文件的时间倒退、重叠或格式错误会拒绝处理。
- **安全输出：** 新字幕保存在原文件所在目录，自动添加语言和模式后缀；重名时添加序号，保留原文件。
- 多文件队列、取消、缓存续译、目标语言独立缓存，以及结构检查报告。

时间轴校验不等于翻译语义准确。离线和联网使用不同模型，译文可能不同；发布前请结合原音检查人名、口气与含义。

## 构建与运行

系统：Windows 10/11 x64，.NET Framework 4.x，Windows PowerShell 5.1 或 PowerShell 7，以及系统自带 `tar`。离线 AI 建议 16 GB 内存（至少 8 GB），完整组件需预留约 4 GB 空间，下载缓存另需约 3 GB。

完整构建的电脑还需已安装 [Microsoft Visual C++ v14 x64 运行库](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)。脚本从系统复制三个 Microsoft 签名有效的运行库 DLL 到本地引擎目录，以供便携运行，并将其纳入完整性清单；不会将这些 DLL 提交到源码仓库。繁体轻量版不需要该构建步骤。

```powershell
git clone https://github.com/SuperXOX/sub-studio.git
cd sub-studio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

也可下载源码 ZIP、解压后双击 `build.cmd`。首次构建需要联网，脚本按 `runtime-lock.json` 下载固定版本组件并校验 SHA256，然后编译、执行全部 128 项回归检查。运行：

```text
dist\SUB Studio\字幕翻译 Studio.exe
```

只需要繁体转换时，可构建轻量版，无需下载大模型和 AI 引擎：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -TraditionalOnly
```

轻量版保留相同界面，已包含繁体转换组件；其他翻译模式需补齐运行组件后使用。完整构建再次运行 `scripts\build.ps1` 即可。

## 使用与移动

1. 拖入或添加 `.srt` 文件。
2. 点击卡片上的“一键繁体”，或选择目标语言及“离线 / 联网 AI”后点击“开始翻译”。
3. 在字幕原目录查看新文件和检查报告。

退出软件后，复制或移动**整个 `dist\SUB Studio` 文件夹**即可使用，不要只复制 EXE。普通运行不需要另外安装 Python、Node、OpenCC 或 Codex App。联网登录保存在当前电脑，移动到新电脑后需重新登录；本项目不会复制账号凭据。便携范围为 Windows x64，同机换目录已验证，第二台实体电脑尚未实测。

联网模式会将字幕正文、文件名主题、相邻字幕上下文及术语发送给联网 AI。离线模式和繁体转换在本机执行。运行后生成的 `data` 保存缓存与状态；退出软件后可删除它，以清除缓存并失去续译记录。

可选命令行示例：

```powershell
& '.\dist\SUB Studio\字幕翻译 Studio.exe' --translate --mode offline --target ja --report '.\result.json' 'D:\subtitles\video.srt'
& '.\dist\SUB Studio\字幕翻译 Studio.exe' --check --report '.\integrity.json'
```

`--target`：`zh-Hant`、`en`、`ja`、`ko`、`th`、`hi`、`es`、`it`。繁体目标始终使用本机转换。

## 开发与验证

源码使用 C#、WPF 和 Windows 系统自带 .NET Framework 编译器；无需 Visual Studio。`scripts/build.ps1` 执行编译和回归测试。GitHub Actions 构建繁体轻量版，并执行全部确定性检查；不使用个人账号或联网模型。

```powershell
.\scripts\verify-source.ps1
```

该脚本检查 Git 跟踪文件，拒绝运行组件、模型、字幕、账号文件、缓存、明显凭据和个人 Windows 主目录路径。Git 仓库仅包含源码、图标 SVG、说明、许可、固定版本清单和构建脚本；`dist`、`.downloads`、`.local`、`data` 被排除。

| 目录 / 文件 | 用途 |
| --- | --- |
| `源码/` | C#、XAML、回归测试、图标生成器和编译脚本 |
| `assets/sub.svg` | 原创 SUB 标识；构建时生成 PNG / ICO |
| `scripts/` | 下载、构建和发布前检查 |
| `runtime-lock.json` | 上游下载地址、固定版本及 SHA256 |
| `licenses/` | 第三方许可与说明 |
| `dist/SUB Studio/` | 生成的便携程序（不入库） |

欢迎通过 Issues 提交问题，通过 Pull Requests 贡献改进。请使用自行编写的最小字幕样例，避免上传私人字幕或账号信息。修改时间轴解析、格式保护、语言验证或缓存规则时，请同步更新对应回归测试。

## 许可

本项目原创源码与 SUB 标识采用 [MIT License](LICENSE)。第三方引擎、词典、模型及官方 Codex 组件遵循各自许可；构建脚本保留组件原始目录与许可文件。详见 [第三方组件](docs/THIRD_PARTY.md)。联网服务适用账号提供方的服务条款，本项目与该服务提供方无隶属关系。

## English

SUB Studio is a portable Windows SRT subtitle tool with one-click Simplified-to-Traditional Chinese conversion, local offline translation, and online translation using the official Codex CLI and your own account. Targets: Traditional Chinese, English, Japanese, Korean, Thai, Hindi, Spanish, and Italian.

The application preserves cue identifiers, timestamps, order, line counts, and formatting tags, and writes a new file beside the input. Structural validation does not guarantee translation accuracy. Build with `scripts/build.ps1` (first build requires network access), then move the entire `dist/SUB Studio` folder. Use `-TraditionalOnly` for a lightweight local OpenCC build. Windows x64 only; model binaries, subtitles, caches, and credentials are excluded from this repository.

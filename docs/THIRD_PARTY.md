# 第三方组件

MIT 许可适用于本项目原创源码与 SUB 图标。下列运行组件和模型单独遵循其上游许可。源码仓库提供下载脚本和版本清单，不包含运行组件、模型或账号凭据。

| 组件 | 固定版本 | 上游 | 许可 |
| --- | --- | --- | --- |
| OpenCC | 1.4.2 Windows x64 portable | [BYVoid/OpenCC](https://github.com/BYVoid/OpenCC) | Apache-2.0 |
| llama.cpp CPU / Vulkan | b11372 | [ggml-org/llama.cpp](https://github.com/ggml-org/llama.cpp) | MIT |
| Qwen3-4B-Instruct-2507 GGUF | `a06e946bb6b655725eafa393f4a9745d460374c9` / Q4_K_M | [unsloth model repository](https://huggingface.co/unsloth/Qwen3-4B-Instruct-2507-GGUF), [Qwen model](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507) | Apache-2.0 |
| 官方 Codex CLI Windows package | 0.160.0 | [openai/codex](https://github.com/openai/codex) | Apache-2.0；附带依赖适用各自许可 |
| Microsoft Visual C++ v14 运行库 | 来自构建电脑的已安装版本，校验 Microsoft 签名 | [官方运行库说明](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) | Microsoft 专有许可，另行适用其开发者再分发条款 |

`runtime-lock.json` 固定每项组件的下载地址和 SHA256。`licenses/` 保留已获取的许可与说明，安装脚本保留上游完整文件布局与附带 NOTICE，尤其是 Codex package 的辅助程序和 voice 依赖许可。若重新分发生成的便携包，请同时保留 `licenses/`、组件中的许可、NOTICE 和依赖说明，并遵守涉及的分发义务。

联网翻译属于账号提供方的在线服务，是否可用及额度取决于当前账号与服务条款，项目许可证不授予服务额度。项目没有提供、收集或分发任何登录凭据。

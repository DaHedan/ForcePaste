# <img width="32" height="32" alt="FP" src="https://github.com/user-attachments/assets/eab41b84-cdd4-43a9-8668-bd45695051bc" /> ForcePaste v1.0 ![GitHub 版本](https://img.shields.io/github/v/release/DaHedan/ForcePaste?include_prereleases) ![许可证](https://img.shields.io/github/license/DaHedan/ForcePaste) ![支持系统](https://img.shields.io/badge/OS-Windows_10/11-blue?logo=windows) ![总下载量](https://img.shields.io/github/downloads/DaHedan/ForcePaste/total) ![最后提交](https://img.shields.io/github/last-commit/DaHedan/ForcePaste)
_快捷键强制粘贴工具_ — 解决部分输入框禁用粘贴的问题，通过模拟键盘输入逐字粘贴剪贴板内容

__想要了解关于 ForcePaste 的详细信息，请前往 [ForcePaste v1.0 Wiki](https://github.com/DaHedan/ForcePaste/wiki/ForcePaste-v1.0-Wiki)。__

## 📜 许可协议
本项目采用 [GPL-3.0 许可证](https://github.com/DaHedan/ForcePaste/blob/main/LICENSE)

## 📦 获取工具 ![Windows](https://img.shields.io/badge/下载-Windows_应用程序-blue?logo=windows)
> 本软件安装包使用 Inno Setup Compiler 制作。  
> 本软件依赖 .NET 8.0 运行，您可以通过微软官方渠道下载安装该组件，或者下载自包含该组件的软件包。

如果你的需求是下载这个软件去使用，而不是需要源代码，请到 [**Releases ForcePaste v1.0**](https://github.com/DaHedan/ForcePaste/releases/tag/v1.0.0) 下载对应的文件，不要下载上面的 Code

### 普通用户推荐下载
Windows 64位系统：[ForcePaste_1.0_x64_selfcontained_Setup.exe](https://github.com/DaHedan/ForcePaste/releases/download/v1.0.0/ForcePaste_1.0_x64_selfcontained_Setup.exe)  
Windows 32位系统：[ForcePaste_1.0_x86_selfcontained_Setup.exe](https://github.com/DaHedan/ForcePaste/releases/download/v1.0.0/ForcePaste_1.0_x86_selfcontained_Setup.exe)

## 🖥️ 功能介绍
### 主程序
1. 运行后在屏幕边缘显示半透明悬浮球，可通过 **Ctrl+Alt+V** 触发强制粘贴，将剪贴板内容以模拟键盘输入的方式逐字粘贴到当前活动窗口。
2. 悬浮球可自由拖拽至屏幕任意位置。
3. 鼠标悬停在悬浮球上可展开操作面板，左键单击悬浮球可固定操作面板保持展开状态。
### 操作面板
1. **剪贴板** — 可查看要粘贴的内容
2. **粘贴速度** — 逐字输入间隔（1~30ms），数值越大速度越慢，默认 5ms。
3. **随机波动** — 模拟人类打字节奏的随机延迟（0~20ms），默认 0ms。
4. **快捷键** — 可自定义粘贴触发快捷键，默认 Ctrl+Alt+V。
5. **主题切换** — 支持浅色、深色、跟随系统三种模式。
6. **字体大小** — 调节悬浮球文字大小（8~24），默认 13。
7. 所有设置自动保存至 `config/settings.json`，重启后保留。
### 系统托盘
- 最小化至系统托盘后，强制粘贴功能依然可用，右键托盘图标可恢复悬浮球或退出程序。

## ⚠️ 用户须知
1. 此工具仅供非商业使用，用户需自行承担使用过程中的风险（如程序异常、设备问题等），作者不对任何直接或间接损失负责。
2. 此工具仅在用户本地存储配置数据（`config/` 目录），不会收集、上传任何用户信息。
3. 若需二次分发或商用，需提前联系作者获得授权。

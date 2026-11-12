# ScreenInk 屏幕墨层

一个 Windows 10/11 的透明屏幕绘制层：在任意程序上临时画线、框选、自由绘制，并写内联备注。适合直播讲解、录屏演示、截图评审和设计走查。

> 定位：**视觉覆膜**，不是对象级/内容级标注。它不读取目标程序内存，也不保证能识别按钮、表格或商品对象。

## 快速开始

1. 下载 `ScreenInk.exe`（Windows x64）。
2. 双击运行，出现 `ScreenInk 屏幕墨层` 小面板。
3. 在窗口下拉框选择目标程序，或保留「全屏（不锁定）」。
4. 点「开始标注」；标注中同一按钮变为「结束标注」。
5. **按住 Alt 约 0.3 秒后**，左键拖动开始画线/箭头/手绘/框。
6. 松开后，在终点弹出的输入框写备注，按 Enter 或「保存标注」。
7. 折线工具：左键逐点添加，右键完成折线。手绘：按住左键连续拖动，松开结束。
8. 已画标注可切到「选择/移动」：Alt+点击选中，拖白点改端点，拖线体移动；「改备注」「删除选中」在面板。
9. 点「结束标注」恢复普通鼠标操作；「保存/导出」按路径保存 PNG 或覆膜文件。

## 当前能力

- 任意窗口上透明覆盖绘制；
- 长按 Alt + 左键画线/箭头/框；短距离逐点折线用「折线(多点)」；手绘是按住左键连续拖动，松开后写备注；
- 目标窗口锁定、移动/缩放跟随、最小化时隐藏覆膜；
- 内联备注；
- 本地记录：`%APPDATA%\ScreenInk\annotations.jsonl`；
- 覆膜文件：面板「保存/导出」选择路径并选 PNG 或覆膜文件；「打开覆膜」加载；「浏览路径」打开最近保存目录；
- 导出 PNG：`%USERPROFILE%\Pictures\ScreenInk\ScreenInk-*.png`；
- 无网络请求、无遥测。

## 接入智能体

运行时会启动本地只读桥接（仅 127.0.0.1）：

- GET http://127.0.0.1:3212/context：目标程序、工具、最近标注、事件、启发式意图；
- GET /annotations、GET /events；
- 供本地 agent 读取，不上传网络。

MCP：运行 ScreenInk.exe --mcp（GUI 同时运行），工具包括 context/annotations/events/target/intent；配置示例 docs/mcp-config.example.json，Skill 模板 skills/screenink/SKILL.md。详见 AGENT-INTEGRATION.md。意图是关键词/行为启发式，不是读心或对象识别。

## 限制

- 仅 Windows 10/11；需要 .NET Framework 4.x（Windows 自带）。
- 不读取目标程序内容/内存；不能做对象级绑定。
- 独占全屏游戏、受保护视频、UAC 安全桌面、部分高权限窗口可能无法覆盖。
- 历史记录会保存到本地；重新选择同一目标窗口/全屏时，按 app/title 或 HWND 自动恢复（窗口标题变化时可能不恢复）。
- 使用全局鼠标/键盘钩子，部分杀毒软件可能提示；不保证在所有机器上不被拦截。
- 没有签名证书；发布 exe 可能触发 SmartScreen。

## 快捷键

- 长按 `Alt`（约 0.3 秒）+ 左键拖动：画线；
- `Ctrl+Alt+Q`：结束标注；
- `Ctrl+Alt+L`：清空当前覆膜；
- `Ctrl+Alt+P`：穿透/画线切换（预留，面板当前以结束/开始控制）。

## 从源码构建

需要 Windows + .NET Framework 4.x 的 `csc.exe`：

```bat
build.cmd
```

产物：`release\ScreenInk.exe`。

## 设计边界

ScreenInk 是 A 方案（通用视觉覆膜）。B 方案（UI Automation/CDP/DOM/OCR 对象级标注）暂不在主线，见 `FUTURE-B.md`。

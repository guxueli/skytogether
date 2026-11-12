# FUTURE-B：对象级标注（封存）

ScreenInk A 只做通用视觉覆膜。B 目标是“绑定 UI 对象/内容”的对象级标注，暂不实施。

未来若重新评估，按适配器分层：

1. Windows UI Automation：`AutomationElement.FromPoint`、`AutomationId`、`RuntimeId`、`BoundingRectangle`、`ControlType`；
2. 浏览器：Edge/Chrome CDP DOM selector；
3. Office：COM/UIA；
4. 自绘应用/游戏：应用 SDK，或视觉/OCR 兜底；
5. 通用兜底：窗口相对坐标 + OCR patch/hash。

约束：

- 不读进程内存，不做注入；
- UIPI/高权限窗口、UAC 安全桌面、独占全屏、游戏 Raw Input 都有边界；
- 对象级记录必须包含目标身份、锚点、相对几何、置信度和版本；
- 新增适配器不得污染 A 的主路径，保持 A 可独立运行。

当前决定：B 封存，A 先发布 `v0.1.0-alpha`，根据真实用户反馈再决定是否投入。

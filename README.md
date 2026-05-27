# Music Pixel Pet

基于 `Electron + React + TypeScript + Vite + C# Helper` 的 Windows 音乐像素桌宠工具。

## 当前实现范围

- 透明无边框桌宠窗口
- 托盘显示、隐藏、退出
- 鼠标拖动、双击播放/暂停、悬停控制条、滚轮调节音量
- 监听并控制白名单播放器会话
- 保存窗口位置、置顶、自启动、控制条显示方式、白名单、歌曲规则
- C# Helper 通过 JSON Lines 与 Electron 主进程通信

## 项目结构

```text
Music-Pet/
├─ src/
│  ├─ main/                Electron 主进程
│  ├─ preload/             安全桥接层
│  ├─ renderer/            React 桌宠界面
│  └─ shared/              共享类型与 IPC 协议
├─ public/assets/pet/      默认像素桌宠资源
├─ native-helper/          Windows 媒体监听与控制 Helper
└─ DevelopDoc/             需求与说明文档
```

## 启动前准备

1. 安装 Node.js 20+
2. 安装 .NET SDK 8
3. 在项目根目录安装前端依赖
4. 构建 Windows Helper

## 常用命令

```powershell
npm install
npm run build:helper
npm run dev
```

## Windows Helper 协议

Electron 主进程发送：

```json
{ "type": "configure", "playerWhitelist": ["cloudmusic", "qqmusic"] }
{ "type": "command", "command": "playPause" }
{ "type": "command", "command": "next" }
{ "type": "command", "command": "previous" }
{ "type": "command", "command": "adjustVolume", "delta": 1 }
```

Helper 返回：

```json
{ "type": "ready" }
{ "type": "snapshot", "snapshot": { "connected": true } }
{ "type": "error", "message": "..." }
```

## 说明

- 默认只关注 `cloudmusic` 和 `qqmusic` 标识。
- 当前只提供 `default` 皮肤，但皮肤字段和资源目录已经预留好扩展点。
- 本仓库当前环境若未安装 `npm` 依赖和 `.NET SDK`，源码可直接查看，但需要补齐依赖后才能运行。

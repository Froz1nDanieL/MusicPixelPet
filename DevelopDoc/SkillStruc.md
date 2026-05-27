下面给你一套**完整、可落地、偏轻量但扩展性强**的音乐桌宠工具技术栈。我按“推荐主方案 + 可替代方案 + 模块拆分”来讲。

# 一、总体推荐技术栈

我最推荐：

> **Tauri 2 + React + TypeScript + Rust + SQLite + Web Audio/Canvas 动画**

适合做一个轻量、常驻托盘、透明置顶、有桌宠动画、有音频识别能力的桌面应用。

---

# 二、整体架构

```text
桌面应用壳：Tauri 2
    ├─ 前端 UI：React + TypeScript
    ├─ 动画渲染：Canvas / WebGL / Live2D / Spine
    ├─ 后端能力：Rust
    │   ├─ 系统音频捕获
    │   ├─ 音频特征分析
    │   ├─ 当前播放信息读取
    │   ├─ 全局快捷键
    │   ├─ 系统托盘
    │   └─ 本地数据库
    └─ 本地存储：SQLite
```

核心思想是：

> **前端负责桌宠表现，Rust 后端负责系统能力和音频分析。**

---

# 三、桌面端框架

## 首选：Tauri 2

用途：

- 创建透明无边框窗口
- 置顶显示桌宠
- 系统托盘
- 全局快捷键
- 调用 Rust 后端
- 打包 Windows / macOS / Linux

推荐理由：

- 比 Electron 更轻
- 常驻桌面工具更省内存
- Rust 后端适合处理音频和系统调用
- 前端仍然可以用 React 做漂亮 UI

建议使用：

```text
Tauri 2
Rust
React
TypeScript
Vite
```

---

## 可替代：Electron

适合你更熟悉 Node.js 的情况。

优点：

- 生态成熟
- 前端开发体验简单
- Node 包很多

缺点：

- 内存占用较高
- 常驻桌宠可能显得重
- 音频底层能力仍然需要 native module

如果你追求轻量，我不建议第一选择 Electron。

---

## 可替代：Python + PySide6

适合快速做原型。

优点：

- 音频分析库丰富
- 开发速度快
- 适合验证功能

缺点：

- 桌宠透明窗口和动画体验一般
- 打包体积可能大
- 跨平台体验不如 Tauri

适合做 MVP 验证，不太适合最终产品。

---

# 四、前端技术栈

## UI 框架

推荐：

```text
React
TypeScript
Vite
```

React 负责：

- 桌宠窗口
- 设置面板
- 悬浮音乐卡片
- 状态显示
- 模式切换
- 托盘设置页面

---

## 状态管理

推荐轻量方案：

```text
Zustand
```

用途：

- 当前桌宠状态
- 当前歌曲信息
- 当前音乐能量值
- 动作强度设置
- 模式设置
- 用户偏好

不建议一开始用 Redux，太重。

---

## UI 组件库

设置页可以用：

```text
shadcn/ui
Radix UI
Tailwind CSS
```

用途：

- 开关
- 滑条
- 下拉框
- 标签页
- 弹窗
- 快捷键设置

桌宠本体不需要复杂 UI 组件，主要靠动画。

---

## 样式方案

推荐：

```text
Tailwind CSS
CSS Modules
```

桌宠窗口通常需要：

- 透明背景
- pointer-events 控制
- 可拖拽区域
- 悬停显示卡片
- 不同模式下透明度变化

---

# 五、桌宠动画技术栈

这里分三档。

## 方案 A：第一版推荐，帧动画 / WebP 序列

技术：

```text
Canvas
requestAnimationFrame
WebP / PNG Sprite Sheet
```

优点：

- 最容易实现
- 最稳定
- 美术资源要求低
- 动作切换简单

适合第一版。

动作资源可以设计成：

```text
idle.webp
nod.webp
jump.webp
dance.webp
sleep.webp
shock.webp
sing.webp
```

实现方式：

```text
AnimationController
    ├─ 根据 petState 播放对应动画
    ├─ 根据 beatEvent 插入短动作
    ├─ 根据 energy 调整播放速度
    └─ 根据 userMode 调整动作幅度
```

---

## 方案 B：Live2D

技术：

```text
pixi-live2d-display
PixiJS
Live2D Cubism SDK
```

优点：

- 桌宠感最强
- 表情自然
- 适合二次元风格

缺点：

- 模型制作成本高
- 集成复杂度更高
- 商用授权需要注意

适合第二阶段升级。

---

## 方案 C：Spine / DragonBones

技术：

```text
Spine Runtime
PixiJS
```

优点：

- 动作控制精细
- 比帧动画灵活
- 适合卡通角色

缺点：

- 需要骨骼动画资源
- 工具链比帧动画复杂

适合你之后想做更专业的桌宠表现。

---

# 六、音频捕获技术栈

这是项目最关键的底层部分。

## Windows 推荐

```text
WASAPI Loopback
Rust crate: cpal
```

用途：

- 捕获系统正在播放的声音
- 不需要用户上传音频文件
- 可以监听 Spotify、网易云、QQ 音乐、浏览器等声音

相关 Rust 库：

```text
cpal
rodio
hound
rustfft
```

建议：

- `cpal`：音频输入/输出捕获
- `rustfft`：FFT 频谱分析
- `hound`：WAV 调试和测试
- `ringbuf`：音频缓冲区

---

## macOS

可选方案：

```text
ScreenCaptureKit
CoreAudio
BlackHole / Soundflower 虚拟声卡方案
```

macOS 系统音频捕获限制更麻烦。

第一版如果你主要面向 Windows，可以先只做 Windows。
之后再扩展 macOS。

---

## Linux

可选方案：

```text
PulseAudio
PipeWire
ALSA
```

Linux 可以做，但发行版差异较多。
不建议第一版重点支持。

---

# 七、音频分析技术栈

不用 AI 模型时，核心是传统数字信号处理。

## Rust 侧分析库

推荐：

```text
rustfft
biquad
realfft
dasp
```

你需要实现或封装这些特征：

```text
RMS 音量
Peak 峰值
FFT 频谱
低频能量
中频能量
高频能量
频谱质心
节拍峰值检测
BPM 粗估计
静音检测
音量突变检测
```

---

## 必做特征

第一版建议只做这些：

| 特征        | 用途              |
| ----------- | ----------------- |
| RMS         | 判断音量和能量    |
| Peak        | 判断突然爆音      |
| FFT         | 分析频段          |
| Bass Energy | 低频踩点          |
| Mid Energy  | 判断人声/旋律突出 |
| High Energy | 高频亮度          |
| Beat Event  | 触发跳跃、点头    |
| Silence     | 判断暂停/无音乐   |

---

## 音乐状态判断

你可以在 Rust 里输出统一状态：

```ts
type MusicState =
  | "silent"
  | "calm"
  | "normal"
  | "energetic"
  | "bass_heavy"
  | "vocal_focused"
  | "bright"
  | "shock";
```

Rust 后端每 50ms 到 100ms 推送一次：

```ts
interface AudioFeatures {
  rms: number;
  peak: number;
  bass: number;
  mid: number;
  high: number;
  bpm?: number;
  beat: boolean;
  state: MusicState;
  timestamp: number;
}
```

前端根据这些数据驱动桌宠动画。

---

# 八、当前播放信息读取

这部分用来获取歌曲名、歌手、封面、播放状态。

## Windows

推荐：

```text
Windows Global System Media Transport Controls
windows-rs
```

可获取：

- 歌曲名
- 歌手
- 专辑名
- 播放/暂停状态
- 进度
- 封面缩略图
- 当前媒体应用

Rust 相关：

```text
windows
windows-core
```

---

## 跨平台补充

可以考虑：

```text
MPRIS
Media Session API
Last.fm API
Spotify API
```

但第一版建议：

- Windows：系统媒体会话
- 其他平台：先做音频反应，不强求歌曲信息

---

# 九、本地数据库和配置

## 推荐数据库

```text
SQLite
```

Rust 库：

```text
sqlx
rusqlite
```

我推荐：

```text
rusqlite
```

简单稳定，适合本地工具。

---

## 存储内容

不要存太多，避免产品变臃肿。

建议只存：

```text
用户设置
桌宠位置
透明度
动作强度
情景模式
手动歌曲标签
最近播放歌曲缓存
快捷键配置
```

表设计可以很简单：

```text
settings
tracks
track_tags
pet_profiles
```

---

# 十、系统能力技术栈

## 系统托盘

Tauri 插件：

```text
tauri-plugin-tray
```

托盘菜单：

```text
显示 / 隐藏桌宠
专注模式
派对模式
睡前模式
设置
退出
```

---

## 全局快捷键

Tauri 插件：

```text
tauri-plugin-global-shortcut
```

快捷键建议：

```text
Ctrl + Alt + M：显示 / 隐藏桌宠
Ctrl + Alt + Up：增强动作
Ctrl + Alt + Down：降低动作
Ctrl + Alt + L：给当前歌曲打标签
```

---

## 通知

Tauri 插件：

```text
tauri-plugin-notification
```

用于：

- 第一次识别到播放器
- 歌曲标签保存成功
- 桌宠进入专注模式

不要频繁弹通知。

---

## 自启动

Tauri 插件：

```text
tauri-plugin-autostart
```

常驻工具很需要这个。

---

## 窗口控制

Tauri 内置能力：

```text
transparent window
always on top
decorations false
skip taskbar
resizable false
```

你需要的桌宠窗口特性：

```text
透明
无边框
置顶
不显示任务栏
可拖拽
可穿透点击
悬停时可交互
```

---

# 十一、桌宠行为系统

建议自己写一个轻量状态机。

## 状态机技术

不用上很重的库，TypeScript 自己写即可。

也可以用：

```text
xstate
```

但第一版不一定需要。

---

## 推荐结构

```ts
type PetState =
  | "idle"
  | "listening"
  | "calm"
  | "nod"
  | "jump"
  | "dance"
  | "sing"
  | "shock"
  | "sleep"
  | "focus";
```

状态来源：

```text
音频状态
当前播放状态
用户模式
鼠标交互
系统环境
```

决策优先级：

```text
用户强制模式
    >
全屏 / 专注规则
    >
音乐状态
    >
随机待机动作
```

---

# 十二、建议的开发语言分工

## Rust 负责

```text
系统音频捕获
FFT 和音频特征分析
媒体信息读取
托盘和系统级功能
SQLite 数据存储
本地配置读写
向前端推送事件
```

## TypeScript / React 负责

```text
桌宠动画
悬浮卡片
设置页面
动作状态机
用户交互
主题和皮肤
```

---

# 十三、事件通信方案

Tauri 前后端通信：

```text
invoke
event emit / listen
```

Rust 后端定期发送：

```text
audio-features
music-state-changed
track-changed
playback-state-changed
beat-detected
```

前端监听：

```ts
listen<AudioFeatures>("audio-features", (event) => {
  updateAudioFeatures(event.payload);
});
```

建议推送频率：

```text
audio-features: 10 - 20 次/秒
beat-detected: 实时事件
track-changed: 变化时发送
```

不要每毫秒推送，否则前端会浪费性能。

---

# 十四、性能优化技术

你这个工具必须轻。

建议目标：

```text
内存：低于 150MB
CPU 空闲：低于 2%
听歌分析时：低于 5% 到 8%
前端帧率：30fps 或 60fps 可选
```

优化方法：

- 音频分析窗口大小固定，例如 1024 / 2048 samples
- FFT 频率不要太高，10 到 20 次/秒够用
- 桌宠动画可设置 30fps
- 全屏时降低动画刷新率
- 静音时停止 FFT
- 设置“省电模式”
- 不要在 React 状态里存每一帧音频数据
- 高频数据直接给动画控制器，不走复杂 UI 渲染

---

# 十五、项目目录结构

建议这样组织：

```text
music-pet/
├─ src/
│  ├─ app/
│  │  ├─ App.tsx
│  │  └─ routes.tsx
│  ├─ components/
│  │  ├─ PetWindow/
│  │  ├─ MusicCard/
│  │  ├─ SettingsPanel/
│  │  └─ TrayHint/
│  ├─ pet/
│  │  ├─ AnimationController.ts
│  │  ├─ PetStateMachine.ts
│  │  ├─ PetActionMap.ts
│  │  └─ petTypes.ts
│  ├─ audio/
│  │  ├─ audioTypes.ts
│  │  └─ audioStore.ts
│  ├─ store/
│  │  ├─ useSettingsStore.ts
│  │  ├─ usePetStore.ts
│  │  └─ useTrackStore.ts
│  ├─ styles/
│  └─ main.tsx
│
├─ src-tauri/
│  ├─ src/
│  │  ├─ main.rs
│  │  ├─ audio/
│  │  │  ├─ capture.rs
│  │  │  ├─ features.rs
│  │  │  ├─ beat.rs
│  │  │  └─ detector.rs
│  │  ├─ media/
│  │  │  └─ windows_media.rs
│  │  ├─ db/
│  │  │  ├─ mod.rs
│  │  │  └─ migrations.rs
│  │  ├─ settings/
│  │  ├─ tray.rs
│  │  └─ commands.rs
│  ├─ Cargo.toml
│  └─ tauri.conf.json
│
├─ assets/
│  ├─ pets/
│  │  ├─ default/
│  │  │  ├─ idle.webp
│  │  │  ├─ nod.webp
│  │  │  ├─ jump.webp
│  │  │  ├─ dance.webp
│  │  │  ├─ sing.webp
│  │  │  ├─ shock.webp
│  │  │  └─ sleep.webp
│  └─ icons/
│
├─ package.json
├─ vite.config.ts
└─ README.md
```

---

# 十六、依赖清单

## 前端依赖

```bash
pnpm add react react-dom zustand
pnpm add @tauri-apps/api
pnpm add lucide-react
pnpm add clsx tailwind-merge
pnpm add framer-motion
```

设置 UI：

```bash
pnpm add @radix-ui/react-slider
pnpm add @radix-ui/react-switch
pnpm add @radix-ui/react-dropdown-menu
pnpm add @radix-ui/react-dialog
```

动画可选：

```bash
pnpm add pixi.js
pnpm add pixi-live2d-display
```

如果第一版用 Canvas 帧动画，Pixi 都可以先不加。

---

## Tauri 插件

```bash
pnpm add @tauri-apps/plugin-global-shortcut
pnpm add @tauri-apps/plugin-notification
pnpm add @tauri-apps/plugin-autostart
pnpm add @tauri-apps/plugin-store
```

---

## Rust 依赖

```toml
[dependencies]
tauri = "2"
serde = { version = "1", features = ["derive"] }
serde_json = "1"
cpal = "0.15"
rustfft = "6"
ringbuf = "0.4"
rusqlite = { version = "0.31", features = ["bundled"] }
anyhow = "1"
thiserror = "1"
tokio = { version = "1", features = ["full"] }
```

Windows 媒体信息：

```toml
windows = { version = "0.58", features = [
  "Media_Control",
  "Storage_Streams",
  "Foundation",
] }
```

具体 features 可能需要根据实际 API 调整。

---

# 十七、最小可行版本技术栈

如果你想先做出来，不要一开始就上完整版本。

## MVP 技术栈

```text
Tauri 2
React
TypeScript
Rust
cpal
rustfft
Zustand
Canvas 帧动画
SQLite / JSON 配置
```

## MVP 功能

```text
透明桌宠窗口
托盘菜单
系统音频捕获
RMS 音量检测
低频 FFT 检测
Beat 事件
5 个动作：idle / nod / jump / dance / sleep
动作强度设置
全屏时自动降低动作
```

第一版甚至可以先不做：

```text
歌曲名读取
BPM 精准估算
Live2D
复杂设置页
多皮肤
听歌统计
```

---

# 十八、进阶版本技术栈

当 MVP 稳定后再加：

```text
Windows Media Session 读取歌曲信息
SQLite 歌曲标签
Live2D / Spine
自动模式切换
快捷键控制
更多桌宠皮肤
插件化动作包
```

进阶功能对应技术：

| 功能         | 技术                                           |
| ------------ | ---------------------------------------------- |
| 当前歌曲信息 | Windows Global System Media Transport Controls |
| 歌曲封面     | Windows Media Session thumbnail                |
| 手动歌曲标签 | SQLite                                         |
| Live2D 桌宠  | PixiJS + pixi-live2d-display                   |
| 音乐卡片     | React + Framer Motion                          |
| 全局快捷键   | Tauri global shortcut plugin                   |
| 托盘常驻     | Tauri tray                                     |
| 自启动       | Tauri autostart plugin                         |
| 音频可视化   | Canvas / WebGL                                 |

---

# 十九、我给你的最终推荐组合

## 最稳组合

```text
Tauri 2
React
TypeScript
Rust
cpal
rustfft
rusqlite
Zustand
Canvas 帧动画
Tailwind CSS
```

适合第一版上线。

---

## 最有桌宠质感组合

```text
Tauri 2
React
TypeScript
Rust
cpal
rustfft
PixiJS
Live2D Cubism
Zustand
SQLite
```

适合后续做精致产品。

---

## 最快验证组合

```text
Python
PySide6
sounddevice
numpy
scipy
pyqtgraph
```

适合先验证音频识别和动作逻辑，但不推荐作为最终桌面产品主线。

---

# 二十、建议开发路线

## 第 1 阶段：音频反应原型

目标：

```text
能捕获系统声音
能计算音量和低频强度
能触发 beat 事件
```

技术：

```text
Rust + cpal + rustfft
```

---

## 第 2 阶段：桌宠窗口

目标：

```text
透明置顶窗口
加载帧动画
根据 beat 做动作
```

技术：

```text
Tauri + React + Canvas
```

---

## 第 3 阶段：状态机

目标：

```text
桌宠根据 silent / calm / normal / energetic / bass_heavy 切换动作
```

技术：

```text
TypeScript 状态机 + Zustand
```

---

## 第 4 阶段：轻量实用功能

目标：

```text
悬停音乐卡片
专注模式
动作强度设置
托盘菜单
全屏自动弱化
```

---

## 第 5 阶段：增强质感

目标：

```text
歌曲信息读取
封面显示
手动标签
更多动作
Live2D 支持
```

---

# 最终一句话

你的音乐桌宠工具最适合的完整技术栈是：

> **Tauri 2 做轻量桌面壳，React + TypeScript 做桌宠界面和设置页，Rust 负责系统音频捕获与频谱分析，cpal + rustfft 做非 AI 音乐状态识别，Zustand 管理状态，SQLite 保存设置和标签，第一版用 Canvas 帧动画，后续升级到 Live2D 或 PixiJS。**

# Worldex / Terminal Ballet

七个真实 Windows PowerShell / CMD 控制台随本地音乐共同演出。32 个语义场景，7 种窗口构图，Win32 帧缓冲目标 24 FPS，音频频谱与节拍驱动字符图形。四窗直接运行 PowerShell，三窗通过 CMD 承载同一渲染引擎。

## 启动

环境：Windows、Windows PowerShell 5.1、Python（`python -m pip install -r requirements.txt`）、PATH 中可用的 FFmpeg。

双击 `start.cmd`，默认读取桌面的 `world.execute (me) ; - Mili.flac`。也可指定音频：

```powershell
.\Start-Worldex.ps1 -AudioPath 'C:\Music\song.flac'
.\Start-Worldex.ps1 -SeekSeconds 147 -PreviewSeconds 20
.\Start-Worldex.ps1 -CueOffsetSeconds 0.2
.\Start-Worldex.ps1 -RenderOnly
.\Stop-Worldex.ps1
```

`-SeekSeconds` 从指定进度播放，`-PreviewSeconds` 限制本次演出时长；默认完整播放一遍。`-CueOffsetSeconds` 正数表示视觉场景提前，负数表示延后。`-Mute` 仅将本次播放静音。`-RenderOnly` 生成静态场景预览，不播放。

点击任意演出终端后：空格暂停／继续，左右键跳转 10 秒，Home 从头开始，Esc 全部停止。全局快捷键 Ctrl+Shift+Q 或双击 `stop.cmd` 也能停止。演出期间舞台与七窗置顶，结束恢复；关闭任意一个演出窗口后，控制器会自动结束其余窗口与声音。控制器检测不到音频时钟心跳时，窗口自动退出。

## 图形与同步

供电线路、旋转立方体、经纬球、点云、圆周、波形与切线意象、无穷曲线、倒转时钟、共振、植物、果实、猫、观察者之眼、记忆碎片、冲突大字、递归风暴、心形曲线与囚笼。中央展示语义主图，两侧以频谱、信号、几何和状态补充。

音频先由 FFmpeg 解码为 PCM，Python 分析频谱通量、全曲周期性、节拍相位与 16 频带能量；七个窗口通过命名共享内存读取同一声卡播放位置。暂停与跳转同时作用于声音和画面。语义场景参考歌词时间戳，未经本录音逐字人工对齐；不是逐字卡拉 OK 字幕。

准备、分析、播放控制器与控制台均通过 WMI 独立启动，脱离启动终端的进程作业限制。播放前等待全部七窗就绪，结束自动关闭舞台。缓存、音频、日志与预览位于 Git 忽略的 `.runtime/`，不上传歌曲。分析依赖和源码来自本地，不调用云端模型。

视觉规范见 `docs/art-direction.md`，主题在 `assets/design-tokens.json`，可编辑场景在 `assets/score.json`。当前时间表针对 Mili 原版约 212 秒录音，换曲需要重新编排。

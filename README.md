# Worldex

使用真实 Windows 控制台窗口呈现的 `world.execute(me);` 编排原型。

双击 `start.cmd` 启动，双击 `stop.cmd` 全部停止。也可以运行：

```powershell
.\Start-Worldex.ps1 -DurationSeconds 600
.\Stop-Worldex.ps1
```

七个独立 PowerShell 控制台共享时钟，每 32 秒循环：六窗网格与中央主舞台 → 对角层叠 → 中央窗口放大 → 网格复位。各窗口显示不同的代码、内存、逻辑、网络和输出动画，使用主屏幕工作区自动适配尺寸。ESC 关闭单个窗口，默认十分钟后全部退出。

控制台通过独立 conhost 进程启动，启动脚本退出后继续运行；不修改默认终端配置。四个窗口直接运行 PowerShell，三个通过 CMD 运行 PowerShell 动画脚本。当前版本未加入歌曲音频或逐拍同步。

运行状态写入被 Git 忽略的 `.runtime/`。再次启动会先通知上一组窗口退出。

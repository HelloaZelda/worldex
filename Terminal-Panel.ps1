param([int]$Index, [string]$Config)
$ErrorActionPreference = 'Stop'
$settings = Get-Content -LiteralPath $Config -Raw -Encoding UTF8 | ConvertFrom-Json
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WorldexWindow {
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int t, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
}
'@
$window = [WorldexWindow]::GetConsoleWindow()
$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$names = @('BOOT / 01','MEMORY / 02','OBJECT / 03','LOGIC / 04','NETWORK / 05','OUTPUT / 06','WORLD.EXECUTE(ME);')
$colors = @('Cyan','DarkCyan','Green','Yellow','Magenta','White','Cyan')
$Host.UI.RawUI.WindowTitle = 'WORLDEX | ' + $names[$Index]
[Console]::BackgroundColor = [ConsoleColor]::Black
[Console]::ForegroundColor = [ConsoleColor]$colors[$Index]
try { [Console]::CursorVisible = $false } catch {}
# Normalized rectangles: six satellite terminals and a central main stage.
$grid = @(
    @(0.02,0.025,0.31,0.29), @(0.345,0.025,0.31,0.29), @(0.67,0.025,0.31,0.29),
    @(0.02,0.665,0.31,0.31), @(0.345,0.665,0.31,0.31), @(0.67,0.665,0.31,0.31),
    @(0.20,0.33,0.60,0.315)
)
$stop = Join-Path (Join-Path $settings.Root '.runtime') ($settings.Run + '.stop')
$metadata = Join-Path (Join-Path $settings.Root '.runtime') ($settings.Run + '-' + $Index + '.json')
@{ PID = $PID; Handle = $window.ToInt64(); Index = $Index } | ConvertTo-Json | Set-Content -LiteralPath $metadata
$lastFrame = -1
$lastRect = ''
try {
    [WorldexWindow]::ShowWindow($window, 0) | Out-Null
    while ($true) {
        $elapsed = ([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() - $settings.Epoch) / 1000.0
        if ((Test-Path -LiteralPath $stop) -or $elapsed -gt $settings.Duration) { break }
        $entrance = $Index * 0.32
        if ($elapsed -lt $entrance) { Start-Sleep -Milliseconds 40; continue }
        $cycle = $elapsed % 32
        $r = $grid[$Index].Clone()
        # 0-12s: grid. 12-20s: cascade. 20-28s: main stage. 28-32s: return.
        if ($cycle -ge 12 -and $cycle -lt 20) {
            $blend = [Math]::Min(1, ($cycle - 12) / 1.3)
            $target = @((0.07 + $Index * 0.085), (0.06 + $Index * 0.085), 0.43, 0.39)
        } elseif ($cycle -ge 20 -and $cycle -lt 28) {
            $blend = [Math]::Min(1, ($cycle - 20) / 1.1)
            $target = $grid[$Index].Clone()
            if ($Index -eq 6) { $target = @(0.13,0.22,0.74,0.55) }
        } else { $blend = 0; $target = $r }
        for ($j=0; $j -lt 4; $j++) { $r[$j] = $r[$j] + ($target[$j] - $r[$j]) * $blend }
        $ease = [Math]::Min(1, [Math]::Max(0, ($elapsed - $entrance) / 0.65))
        $ease = 1 - [Math]::Pow(1 - $ease, 3)
        $x = $area.X + [int]($area.Width * $r[0])
        $y = $area.Y + [int]($area.Height * $r[1] + (1 - $ease) * 100)
        $w = [int]($area.Width * $r[2]); $h = [int]($area.Height * $r[3])
        $rect = "$x,$y,$w,$h"
        if ($rect -ne $lastRect) {
            [WorldexWindow]::SetWindowPos($window,[IntPtr]::Zero,$x,$y,$w,$h,0x0014) | Out-Null
            $lastRect = $rect
        }
        if ($lastFrame -eq -1) { [WorldexWindow]::ShowWindow($window, 4) | Out-Null }
        $frame = [int][Math]::Floor($elapsed * 6)
        if ($frame -ne $lastFrame) {
            $lastFrame = $frame
            $phase = if ($cycle -lt 12) {'INITIALIZE'} elseif ($cycle -lt 20) {'CONNECT'} elseif ($cycle -lt 28) {'EXECUTE'} else {'REBUILD'}
            $lines = @('  W O R L D E X   /   TERMINAL CHOREOGRAPHY', ('  ' + $names[$Index]), ('  {0,-12}  T+{1:000.0}s' -f $phase,$elapsed), '  ----------------------------------------')
            if ($Index -eq 6) {
                $lines += @('', '       world.execute(me);', '', '       [ YOU ]  <------>  [ WORLD ]', '', ('       SYSTEM: ' + $phase), '       7 WINDOWS / ONE SHARED CLOCK')
            } else {
                for ($row=0; $row -lt 7; $row++) {
                    $n = ($frame * 31 + $row * 137 + $Index * 4096) % 65536
                    switch ($Index) {
                        0 { $line = '  [{0:000}] module_{1:00} ........ READY' -f $frame,($row + 1) }
                        1 { $line = '  0x{0:X4}  {1:X4}  {2:X4}  ALLOCATED' -f ($row*256),$n,(65535-$n) }
                        2 { $line = '  object[{0}] -> {{ life: {1:000}, active }}' -f $row,($n%100) }
                        3 { $line = '  if (signal_{0}) => {1}' -f $row,(@('TRUE','FALSE')[($frame+$row)%2]) }
                        4 { $line = '  node_{0} <========> node_{1}   {2:00}ms' -f $row,($row+1),($n%40) }
                        5 { $line = '  OUT {0:0000} | {1}' -f $n,('#' * (3 + ($frame+$row)%19)) }
                    }
                    $lines += $line
                }
            }
            $lines += @('', '  [ESC] close panel   /   Stop-Worldex.ps1')
            try {
                $width = [Math]::Max(1,[Console]::WindowWidth - 1)
                $height = [Math]::Max(1,[Console]::WindowHeight - 1)
                [Console]::SetCursorPosition(0,0)
                for ($row=0; $row -lt [Math]::Min($height, $lines.Count); $row++) {
                    $line = $lines[$row]
                    if ($line.Length -gt $width) { $line = $line.Substring(0,$width) }
                    [Console]::WriteLine($line.PadRight($width))
                }
                if ([Console]::KeyAvailable -and [Console]::ReadKey($true).Key -eq 'Escape') { break }
            } catch {}
        }
        Start-Sleep -Milliseconds 40
    }
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path (Join-Path $settings.Root '.runtime') ($settings.Run + '-' + $Index + '.log')) -Encoding UTF8
} finally {
    Remove-Item -LiteralPath $metadata -ErrorAction SilentlyContinue
}

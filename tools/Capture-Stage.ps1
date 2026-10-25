param([string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) '.runtime\stage.png'))
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$run=Get-Content -LiteralPath (Join-Path $root '.runtime\run.json') -Raw -Encoding UTF8|ConvertFrom-Json
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @'
using System;using System.Runtime.InteropServices;
public class WorldexCapture {
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int L,T,R,B;}
 [DllImport("user32.dll")]public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
[WorldexCapture]::SetThreadDpiAwarenessContext([IntPtr](-4))|Out-Null
$area=[System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$image=New-Object System.Drawing.Bitmap($area.Width,$area.Height)
$canvas=[System.Drawing.Graphics]::FromImage($image)
$canvas.Clear([System.Drawing.Color]::FromArgb(8,12,18))
try {
    $drawn=0
    $panels=Get-ChildItem -LiteralPath (Join-Path $root '.runtime') -Filter ($run.Run+'-*.json')|Where-Object Name -Match '-\d+\.json$'
    foreach($file in $panels) {
        $meta=Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8|ConvertFrom-Json
        $handle=[IntPtr]$meta.Handle
        if(-not [WorldexCapture]::IsWindowVisible($handle)){continue}
        $rect=New-Object WorldexCapture+Rect
        if(-not [WorldexCapture]::GetWindowRect($handle,[ref]$rect)){continue}
        $bitmap=New-Object System.Drawing.Bitmap(($rect.R-$rect.L),($rect.B-$rect.T))
        $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $dc=$graphics.GetHdc()
            try { $ok=[WorldexCapture]::PrintWindow($handle,$dc,2) } finally {$graphics.ReleaseHdc($dc)}
            if($ok){$canvas.DrawImageUnscaled($bitmap,$rect.L-$area.X,$rect.T-$area.Y);$drawn++}
        } finally {$graphics.Dispose();$bitmap.Dispose()}
    }
    if($drawn -eq 0){throw 'No active stage windows could be captured.'}
    $image.Save($Output)
    Write-Host $Output
}finally{$canvas.Dispose();$image.Dispose()}

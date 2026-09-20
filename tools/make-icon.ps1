$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(256,256)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode='AntiAlias'
function Round([int]$x,[int]$y,[int]$w,[int]$h,[int]$r,[Drawing.Color]$color){
 $path=[Drawing.Drawing2D.GraphicsPath]::new();$d=$r*2
 $path.AddArc($x,$y,$d,$d,180,90);$path.AddArc($x+$w-$d,$y,$d,$d,270,90);$path.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90);$path.AddArc($x,$y+$h-$d,$d,$d,90,90);$path.CloseFigure()
 $brush=[Drawing.SolidBrush]::new($color);$graphics.FillPath($brush,$path);$brush.Dispose();$path.Dispose()
}
try{
 Round 0 0 256 256 60 ([Drawing.Color]::FromArgb(15,23,28))
 Round 32 84 192 88 44 ([Drawing.Color]::FromArgb(151,235,204))
 foreach($bar in @(@(68,116,16,24),@(102,102,16,52),@(138,96,16,64),@(172,114,16,28))){Round $bar[0] $bar[1] $bar[2] $bar[3] 8 ([Drawing.Color]::FromArgb(20,52,45))}
 $png=[IO.MemoryStream]::new();$bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png);$bytes=$png.ToArray();$png.Dispose()
 $file=[IO.File]::Create((Join-Path (Split-Path $PSScriptRoot -Parent) 'assets\music-island.ico'));$writer=[IO.BinaryWriter]::new($file)
 try{$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$bytes.Length);$writer.Write([uint32]22);$writer.Write($bytes)}finally{$writer.Dispose()}
}finally{$graphics.Dispose();$bitmap.Dispose()}

param([switch]$GoldenAttacks)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRefs = Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName
$taskRefs += [System.Drawing.Bitmap].Assembly.Location
$taskRefs += Get-ChildItem $PSHOME -Filter 'System.Private.Windows.*.dll' | Select-Object -ExpandProperty FullName
Add-Type -ReferencedAssemblies $taskRefs -TypeDefinition @'
using System;
using System.Drawing;
public static class AwakeningPixelCheck {
 public static int Top(string path) {
  using(var img=new Bitmap(path)) {
   for(int y=0;y<img.Height;y++) for(int x=0;x<img.Width;x++) if(img.GetPixel(x,y).A>0) return y;
  }
  throw new Exception("Empty artwork: "+path);
 }
 public static void Verify(string path) {
  using(var img=new Bitmap(path)) {
   int w=img.Width,h=img.Height;
   if(w!=1024 || h!=1024) throw new Exception("Canvas must be 1024: "+path);
   bool[] solid=new bool[w*h];int[] queue=new int[w*h];int components=0,largest=0,secondary=0;
   int left=w,top=h,right=0,bottom=0;
   for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(img.GetPixel(x,y).A>0) {
    solid[y*w+x]=true;left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
   }
   for(int seed=0;seed<solid.Length;seed++) if(solid[seed]) {
    int count=1,read=0;queue[0]=seed;solid[seed]=false;
    while(read<count) {
     int p=queue[read++],x=p%w,y=p/w;
     int[] next={x>0?p-1:-1,x<w-1?p+1:-1,y>0?p-w:-1,y<h-1?p+w:-1};
     foreach(int n in next) if(n>=0 && solid[n]) {solid[n]=false;queue[count++]=n;}
    }
    components++;if(count>largest) {secondary=largest;largest=count;} else secondary=Math.Max(secondary,count);
   }
   if(left<8 || top<8 || right>=w-8 || bottom>=h-8 || largest<1000 || secondary>24)
    throw new Exception("Clipping or detached artwork in "+path+"; secondary="+secondary);
   Console.WriteLine(System.IO.Path.GetFileName(path)+": margin OK, main="+largest+", secondary="+secondary);
  }
 }
}
'@
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskFolder = Join-Path $taskRoot 'game/Assets/Art/Bosses/FairyKingdom/Movement'
$taskNames = @('CrayonHero_Awakened','CrayonHero_Awaken_0','CrayonHero_Awaken_1','CrayonHero_Awaken_2','CrayonHero_Awaken_3')
if ($GoldenAttacks) {
 foreach ($taskGroup in @('Swing','Dash','Slam','SlamCharge')) {
  foreach ($taskIndex in 0..3) { $taskNames += 'CrayonHero_Golden_'+$taskGroup+'_'+$taskIndex }
 }
}
$taskGuids = @()
$taskPrefab = [IO.File]::ReadAllText((Join-Path $taskRoot 'game/Assets/Prefabs/CrayonHeroBoss.prefab'),[Text.Encoding]::UTF8)
foreach ($taskName in $taskNames) {
    [AwakeningPixelCheck]::Verify((Join-Path $taskFolder ($taskName+'.png')))
    $taskMeta = [IO.File]::ReadAllText((Join-Path $taskFolder ($taskName+'.png.meta')),[Text.Encoding]::UTF8)
    $taskGuid = [regex]::Match($taskMeta,'(?m)^guid: ([a-f0-9]{32})').Groups[1].Value
    $taskUnused = $taskName -in @('CrayonHero_Golden_Slam_0','CrayonHero_Golden_Slam_1')
    if (!$taskGuid -or $taskGuids -contains $taskGuid -or (!$taskUnused -and !$taskPrefab.Contains('guid: '+$taskGuid))) { throw 'Missing or duplicate GUID/prefab reference: '+$taskName }
    if (!$taskMeta.Contains('spritePixelsToUnits: 400') -or !$taskMeta.Contains('spritePivot: {x: 0.5, y: 0.5}')) { throw 'Registration import drift: '+$taskName }
    $taskGuids += $taskGuid
}
Write-Output ('Awakening image and prefab reference checks passed ('+$taskNames.Count+' PNGs).')
$taskIdleHash = (Get-FileHash -LiteralPath (Join-Path $taskFolder 'CrayonHero_Awakened.png') -Algorithm SHA256).Hash
$taskRaisedHash = (Get-FileHash -LiteralPath (Join-Path $taskFolder 'CrayonHero_Awaken_2.png') -Algorithm SHA256).Hash
$taskSettleHash = (Get-FileHash -LiteralPath (Join-Path $taskFolder 'CrayonHero_Awaken_3.png') -Algorithm SHA256).Hash
if ($taskRaisedHash -eq $taskIdleHash -or $taskSettleHash -ne $taskIdleHash) { throw 'Transformation must show a dedicated raised sword pose and settle into the supplied idle.' }
Write-Output 'Raised-sword climax and exact supplied idle restoration checks passed.'
if ($GoldenAttacks) {
 $taskTops = @(1..3 | ForEach-Object { [AwakeningPixelCheck]::Top((Join-Path $taskFolder ('CrayonHero_Golden_SlamCharge_'+$_.ToString()+'.png'))) })
 if ($taskTops[1] -ge $taskTops[0]-8 -or $taskTops[2] -ge $taskTops[1]-8) { throw 'Each golden charge stage must visibly grow above the previous stage.' }
 Write-Output ('Golden charge aura grows at all three stages: top rows '+($taskTops -join ', ')+'.')
}

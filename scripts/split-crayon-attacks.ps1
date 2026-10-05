param([Parameter(Mandatory=$true)][string]$SourcePath,
      [Parameter(Mandatory=$true)][ValidateSet('Swing','Dash','Slam','SlamCharge','Awaken','Awakened','Golden_Swing','Golden_Dash','Golden_Slam','Golden_SlamCharge')][string]$Attack, [string]$PaddedSourcePath, [string]$IvoryCrystal, [string]$IvoryCrystals, [int]$SingleFrame = -1)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRefs = Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName
$taskRefs += [System.Drawing.Bitmap].Assembly.Location
$taskRefs += Get-ChildItem $PSHOME -Filter 'System.Private.Windows.*.dll' | Select-Object -ExpandProperty FullName
Add-Type -ReferencedAssemblies $taskRefs -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
public static class CrayonFrameRegistration {
 public static Rectangle IvoryCrystal;
 public static Rectangle[] GoldenCrystals;
 public static int SingleFrame=-1;
 class PosePart { public int[] Pixels; public Rectangle Bounds; }
 public static void PadAtlas(string source,string destination,bool awakening=false) {
  using(var img=new Bitmap(source)) {
   int w=img.Width,h=img.Height;bool[] solid=new bool[w*h];int[] queue=new int[w*h];
   var parts=new List<PosePart>();
   for(int y=0;y<h;y++) for(int x=0;x<w;x++) solid[y*w+x]=img.GetPixel(x,y).A>(awakening?128:8);
   for(int seed=0;seed<solid.Length;seed++) {
    if(!solid[seed]) continue;
    int count=1,read=0,l=w,t=h,r=0,b=0;queue[0]=seed;solid[seed]=false;
    while(read<count) {
     int p=queue[read++],x=p%w,y=p/w;
     l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);
     int[] next={x>0?p-1:-1,x<w-1?p+1:-1,y>0?p-w:-1,y<h-1?p+w:-1};
     foreach(int n in next) if(n>=0 && solid[n]) {solid[n]=false;queue[count++]=n;}
    }
    if(count>1000) {var pixels=new int[count];Array.Copy(queue,pixels,count);parts.Add(new PosePart{Pixels=pixels,Bounds=Rectangle.FromLTRB(l,t,r+1,b+1)});}
   }
   parts.Sort((a,b)=>b.Pixels.Length.CompareTo(a.Pixels.Length));
   if(parts.Count<4) throw new Exception("Atlas does not contain four separate poses.");
   parts=parts.GetRange(0,4);
   parts.Sort((a,b)=>(a.Bounds.Top+a.Bounds.Bottom).CompareTo(b.Bounds.Top+b.Bounds.Bottom));
   if(parts[0].Bounds.Left>parts[1].Bounds.Left) {var p=parts[0];parts[0]=parts[1];parts[1]=p;}
   if(parts[2].Bounds.Left>parts[3].Bounds.Left) {var p=parts[2];parts[2]=parts[3];parts[3]=p;}
   int largest=1;foreach(var p in parts) largest=Math.Max(largest,Math.Max(p.Bounds.Width,p.Bounds.Height));
   float scale=800f/largest;
   using(var atlas=new Bitmap(2048,2048,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(atlas)) {
    g.CompositingMode=CompositingMode.SourceCopy;
    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    for(int i=0;i<4;i++) {
     var part=parts[i];var box=part.Bounds;
     using(var pose=new Bitmap(box.Width,box.Height,PixelFormat.Format32bppArgb)) {
      foreach(int p in part.Pixels) pose.SetPixel(p%w-box.Left,p/w-box.Top,img.GetPixel(p%w,p/w));
      float pw=box.Width*scale,ph=box.Height*scale;
      if(GoldenCrystals!=null) {
       var crystal=GoldenCrystals[i];
       GoldenCrystals[i]=new Rectangle(
           (int)((1024-pw)/2+(crystal.Left-box.Left)*scale),
           (int)(912-ph+(crystal.Top-box.Top)*scale),
           (int)(crystal.Width*scale),(int)(crystal.Height*scale));
      }
      if(awakening && i==2) IvoryCrystal=new Rectangle(
          (int)((1024-pw)/2+(IvoryCrystal.Left-box.Left)*scale),
          (int)(912-ph+(IvoryCrystal.Top-box.Top)*scale),
          (int)(IvoryCrystal.Width*scale),(int)(IvoryCrystal.Height*scale));
      g.DrawImage(pose,new RectangleF(i%2*1024+(1024-pw)/2,i/2*1024+912-ph,pw,ph));
     }
    }
    atlas.Save(destination,ImageFormat.Png);
   }
  }
 }
 static Rectangle Helmet(Bitmap img, Rectangle cell) {
  int w=cell.Width,h=(int)(cell.Height*0.64f),best=0;
  bool[] blue=new bool[w*h]; int[] queue=new int[w*h];
  Rectangle result=Rectangle.Empty;
  for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
   Color p=img.GetPixel(cell.Left+x,cell.Top+y);
   blue[y*w+x]=p.A>200 && p.B>170 && p.R<125 && p.G<190 && p.B>p.G*1.25f;
  }
  for(int seed=0;seed<blue.Length;seed++) {
   if(!blue[seed]) continue;
   int count=1,read=0,l=w,t=h,r=0,b=0;queue[0]=seed;blue[seed]=false;
   while(read<count) {
    int pos=queue[read++],x=pos%w,y=pos/w;
    l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);
    int[] next={x>0?pos-1:-1,x<w-1?pos+1:-1,y>0?pos-w:-1,y<h-1?pos+w:-1};
    foreach(int n in next) if(n>=0 && blue[n]) {blue[n]=false;queue[count++]=n;}
   }
   if(count>best) {best=count;result=Rectangle.FromLTRB(cell.Left+l,cell.Top+t,cell.Left+r+1,cell.Top+b+1);}
  }
  if(best<100) throw new Exception("Helmet registration failed.");
  return result;
 }
 static int Boots(Bitmap img, Rectangle cell) {
  for(int y=cell.Bottom-1;y>cell.Top+cell.Height*0.65f;y--) {
   int n=0;
   for(int x=cell.Left;x<cell.Right;x++) {
    Color p=img.GetPixel(x,y);
    if(p.A>200 && p.R>65 && p.R<190 && p.G>30 && p.G<130 && p.B>15 && p.B<110 && p.R>p.G*1.3f) n++;
   }
   if(n>10) return y;
  }
  throw new Exception("Boot registration failed.");
 }
 static Bitmap ExtractPose(Bitmap img, Rectangle cell, out Rectangle bounds, int alphaThreshold=8, int padding=8) {
  int w=cell.Width,h=cell.Height,best=0;
  bool[] solid=new bool[w*h];int[] queue=new int[w*h],members=null;
  bounds=Rectangle.Empty;
  for(int y=0;y<h;y++) for(int x=0;x<w;x++) solid[y*w+x]=img.GetPixel(cell.Left+x,cell.Top+y).A>alphaThreshold;
  for(int seed=0;seed<solid.Length;seed++) {
   if(!solid[seed]) continue;
   int count=1,read=0,l=w,t=h,r=0,b=0;queue[0]=seed;solid[seed]=false;
   while(read<count) {
    int pos=queue[read++],x=pos%w,y=pos/w;
    l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);
    int[] next={x>0?pos-1:-1,x<w-1?pos+1:-1,y>0?pos-w:-1,y<h-1?pos+w:-1};
    foreach(int n in next) if(n>=0 && solid[n]) {solid[n]=false;queue[count++]=n;}
   }
   if(count>best) {
    best=count;members=new int[count];Array.Copy(queue,members,count);
    bounds=Rectangle.FromLTRB(l,t,r+1,b+1);
   }
  }
  if(best<1000) throw new Exception("Main pose missing.");
  if(bounds.Left<padding || bounds.Top<padding || bounds.Right>w-padding || bounds.Bottom>h-padding)
   throw new Exception("Pose/effect encroaches on cell margin: "+bounds+" in "+cell+"; regenerate with wider gutters.");
  var result=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  // Copy only this pose's actual connected pixels, never the entire bounding rectangle from the atlas.
  foreach(int p in members) result.SetPixel(p%w,p/w,img.GetPixel(cell.Left+p%w,cell.Top+p/w));
  return result;
 }
 public static void Split(string path,string reference,string folder,string attack) {
  using(var img=new Bitmap(path)) using(var original=new Bitmap(reference)) {
   var originalCell=new Rectangle(0,0,original.Width,original.Height);
   var anchor=Helmet(original,originalCell);
   float baseline=Boots(original,originalCell)+256;
   int frames=attack=="Awakened" || SingleFrame>=0 ?1:4;
   for(int i=0;i<frames;i++) {
    if(attack=="Awaken" && i==3) {
     System.IO.File.Copy(System.IO.Path.Combine(folder,"CrayonHero_Awakened.png"),
         System.IO.Path.Combine(folder,"CrayonHero_Awaken_"+i+".png"),true);
     continue;
    }
    int left=frames==1?0:i%2*img.Width/2,top=frames==1?0:i/2*img.Height/2;
    int right=frames==1?img.Width:i%2==0?img.Width/2:img.Width,bottom=frames==1?img.Height:i/2==0?img.Height/2:img.Height;
    Rectangle cell;
    using(var isolated=ExtractPose(img,Rectangle.FromLTRB(left,top,right,bottom),out cell,attack.StartsWith("Awaken") || attack.StartsWith("Golden_")?128:8,frames==1?0:8)) {
    // Explicit ivory-crystal registration for the supplied 1536x1024 design.
    // Unlike the normal helmet this crystal is not blue, so do not mistake the blue collar for the head.
    var head=GoldenCrystals!=null ? GoldenCrystals[i] : attack=="Awakened" ? new Rectangle(636,91,250,220) : attack=="Awaken" && i==2
        ? IvoryCrystal : Helmet(isolated,cell);
    float scale=(float)anchor.Width/head.Width;
    float x=anchor.Left+anchor.Width/2f+256-(head.Left-cell.Left+head.Width/2f)*scale;
    float y=baseline-(Boots(isolated,cell)-cell.Top)*scale;
    if(x<8 || y<8 || x+cell.Width*scale>1016 || y+cell.Height*scale>1016) throw new Exception("Registered pose/effect exceeds output margins.");
    using(var frame=new Bitmap(1024,1024,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(frame)) {
     g.CompositingMode=CompositingMode.SourceCopy;
     g.InterpolationMode=InterpolationMode.HighQualityBicubic;
     g.PixelOffsetMode=PixelOffsetMode.HighQuality;
     g.DrawImage(isolated,new RectangleF(x,y,cell.Width*scale,cell.Height*scale),cell,GraphicsUnit.Pixel);
     frame.Save(System.IO.Path.Combine(folder,"CrayonHero_"+attack+(SingleFrame>=0?"_"+SingleFrame:frames==1?"":"_"+i)+".png"),ImageFormat.Png);
    }
    Console.WriteLine(attack+" "+i+": helmet="+head+", scale="+scale+", origin="+x+","+y);
    }
   }
  }
 }
}
'@
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskFolder = Join-Path $taskRoot 'game/Assets/Art/Bosses/FairyKingdom/Movement'
[CrayonFrameRegistration]::SingleFrame = $SingleFrame
if ($Attack -eq 'Awaken') {
 if (!$IvoryCrystal) { throw 'Awaken requires source-sheet ivory crystal registration: -IvoryCrystal x,y,width,height' }
 $taskCrystal = @($IvoryCrystal.Split(',') | ForEach-Object { [int]$_ })
 if ($taskCrystal.Count -ne 4 -or $taskCrystal[2] -le 0 -or $taskCrystal[3] -le 0) { throw 'Invalid ivory crystal bounds.' }
 [CrayonFrameRegistration]::IvoryCrystal = [Drawing.Rectangle]::new($taskCrystal[0],$taskCrystal[1],$taskCrystal[2],$taskCrystal[3])
}
if ($Attack.StartsWith('Golden_')) {
 $taskBoxes = @($IvoryCrystals.Split(';'))
 $taskExpected = if ($SingleFrame -ge 0) { 1 } else { 4 }
 if ($taskBoxes.Count -ne $taskExpected) { throw 'Golden attacks require one source crystal bounds per pose: -IvoryCrystals x,y,w,h;x,y,w,h;x,y,w,h;x,y,w,h' }
 [CrayonFrameRegistration]::GoldenCrystals = [Drawing.Rectangle[]]::new($taskExpected)
 for ($taskIndex = 0; $taskIndex -lt $taskExpected; $taskIndex++) {
  $taskValues = @($taskBoxes[$taskIndex].Split(',') | ForEach-Object { [int]$_ })
  if ($taskValues.Count -ne 4 -or $taskValues[2] -le 0 -or $taskValues[3] -le 0) { throw 'Invalid golden crystal bounds.' }
  [CrayonFrameRegistration]::GoldenCrystals[$taskIndex] = [Drawing.Rectangle]::new($taskValues[0],$taskValues[1],$taskValues[2],$taskValues[3])
 }
}
if ($PaddedSourcePath) {
 [CrayonFrameRegistration]::PadAtlas($SourcePath,$PaddedSourcePath,($Attack -eq 'Awaken' -or $Attack.StartsWith('Golden_')))
 $SourcePath = $PaddedSourcePath
}
[CrayonFrameRegistration]::Split($SourcePath,(Join-Path $taskFolder 'CrayonHero_Walk_0.png'),$taskFolder,$Attack)

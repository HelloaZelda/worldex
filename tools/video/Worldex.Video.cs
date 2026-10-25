namespace Worldex.Video {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Drawing.Text;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Web.Script.Serialization;

    public class Job {
        public string Playback,Output,Ffmpeg,Directory,Encoder;
        public int Width,Height,Fps;
        public int FrameCount;
        public bool VideoOnly;
        public double Start,Length;
    }
    public sealed class Renderer : IDisposable {
        readonly Visual visual;readonly Job job;readonly Settings settings;
        readonly Bitmap bitmap;readonly Graphics graphics;
        readonly Color[] colors;readonly SolidBrush[] brushes;
        readonly Font normal,tile,caption,small,header,title;
        readonly StringFormat format;
        readonly int[] order={0,1,2,3,4,5,6,9,10,11,12,13,14,15,16,17,7,8};
        class RasterGlyph {public int Width,Height;public int[] Pixels;}
        struct Placement {public RasterGlyph Glyph;public int X,Y;}
        readonly Dictionary<int,RasterGlyph> glyphs=new Dictionary<int,RasterGlyph>();
        readonly List<Placement> placements=new List<Placement>(16384);
        readonly int[] raster;
        public Renderer(Job config) {
            job=config;settings=Visual.Load<Settings>(job.Playback);visual=new Visual(settings);
            bitmap=new Bitmap(job.Width,job.Height,PixelFormat.Format32bppArgb);
            raster=new int[job.Width*job.Height];
            graphics=Graphics.FromImage(bitmap);graphics.PageUnit=GraphicsUnit.Pixel;
            graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            graphics.CompositingQuality=CompositingQuality.HighSpeed;
            colors=visual.Palette;colors[0]=Color.FromArgb(8,13,20);colors[1]=Color.FromArgb(83,109,126);
            colors[8]=Color.FromArgb(79,24,47);colors[9]=Color.FromArgb(11,47,44);
            brushes=new SolidBrush[16];for(int i=0;i<16;i++)brushes[i]=new SolidBrush(colors[i]);
            float scale=job.Height/1080f;
            normal=new Font("Consolas",14*scale,FontStyle.Regular,GraphicsUnit.Pixel);
            tile=new Font("Consolas",12*scale,FontStyle.Regular,GraphicsUnit.Pixel);
            caption=new Font("Consolas",24*scale,FontStyle.Regular,GraphicsUnit.Pixel);
            small=new Font("Consolas",10*scale,FontStyle.Regular,GraphicsUnit.Pixel);
            header=new Font("Consolas",11*scale,FontStyle.Regular,GraphicsUnit.Pixel);
            title=new Font("Consolas",21*scale,FontStyle.Regular,GraphicsUnit.Pixel);
            format=(StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags|=StringFormatFlags.MeasureTrailingSpaces|StringFormatFlags.NoWrap;
        }
        static GraphicsPath Round(RectangleF box,float radius) {
            float d=Math.Max(1,radius*2);var p=new GraphicsPath();
            p.AddArc(box.Left,box.Top,d,d,180,90);p.AddArc(box.Right-d,box.Top,d,d,270,90);
            p.AddArc(box.Right-d,box.Bottom-d,d,d,0,90);p.AddArc(box.Left,box.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        static void ClearRow(Canvas canvas,int y) {for(int x=0;x<canvas.W;x++){canvas.Cells[y*canvas.W+x].Char=' ';canvas.Cells[y*canvas.W+x].Attribute=1;}}
        void Glyph(char ch,int color,Font font,int cw,int lineHeight,int x,int y) {
            if(ch==' ')return;
            int fontId=Object.ReferenceEquals(font,normal)?1:Object.ReferenceEquals(font,tile)?2:Object.ReferenceEquals(font,caption)?3:4;
            int key=(fontId<<24)|(color<<16)|(int)ch;
            RasterGlyph glyph;
            if(!glyphs.TryGetValue(key,out glyph)) {
                glyph=new RasterGlyph();glyph.Width=cw;glyph.Height=lineHeight;glyph.Pixels=new int[cw*lineHeight];
                using(var image=new Bitmap(cw,lineHeight,PixelFormat.Format32bppArgb)) {
                    using(var g=Graphics.FromImage(image)) {
                        g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                        g.DrawString(ch.ToString(),font,brushes[color],new PointF(0,-1),format);
                    }
                    var data=image.LockBits(new Rectangle(0,0,cw,lineHeight),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                    try{Marshal.Copy(data.Scan0,glyph.Pixels,0,glyph.Pixels.Length);}finally{image.UnlockBits(data);}
                }
                glyphs.Add(key,glyph);
            }
            placements.Add(new Placement{Glyph=glyph,X=x,Y=y});
        }
        void Rasterize() {
            graphics.Flush();
            var data=bitmap.LockBits(new Rectangle(0,0,job.Width,job.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try {
                Marshal.Copy(data.Scan0,raster,0,raster.Length);
                foreach(var item in placements) {
                    var glyph=item.Glyph;
                    int maxY=Math.Min(glyph.Height,job.Height-item.Y),maxX=Math.Min(glyph.Width,job.Width-item.X);
                    for(int y=Math.Max(0,-item.Y);y<maxY;y++) {
                        int target=(item.Y+y)*job.Width+item.X,source=y*glyph.Width;
                        for(int x=Math.Max(0,-item.X);x<maxX;x++) {
                            int pixel=glyph.Pixels[source+x];uint alpha=(uint)pixel>>24;if(alpha==0)continue;
                            if(alpha==255){raster[target+x]=pixel;continue;}
                            uint old=(uint)raster[target+x],value=(uint)pixel,inverse=255-alpha;
                            uint rb=((((value&0x00ff00ff)*alpha+(old&0x00ff00ff)*inverse)>>8)&0x00ff00ff);
                            uint green=((((value&0x0000ff00)*alpha+(old&0x0000ff00)*inverse)>>8)&0x0000ff00);
                            raster[target+x]=(int)(0xff000000|rb|green);
                        }
                    }
                }
                Marshal.Copy(raster,0,data.Scan0,raster.Length);
            }finally{bitmap.UnlockBits(data);}
        }
        void Cells(Canvas canvas,Font font,int cw,int lineHeight,int left,int top) {
            for(int row=0;row<canvas.H;row++) {
                int run=0;
                while(run<canvas.W) {
                    int bg=(canvas.Cells[row*canvas.W+run].Attribute>>4)&15,finish=run+1;
                    while(finish<canvas.W&&((canvas.Cells[row*canvas.W+finish].Attribute>>4)&15)==bg)finish++;
                    if(bg!=0)graphics.FillRectangle(brushes[bg],left+run*cw,top+row*lineHeight,(finish-run)*cw,lineHeight);
                    run=finish;
                }
                for(int col=0;col<canvas.W;col++) {
                    var cell=canvas.Cells[row*canvas.W+col];Glyph(cell.Char,cell.Attribute&15,font,cw,lineHeight,left+col*cw,top+row*lineHeight);
                }
            }
        }
        public Bitmap Frame(double time) {
            float scale=job.Height/1080f;graphics.Clear(colors[0]);placements.Clear();
            using(var grid=new Pen(Color.FromArgb(12,54,78,92))) {
                for(int x=24;x<job.Width;x+=48)for(int y=24;y<job.Height;y+=48)graphics.DrawLine(grid,x,y,x+1,y);
            }
            graphics.DrawString("M I L I",header,brushes[2],job.Width*.025f,job.Height*.025f,format);
            graphics.DrawString("world.execute(me);",title,brushes[6],job.Width*.025f,job.Height*.044f,format);
            graphics.DrawString("TERMINAL BALLET  /  WORLD EX",small,brushes[1],job.Width*.80f,job.Height*.975f,format);
            foreach(int index in order) {
                if(!visual.FormationVisible(index,time))continue;
                Box box=visual.Position(index,time);
                if(index==7){box.y=.79;box.h=.195;}
                int x=(int)Math.Round(box.x*job.Width),y=(int)Math.Round(box.y*job.Height),w=(int)Math.Round(box.w*job.Width),h=(int)Math.Round(box.h*job.Height);
                int bar=(int)Math.Round((index==7?22:20)*scale),pad=Math.Max(4,(int)(6*scale));
                bool tiled=index!=7&&index!=8&&w<job.Width*.2;
                int accent=4;
                string name=visual.FormationTitle(index,time).Replace("WORLDEX / ","");
                bool warm=name.StartsWith("ME")||name.Contains("TERMINATE");if(!warm)accent=2;
                using(var path=Round(new RectangleF(x,y,w,h),4*scale))
                using(var shadow=Round(new RectangleF(x,y+4*scale,w,h),4*scale))
                using(var shade=new SolidBrush(Color.FromArgb(90,0,0,0))) {
                    graphics.FillPath(shade,shadow);
                    using(var surface=new SolidBrush(Color.FromArgb(10,17,25)))graphics.FillPath(surface,path);
                    var state=graphics.Save();graphics.SetClip(path);
                    using(var cap=new SolidBrush(warm?Color.FromArgb(30,21,34):Color.FromArgb(17,29,40)))graphics.FillRectangle(cap,x,y,w,bar);
                    string badge=index==7?"TXT":index==8?"OSC":index%2==0?"PS":"CMD";
                    using(var stamp=new SolidBrush(Color.FromArgb(34,colors[accent])))graphics.FillRectangle(stamp,x+pad,y+4*scale,25*scale,12*scale);
                    graphics.DrawString(badge,small,brushes[accent],x+pad+2*scale,y+4*scale,format);
                    int nameLeft=x+pad+(int)(32*scale);
                    graphics.DrawString(name,header,brushes[3],new RectangleF(nameLeft,y+3*scale,Math.Max(10,w-pad-(nameLeft-x)-10*scale),bar),format);
                    using(var line=new Pen(Color.FromArgb(45,colors[accent])))graphics.DrawLine(line,x,y+bar,w+x,y+bar);
                    Font font=index==7?caption:index==8?small:tiled?tile:normal;
                    int cw=Math.Max(4,(int)Math.Round(font.Size*.55)),lh=Math.Max(7,(int)Math.Round(font.Size*(index==7?1.16:1.28)));
                    int columns=Math.Max(8,(w-2*pad)/cw),rows=Math.Max(4,(h-bar-2*pad)/lh);
                    Canvas canvas=visual.Draw(index,columns,rows,time,false);
                    if(index==7){ClearRow(canvas,0);}
                    if(index==8){ClearRow(canvas,0);ClearRow(canvas,canvas.H-1);}
                    // Do not queue glyphs outside the inner viewport when a small
                    // terminal has fewer rows than the live renderer's minimum.
                    int fitRows=Math.Max(0,(h-bar-2*pad)/lh);
                    if(canvas.H>fitRows){canvas.H=fitRows;}
                    Cells(canvas,font,cw,lh,x+pad,y+bar+pad);
                    graphics.Restore(state);
                    using(var border=new Pen(Color.FromArgb(warm?110:80,colors[accent]),Math.Max(1,scale)))graphics.DrawPath(border,path);
                }
            }
            Rasterize();
            double fade=Math.Min(1,Math.Max(0,(settings.Duration-time)/.70));
            if(fade<1)using(var shade=new SolidBrush(Color.FromArgb((int)((1-fade)*255),colors[0])))graphics.FillRectangle(shade,0,0,job.Width,job.Height);
            return bitmap;
        }
        public static void Stills(string path) {
            Job job=Visual.Load<Job>(path);using(var renderer=new Renderer(job)) {
                foreach(double time in new[]{.8,14.6,38.2,67.25,124.4,160.0,183.7,200.0,206.5})renderer.Frame(time).Save(Path.Combine(job.Directory,"style-"+time.ToString("000.0",System.Globalization.CultureInfo.InvariantCulture)+".png"));
            }
        }
        static void Status(Job job,string state,int frame,int total,double fps,string message) {
            var data=new {stage=state,frame=frame,total=total,percent=total==0?0:100.0*frame/total,fps=fps,etaSeconds=fps>0?(total-frame)/fps:0,output=job.Output,message=message};
            File.WriteAllText(Path.Combine(job.Directory,"progress.json"),new JavaScriptSerializer().Serialize(data));
        }
        public static void Export(string path) {
            Job job=Visual.Load<Job>(path);Directory.CreateDirectory(job.Directory);var clock=Stopwatch.StartNew();
            try {
                using(var renderer=new Renderer(job))using(var log=new StreamWriter(Path.Combine(job.Directory,"encoder.log"),false,Encoding.UTF8)) {
                    log.AutoFlush=true;
                    double duration=job.Length>0?job.Length:renderer.settings.Duration-job.Start;int total=job.FrameCount>0?job.FrameCount:(int)Math.Ceiling(duration*job.Fps);
                    string video=job.Encoder=="h264_nvenc"?"-c:v h264_nvenc -preset p5 -rc vbr -cq 19 -b:v 0":"-c:v libx264 -preset fast -crf 18 -threads 4";
                    string temporary=Path.Combine(job.Directory,"rendering.mp4");
                    var start=new ProcessStartInfo(job.Ffmpeg);
                    string audio=job.VideoOnly?"":" -ss "+job.Start.ToString(System.Globalization.CultureInfo.InvariantCulture)+" -i \""+renderer.settings.Audio+"\"";
                    string audioCodec=job.VideoOnly?" -an":" -c:a aac -b:a 192k -ar 48000";
                    start.Arguments="-hide_banner -loglevel warning -y -f rawvideo -pixel_format bgra -video_size "+job.Width+"x"+job.Height+" -framerate "+job.Fps+" -i pipe:0"+audio+" "+video+" -pix_fmt yuv420p"+audioCodec+" -movflags +faststart -shortest \""+temporary+"\"";
                    start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardInput=true;start.RedirectStandardError=true;
                    using(var encoder=new Process()) {
                        encoder.StartInfo=start;
                        encoder.ErrorDataReceived+=(sender,e)=>{if(e.Data!=null)lock(log)log.WriteLine(e.Data);};
                        encoder.Start();encoder.BeginErrorReadLine();var bytes=new byte[job.Width*job.Height*4];double drawing=0,writing=0;
                        try {
                            for(int frame=0;frame<total;frame++) {
                                if(File.Exists(Path.Combine(job.Directory,"cancel")))throw new OperationCanceledException("Video export cancelled");
                                var frameClock=Stopwatch.StartNew();Bitmap bitmap=renderer.Frame(job.Start+frame/(double)job.Fps);
                                var bits=bitmap.LockBits(new Rectangle(0,0,job.Width,job.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                                try{Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);}finally{bitmap.UnlockBits(bits);}
                                drawing+=frameClock.Elapsed.TotalMilliseconds;frameClock.Restart();
                                encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);writing+=frameClock.Elapsed.TotalMilliseconds;
                                if(frame%job.Fps==0)Status(job,"rendering",frame,total,(frame+1)/Math.Max(.001,clock.Elapsed.TotalSeconds),String.Format("draw {0:0.0}ms / encode pipe {1:0.0}ms",drawing/(frame+1),writing/(frame+1)));
                            }
                            encoder.StandardInput.Close();Status(job,"finalizing",total,total,total/clock.Elapsed.TotalSeconds,null);
                            encoder.WaitForExit();if(encoder.ExitCode!=0)throw new IOException("FFmpeg failed: "+encoder.ExitCode+". See encoder.log");
                            Directory.CreateDirectory(Path.GetDirectoryName(job.Output));File.Copy(temporary,job.Output,true);
                            Status(job,"complete",total,total,total/clock.Elapsed.TotalSeconds,null);
                        }catch{try{encoder.StandardInput.Close();if(!encoder.HasExited)encoder.Kill();}catch{}throw;}
                    }
                }
            }catch(Exception ex){Status(job,"error",0,0,0,ex.ToString());File.WriteAllText(Path.Combine(job.Directory,"error.log"),ex.ToString());}
        }
        public void Dispose() {
            foreach(var brush in brushes)brush.Dispose();
            normal.Dispose();tile.Dispose();caption.Dispose();small.Dispose();header.Dispose();title.Dispose();format.Dispose();graphics.Dispose();bitmap.Dispose();
        }
    }
}

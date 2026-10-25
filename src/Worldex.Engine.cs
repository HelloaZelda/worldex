using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Worldex {
    public class Settings {
        public string Root, Run, Audio, Features, Score, Tokens;
        public double Duration, Bpm, BeatOffset, Seek, CueOffset, Preview;
        public bool Mute;
    }
    public class Scene { public double time; public string kind, label, layout, code; }
    public class Score { public Scene[] scenes; }
    public class Theme {
        public Dictionary<string,object> primitive;
        public Dictionary<string,string> semantic, component;
        public Color Color(string role) { return ColorTranslator.FromHtml((string)primitive[semantic[role]]); }
        public string Font { get { return (string)primitive["font"]; } }
        public int Number(string key) { return Convert.ToInt32(primitive[key]); }
    }
    public static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct Coord { public short X,Y; public Coord(int x,int y) { X=(short)x;Y=(short)y; } }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public short L,T,R,B; }
        [StructLayout(LayoutKind.Explicit, CharSet=CharSet.Unicode)] public struct Cell {
            [FieldOffset(0)] public char Char;
            [FieldOffset(2)] public ushort Attribute;
        }
        [StructLayout(LayoutKind.Sequential)] public struct BufferInfo {
            public uint Size; public Coord Buffer, Cursor; public ushort Attributes; public Rect Window;
            public Coord MaxWindow; public ushort PopupAttributes; public int Fullscreen;
            [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public uint[] Colors;
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct FontInfo {
            public uint Size, Font; public Coord Cell; public int Family, Weight;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Face;
        }
        [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int n);
        [DllImport("kernel32.dll")] public static extern bool SetConsoleOutputCP(uint page);
        [DllImport("kernel32.dll")] public static extern bool SetConsoleCP(uint page);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern bool WriteConsoleOutputW(IntPtr h, Cell[] cells, Coord size, Coord origin, ref Rect region);
        [DllImport("kernel32.dll")] public static extern bool GetConsoleScreenBufferInfoEx(IntPtr h, ref BufferInfo info);
        [DllImport("kernel32.dll")] public static extern bool SetConsoleScreenBufferInfoEx(IntPtr h, ref BufferInfo info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern bool SetCurrentConsoleFontEx(IntPtr h, bool max, ref FontInfo info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern bool GetCurrentConsoleFontEx(IntPtr h, bool max, ref FontInfo info);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("kernel32.dll")] public static extern bool GetConsoleMode(IntPtr h, out uint mode);
        [DllImport("kernel32.dll")] public static extern bool SetConsoleMode(IntPtr h, uint mode);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int SetWindowLong(IntPtr h,int index,int value);
    }
    public class Stage : Form {
        public Action Quit;
        protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);Native.RegisterHotKey(Handle,1,0x4006,0x51);}
        protected override void WndProc(ref Message m) {if(m.Msg==0x0312&&Quit!=null)Quit();base.WndProc(ref m);}
        protected override void OnFormClosed(FormClosedEventArgs e) {Native.UnregisterHotKey(Handle,1);base.OnFormClosed(e);}
    }
    public sealed class Audio : IDisposable {
        [StructLayout(LayoutKind.Sequential,Pack=2)] struct Format {
            public ushort tag, channels; public uint rate, bytesPerSecond; public ushort align,bits,extra;
        }
        [StructLayout(LayoutKind.Sequential)] struct Header {
            public IntPtr data; public uint length,recorded; public UIntPtr user; public uint flags,loops; public IntPtr next; public UIntPtr reserved;
        }
        [StructLayout(LayoutKind.Sequential)] struct Position { public uint type,value,pad; }
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h,uint device,ref Format format,IntPtr callback,IntPtr instance,uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern int waveOutGetPosition(IntPtr h,ref Position position,uint size);
        [DllImport("winmm.dll")] static extern int waveOutPause(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutRestart(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
        IntPtr device, data, header; int length; uint speed,rate; ushort align; double offset; bool prepared;
        uint HeaderSize { get { return (uint)Marshal.SizeOf(typeof(Header)); } }
        static void Check(int result,string action) { if(result!=0) throw new IOException(action+" failed: waveOut "+result); }
        public Audio(string path,bool mute) {
            using(var reader=new BinaryReader(File.OpenRead(path))) {
                if(new string(reader.ReadChars(4))!="RIFF") throw new IOException("Not a RIFF file");
                reader.ReadUInt32(); if(new string(reader.ReadChars(4))!="WAVE") throw new IOException("Not WAVE");
                Format format=new Format(); byte[] samples=null;
                while(reader.BaseStream.Position+8<=reader.BaseStream.Length) {
                    string id=new string(reader.ReadChars(4)); uint size=reader.ReadUInt32(); long end=reader.BaseStream.Position+size;
                    if(id=="fmt ") { format.tag=reader.ReadUInt16();format.channels=reader.ReadUInt16();format.rate=reader.ReadUInt32();format.bytesPerSecond=reader.ReadUInt32();format.align=reader.ReadUInt16();format.bits=reader.ReadUInt16(); }
                    if(id=="data") samples=reader.ReadBytes((int)size);
                    reader.BaseStream.Position=end+(size%2);
                }
                if(samples==null || format.tag!=1 || format.bits!=16) throw new IOException("Expected PCM16 samples");
                length=samples.Length; speed=format.bytesPerSecond;rate=format.rate;align=format.align;
                // Preview mute is silent PCM, never a device/session volume change.
                if(mute)Array.Clear(samples,0,samples.Length);
                data=Marshal.AllocHGlobal(length); Marshal.Copy(samples,0,data,length);
                header=Marshal.AllocHGlobal((int)HeaderSize);
                try { Check(waveOutOpen(out device,0xffffffff,ref format,IntPtr.Zero,IntPtr.Zero,0),"Open audio"); }
                catch { Dispose();throw; }
            }
        }
        public void Seek(double seconds) {
            waveOutReset(device);
            if(prepared) { Check(waveOutUnprepareHeader(device,header,HeaderSize),"Release audio buffer"); prepared=false; }
            int skip=(int)Math.Min(length-align,Math.Max(0,seconds*speed)); skip-=skip%align;
            offset=(double)skip/speed;
            var item=new Header();item.data=IntPtr.Add(data,skip);item.length=(uint)(length-skip);
            Marshal.StructureToPtr(item,header,false);
            Check(waveOutPrepareHeader(device,header,HeaderSize),"Prepare audio");prepared=true;
            Check(waveOutWrite(device,header,HeaderSize),"Play audio");
        }
        public double Time {
            get { var p=new Position(); p.type=4; Check(waveOutGetPosition(device,ref p,12),"Read audio clock"); return offset+(p.type==4?(double)p.value/speed:p.type==2?(double)p.value/rate:p.value/1000.0); }
        }
        public void Pause(bool pause) { Check(pause?waveOutPause(device):waveOutRestart(device),"Pause/resume"); }
        public void Dispose() {
            if(device!=IntPtr.Zero) { waveOutReset(device);if(prepared) waveOutUnprepareHeader(device,header,HeaderSize);waveOutClose(device);device=IntPtr.Zero; }
            if(header!=IntPtr.Zero) {Marshal.FreeHGlobal(header);header=IntPtr.Zero;}
            if(data!=IntPtr.Zero) {Marshal.FreeHGlobal(data);data=IntPtr.Zero;}
        }
    }
    public class Features {
        float[] values; int fps, count, stride;
        public Features(string path) { using(var b=new BinaryReader(File.OpenRead(path))) { fps=b.ReadInt32();count=b.ReadInt32();stride=b.ReadInt32();values=new float[count*stride];for(int i=0;i<values.Length;i++)values[i]=b.ReadSingle(); } }
        public double At(double time,int band) {
            double frame=Math.Max(0,Math.Min(count-1,time*fps));int a=(int)frame,b=Math.Min(count-1,a+1);
            return values[a*stride+band]*(1-(frame-a))+values[b*stride+band]*(frame-a);
        }
    }
    public struct Box {
        public double x,y,w,h;public Box(double a,double b,double c,double d) {x=a;y=b;w=c;h=d;}
        public static Box Mix(Box a,Box b,double t) { return new Box(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.w+(b.w-a.w)*t,a.h+(b.h-a.h)*t); }
    }
    public class Canvas {
        public int W,H;public Native.Cell[] Cells;
        public Canvas(int w,int h) { W=w;H=h;Cells=new Native.Cell[w*h];Clear(); }
        public void Clear() { for(int i=0;i<Cells.Length;i++){Cells[i].Char=' ';Cells[i].Attribute=1;} }
        public void Put(int x,int y,char c,int color) { if(x>=0&&y>=0&&x<W&&y<H) { Cells[y*W+x].Char=c;Cells[y*W+x].Attribute=(ushort)color; } }
        public void Text(int x,int y,string text,int color) { for(int i=0;i<text.Length;i++)Put(x+i,y,text[i],color); }
        public void Center(int y,string text,int color) { Text((W-text.Length)/2,y,text,color); }
        public void Line(double x0,double y0,double x1,double y1,char c,int color) {
            int n=(int)Math.Max(Math.Abs(x1-x0),Math.Abs(y1-y0))+1;
            for(int i=0;i<=n;i++){double t=(double)i/n;Put((int)Math.Round(x0+(x1-x0)*t),(int)Math.Round(y0+(y1-y0)*t),c,color);}
        }
        public void Shape(double x,double y,char c,int color) { Put((int)(W/2.0+x),(int)(H/2.0+y),c,color); }
        public void Circle(double x,double y,double r,double ratio,int color,char ch) {
            for(double t=0;t<Math.PI*2;t+=.035) Shape(x+r*Math.Cos(t),y+r*ratio*Math.Sin(t),ch,color);
        }
        static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string> {
            {'W',"10001/10001/10101/10101/01010"},{'O',"01110/10001/10001/10001/01110"},{'R',"11110/10001/11110/10100/10010"},
            {'L',"10000/10000/10000/10000/11111"},{'D',"11110/10001/10001/10001/11110"},{'E',"11111/10000/11110/10000/11111"},
            {'X',"10001/01010/00100/01010/10001"},{'C',"01111/10000/10000/10000/01111"},{'U',"10001/10001/10001/10001/01110"},
            {'T',"11111/00100/00100/00100/00100"},{'M',"10001/11011/10101/10001/10001"},{'H',"10001/10001/11111/10001/10001"},
            {'A',"01110/10001/11111/10001/10001"},{'I',"11111/00100/00100/00100/11111"},{'N',"10001/11001/10101/10011/10001"},
            {'B',"11110/10001/11110/10001/11110"},{'Y',"10001/01010/00100/00100/00100"},{'F',"11111/10000/11110/10000/10000"},
            {'6',"01110/10000/11110/10001/01110"},{'5',"11111/10000/11110/00001/11110"},{'4',"10010/10010/11111/00010/00010"},
            {'3',"11110/00001/01110/00001/11110"},{'2',"01110/10001/00110/01000/11111"},{'1',"00100/01100/00100/00100/01110"},
            {'0',"01110/10011/10101/11001/01110"},{'?',"01110/10001/00110/00000/00100"}
        };
        public void Big(string word,int y,int color,int scale) {
            int left=(W-(word.Length*6-1)*scale)/2;
            for(int i=0;i<word.Length;i++) {
                if(!Glyphs.ContainsKey(word[i]))continue;var rows=Glyphs[word[i]].Split('/');
                for(int r=0;r<5;r++)for(int c=0;c<5;c++)if(rows[r][c]=='1')for(int yy=0;yy<scale;yy++)for(int xx=0;xx<scale;xx++)Put(left+(i*6+c)*scale+xx,y+r*scale+yy,'█',color);
            }
        }
    }
    public class Visual {
        public Settings Config;public Scene[] Scenes;public Features Data;public Theme Theme;
        public Visual(Settings settings) { Config=settings;Scenes=Load<Score>(settings.Score).scenes;Data=new Features(settings.Features);Theme=Load<Theme>(settings.Tokens); }
        public static T Load<T>(string path) { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path)); }
        public int SceneAt(double time) { int i=0;while(i+1<Scenes.Length&&Scenes[i+1].time<=time+Config.CueOffset)i++;return i; }
        public double Beat(double time) { return (time-Config.BeatOffset)*Config.Bpm/60; }
        public static Box Layout(string name,int index,double t) {
            if(index==6) {
                if(name=="solo"||name=="heart")return new Box(.22,.16,.56,.67);
                if(name=="execute")return new Box(.20,.25,.60,.50);
                if(name=="split")return new Box(.29,.16,.42,.67);
                return new Box(.285,.18,.43,.64);
            }
            int row=index/2;bool right=index%2==1;
            if(name=="solo")return new Box(right?.84:.025,.12+row*.25,.135,.18);
            if(name=="execute")return new Box(right?.68:.02,.035+row*.325,.30,.28);
            if(name=="orbit") {
                double angle=-Math.PI/2+index*Math.PI/3;
                return new Box(.5+Math.Cos(angle)*.355-.112,.5+Math.Sin(angle)*.345-.12,.224,.24);
            }
            if(name=="heart")return new Box(right?.80:.02,.08+row*.285,.18,.23);
            return new Box(right?.745:.018,.055+row*.30,.237,.265);
        }
        public Box Position(int index,double time) {
            int s=SceneAt(time);Scene current=Scenes[s];Box to=Layout(current.layout,index,time);
            Box from=Layout(s>0?Scenes[s-1].layout:current.layout,index,time);
            double p=Math.Max(0,Math.Min(1,(time+Config.CueOffset-current.time)/.85));p=p*p*(3-2*p);
            Box b=Box.Mix(from,to,p);
            double pulse=Math.Exp(-(Beat(time)-Math.Floor(Beat(time)))*7)*.002;
            if(current.layout!="solo") {b.x-=pulse*.5;b.w+=pulse;b.y-=pulse*.25;b.h+=pulse*.5;}
            return b;
        }
        public Color[] Palette {
            get { var p=new Color[16];p[0]=Theme.Color("background");p[1]=Theme.Color("quiet");p[2]=Theme.Color("primary");p[3]=Theme.Color("structure");p[4]=Theme.Color("conflict");p[5]=Theme.Color("organic");p[6]=Theme.Color("foreground");for(int i=7;i<16;i++)p[i]=p[6];return p; }
        }
        public Canvas Draw(int index,int width,int height,double time,bool paused) {
            var c=new Canvas(width,height);int sceneIndex=SceneAt(time);Scene scene=Scenes[sceneIndex];
            double age=Math.Max(0,time+Config.CueOffset-scene.time),beat=Beat(time),pulse=Math.Exp(-(beat-Math.Floor(beat))*7),energy=Data.At(time,0);
            bool conflict=scene.kind=="execute"||scene.kind=="error"||scene.kind=="storm"||scene.kind=="countdown";
            int accent=conflict?4:scene.kind=="plant"||scene.kind=="fruit"||scene.kind=="cat"?5:2;
            double radius=Math.Min(width*.36,height*.83)*(1+pulse*.07);
            string[] titles={"OSC / SIGNAL","XYZ / GEOMETRY","PARTICLE / FIELD","MEM / FRAGMENTS","FFT / SPECTRUM","STATE / OBSERVER","W O R L D E X"};
            c.Text(2,0,titles[index],3);c.Text(2,1,new string('─',Math.Max(0,width-4)),1);
            string mode=scene.kind;
            if(index!=6) {
                if(scene.kind=="isolation"||scene.kind=="shutdown")mode="isolation";
                else if(conflict && index%2==0)mode=scene.kind;
                else if(index==0)mode="signal";
                else if(index==1)mode=scene.kind=="circle"||scene.kind=="sine"||scene.kind=="infinity"?scene.kind:"cube";
                else if(index==2)mode="points";
                else if(index==3)mode=scene.kind=="fragments"?"fragments":"data";
                else if(index==4)mode="spectrum";
                else if(index==5)mode=scene.kind=="cat"?"cat":"eye";
            }
            switch(mode) {
                case "power":
                    for(int k=0;k<6;k++) {double y=4+k*(height-8)/6.0;double end=Math.Min(width-4,(age*.7-k*.12)*width);c.Line(3,y,end,y,'─',end>width*.7?2:1);if(end>4)c.Text((int)end-2,(int)y,"[+]",accent);}
                    if(index==6)c.Big("BOOT",height/2-2,6,Math.Max(1,Math.Min(2,width/30)));break;
                case "cube":case "cage":case "cageheart":
                    Cube(c,time,radius,accent,mode=="cageheart"?1:0);
                    if(mode=="cageheart")Heart(c,time,radius*.65,4,pulse);
                    else if(mode=="cage")c.Center(height/2,"[ ENTITY / BOUND ]",6);
                    break;
                case "world":
                    Globe(c,time,radius,accent);break;
                case "points":
                    for(int i=0;i<110;i++){double a=i*2.39996+time*.13,z=1-2*(i+.5)/110,rr=Math.Sqrt(1-z*z);c.Shape(Math.Cos(a)*rr*radius,z*radius*.43, i%9==0?'◆':'·',i%9==0?accent:1);}
                    c.Line(width/2,height/2,width/2+radius,height/2,'─',3);c.Line(width/2,height/2,width/2,height/2-radius*.5,'│',3);break;
                case "circle":
                    for(int i=0;i<3;i++)c.Circle(0,0,radius*(.55+i*.17),.48,i==2?accent:1,'·');
                    double a1=time*.9;double xx=width/2+radius*.89*Math.Cos(a1),yy=height/2+radius*.43*Math.Sin(a1);
                    c.Line(width/2,height/2,xx,yy,'·',3);c.Text((int)xx,(int)yy,"●",6);c.Center(height-5,"x*x + y*y = r*r",3);break;
                case "sine":case "signal":case "current":
                    Wave(c,time,energy,pulse,accent,mode=="current");
                    if(mode=="sine") {
                        double px=c.W*(.5+.22*Math.Sin(time*.7)),phase=px/c.W*4*Math.PI-time*3;
                        double amp=(c.H-9)*.34*(.35+energy*.65),py=c.H/2+Math.Sin(phase)*amp;
                        double slope=Math.Cos(phase)*amp*4*Math.PI/c.W;
                        c.Line(px-c.W*.15,py-slope*c.W*.15,px+c.W*.15,py+slope*c.W*.15,'·',5);
                        c.Put((int)px,(int)py,'◆',6);c.Text((int)px+2,(int)py-1,"dy/dx",5);
                    }
                    break;
                case "infinity":
                    for(int j=0;j<3;j++)for(double a=0;a<6.29;a+=.025) {double k=1+Math.Sin(a)*Math.Sin(a);c.Shape(Math.Cos(a)/k*radius*(.7+j*.15),Math.Sin(a)*Math.Cos(a)/k*radius*.65,'·',j==2?accent:1);}
                    c.Shape(Math.Cos(time)/ (1+Math.Sin(time)*Math.Sin(time))*radius,Math.Sin(time)*Math.Cos(time)/(1+Math.Sin(time)*Math.Sin(time))*radius*.65,'◆',6);break;
                case "time":case "switch":
                    c.Circle(0,0,radius*.8,.48,1,'·');
                    for(int j=0;j<12;j++){double a=j*Math.PI/6;c.Shape(Math.Sin(a)*radius*.8,-Math.Cos(a)*radius*.384,'■',3);}
                    c.Line(width/2,height/2,width/2+Math.Sin(time*.8)*radius*.65,height/2-Math.Cos(time*.8)*radius*.31,'·',accent);
                    c.Center(height/2+3,mode=="switch"?(((int)beat%2==0)?"[ A ]  <==>  [ B ]":"[ B ]  <==>  [ A ]"):"BEFORE < 0 > AFTER",6);break;
                case "resonance":
                    for(int j=0;j<4;j++){double r=(time*5+j*radius/4)%radius;c.Circle(-radius*.32,0,r,.45,j%2==0?accent:1,'·');c.Circle(radius*.32,0,r,.45,j%2==0?3:1,'·');}
                    c.Center(height/2,"A <────────> B",6);break;
                case "plant":
                    for(int k=0;k<10;k++){double y=height-6-k*(height-10)/10.0,x=width/2+Math.Sin(k*.4+time*.4)*2;c.Line(x,y,x,height-5,'│',2);double side=k%2==0?1:-1;for(double p=0;p<1;p+=.035)c.Shape(x-width/2+side*Math.Sin(p*Math.PI)*radius*.4, y-height/2-p*radius*.15,'·',5);}
                    c.Center(height-5,"SEED -> CELL -> LIFE",3);break;
                case "fruit":
                    for(int k=0;k<6;k++)c.Circle(Math.Sin(k)*radius*.17,Math.Cos(k)*radius*.08,radius*.55,.5,k==5?4:1,'·');
                    c.Text(width/2-3,(int)(height/2-radius*.29),"\\ | /",2);c.Center(height-5,"BIOLOGY / REPAIR",5);break;
                case "cat":
                    string[] cat={" /\\_____/\\ ","/           \\","|  o     o  |","|     ^     |"," \\  \\___/  / ","  `-------'  "};
                    for(int j=0;j<cat.Length;j++)c.Center(height/2-3+j,cat[j],j==2?6:accent);
                    c.Circle(0,0,radius*(.65+pulse*.15),.40,1,'·');c.Center(height-5,"purr(t) ~ sin(2*pi*f*t)",3);break;
                case "eye":
                    for(double a=0;a<6.29;a+=.025)c.Shape(Math.Cos(a)*radius,Math.Sin(a)*Math.Sin(a)*Math.Sign(Math.Sin(a))*radius*.30,'·',3);
                    c.Circle(Math.Sin(time*.4)*radius*.16,0,radius*.24,.48,accent,'●');break;
                case "data":case "fragments":
                    for(int j=0;j<height-7;j++) {
                        int n=(j*7919+(int)(time*9)*31+index*997)&65535;
                        string text=String.Format("{0:X4}  {1:X4} {2:X4} {3:X4}  {4}",j*32,n,n^34817,n^1783,j%3==0?"ALLOC":"LINK ");
                        int shift=mode=="fragments"?(int)(Math.Sin(j*19.7)*Math.Min(width,age*7)):0;
                        if(mode!="fragments"||j%5>age*.1)c.Text(2+shift,3+j,text,j%5==0?accent:1);
                    }break;
                case "spectrum":
                    for(int k=0;k<16;k++){int x=2+k*(width-4)/16;int hh=(int)(Data.At(time,k+4)*(height-8));for(int y=0;y<hh;y++)for(int dx=0;dx<Math.Max(1,(width-4)/16-1);dx++)c.Put(x+dx,height-5-y,y%3==0?'─':'█',y>height*.5?3:accent);}
                    break;
                case "execute":case "error":case "storm":case "countdown":
                    for(int j=0;j<40;j++){double a=j*2.399+time*(mode=="storm"?.8:.25);double r=((j*.113+time*.30)%1)*radius;c.Shape(Math.Cos(a)*r,Math.Sin(a)*r*.5,j%3==0?'+':'·',1);}
                    string word=mode=="error"?"ERROR":mode=="countdown"?Math.Max(1,6-(int)(age/.45)).ToString():mode=="storm"?"RECUR":"EXEC";
                    int scale=index==6?Math.Max(1,Math.Min(2,(width-6)/(word.Length*6))):1;
                    c.Big(word,height/2-(5*scale)/2,pulse>.35?6:4,scale);
                    c.Center(height-5,mode=="error"?"[ INVALID WORLD STATE ]":"CALL / RETURN / CALL / RETURN",accent);break;
                case "heart":
                    Heart(c,time,radius,accent,pulse);if(index==6)c.Center(height-5,"(x*x + y*y - 1)^3 - x*x*y*y*y = 0",3);break;
                case "isolation":
                    if(index==6){c.Circle(0,0,radius*.25,.48,1,'·');c.Center(height/2,"ME",3);c.Center(height/2+3,"NO PEER FOUND",1);}
                    else { c.Center(height/2,"- - -",1);c.Center(height/2+2,"DISCONNECTED",1); }break;
                case "shutdown":
                    if(age<1.2)c.Line(3,height/2,width-3,height/2,'─',3);
                    else { c.Center(height/2,"PROCESS EXITED",3);c.Center(height/2+2,"[ 0 ]",1); }break;
            }
            // The upper and lower gutters remain legible over every graphic.
            for(int x=0;x<width;x++) {c.Put(x,height-3,' ',1);c.Put(x,height-2,' ',1);c.Put(x,height-1,' ',1);}
            c.Text(2,height-3,scene.code,accent);
            string clock=TimeSpan.FromSeconds(Math.Max(0,time)).ToString(@"mm\:ss")+" / "+TimeSpan.FromSeconds(Config.Duration).ToString(@"mm\:ss");
            c.Text(2,height-2,index==6?scene.label:clock+"  "+Config.Bpm.ToString("0.0")+" BPM",3);
            if(index==6)c.Text(2,height-1,(paused?"PAUSED":"PLAYING")+"  SPACE pause | <- -> seek | ESC stop",1);
            else {int fill=(int)((width-4)*Math.Min(1,Math.Max(0,time/Config.Duration)));c.Text(2,height-1,new string('━',fill),accent);}
            return c;
        }
        static void Wave(Canvas c,double time,double energy,double pulse,int accent,bool current) {
            c.Line(2,c.H/2,c.W-3,c.H/2,'─',1);
            for(int j=2;j<c.W-2;j++) {
                double a=(j/(double)c.W*4*Math.PI)-time*3;
                double value=current&&((int)(time*2)%2==1)?Math.Sign(Math.Sin(a)):Math.Sin(a);
                int y=(int)(c.H/2+value*(c.H-9)*.34*(.35+energy*.65));c.Put(j,y,'●',accent);
                if(j%3==0)c.Put(j,(int)(c.H/2+Math.Sin(a*.5+1)*(c.H-9)*.20),'·',3);
            }
        }
        static void Heart(Canvas c,double time,double radius,int color,double pulse) {
            for(int layer=0;layer<3;layer++)for(double a=0;a<6.29;a+=.018) {
                double s=(.75+layer*.12)*(1+pulse*.06),x=16*Math.Pow(Math.Sin(a),3)/18,y=-(13*Math.Cos(a)-5*Math.Cos(2*a)-2*Math.Cos(3*a)-Math.Cos(4*a))/18;
                c.Shape(x*radius*s,y*radius*.46*s,layer==2?'●':'·',layer==2?color:1);
            }
        }
        static void Globe(Canvas c,double time,double radius,int accent) {
            for(int j=0;j<10;j++)for(double a=0;a<6.29;a+=.035){double lat=(j/9.0-.5)*Math.PI,x=Math.Cos(lat)*Math.Cos(a+time*.22),z=Math.Cos(lat)*Math.Sin(a+time*.22);c.Shape(x*radius,Math.Sin(lat)*radius*.45,'·',z>0?accent:1);}
            for(int j=0;j<10;j++)for(double a=-1.57;a<1.57;a+=.035){double phi=j*Math.PI/5+time*.22,z=Math.Cos(a)*Math.Sin(phi);c.Shape(Math.Cos(a)*Math.Cos(phi)*radius,Math.Sin(a)*radius*.45,'·',z>0?3:1);}
        }
        static void Cube(Canvas c,double time,double radius,int color,int cage) {
            double[,] v=new double[8,2];
            for(int i=0;i<8;i++){
                double x=(i&1)==0?-1:1,y=(i&2)==0?-1:1,z=(i&4)==0?-1:1;
                double a=time*.35,xx=x*Math.Cos(a)+z*Math.Sin(a),zz=-x*Math.Sin(a)+z*Math.Cos(a);
                double yy=y*Math.Cos(.4)-zz*Math.Sin(.4);zz=y*Math.Sin(.4)+zz*Math.Cos(.4);
                double p=2.5/(3.5+zz*.5);v[i,0]=c.W/2+xx*radius*.62*p;v[i,1]=c.H/2+yy*radius*.30*p;
            }
            for(int i=0;i<8;i++)for(int axis=0;axis<3;axis++){int k=i^(1<<axis);if(i<k)c.Line(v[i,0],v[i,1],v[k,0],v[k,1],'·',color);}
            if(cage==1)for(int k=-2;k<=2;k++)c.Line(c.W/2+k*radius*.22,c.H/2-radius*.45,c.W/2+k*radius*.22,c.H/2+radius*.45,'│',1);
        }
    }
    public static class Engine {
        static string Stop(Settings s) {return Path.Combine(s.Root,".runtime",s.Run+".stop");}
        static string Map(Settings s) {return "Local\\Worldex."+s.Run;}
        public static void Director(string configPath) {
            Native.SetThreadDpiAwarenessContext(new IntPtr(-4));
            var s=Visual.Load<Settings>(configPath);
            try {
                using(var memory=MemoryMappedFile.CreateNew(Map(s),4096))using(var map=memory.CreateViewAccessor())using(var audio=new Audio(s.Audio,s.Mute)) {
                    map.Write(0,-1.0);map.Write(24,DateTime.UtcNow.Ticks);
                    var wait=Stopwatch.StartNew();bool all=false;
                    while(wait.Elapsed.TotalSeconds<30&&!File.Exists(Stop(s))) {
                        all=true;for(int i=0;i<7;i++)if(map.ReadInt32(64+i*4)!=1)all=false;
                        map.Write(24,DateTime.UtcNow.Ticks);if(all)break;Thread.Sleep(30);
                    }
                    if(!all)throw new TimeoutException("Seven terminal panels did not become ready. See panel logs.");
                    using(var backdrop=new Stage()) {
                        backdrop.FormBorderStyle=FormBorderStyle.None;backdrop.ShowInTaskbar=false;backdrop.StartPosition=FormStartPosition.Manual;backdrop.TopMost=true;
                        backdrop.Bounds=Screen.PrimaryScreen.WorkingArea;backdrop.BackColor=ColorTranslator.FromHtml("#080c12");backdrop.Text="Worldex / Stage";
                        backdrop.KeyPreview=true;backdrop.KeyDown+=(sender,e)=>{if(e.KeyCode==Keys.Escape)map.Write(8,1);};
                        backdrop.Quit=()=>map.Write(8,1);
                        backdrop.Show();Native.SetWindowPos(backdrop.Handle,new IntPtr(-1),0,0,0,0,0x0013);
                        Application.DoEvents();
                        for(int i=0;i<7;i++)Native.SetWindowPos(new IntPtr(map.ReadInt64(192+i*8)),new IntPtr(-1),0,0,0,0,0x0013);
                        // Panels lift themselves above the stage only once it exists.
                        map.Write(32,1);
                        audio.Seek(s.Seek);bool paused=false;var preview=Stopwatch.StartNew();double lastTime=s.Seek;var status=Stopwatch.StartNew();
                        while(!File.Exists(Stop(s))&&map.ReadInt32(8)==0) {
                            if(preview.Elapsed.TotalSeconds>3) {
                                bool lost=false;for(int i=0;i<7;i++)if((DateTime.UtcNow.Ticks-map.ReadInt64(128+i*8))/(double)TimeSpan.TicksPerSecond>3)lost=true;
                                if(lost)break;
                            }
                            int cmd=map.ReadInt32(16);if(cmd!=0)map.Write(16,0);
                            if(cmd==1){paused=!paused;audio.Pause(paused);map.Write(12,paused?1:0);}
                            if(cmd>=2&&cmd<=4){audio.Seek(cmd==4?0:Math.Max(0,Math.Min(s.Duration-.1,lastTime+(cmd==2?-10:10))));if(paused)audio.Pause(true);}
                            lastTime=audio.Time;map.Write(0,lastTime);map.Write(24,DateTime.UtcNow.Ticks);
                            if(status.ElapsedMilliseconds>500) {
                                File.WriteAllText(Path.Combine(s.Root,".runtime",s.Run+"-playback.json"),new JavaScriptSerializer().Serialize(new { time=lastTime, paused=paused, ready=7, clock="waveOut device position" }));status.Restart();
                            }
                            if(lastTime>=s.Duration-.04||(s.Preview>0&&preview.Elapsed.TotalSeconds>=s.Preview))break;
                            Application.DoEvents();Thread.Sleep(10);
                        }
                        map.Write(8,1);backdrop.Close();
                    }
                }
            } catch(Exception ex) {File.WriteAllText(Path.Combine(s.Root,".runtime",s.Run+"-director.log"),ex.ToString());File.WriteAllText(Stop(s),"error");}
        }
        public static void Panel(string configPath,int index) {
            Native.SetThreadDpiAwarenessContext(new IntPtr(-4));
            var s=Visual.Load<Settings>(configPath);string metadata=Path.Combine(s.Root,".runtime",s.Run+"-"+index+".json");
            try {
                var visual=new Visual(s);IntPtr window=Native.GetConsoleWindow(),output=Native.GetStdHandle(-11);
                Native.SetConsoleCP(65001);Native.SetConsoleOutputCP(65001);
                Console.Title="WORLDEX / "+(index==6?"MAIN STAGE":(index%2==0?"PowerShell":"CMD")+" / "+(index+1));
                Console.CursorVisible=false;
                uint mode;IntPtr input=Native.GetStdHandle(-10);if(Native.GetConsoleMode(input,out mode))Native.SetConsoleMode(input,(mode|0x80u)&~0x40u);
                double dpi=Native.GetDpiForWindow(window)/96.0;
                // Console font coordinates are logical; conhost applies monitor DPI.
                var font=new Native.FontInfo();font.Size=(uint)Marshal.SizeOf(typeof(Native.FontInfo));font.Cell=new Native.Coord(0,visual.Theme.Number("cellHeight"));font.Face=visual.Theme.Font;font.Weight=400;font.Family=54;
                if(!Native.SetCurrentConsoleFontEx(output,false,ref font))throw new IOException("Unable to set console font");
                var palette=new Native.BufferInfo();palette.Size=(uint)Marshal.SizeOf(typeof(Native.BufferInfo));palette.Colors=new uint[16];
                if(Native.GetConsoleScreenBufferInfoEx(output,ref palette)) {
                    var colors=visual.Palette;for(int i=0;i<16;i++)palette.Colors[i]=(uint)(colors[i].R|(colors[i].G<<8)|(colors[i].B<<16));
                    palette.Window.R++;palette.Window.B++;Native.SetConsoleScreenBufferInfoEx(output,ref palette);
                }
                Console.BackgroundColor=ConsoleColor.Black;Console.Clear();
                File.WriteAllText(metadata,new JavaScriptSerializer().Serialize(new { PID=Process.GetCurrentProcess().Id, Handle=window.ToInt64(), Index=index }));
                MemoryMappedFile memory=null;var retry=Stopwatch.StartNew();
                while(memory==null&&retry.Elapsed.TotalSeconds<25&&!File.Exists(Stop(s))) {
                    try{memory=MemoryMappedFile.OpenExisting(Map(s));}catch(FileNotFoundException){Thread.Sleep(50);}
                }
                if(memory==null)throw new IOException("Director unavailable");
                using(memory)using(var map=memory.CreateViewAccessor()) {
                    map.Write(192+index*8,window.ToInt64());map.Write(128+index*8,DateTime.UtcNow.Ticks);map.Write(64+index*4,1);Rectangle area=Screen.PrimaryScreen.WorkingArea;string lastRect="";int oldScene=-1;bool shown=false;bool lifted=false;var report=Stopwatch.StartNew();
                    while(map.ReadInt32(8)==0&&!File.Exists(Stop(s))) {
                        var timer=Stopwatch.StartNew();
                        if((DateTime.UtcNow.Ticks-map.ReadInt64(24))/(double)TimeSpan.TicksPerSecond>5)break;
                        double time=map.ReadDouble(0);bool paused=map.ReadInt32(12)!=0;int scene=visual.SceneAt(Math.Max(0,time));Box box=visual.Position(index,Math.Max(0,time));
                        map.Write(128+index*8,DateTime.UtcNow.Ticks);
                        int x=area.X+(int)(box.x*area.Width),y=area.Y+(int)(box.y*area.Height),w=(int)(box.w*area.Width),h=(int)(box.h*area.Height);
                        string rect=x+","+y+","+w+","+h;
                        bool stageReady=map.ReadInt32(32)==1;
                        if(rect!=lastRect||scene!=oldScene||(stageReady&&!lifted)) {
                            // Main window moves to the front on scene cuts; no focus stealing.
                            uint flags=((scene!=oldScene&&index==6)||(stageReady&&!lifted))?0x0010u:0x0014u;
                            Native.SetWindowPos(window,new IntPtr(-1),x,y,w,h,flags);lastRect=rect;oldScene=scene;
                            if(stageReady)lifted=true;
                        }
                        if(!shown) {Native.ShowWindow(window,4);shown=true;}
                        int cols=Math.Max(12,Math.Min(240,Console.WindowWidth)),rows=Math.Max(8,Math.Min(100,Console.WindowHeight));
                        // Keep the buffer equal to the viewport: no stale scroll offsets
                        // or legacy 9001-line scrollbars after animated window resizing.
                        try {
                            if(Console.WindowLeft!=0||Console.WindowTop!=0)Console.SetWindowPosition(0,0);
                            if(Console.BufferWidth!=cols||Console.BufferHeight!=rows)Console.SetBufferSize(cols,rows);
                        }catch(ArgumentOutOfRangeException) { }
                        if(report.ElapsedMilliseconds>1000) {
                            var actualFont=new Native.FontInfo();actualFont.Size=(uint)Marshal.SizeOf(typeof(Native.FontInfo));Native.GetCurrentConsoleFontEx(output,false,ref actualFont);
                            File.WriteAllText(metadata,new JavaScriptSerializer().Serialize(new {PID=Process.GetCurrentProcess().Id,Handle=window.ToInt64(),Index=index,Columns=cols,Rows=rows,Dpi=dpi,Font=actualFont.Face,CellWidth=actualFont.Cell.X,CellHeight=actualFont.Cell.Y,ScreenWidth=area.Width,ScreenHeight=area.Height}));report.Restart();
                        }
                        Canvas canvas=visual.Draw(index,cols,rows,Math.Max(0,time),paused);
                        if(time<0)canvas.Center(rows/2,"CONNECTING TO AUDIO CLOCK",6);
                        var region=new Native.Rect();region.R=(short)(cols-1);region.B=(short)(rows-1);
                        if(!Native.WriteConsoleOutputW(output,canvas.Cells,new Native.Coord(cols,rows),new Native.Coord(0,0),ref region))throw new IOException("Console frame write failed: "+Marshal.GetLastWin32Error());
                        while(Console.KeyAvailable) {
                            var key=Console.ReadKey(true).Key;
                            if(key==ConsoleKey.Escape)map.Write(8,1);
                            if(key==ConsoleKey.Spacebar)map.Write(16,1);
                            if(key==ConsoleKey.LeftArrow)map.Write(16,2);
                            if(key==ConsoleKey.RightArrow)map.Write(16,3);
                            if(key==ConsoleKey.Home)map.Write(16,4);
                        }
                        Thread.Sleep(Math.Max(1,1000/visual.Theme.Number("fps")-(int)timer.ElapsedMilliseconds));
                    }
                }
            }catch(Exception ex) {File.WriteAllText(Path.Combine(s.Root,".runtime",s.Run+"-"+index+".log"),ex.ToString());}
            finally {if(File.Exists(metadata))File.Delete(metadata);}
        }
        public static void Preview(string configPath,string directory) {
            var s=Visual.Load<Settings>(configPath);var v=new Visual(s);Directory.CreateDirectory(directory);
            double[] moments={1,31,34.5,38.5,82,113,150,181,190};
            foreach(double time in moments) {
                using(var bitmap=new Bitmap(1646,981))using(var g=Graphics.FromImage(bitmap))using(var font=new Font(v.Theme.Font,10,FontStyle.Regular,GraphicsUnit.Pixel)) {
                    g.Clear(v.Palette[0]);g.TextRenderingHint=TextRenderingHint.SingleBitPerPixelGridFit;
                    int[] order={0,1,2,3,4,5,6};foreach(int index in order) {
                        Box box=v.Position(index,time);int x=(int)(box.x*bitmap.Width),y=(int)(box.y*bitmap.Height),w=(int)(box.w*bitmap.Width),h=(int)(box.h*bitmap.Height);
                        using(var bg=new SolidBrush(Color.FromArgb(24,30,39)))g.FillRectangle(bg,x,y,w,22);
                        g.DrawString("WORLDEX / "+index,font,Brushes.LightGray,x+8,y+5);
                        int cols=Math.Max(12,(w-12)/7),rows=Math.Max(8,(h-30)/13);Canvas c=v.Draw(index,cols,rows,time,false);
                        for(int row=0;row<rows;row++)for(int col=0;col<cols;col++) {
                            Native.Cell cell=c.Cells[row*cols+col];if(cell.Char==' ')continue;
                            using(var brush=new SolidBrush(v.Palette[cell.Attribute]))g.DrawString(cell.Char.ToString(),font,brush,x+5+col*7,y+25+row*13,StringFormat.GenericTypographic);
                        }
                    }
                    bitmap.Save(Path.Combine(directory,"stage-"+((int)time).ToString("000")+".png"));
                }
            }
        }
    }
}

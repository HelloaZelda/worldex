namespace Worldex {
    using System;
    using System.IO;
    using System.Collections.Generic;

    public class SungWord { public string text;public double start,end; }
    public class SungLine { public string text,method;public double start,end;public SungWord[] words; }
    public class LyricTrack { public SungLine[] lines; }
    public class RhythmBeat { public double time,strength;public int index,slot;public bool predicted; }
    public class RhythmEvent { public double time,strength;public string kind; }
    public class RhythmTrack { public RhythmBeat[] beats;public RhythmEvent[] events; }

    public partial class Visual {
        public LyricTrack Lyrics;public RhythmTrack Rhythm;public ActionCue[] Actions;
        void LoadPerformance() {
            Lyrics=Load<LyricTrack>(Config.Lyrics);Rhythm=Load<RhythmTrack>(Config.Rhythm);
            Actions=Load<Score>(Config.Score).actions;
        }
        static int Previous(double time,int count,Func<int,double> get) {
            int lo=0,hi=count-1;
            while(lo<hi){int mid=(lo+hi+1)/2;if(get(mid)<=time)lo=mid;else hi=mid-1;}return lo;
        }
        public int LineAt(double time) {return Previous(time,Lyrics.lines.Length,i=>Lyrics.lines[i].start);}
        public int BeatAt(double time) {return Previous(time,Rhythm.beats.Length,i=>Rhythm.beats[i].time);}
        double RhythmPhase(double time) {
            int i=BeatAt(time);double start=Rhythm.beats[i].time,end=i+1<Rhythm.beats.Length?Rhythm.beats[i+1].time:start+.4615;
            return i+Math.Max(0,Math.Min(.999,(time-start)/Math.Max(.1,end-start)));
        }
        public double Pulse(double time,int channel) {return Data.At(time,20+channel);}
        ActionCue LastAction(double time) {if(Actions==null||Actions.Length==0)return null;return Actions[Previous(time,Actions.Length,i=>Actions[i].time)];}
        static double Noise(int i) {double n=Math.Sin(i*127.1+311.7)*43758.5453;return n-Math.Floor(n);}
        static void ClearRow(Canvas c,int y) {for(int x=0;x<c.W;x++)c.Put(x,y,' ',1);}
        static void Frame(Canvas c,int color) {
            c.Line(1,1,c.W-2,1,'─',color);c.Line(1,c.H-2,c.W-2,c.H-2,'─',color);
            c.Line(1,1,1,c.H-2,'│',color);c.Line(c.W-2,1,c.W-2,c.H-2,'│',color);
        }
        Canvas DrawLyrics(int width,int height,double time,bool paused) {
            var c=new Canvas(width,height);int n=LineAt(time+Config.CueOffset);var line=Lyrics.lines[n];
            bool keyword=line.text.Length>1&&line.text==line.text.ToUpper();
            bool active=time+Config.CueOffset>=line.start-.08&&time+Config.CueOffset<=line.end+(keyword?1.15:.25);
            c.Text(1,0,"LYRICS / "+TimeSpan.FromSeconds(time).ToString(@"mm\:ss\.ff")+(paused?" / PAUSED":""),3);
            if(height>5)for(int previous=n;previous>=Math.Max(0,n-6);previous--) {
                var emphasis=Lyrics.lines[previous];double local=time+Config.CueOffset;
                if(emphasis.text.Length>1&&emphasis.text==emphasis.text.ToUpper()&&local>=emphasis.start&&local<=emphasis.end+1.5&&local-emphasis.start<3.5) {
                    if(emphasis.text!=line.text||!active)c.Center(1,"[ "+emphasis.text+" ]",emphasis.text.Contains("EXECUTION")?4:5);
                    break;
                }
            }
            if(!active) {
                var interlude=Scenes[SceneAt(time)];
                string message=interlude.kind=="stall"?"[ FINALIZING / 99% / WAITING FOR RETURN ]":interlude.kind=="storm"?"[ RECURSION / STACK OVERFLOW ]":"[ INSTRUMENTAL / WINDOW CHOREOGRAPHY ]";
                c.Center(Math.Max(1,height/2),message,interlude.kind=="stall"?4:1);return c;
            }
            var rows=new List<List<SungWord>>();var current=new List<SungWord>();int length=0;
            foreach(var word in line.words){if(length+word.text.Length+1>width-4&&current.Count>0){rows.Add(current);current=new List<SungWord>();length=0;}current.Add(word);length+=word.text.Length+1;}
            if(current.Count>0)rows.Add(current);
            int y=Math.Max(1,(height-rows.Count)/2);
            foreach(var row in rows) {
                int total=0;foreach(var w in row)total+=w.text.Length+1;int x=(width-total)/2;
                foreach(var w in row) {
                    double local=time+Config.CueOffset;int color=local>=w.end?2:local>=w.start?6:1;
                    c.Text(x,y,w.text,color);
                    if(local>=w.start&&local<w.end&&y+1<height-1){int len=(int)(w.text.Length*Math.Min(1,(local-w.start)/Math.Max(.05,w.end-w.start)));c.Text(x,y+1,new string('━',Math.Max(1,len)),2);}
                    x+=w.text.Length+1;
                }y++;
            }
            int fill=(int)((width-2)*Math.Max(0,Math.Min(1,(time+Config.CueOffset-line.start)/Math.Max(.1,line.end-line.start))));
            c.Text(1,height-1,new string('━',fill),2);return c;
        }
        Canvas DrawPercussion(int width,int height,double time) {
            var c=new Canvas(width,height);int i=BeatAt(time);var beat=Rhythm.beats[i];
            c.Text(1,0,"AUDIO ATTACKS / LIVE  |  BAR "+(i/4+1)+"  BEAT "+(beat.slot+1),3);
            string[] labels={"KICK ","SNARE","HAT  "};
            for(int k=0;k<3;k++){int y=k+1;if(y>=height-1)break;c.Text(1,y,labels[k],1);int fill=(int)(Pulse(time,k)*Math.Min(20,width/4));c.Text(8,y,new string('█',fill),k==1?4:2);}
            int origin=width/2;
            for(int k=0;k<16;k++) {double at=time-1.2+k*.15;double strength=0;foreach(var e in Rhythm.events)if(Math.Abs(e.time-at)<.06)strength=Math.Max(strength,e.strength);if(origin+k*2<width-1)c.Put(origin+k*2,Math.Min(height-2,3),strength>.6?'█':strength>.25?'▄':'·',strength>.6?6:1);}
            c.Text(1,height-1,"SPACE pause | <- -> seek | CTRL+SHIFT+Q stop",1);return c;
        }
        public Canvas Draw(int index,int width,int height,double time,bool paused) {
            if(index==7)return DrawLyrics(width,height,time,paused);
            if(index==8)return DrawPercussion(width,height,time);
            if(FormationAt(time).Tiles)return DrawFormationTile(index,width,height,time);
            var scene=Scenes[SceneAt(time)];double age=Math.Max(0,time+Config.CueOffset-scene.time),kick=Pulse(time,0),snare=Pulse(time,1),beatPulse=Pulse(time,3);
            bool danger=scene.kind=="error"||scene.kind=="execute"||scene.kind=="lock"||scene.kind=="storm"||scene.kind=="final";
            int accent=danger?4:scene.kind=="eggplant"||scene.kind=="fruit"||scene.kind=="cat"?5:2;
            var c=new Canvas(width,height);var field=new Canvas(width,Math.Max(4,height-3));
            string kind=scene.kind;double localTime=time;
            // Satellite roles change with the sung line; they belong to its scene.
            if(index<6) {
                int role=(index+scene.variant)%6;
                if(scene.kind=="isolation"||scene.kind=="disconnect")kind="disconnect";
                else if(role==0)kind="stream";
                else if(role==1)kind="spectrum";
                else if(role==2&&scene.kind!="title")kind="particles";
            }
            DrawField(field,kind,localTime,age,accent,kick,snare,beatPulse,index,scene);
            // Word impacts and CRT interference are deterministic, never wall-clock jitter.
            var action=LastAction(time+Config.CueOffset);
            double hit=action==null?0:Math.Exp(-Math.Max(0,time+Config.CueOffset-action.time)/.12)*action.strength;
            if(index<6&&action!=null&&action.actor==index&&hit>.3)Frame(field,accent);
            if(scene.kind=="disconnect"||scene.kind=="error"||scene.kind=="storm"||scene.kind=="lock") {
                double intensity=scene.kind=="disconnect"?Math.Max(0,Math.Min(.7,(time-110)/8)):.25+snare*.5;
                for(int y=2;y<field.H-1;y++)if(Noise(y*17+(int)(time*13))<intensity*.22) {
                    int shift=(int)((Noise(y*97+(int)(time*10))-.5)*width*.18);var copy=(Native.Cell[])field.Cells.Clone();
                    for(int x=2;x<width-2;x++){int from=x-shift;if(from>=0&&from<width)field.Cells[y*width+x]=copy[y*width+from];}
                }
            }
            for(int y=0;y<field.H;y++)for(int x=0;x<width;x++)c.Cells[y*width+x]=field.Cells[y*width+x];
            ClearRow(c,height-3);ClearRow(c,height-2);ClearRow(c,height-1);
            c.Text(1,height-3,scene.code,accent);
            c.Text(1,height-2,index==6?scene.label:"NODE "+(index+1)+" / "+kind.ToUpper(),3);
            int count=(int)((width-2)*time/Config.Duration);c.Text(1,height-1,new string('━',Math.Max(0,Math.Min(width-2,count))),accent);
            return c;
        }
        void DrawField(Canvas c,string kind,double t,double age,int accent,double kick,double snare,double pulse,int index,Scene scene) {
            double radius=Math.Min(c.W*.39,(c.H-3)*.86),p=Math.Min(1,age/.6);
            // A fine, moving registration grid behind the image.
            for(int y=2;y<c.H-1;y+=4)for(int x=2;x<c.W-2;x+=8)c.Put(x,y,'·',1);
            if(kind=="world"||kind=="data"||kind=="stream"||kind=="storm"||kind=="title")DataRain(c,t,accent,kind=="world"?.65:1.0);
            if(index==6&&c.W>=80) {
                // Diagnostic gutters frame the central image rather than leaving
                // the rest of the terminal as an empty presentation slide.
                for(int y=3;y<c.H-2;y+=2) {
                    int n=((int)(t*13)+y*197)&65535;
                    c.Text(2,y,(y%4==1?"me.":"bus")+n.ToString("X4"),y%6==1?accent:1);
                    c.Text(c.W-12,y,(y%4==1?"you.":"ptr ")+(n^29471).ToString("X4"),y%6==1?3:1);
                }
                for(int x=-4;x<=4;x++)c.Line(c.W/2+x*2,c.H*.65,c.W/2+x*c.W*.16,c.H-2,'·',1);
                for(int y=0;y<4;y++){double yy=c.H*.66+Math.Pow((y+t*.5)%4/4.0,1.4)*c.H*.32;c.Line(10,yy,c.W-11,yy,'·',1);}
            }
            c.Text(1,0,index==6?"WORLD.EXECUTE(ME); / "+kind.ToUpper():"BUS "+index+" / "+kind.ToUpper(),3);
            switch(kind) {
                case "power":case "shield":case "checks":case "assembly":
                    Circuits(c,t,age,kind,accent,kick);break;
                case "cube":case "dimension":case "cage":case "cageheart":
                    Cube(c,t*1.5,radius*(.72+.25*p+.1*kick),kind=="cageheart"?4:accent,kind=="cageheart"?1:0);
                    if(kind=="dimension")Cloud(c,t,radius,accent,1);
                    if(kind=="cage"||kind=="cageheart") {
                        int gap=Math.Max(3,(int)(c.W*(.10-.04*p)));
                        for(int x=3;x<c.W-3;x+=gap)c.Line(x,2,x,c.H-2,'│',1);
                        if(kind=="cageheart")FilledHeart(c,t,radius*.63,4,0);
                        else c.Center(c.H/2,"[ INSIDE THE SYSTEM ]",6);
                    }break;
                case "world":
                    Globe(c,t*1.7,radius*(.85+.1*kick),accent);OrbitPackets(c,t,radius,3);break;
                case "points":case "particles":
                    Cloud(c,t,radius,accent,0);break;
                case "circle":case "circumference":
                    CircleUnroll(c,t,age,radius,accent,kind=="circumference");break;
                case "sine":case "tangent":case "current":case "limit":
                    FunctionPlot(c,t,age,kind,accent,kick);break;
                case "infinity":case "vortex":case "storm":
                    Tunnel(c,t,radius,accent,kind=="infinity"?0:1,kick);break;
                case "iris":case "eye":
                    Eye(c,t,radius,accent,kind=="iris"?1-p:1,kick);break;
                case "time":case "daynight":case "identity":case "roles":
                    Transform(c,t,radius,kind,accent,kick,scene.variant);break;
                case "merge":case "stimulus":case "completion":
                    Resonance(c,t,age,radius,accent,kick,kind);break;
                case "meter":case "stall":
                    Meter(c,t,age,accent,kind=="stall");break;
                case "happy":
                    c.Circle(0,0,radius*.8,.44,accent,'#');c.Shape(-radius*.3,-radius*.13,'●',6);c.Shape(radius*.3,-radius*.13,'●',6);
                    for(double a=0;a<Math.PI;a+=.04)c.Shape(Math.Cos(a)*radius*.42,Math.Sin(a)*radius*.19,'#',accent);
                    OrbitPackets(c,t,radius,accent);break;
                case "eggplant":case "fruit":case "cat":
                    Organic(c,t,radius,accent,kind,kick);break;
                case "ecg":
                    Ecg(c,t,accent,kick);break;
                case "disconnect":case "isolation":
                    Disconnect(c,t,age,accent,index,scene);break;
                case "fragments":case "broken":
                    FilledHeart(c,t,radius*.8,4,kind=="broken"?Math.Min(1,age*.4):Math.Min(1,age*.8));
                    for(int k=0;k<60;k++){double x=(Noise(k*11)-.5)*c.W+Math.Sin(k*17)*age*5,y=(Noise(k*13)-.5)*c.H+age*(1+Noise(k)*3);c.Shape(x,y,k%3==0?'#':'+',k%3==0?4:1);}break;
                case "execute":case "error":case "lock":case "final":case "countdown":
                    Command(c,t,age,radius,accent,kind,kick,scene,index);break;
                case "network":case "attention":case "equation":
                    Learning(c,t,age,radius,accent,kind,kick);break;
                case "release":
                    FilledHeart(c,t,radius*.65,4,0);c.Line(c.W/2,2,c.W/2,c.H-2,'│',1);
                    double distance=Math.Min(c.W*.4,age*c.W*.12);c.Text((int)(c.W/2+distance),(int)(c.H*.35),"[ YOU ]",6);c.Text(c.W/2-7,c.H/2,"[ ME ]",4);break;
                case "title":
                    Title(c,t,age,accent,index);break;
                case "stream":case "data":
                    DataStream(c,t,age,accent,kind=="data");break;
                case "spectrum":
                    for(int k=0;k<16;k++){int x=2+k*(c.W-4)/16,h=(int)(Data.At(t,k+4)*(c.H-4));for(int y=0;y<h;y++)for(int dx=0;dx<Math.Max(1,(c.W-4)/16-1);dx++)c.Put(x+dx,c.H-2-y,y%3==0?'─':'█',y>c.H*.6?6:accent);}break;
                default:Cloud(c,t,radius,accent,0);break;
            }
            double titleHold=Math.Min(2.2,Math.Max(1.1,scene.end-scene.time));
            if(index==6&&!String.IsNullOrEmpty(scene.focus)&&scene.focus.Length<22&&kind!="countdown"&&kind!="execute"&&kind!="final"&&age<titleHold) {
                for(int y=Math.Max(1,c.H/2-3);y<Math.Min(c.H-1,c.H/2+3);y++)ClearRow(c,y);
                c.Big(scene.focus.Replace(" ",""),c.H/2-2,6,1);
            }
        }
        static void Circuits(Canvas c,double t,double age,string kind,int color,double kick) {
            int lines=Math.Max(4,c.H/3);
            for(int j=0;j<lines;j++) {
                int y=2+j*(c.H-3)/lines,x=(int)(c.W*(.12+.75*Noise(j*7)));
                double fill=kind=="checks"?Math.Min(1,age*3-j*.08):Math.Min(1,age*.8+.15);
                c.Line(2,y,Math.Max(2,c.W*fill),y,'─',j%3==0?color:1);
                c.Line(x,y,x,Math.Min(c.H-2,y+2),'│',3);c.Text(Math.Max(2,c.W-15),y,fill>.8?"[ OK ]":"[ ... ]",color);
                double packet=(t*22+j*9)%(c.W-5);c.Text((int)packet,y,"=>",6);
            }
            if(kind=="shield") {
                for(int k=0;k<3;k++){double r=Math.Min(c.W*.3,c.H*.9)*(.65+k*.15+kick*.08);for(int j=0;j<6;j++){double a=j*Math.PI/3,b=(j+1)*Math.PI/3;c.Line(c.W/2+Math.Cos(a)*r,c.H/2+Math.Sin(a)*r*.45,c.W/2+Math.Cos(b)*r,c.H/2+Math.Sin(b)*r*.45,'#',k==2?color:1);}}
                c.Center(c.H/2,"ACCESS / GUARDED",6);
            }
            if(kind=="assembly")Cube(c,t,Math.Min(c.W*.3,c.H*.7)*(1-Math.Exp(-age)),color,0);
        }
        static void Cloud(Canvas c,double t,double radius,int color,int extrude) {
            double angle=t*.6,tilt=Math.Sin(t*.17)*.45;
            for(int k=0;k<260;k++) {
                double z=Noise(k*19)*2-1,a=k*2.399963,rr=Math.Sqrt(1-z*z);
                double x=Math.Cos(a)*rr,y=Math.Sin(a)*rr;
                if(extrude==1)z*=.25+.75*(.5+.5*Math.Sin(t));
                double xx=x*Math.Cos(angle)-z*Math.Sin(angle),zz=x*Math.Sin(angle)+z*Math.Cos(angle),yy=y*Math.Cos(tilt)-zz*Math.Sin(tilt);
                double perspective=2/(2.5+zz*.7);c.Shape(xx*radius*perspective,yy*radius*.43*perspective,zz<-.4?'#':zz<.2?'+':'·',zz<-.2?color:1);
            }
            c.Line(c.W/2,c.H/2,c.W/2+radius,c.H/2,'─',3);c.Line(c.W/2,c.H/2,c.W/2,c.H/2-radius*.4,'│',3);
        }
        static void OrbitPackets(Canvas c,double t,double radius,int color) {
            for(int k=0;k<18;k++){double a=t*1.6+k*Math.PI/9;double r=radius*(.80+.15*Math.Sin(t*.6+k));c.Shape(Math.Cos(a)*r,Math.Sin(a)*r*.46,k%4==0?'■':'·',k%4==0?color:1);}
        }
        static void CircleUnroll(Canvas c,double t,double age,double radius,int color,bool unroll) {
            double amount=unroll?Math.Min(1,age*.8):0;
            for(double a=0;a<Math.PI*2;a+=.025){double x=Math.Cos(a)*radius*.75,y=Math.Sin(a)*radius*.35;
                x=x*(1-amount)+(a/Math.PI-1)*radius*amount;y=y*(1-amount)+Math.Sin(t*2)*amount;
                c.Shape(x,y,a<t%6.28?'#':'·',a<t%6.28?color:1);}
            c.Line(c.W/2,c.H/2,c.W/2+Math.Cos(t)*radius*.75,c.H/2+Math.Sin(t)*radius*.35,'·',3);
            c.Center(c.H-2,unroll?"2*pi*r  ->  STRAIGHTEN THE ORBIT":"x*x + y*y = r*r",3);
        }
        static void FunctionPlot(Canvas c,double t,double age,string kind,int color,double kick) {
            double amp=(c.H-5)*.36;int center=c.H/2;
            c.Line(1,center,c.W-2,center,'─',1);c.Line(c.W/2,2,c.W/2,c.H-2,'│',1);
            for(int x=2;x<c.W-2;x++) {
                double phase=x/(double)c.W*4*Math.PI-t*2.3;double value=Math.Sin(phase);
                if(kind=="current"&&((int)age%2==1))value=Math.Sign(value);
                if(kind=="limit")value=Math.Tanh((x/(double)c.W-.5)*7)*(1-.1*Math.Exp(-age));
                int y=(int)(center+value*amp);c.Put(x,y,'#',color);
                if(x%2==0)c.Put(x,(int)(center+Math.Sin(phase+1)*amp*.65),'·',1);
            }
            if(kind=="tangent"||kind=="sine") {
                double x=c.W*(.5+.2*Math.Sin(t*.7)),phase=x/c.W*4*Math.PI-t*2.3,y=center+Math.Sin(phase)*amp,slope=Math.Cos(phase)*amp*4*Math.PI/c.W;
                c.Line(x-c.W*.17,y-slope*c.W*.17,x+c.W*.17,y+slope*c.W*.17,'+',5);c.Put((int)x,(int)y,'■',6);
            }
            if(kind=="limit"){c.Line(1,center-amp,c.W-2,center-amp,'·',4);c.Line(1,center+amp,c.W-2,center+amp,'·',4);}
        }
        static void Tunnel(Canvas c,double t,double radius,int color,int twist,double kick) {
            for(int ring=0;ring<12;ring++) {
                double z=(ring/12.0+t*.28)%1,scale=Math.Pow(z,1.6),r=radius*scale*(1+kick*.09);
                for(int k=0;k<40;k++){double a=k*Math.PI/20+t*.45+ring*.13*twist;double x=Math.Cos(a),y=Math.Sin(a);
                    if(twist==0){double d=1+y*y;x/=d;y=y*Math.Cos(a)/d*1.9;}
                    c.Shape(x*r,y*r*.44,k%5==0?'#':'·',ring%3==0?color:1);
                }
            }
            for(int k=0;k<60;k++){double z=(Noise(k*11)+t*.2)%1,a=Noise(k*31)*6.28;c.Shape(Math.Cos(a)*radius*z,Math.Sin(a)*radius*z*.45,z>.8?'+':'·',z>.8?3:1);}
        }
        static void Eye(Canvas c,double t,double radius,int color,double open,double kick) {
            for(double a=0;a<6.29;a+=.025)c.Shape(Math.Cos(a)*radius,Math.Sin(a)*Math.Abs(Math.Sin(a))*radius*.30*open,'#',color);
            for(int k=0;k<4;k++)c.Circle(Math.Sin(t*.8)*radius*.15,0,radius*(.12+k*.06),.45*open,k==3?3:1,'·');
            c.Shape(Math.Sin(t*.8)*radius*.15,0,'●',6);OrbitPackets(c,t,radius,3);
        }
        void Transform(Canvas c,double t,double radius,string kind,int color,double kick,int variant) {
            if(kind=="identity"||kind=="roles") {
                double travel=Math.Sin(t*2)*radius*.35;
                for(int n=0;n<2;n++){double x=(n==0?-1:1)*travel;c.Circle(x,-radius*.16,radius*.18,.5,n==0?color:4,'#');
                    c.Line(c.W/2+x,c.H/2,c.W/2+x,c.H/2+radius*.25,'│',3);c.Line(c.W/2+x-radius*.2,c.H/2+radius*.1,c.W/2+x+radius*.2,c.H/2+radius*.1,'─',3);}
                c.Center(c.H-2,kind=="identity"?"F <== REWRITE ==> M":"S <== ROLE SWAP ==> M",6);
            } else {
                c.Circle(0,0,radius*.8,.43,1,'·');for(int k=0;k<12;k++){double a=k*Math.PI/6;c.Shape(Math.Cos(a)*radius*.8,Math.Sin(a)*radius*.344,'#',3);}
                c.Line(c.W/2,c.H/2,c.W/2+Math.Sin(t*1.8)*radius*.65,c.H/2-Math.Cos(t*1.8)*radius*.29,'#',color);
                if(kind=="daynight") {bool day=((int)Beat(t))%2==0;c.Circle(-radius*.4,-radius*.2,radius*.16,.5,day?5:3,day?'#':'·');c.Center(c.H-2,day?"AM / DAYLIGHT":"PM / NIGHTFALL",6);}
                else for(int k=-2;k<=2;k++)c.Text(c.W/2+k*(Math.Max(5,c.W/7)),c.H/2+3,(617-(int)t*17+k*1000).ToString(),k==0?6:1);
            }
        }
        void Resonance(Canvas c,double t,double age,double radius,int color,double kick,string kind) {
            double distance=radius*(kind=="merge"?Math.Max(0,.55-age*.16):.35);
            for(int side=-1;side<=1;side+=2)for(int j=0;j<6;j++){double r=((t*12+j*radius/6)%radius)*(1+kick*.1);c.Circle(side*distance,0,r,.43,j%2==0?color:1,'·');}
            for(int k=0;k<30;k++){double p=(k/30.0+t*.5)%1;c.Shape(-distance+2*distance*p,Math.Sin(p*6.28+t)*radius*.07,'+',3);}
            if(kind=="completion")FilledHeart(c,t,radius*.6,color,0);
            if(kind=="stimulus")for(int k=0;k<10;k++){double a=k*Math.PI/5+t*.5;c.Line(c.W/2,c.H/2,c.W/2+Math.Cos(a)*radius,c.H/2+Math.Sin(a)*radius*.44,'·',kick>.3?6:1);}
        }
        static void Meter(Canvas c,double t,double age,int color,bool stall) {
            double progress=stall?.99:Math.Min(1,age*.8);int width=Math.Max(4,c.W-8),left=4,top=c.H/2-2;
            for(int y=0;y<4;y++){c.Text(left,top+y,"[",3);c.Text(left+width-1,top+y,"]",3);for(int x=1;x<width-1;x++)c.Put(left+x,top+y,x<width*progress?(y%2==0?'#':'='):'·',x<width*progress?color:1);}
            c.Center(top-2,stall?"WAITING FOR A RETURN VALUE":"SATISFACTION / FILL",6);c.Center(top+5,(progress*100).ToString("0")+"%"+(stall?"  ... RETRY "+((int)age*7):""),stall?4:color);
        }
        static void Organic(Canvas c,double t,double radius,int color,string kind,double kick) {
            if(kind=="cat") {
                int sx=Math.Max(1,Math.Min(5,c.W/24)),sy=Math.Max(1,Math.Min(4,c.H/9));
                double walk=Math.Sin(t*.9)*c.W*.07;int x=c.W/2-6*sx+(int)walk,y=c.H/2-3*sy-(int)(kick*2);
                string[] cat={" /\\_____/\\ ","(  o   o  )","(    ^    )"," \\  ---  / "," /|     |\\ ","(_|_____|_)"};
                if((int)(t*3)%13==0)cat[1]="(  -   -  )";
                for(int row=0;row<cat.Length;row++)for(int col=0;col<cat[row].Length;col++) {
                    char ch=cat[row][col];int xx=x+col*sx,yy=y+row*sy;
                    if(ch=='_'||ch=='-')c.Line(xx,yy+sy/2,xx+sx-1,yy+sy/2,'─',color);
                    else if(ch=='/'||ch=='\\')c.Line(xx,yy+(ch=='/'?sy-1:0),xx+sx-1,yy+(ch=='/'?0:sy-1),'#',color);
                    else if(ch=='|')c.Line(xx+sx/2,yy,xx+sx/2,yy+sy-1,'│',color);
                    else if(ch=='('||ch==')') {for(int k=0;k<sy;k++)c.Put(xx+(int)(Math.Sin(k/(double)Math.Max(1,sy-1)*Math.PI)*sx*(ch=='('?-.5:.5)),yy+k,'#',color);}
                    else if(ch=='o') {for(int a=0;a<Math.Max(1,sy);a++)for(int b=0;b<Math.Max(1,sx-1);b++)c.Put(xx+b,yy+a,(a==0||b==0)?'#':' ',6);}
                    else if(ch!=' ')c.Put(xx+sx/2,yy+sy/2,ch,color);
                }
                for(int k=0;k<32;k++)c.Put(x+11*sx+(int)(Math.Sin(k*.13+t*2)*sx),y+5*sy-k/2,'~',color);
                c.Text(Math.Max(1,x-5),Math.Max(1,y-2),"purr(f) / "+((int)(40+kick*160))+" Hz",3);
                for(int k=0;k<4;k++){double r=(t*7+k*radius/4)%radius;c.Circle(walk,0,r,.42,k%2==0?1:color,'·');}
                return;
            }
            for(int y=2;y<c.H-2;y++)for(int x=2;x<c.W-2;x++) {
                double nx=(x-c.W/2.0)/radius,ny=(y-c.H/2.0)/(radius*.43);
                double shape=kind=="fruit"?nx*nx+ny*ny:Math.Pow((nx+.2*ny)/( .65+.12*ny),2)+Math.Pow(ny*.8,2);
                if(shape<.65)c.Put(x,y,shape<.12?'#':(x+y)%3==0?'+':':',shape<.2?6:color);
            }
            c.Text(c.W/2-3,(int)(c.H/2-radius*.37),"\\ | /",2);
            for(int k=0;k<25;k++){double a=Noise(k*13)*6.28,r=radius*((Noise(k*17)+t*.4)%1);c.Shape(Math.Cos(a)*r,Math.Sin(a)*r*.43,'·',1);}
        }
        void Ecg(Canvas c,double t,int color,double kick) {
            for(int x=1;x<c.W-1;x++) {
                double at=t-(c.W-x)*.015;double response=Pulse(at,0),sn=Pulse(at-.025,1),v=response-.65*Pulse(at-.045,0)+.25*sn;
                int y=(int)(c.H/2-v*(c.H-4)*.40);c.Put(x,y,x>c.W-5?'#':'·',x>c.W-5?6:color);
                if(x%2==0)c.Put(x,c.H/2,'─',1);
            }
            FilledHeart(c,t,Math.Min(c.W*.12,c.H*.30)*(1+kick*.2),4,0);
        }
        static void Disconnect(Canvas c,double t,double age,int color,int index,Scene scene) {
            double stage=Math.Max(0,t-110.7);int remaining=Math.Max(0,6-(int)(stage/1.0));
            if(index<6&&index>=remaining){c.Center(c.H/2,"[ SIGNAL LOST ]",4);for(int y=2;y<c.H-2;y++)if(Noise(y*19+(int)(t*12))>.8)c.Text(2,y,"///  ...  ///",1);return;}
            c.Center(c.H/2,"[ ME ]",6);
            for(int k=0;k<6;k++){double a=k*Math.PI/3,r=Math.Min(c.W*.40,c.H*.8),x=c.W/2+Math.Cos(a)*r,y=c.H/2+Math.Sin(a)*r*.43;
                if(k<remaining)c.Line(c.W/2,c.H/2,x,y,'·',color);
                else {for(int j=1;j<8;j++){double f=j/8.0;c.Put((int)(c.W/2+(x-c.W/2)*f+Math.Sin(t*9+j)*stage),(int)(c.H/2+(y-c.H/2)*f+stage*.25),'x',4);}}
                c.Text((int)x-2,(int)y,k<remaining?"[YOU]":"[ X ]",k<remaining?3:1);
            }
        }
        void FilledHeart(Canvas c,double t,double radius,int color,double broken) {
            radius*=1+.13*Pulse(t,0);
            for(int y=2;y<c.H-2;y++)for(int x=1;x<c.W-1;x++) {
                double nx=(x-c.W/2.0)/(radius*.75),ny=-(y-c.H/2.0)/(radius*.34);
                double q=nx*nx+ny*ny-1,shape=q*q*q-nx*nx*ny*ny*ny;
                if(shape<=0) {
                    double shift=broken*Math.Sign(nx)*radius*.3;double fall=broken*broken*Noise(x*7+y)*c.H*.7;
                    c.Put((int)(x+shift),(int)(y+fall),Noise(x*7+y)>.5?'#':'+',shape<-.1?color:6);
                }
            }
        }
        static void Command(Canvas c,double t,double age,double radius,int color,string kind,double kick,Scene scene,int index) {
            for(int y=2;y<c.H-1;y++) {int n=(int)(t*23+y*7919)&65535;string word=(y%3==0?"CALL ":"PUSH ")+n.ToString("X4");c.Text(1,y,word,1);c.Text(c.W-word.Length-1,y,word,1);}
            string label=kind=="countdown"?scene.focus:kind=="error"?"ERROR":kind=="lock"?"ONLY":kind=="final"?"EXECUTION":"EXECUTION";
            if(kind=="countdown"&&index<6)label=new[]{"EIN","DOS","TROIS","NE","FEM","LIU"}[index];
            if(String.IsNullOrEmpty(label))label="EXEC";
            if(label=="TRIOS")label="TROIS";
            int scale=Math.Max(1,Math.Min(2,(c.W-4)/Math.Max(6,label.Length*6)));
            int start=c.H/2-scale*5/2;
            for(int y=Math.Max(1,start-1);y<Math.Min(c.H-1,start+scale*5+1);y++)ClearRow(c,y);
            int ink=kind=="countdown"&&index<6?(label==scene.focus?6:1):kick>.45?6:4;
            c.Big(label,start,ink,scale);
            if(kind=="lock") {Frame(c,4);c.Center(c.H-3,"NO OTHER PROCESS MAY OWN YOU",4);}
            if(kind=="final")c.Center(c.H-3,age>2?"PROCESS ENDED / THE LOOP REMAINS":"99% -> EXECUTION",4);
            if(kind=="error")for(int k=0;k<4;k++)c.Text(2,Math.Min(c.H-2,2+k),"E"+(403+k)+" / ARGUMENT REJECTED",4);
        }
        void Learning(Canvas c,double t,double age,double radius,int color,string kind,double kick) {
            int layers=6,nodes=5;double spacing=(c.W-8)/(double)(layers-1);
            for(int layer=0;layer<layers-1;layer++)for(int a=0;a<nodes;a++)for(int b=0;b<nodes;b++) {
                double x1=4+layer*spacing,y1=3+a*(c.H-6)/(double)(nodes-1),x2=x1+spacing,y2=3+b*(c.H-6)/(double)(nodes-1);
                if(Noise(layer*101+a*17+b)>.45)c.Line(x1,y1,x2,y2,'·',1);
                double p=(t*1.5+Noise(layer*101+a*17+b))%1;if(p>.15&&p<.85)c.Put((int)(x1+(x2-x1)*p),(int)(y1+(y2-y1)*p),'+',kick>.4?4:color);
            }
            for(int l=0;l<layers;l++)for(int n=0;n<nodes;n++)c.Text((int)(4+l*spacing)-1,(int)(3+n*(c.H-6)/(double)(nodes-1)),"[o]",(l+n+(int)(t*3))%4==0?4:3);
            if(kind=="attention")for(int k=0;k<16;k++)c.Put(c.W/2-4+k%4*2,c.H/2-2+k/4,Noise(k+(int)(t*6))>.5?'█':'░',2);
            if(kind=="equation") {for(int y=c.H/2-7;y<c.H/2+7;y++)ClearRow(c,y);FilledHeart(c,t,radius*.62,4,0);c.Center(c.H-2,"(x*x+y*y-1)^3 - x*x*y*y*y = 0",6);}
            c.Text(2,1,"INPUT -> EMBED -> ATTEND -> PREDICT -> ???",4);
        }
        static void Title(Canvas c,double t,double age,int color,int index) {
            int reveal=(int)(Math.Max(0,Math.Min(1,(age-1)/4))*c.W*c.H);
            for(int y=1;y<c.H-1;y++)for(int x=1;x<c.W-1;x++)if(Noise(x*37+y*53)>.40) {
                int k=y*c.W+x;c.Put(x,y,k<reveal?' ':"01{}[]<>/|+-"[(x*17+y*31+(int)(t*14))%12],k%13==0?color:1);
            }
            string word=index==6?"WORLD":new[]{"CREATE","SYSTEM","POINTS","MEMORY","SIGNAL","RETURN"}[index%6];
            if(age>1){int scale=Math.Max(1,Math.Min(2,(c.W-4)/(word.Length*6)));c.Big(word,c.H/2-scale*2,6,scale);}
            if(index==6)c.Center(c.H/2+6,". E X E C U T E ( M E ) ;",color);
        }
        static void DataStream(Canvas c,double t,double age,int color,bool fill) {
            for(int y=1;y<c.H-1;y++) {
                int sequence=(int)(t*24)+y,offset=sequence*7919;
                string[] commands={"WRITE","ALLOC","LINK ","COPY ","EXEC ","SYNC "};
                string row=sequence.ToString("X6")+" "+commands[sequence%commands.Length]+" ";
                for(int x=0;x<c.W/5+1;x++)row+=((offset+x*131)&65535).ToString("X4")+" ";
                int visible=fill?(int)(Math.Min(1,age*.6)*(c.W-2)):c.W-2;
                c.Text(1,y,row.Substring(0,Math.Min(row.Length,Math.Max(0,visible))),sequence%6==0?6:sequence%3==0?3:color);
            }
        }
        static void DataRain(Canvas c,double time,int color,double density) {
            const string symbols="01ABCDEF{}[]<>/|";int scroll=(int)(time*22);
            for(int x=1;x<c.W-1;x++) {
                if(Noise(x*17)>density)continue;
                int head=(scroll+x*11)%Math.Max(1,c.H);
                for(int y=1;y<c.H-1;y++) {
                    int tail=(head-y+c.H)%Math.Max(1,c.H);if(tail>c.H*.78)continue;
                    char glyph=symbols[(x*13+(y+scroll)*7)%symbols.Length];
                    c.Put(x,y,glyph,tail==0?6:tail<3?color:1);
                }
            }
        }
    }
}

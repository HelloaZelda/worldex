namespace Worldex {
    using System;
    using System.Collections.Generic;

    public class Formation {
        public string Name;public bool Tiles;public double Anchor;
        public Box[] Boxes=new Box[16];public bool[] Visible=new bool[16];
    }
    public partial class Visual {
        static int Actor(int index) {return index<7?index:index-2;}
        static readonly string[] HeartMask={"01010","11111","11111","01110","00100"};
        static readonly System.Collections.Generic.Dictionary<string,string[]> Symbols=new System.Collections.Generic.Dictionary<string,string[]> {
            {"1",new[]{"00100","01100","00100","00100","01110"}},
            {"2",new[]{"01110","10001","00110","01000","11111"}},
            {"3",new[]{"11110","00001","01110","00001","11110"}},
            {"4",new[]{"10010","10010","11111","00010","00010"}},
            {"5",new[]{"11111","10000","11110","00001","11110"}},
            {"6",new[]{"01110","10000","11110","10001","01110"}},
            {"F",new[]{"11111","10000","11110","10000","10000"}},
            {"M",new[]{"10001","11011","10101","10001","10001"}},
            {"X",new[]{"10001","01010","00100","01010","10001"}},
            {"target",new[]{"00100","01110","11111","01110","00100"}},
            {"shield",new[]{"01110","10001","10001","01010","00100"}}
        };
        string LatestToken(double time) {
            var line=Lyrics.lines[LineAt(time)];string token="";
            foreach(var word in line.words)if(word.start<=time)token=word.text.Trim(' ',',','.',':',';','!','?').ToUpper();
            return token;
        }
        Formation Mask(string name,string[] rows,double anchor) {
            var f=new Formation();f.Name=name;f.Tiles=true;f.Anchor=anchor;int n=0;
            for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++)if(rows[y][x]=='1'&&n<16) {
                f.Boxes[n]=new Box(.285+x*.086,.14+y*.12,.082,.116);f.Visible[n]=true;n++;
            }
            for(int i=n;i<16;i++)f.Boxes[i]=new Box(.46,.40,.082,.116);
            return f;
        }
        Formation FormationAt(double time) {
            double cueTime=time+Config.CueOffset;var scene=Scenes[SceneAt(time)];string kind=scene.kind;
            bool heart=kind=="happy"||kind=="merge"||kind=="completion"||kind=="lock"||kind=="disconnect"||kind=="isolation"||kind=="fragments"||kind=="broken"||kind=="cageheart"||kind=="release"||kind=="equation";
            if(heart) {
                var f=Mask("HEART",HeartMask,scene.time);
                if(kind=="disconnect"||kind=="isolation") {
                    int steps=0;foreach(var line in Lyrics.lines)if(line.start>=110&&line.start<118&&line.start<=cueTime&&line.text.ToLower().Contains("left"))steps++;
                    int missing=kind=="isolation"?15:Math.Min(15,steps*2);
                    for(int i=0;i<missing;i++)f.Visible[i]=false;
                }
                if(kind=="fragments") {
                    int missing=Math.Min(15,(int)(Math.Max(0,cueTime-118)*2));for(int i=0;i<missing;i++)f.Visible[i]=false;
                }
                if(kind=="completion")for(int i=0;i<16;i++)if(cueTime-scene.time<i*.025)f.Visible[i]=false;
                if(kind=="broken") {
                    double amount=Math.Min(1,Math.Max(0,(cueTime-scene.time)/.7));
                    for(int i=0;i<16;i++){var b=f.Boxes[i];b.x+=(b.x<.47?-.06:.06)*amount;f.Boxes[i]=b;}
                }
                if(kind=="release") {
                    double amount=Math.Min(1,Math.Max(0,cueTime-scene.time));var b=f.Boxes[1];b.x+=amount*.20;b.y-=amount*.07;f.Boxes[1]=b;
                }
                if(kind=="happy"||kind=="completion"||kind=="lock"||kind=="equation") {
                    double scale=1+Pulse(time,0)*.035;
                    for(int i=0;i<16;i++){var b=f.Boxes[i];b.x=.5+(b.x+b.w/2-.5)*scale-b.w/2;b.y=.44+(b.y+b.h/2-.44)*scale-b.h/2;f.Boxes[i]=b;}
                }
                return f;
            }
            if(kind=="countdown") {
                string[] words={"EIN","DOS","TROIS","NE","FEM","LIU"};int number=Array.IndexOf(words,scene.focus);
                return Mask("COUNT "+(number+1),Symbols[(Math.Max(0,number)+1).ToString()],scene.time);
            }
            if(kind=="identity") {
                string token=LatestToken(cueTime);string letter=token=="M"?"M":"F";double anchor=scene.time;
                var line=Lyrics.lines[LineAt(cueTime)];foreach(var word in line.words)if(word.start<=cueTime&&word.text.Trim(',', '.').ToUpper()==letter)anchor=word.start;
                return Mask(letter,Symbols[letter],anchor);
            }
            if(kind=="shield")return Mask("SHIELD",Symbols["shield"],scene.time);
            if(kind=="execute")return Mask("EXECUTE",Symbols[scene.variant%2==0?"target":"X"],scene.time);
            if(kind=="final"||kind=="error")return Mask("TERMINATE",Symbols["X"],scene.time);
            bool curve=kind=="circle"||kind=="circumference"||kind=="infinity"||kind=="sine"||kind=="tangent"||kind=="current"||kind=="ecg"||kind=="power";
            if(curve) {
                var f=new Formation();f.Name=kind.ToUpper();f.Tiles=true;f.Anchor=scene.time;
                string token=LatestToken(cueTime);
                if(kind=="current"&&token=="DC") {f.Name="DC";var line=Lyrics.lines[LineAt(cueTime)];foreach(var word in line.words)if(word.text.Trim(',','.').ToUpper()=="DC")f.Anchor=word.start;}
                for(int i=0;i<16;i++) {
                    double x=0,y=0,w=.078,h=.10,a=i*Math.PI/8;
                    double phase=RhythmPhase(time);
                    if(kind=="circle"||kind=="infinity")a+=phase*.14;
                    if(kind=="circle") {x=.5+Math.Cos(a)*.155-w/2;y=.44+Math.Sin(a)*.260-h/2;}
                    else if(kind=="circumference") {x=.035+i*.058;y=.43;w=.064;h=.10;}
                    else if(kind=="infinity") {double d=1+Math.Sin(a+.08)*Math.Sin(a+.08);x=.5+Math.Cos(a+.08)/d*.36-w/2;y=.44+Math.Sin(a+.08)*Math.Cos(a+.08)/d*.43-h/2;}
                    else if(kind=="power") {if(i<12){a=(i/11.0*.84+.08)*Math.PI*2;x=.5+Math.Sin(a)*.155-w/2;y=.43-Math.Cos(a)*.25-h/2;}else{x=.5-w/2;y=.18+(i-12)*.07;}}
                    else {
                        x=.035+i*.058;w=.064;h=.10;
                        double wave=Math.Sin(i/15.0*Math.PI*4-phase*Math.PI/4)*.19;
                        if(f.Name=="DC")wave=0;
                        if(kind=="ecg"){
                            double position=(i/16.0-phase*.25)%1;if(position<0)position+=1;
                            wave=-.23*Math.Exp(-Math.Pow((position-.36)/.035,2))+.14*Math.Exp(-Math.Pow((position-.43)/.055,2))+.045*Math.Exp(-Math.Pow((position-.23)/.06,2));
                        }
                        y=.42+wave-h/2;
                    }
                    f.Boxes[i]=new Box(x,y,w,h);f.Visible[i]=true;
                }
                return f;
            }
            if(kind=="cage") {
                var f=new Formation();f.Name="CAGE";f.Tiles=true;f.Anchor=scene.time;
                for(int i=0;i<16;i++) {
                    if(i<4)f.Boxes[i]=new Box(.23+i*.14,.16,.12,.10);
                    else if(i<8)f.Boxes[i]=new Box(.23+(i-4)*.14,.66,.12,.10);
                    else if(i<11)f.Boxes[i]=new Box(.23,.28+(i-8)*.12,.10,.10);
                    else if(i<14)f.Boxes[i]=new Box(.67,.28+(i-11)*.12,.10,.10);
                    else f.Boxes[i]=new Box(i==14?.38:.52,.42,.10,.12);
                    f.Visible[i]=true;
                }return f;
            }
            if(kind=="network"||kind=="attention") {
                var f=new Formation();f.Name="NEURAL GRID";f.Tiles=true;f.Anchor=scene.time;
                for(int i=0;i<16;i++){f.Boxes[i]=new Box(.20+i%4*.15,.15+i/4*.15,.13,.13);f.Visible[i]=true;}return f;
            }
            if(kind=="stall"||kind=="meter") {
                var f=new Formation();f.Name=kind=="stall"?"LOAD 99%":"SATISFACTION";f.Tiles=true;f.Anchor=scene.time;
                for(int i=0;i<16;i++){f.Boxes[i]=new Box(.065+i%8*.11,.30+i/8*.18,.10,.15);f.Visible[i]=true;}return f;
            }
            // Other verses retain a readable central picture and six supporting
            // terminals. Extra actors are hidden until a symbol needs them.
            var stage=new Formation();stage.Name="STAGE";stage.Anchor=scene.time;
            for(int i=0;i<16;i++) {int index=i<7?i:i+2;stage.Boxes[i]=i<7?Layout("grid",index,0):new Box(.45,.40,.10,.10);stage.Visible[i]=i<7;}
            return stage;
        }
        public Box FormationPosition(int index,double time) {
            if(index==7||index==8)return Layout("grid",index,0);
            int actor=Actor(index);var current=FormationAt(time);var before=FormationAt(Math.Max(0,time-.30));
            double p=Math.Max(0,Math.Min(1,(time+Config.CueOffset-current.Anchor)/.28));p=p*p*(3-2*p);
            return Box.Mix(before.Boxes[actor],current.Boxes[actor],p);
        }
        public bool FormationVisible(int index,double time) {return index==7||index==8||FormationAt(time).Visible[Actor(index)];}
        public string FormationTitle(int index,double time) {
            if(index==7)return "WORLDEX / LYRICS";if(index==8)return "WORLDEX / RHYTHM";
            var f=FormationAt(time);return f.Tiles?(f.Name=="HEART"?"ME":"SYS")+" ["+(Actor(index)+1).ToString("00")+"]":"WORLDEX / "+(index==6?"MAIN":"NODE "+(index+1));
        }
        Canvas DrawFormationTile(int index,int width,int height,double time) {
            var f=FormationAt(time);var scene=Scenes[SceneAt(time)];int actor=Actor(index);
            var c=new Canvas(width,height);bool heart=f.Name=="HEART",danger=heart||f.Name=="TERMINATE"||f.Name=="EXECUTE";
            int accent=danger?4:2,bg=danger?8:9;
            double kick=Pulse(time,0),snare=Pulse(time,1);
            for(int i=0;i<c.Cells.Length;i++){c.Cells[i].Attribute=(ushort)(1|(bg<<4));c.Cells[i].Char=' ';}
            // Rows travel upward, columns fall downward: a real flow field rather
            // than stationary random dots. The complete desktop symbol stays legible.
            int scroll=(int)(time*(20+actor%4*3));string alphabet="01ABCDEF{}[]<>/";
            for(int x=1;x<width-1;x++) {
                int head=(scroll+x*7+actor*11)%Math.Max(1,height);
                for(int y=1;y<height-1;y++) {
                    int tail=(head-y+height)%Math.Max(1,height);
                    char glyph=alphabet[(x*17+(y+scroll)*31+actor*7)%alphabet.Length];
                    c.Put(x,y,glyph,(tail==0?6:tail<3?accent:1)|(bg<<4));
                }
            }
            if(f.Name=="NEURAL GRID") {
                double gain=1+Math.Max(0,time-177)*.8;int layer=actor%4;
                for(int y=1;y<height-1;y++){
                    double weight=Math.Sin(actor*17+y*7+time*3)*gain;
                    string data=(time>183&&Math.Abs(weight)>3)?"NaN / GRAD OVERFLOW":String.Format("w{0}.{1} {2:+0.000;-0.000}",layer,y,weight);
                    c.Text(1,y,data,(Math.Abs(weight)>2?4:3)|(bg<<4));
                }
            }
            string word=heart?(actor%3==0?"YOU":"ME"):f.Name=="CAGE"?(actor==14?"ME":actor==15?"YOU":"BOUND"):f.Name=="TERMINATE"?"EXIT":f.Name=="EXECUTE"?"EXEC":f.Name=="NEURAL GRID"?new[]{"INPUT","EMBED","ATTEND","OUTPUT"}[actor%4]:f.Name=="DC"?"DC":f.Name=="CURRENT"?"AC":f.Name=="CIRCLE"?"r":f.Name=="INFINITY"?"INF":f.Name=="LOAD 99%"?(actor==15?"99% / RETRY":"WRITE"):f.Name=="SATISFACTION"?"FILL":(actor+1).ToString("00");
            ClearRow(c,Math.Max(1,height/2));
            for(int x=0;x<width;x++)c.Put(x,Math.Max(1,height/2),' ',accent|(bg<<4));
            c.Center(Math.Max(1,height/2),word,(kick>.25?6:accent)|(bg<<4));
            int yscan=Math.Min(height-1,(int)((time*.8+actor*.13)%1*height));
            for(int x=0;x<width;x++)if((x+actor)%3==0)c.Put(x,yscan,'─',(snare>.2?6:3)|(bg<<4));
            c.Text(1,height-1,"0x"+((actor*4096+(int)(time*19))&65535).ToString("X4"),3|(bg<<4));
            if(f.Name=="LOAD 99%"||f.Name=="SATISFACTION") {
                double progress=f.Name=="LOAD 99%"?Math.Min(.99,.76+Math.Max(0,time-195.9)*.06):Math.Min(1,Math.Max(0,(time-scene.time)/1.25));
                double fill=Math.Max(0,Math.Min(1,progress*16-actor));
                for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                    int background=x<width*fill?9:0;
                    c.Cells[y*width+x].Attribute=(ushort)((c.Cells[y*width+x].Attribute&15)|(background<<4));
                }
                for(int x=0;x<width;x++){int at=(height-1)*width+x;c.Cells[at].Char=' ';c.Cells[at].Attribute=(ushort)((c.Cells[at].Attribute&0xf0)|6);}
                string status=actor==15&&f.Name=="LOAD 99%"?"99% / RETRY "+((int)(time-195.9)*4):fill>=1?"OK / "+(actor+1).ToString("00"):(progress*100).ToString("0")+"%";
                c.Text(1,height-1,status,actor==15?4:6);
            }
            return c;
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Runtime.InteropServices;
[assembly:System.Reflection.AssemblyTitle("Peripheral-Battery·外设电量")]
[assembly:System.Reflection.AssemblyProduct("外设电量")]
[assembly:System.Reflection.AssemblyCopyright("风车车")]
[assembly:System.Reflection.AssemblyVersion("2.3.0.0")]
[assembly:System.Reflection.AssemblyFileVersion("2.3.0.0")]
[assembly:System.Reflection.AssemblyInformationalVersion("2.3.0")]
namespace PeriBatt {
static class Autostart {
 const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";const string Name="PeriBatt";
 public static bool Enabled{get{using(var k=Registry.CurrentUser.OpenSubKey(Key))return k!=null&&string.Equals(k.GetValue(Name) as string,"\""+Application.ExecutablePath+"\"",StringComparison.OrdinalIgnoreCase)&&!Disabled();}}
 static bool Disabled(){using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run")){var b=k==null?null:k.GetValue(Name) as byte[];return b!=null&&b.Length>0&&(b[0]==3||b[0]==7);}}
 public static void Set(bool enabled){using(var k=Registry.CurrentUser.CreateSubKey(Key)){var old=k.GetValue(Name) as string;if(old!=null&&!old.EndsWith("PeriBatt.exe\"",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("同名启动项不属于本程序，未修改。");if(enabled)k.SetValue(Name,"\""+Application.ExecutablePath+"\"");else k.DeleteValue(Name,false);}if(enabled&&Disabled())throw new InvalidOperationException("启动项被 Windows 禁用，请在任务管理器的启动应用中启用 PeriBatt。");}
}
static class PaintTools {
 public static readonly Color Ink=Color.FromArgb(27,29,28),Muted=Color.FromArgb(126,128,127),Track=Color.FromArgb(236,238,241);
 public static Font Font(float size,bool bold=false){return new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);}
 public static void Text(Graphics g,string text,float size,Color color,RectangleF box,bool bold=false,bool right=false){using(var f=Font(size,bold))using(var b=new SolidBrush(color))using(var format=new StringFormat{Alignment=right?StringAlignment.Far:StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.DrawString(text,f,b,box,format);}}
 public static void Round(Graphics g,Color color,float x,float y,float width,float height,float radius){if(width<=0||height<=0)return;float r=Math.Max(.1f,Math.Min(radius,Math.Min(width,height)/2));using(var path=new GraphicsPath()){path.AddArc(x,y,2*r,2*r,180,90);path.AddArc(x+width-2*r,y,2*r,2*r,270,90);path.AddArc(x+width-2*r,y+height-2*r,2*r,2*r,0,90);path.AddArc(x,y+height-2*r,2*r,2*r,90,90);path.CloseFigure();using(var b=new SolidBrush(color))g.FillPath(b,path);}}
 public static void Bolt(Graphics g,float x,float y){using(var p=new Pen(Color.FromArgb(0,185,67),2.2f)){g.DrawLines(p,new[]{new PointF(x+4,y+2),new PointF(x,y+2),new PointF(x,y+13),new PointF(x+4,y+13)});g.DrawLines(p,new[]{new PointF(x+17,y+2),new PointF(x+21,y+2),new PointF(x+21,y+13),new PointF(x+17,y+13)});}using(var b=new SolidBrush(Color.FromArgb(0,185,67)))g.FillPolygon(b,new[]{new PointF(x+10,y),new PointF(x+6,y+9),new PointF(x+11,y+9),new PointF(x+9,y+17),new PointF(x+16,y+6),new PointF(x+11,y+6)});}
}
sealed class GlassPanel:Panel {
 public GlassPanel(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);BackColor=Color.Transparent;}
}
sealed class HeaderButton:Button {
 public string Action;public float ScaleFactor=1;public bool Checked;public bool Busy;bool hover;
 readonly System.Windows.Forms.Timer spinTimer=new System.Windows.Forms.Timer{Interval=20};
 readonly System.Diagnostics.Stopwatch spinClock=new System.Diagnostics.Stopwatch();float spinAngle;
 protected override void OnClick(EventArgs e){
 if(Action=="refresh"&&!Busy){spinClock.Restart();spinAngle=0;spinTimer.Start();Invalidate();}
 base.OnClick(e);
 }
 protected override void Dispose(bool disposing){if(disposing){spinTimer.Dispose();spinClock.Stop();}base.Dispose(disposing);}

 public int StartupPixelWidth(){
 using(var g=CreateGraphics())using(var f=PaintTools.Font(13*ScaleFactor))
 using(var format=new StringFormat{FormatFlags=StringFormatFlags.NoWrap})
 return (int)Math.Ceiling(g.MeasureString("开机自启",f,int.MaxValue,format).Width+45*ScaleFactor)+2;
 }
 public HeaderButton(string action){
 spinTimer.Tick+=(s,e)=>{double t=Math.Min(1,spinClock.Elapsed.TotalMilliseconds/450);spinAngle=(float)(360*t*t*(3-2*t));if(t>=1){spinTimer.Stop();spinClock.Stop();spinAngle=0;}Invalidate();};
 Action=action;AccessibleName=action=="startup"?"开机自启":action=="refresh"?"刷新":"关闭";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;MouseEnter+=(s,e)=>{hover=true;Invalidate();};MouseLeave+=(s,e)=>{hover=false;Invalidate();};}
 protected override void OnPaint(PaintEventArgs e){base.OnPaintBackground(e);var g=e.Graphics;g.ScaleTransform(ScaleFactor,ScaleFactor);g.SmoothingMode=SmoothingMode.AntiAlias;float w=Width/ScaleFactor,h=Height/ScaleFactor;if(hover||Busy)PaintTools.Round(g,Color.FromArgb(35,180,185,180),0,0,w,h,7);var color=Enabled?PaintTools.Muted:Color.LightGray;using(var p=new Pen(color,1.8f)){if(Action=="close"){g.DrawLine(p,w/2-6,h/2-6,w/2+6,h/2+6);g.DrawLine(p,w/2+6,h/2-6,w/2-6,h/2+6);}else if(Action=="refresh"){var saved=g.Save();g.TranslateTransform(w/2,h/2);g.RotateTransform(spinAngle);g.TranslateTransform(-w/2,-h/2);g.DrawArc(p,w/2-7,h/2-7,14,14,35,285);g.DrawLines(p,new[]{new PointF(w/2+7,h/2-8),new PointF(w/2+7,h/2-2),new PointF(w/2+1,h/2-2)});g.Restore(saved);}else{PaintTools.Text(g,"开机自启",13,color,new RectangleF(0,0,w-45,h));PaintTools.Round(g,Checked?Color.FromArgb(45,151,109):Color.FromArgb(230,231,230),w-40,h/2-9,36,18,9);using(var b=new SolidBrush(Color.White))g.FillEllipse(b,w-(Checked?20:37),h/2-7,14,14);}}if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,2,(int)w-4,(int)h-4));}
}
public class MainWindow:Form {
 readonly BatteryManager manager=new BatteryManager();readonly CancellationTokenSource lifetime=new CancellationTokenSource();readonly Panel header=new GlassPanel(),list=new GlassPanel();readonly HeaderButton startup=new HeaderButton("startup"),refresh=new HeaderButton("refresh"),close=new HeaderButton("close");readonly Label empty=new Label();readonly Dictionary<string,DeviceRow> rows=new Dictionary<string,DeviceRow>();float scale;bool busy;bool ended;bool disposed;
 public string BackdropStatus {get;private set;}
 const int RowDip=76,HeaderDip=52,InsetDip=4;
 public MainWindow(){Text="外设电量";Icon=new Icon(typeof(MainWindow).Assembly.GetManifestResourceStream("PeriBatt.ico"),32,32);Padding=new Padding(1);BackColor=Color.White;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterScreen;using(var g=CreateGraphics())scale=g.DpiX/96f;ClientSize=new Size(P(480),P(HeaderDip+2*InsetDip+RowDip)+2);MinimumSize=Size.Empty;
 header.Height=P(HeaderDip);header.Dock=DockStyle.Top;header.Paint+=(s,e)=>{var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(scale,scale);using(var logo=Image.FromStream(typeof(MainWindow).Assembly.GetManifestResourceStream("PeriBatt.png")))g.DrawImage(logo,new Rectangle(18,13,26,26));PaintTools.Text(g,"外设电量",18,PaintTools.Ink,new RectangleF(54,4,200,44),true);using(var p=new Pen(Color.FromArgb(237,238,237)))g.DrawLine(p,8,51,header.Width/scale-8,51);};header.MouseDown+=Drag;
 foreach(var b in new[]{startup,refresh,close}){b.ScaleFactor=scale;header.Controls.Add(b);}startup.Click+=(s,e)=>{try{Autostart.Set(!Autostart.Enabled);}catch(Exception ex){MessageBox.Show(this,ex.Message,"开机自启");}SyncStartup();};refresh.Click+=async(s,e)=>await Scan();close.Click+=(s,e)=>Close();
 list.Dock=DockStyle.Fill;list.AutoScroll=true;empty.BackColor=Color.Transparent;empty.Text="正在检测设备…";empty.ForeColor=PaintTools.Muted;empty.Font=PaintTools.Font(P(14));empty.TextAlign=ContentAlignment.MiddleCenter;empty.Dock=DockStyle.Fill;list.Controls.Add(empty);Controls.Add(list);Controls.Add(header);Resize+=(s,e)=>LayoutContent();list.SizeChanged+=(s,e)=>LayoutRows();Shown+=async(s,e)=>await Scan();Activated+=(s,e)=>SyncStartup();FormClosing+=(s,e)=>{ended=true;lifetime.Cancel();};LayoutContent();SyncStartup();}
 int P(float v){return (int)Math.Round(v*scale);}
 void SyncStartup(){startup.Checked=Autostart.Enabled;startup.AccessibleDescription=startup.Checked?"已开启":"已关闭";startup.Invalidate();}
 void LayoutContent(){int right=ClientSize.Width-P(6);close.SetBounds(right-P(34),P(6),P(34),P(40));refresh.SetBounds(right-P(72),P(6),P(34),P(40));int startupWidth=startup.StartupPixelWidth();startup.SetBounds(right-P(76)-startupWidth,P(6),startupWidth,P(40));LayoutRows();header.Invalidate();}
 void LayoutRows(){int y=P(InsetDip)+list.AutoScrollPosition.Y;int width=Math.Max(1,list.ClientSize.Width-P(40));foreach(var row in rows.Values.OrderBy(r=>r.Data.Category=="Keyboard"?0:r.Data.Category=="Mouse"?1:r.Data.Category=="Gamepad"?2:3).ThenBy(r=>r.Data.Id)){row.SetBounds(P(20),y,width,P(RowDip));y+=P(RowDip);}list.AutoScrollMinSize=new Size(0,rows.Count>5?P(2*InsetDip+RowDip*rows.Count):0);list.HorizontalScroll.Enabled=false;list.HorizontalScroll.Visible=false;}
 [DllImport("user32.dll")]static extern bool ReleaseCapture();[DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}
 void UpdateRow(Device d){if(ended)return;empty.Visible=false;DeviceRow row;if(!rows.TryGetValue(d.Id,out row)){row=new DeviceRow(d,scale);rows.Add(d.Id,row);list.Controls.Add(row);}else row.Data=d;row.AccessibleName=d.Name+" "+d.Connection+" "+d.Status;row.AccessibleDescription=d.Category=="Audio"?AudioTypes.Label(d.AudioType):d.Category;row.Invalidate();}
 public void ApplySnapshot(IList<Device> data){
 list.SuspendLayout();try{list.AutoScrollPosition=Point.Empty;
 foreach(var id in rows.Keys.Where(id=>!data.Any(d=>d.Id==id)).ToArray()){rows[id].Dispose();rows.Remove(id);}
 foreach(var d in data)UpdateRow(d);
 empty.Visible=data.Count==0;empty.Text="暂无设备\n点击刷新重新检测";
 FitWindow();LayoutRows();}finally{list.ResumeLayout();}Invalidate(true);
 }
 void FitWindow(){if(!IsHandleCreated)return;var area=Screen.FromHandle(Handle).WorkingArea;
 int height=Math.Min(P(HeaderDip+2*InsetDip+RowDip*Math.Min(Math.Max(rows.Count,1),5))+2,area.Height);
 Size=new Size(Math.Min(P(480),area.Width),height);
 Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));}
 async Task Scan(){if(busy||ended)return;busy=true;refresh.Busy=true;refresh.Invalidate();try{
 var data=await manager.ScanAsync(lifetime.Token,d=>{});if(!ended)ApplySnapshot(data);
 }catch(OperationCanceledException){}catch(Exception){if(!ended&&rows.Count==0){empty.Visible=true;empty.Text="检测未完成，请点击刷新";}}
 finally{busy=false;if(!ended){refresh.Busy=false;refresh.Invalidate();}}}
 [StructLayout(LayoutKind.Sequential)]struct RectangleEdges{public int Left,Top,Right,Bottom;}
 // Keep the client area opaque white. Extending the DWM frame into GDI child
 // windows caused gray surfaces and native caption repaint over our header.
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
 bool Attribute(int key,int value){return DwmSetWindowAttribute(Handle,key,ref value,4)==0;}
 void ApplyBackdrop(){BackdropStatus="白色背景";
 try{Attribute(38,1);Attribute(33,2);Attribute(34,0x00e8e5e2);Attribute(20,0);
 // Disable non-client rendering (including the heavy system frame shadow).
 Attribute(2,1);
 }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
 BackColor=Color.White;Invalidate(true);}
 protected override CreateParams CreateParams{get{var cp=base.CreateParams;
 // A real borderless window: do not re-add caption, sizing, minimize or maximize styles.
 cp.Style&=~(0x00C00000|0x00040000|0x00020000|0x00010000);
 return cp;}}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ApplyBackdrop();}
 protected override void WndProc(ref Message m){
 if(m.Msg==0x84){m.Result=new IntPtr(1);return;}
 // No native caption may be painted during activation or theme changes.
 if(m.Msg==0x85||m.Msg==0x86){m.Result=new IntPtr(1);return;}
 if(m.Msg==0x2e0){var suggested=(RectangleEdges)Marshal.PtrToStructure(m.LParam,typeof(RectangleEdges));Location=new Point(suggested.Left,suggested.Top);scale=(m.WParam.ToInt64()&0xffff)/96f;header.Height=P(HeaderDip);foreach(var b in new[]{startup,refresh,close})b.ScaleFactor=scale;foreach(var row in rows.Values)row.SetScale(scale);empty.Font=PaintTools.Font(P(14));FitWindow();LayoutContent();m.Result=IntPtr.Zero;return;}
 base.WndProc(ref m);if(m.Msg==0x31e||m.Msg==0x320||m.Msg==0x1a)ApplyBackdrop();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Color.FromArgb(226,229,232)))e.Graphics.DrawRectangle(pen,0,0,ClientSize.Width-1,ClientSize.Height-1);}
 protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;ended=true;lifetime.Cancel();lifetime.Dispose();}base.Dispose(disposing);}
}
public class DeviceRow:Control {
 Device data;float shownPercent,fromPercent;DateTime transition;public Device Data{get{return data;}set{fromPercent=shownPercent;data=value;transition=DateTime.UtcNow;shownPercent=SystemInformation.IsMenuAnimationEnabled?shownPercent:value.Percent;if(pulse!=null&&SystemInformation.IsMenuAnimationEnabled)pulse.Start();}}float scale;public void SetScale(float value){scale=value;Invalidate();}readonly System.Windows.Forms.Timer pulse=new System.Windows.Forms.Timer{Interval=80};float phase;
 public DeviceRow(Device d):this(d,1){}public DeviceRow(Device d,float dpiScale){Data=d;shownPercent=d.Percent;scale=dpiScale;DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;pulse.Tick+=(s,e)=>{phase+=.25f;float t=(float)Math.Min(1,(DateTime.UtcNow-transition).TotalMilliseconds/200);shownPercent=fromPercent+(Data.Percent-fromPercent)*t;Invalidate(new Rectangle((int)(68*scale),(int)(52*scale),Width,(int)(10*scale)));if(Data.State==DeviceState.Online&&Data.Charging)Invalidate(new Rectangle(Math.Max(0,Width-(int)(150*scale)),(int)(18*scale),(int)(150*scale),(int)(32*scale)));};}
 public static Color BatteryColor(Device d){if(d.Kind==BatteryKind.DiscreteLevel)return d.Level<=1?Color.FromArgb(210,65,65):d.Level<d.LevelCount?Color.FromArgb(209,162,41):Color.SeaGreen;return d.Percent<20?Color.FromArgb(210,65,65):d.Percent<=50?Color.FromArgb(209,162,41):d.Percent==100&&d.Kind==BatteryKind.ExactPercent?Color.FromArgb(50,123,215):Color.FromArgb(45,151,109);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(scale,scale);g.TranslateTransform(0,6);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;float w=Width/scale;var d=Data;bool online=d.State==DeviceState.Online;bool animate=online&&d.Kind==BatteryKind.ExactPercent&&d.Percent==100&&d.Freshness==Freshness.Current&&SystemInformation.IsMenuAnimationEnabled;bool charging=online&&d.Charging;bool needsPulse=animate||(charging&&SystemInformation.IsMenuAnimationEnabled);if(needsPulse&&!pulse.Enabled)pulse.Start();else if(!needsPulse&&(DateTime.UtcNow-transition).TotalMilliseconds>=200)pulse.Stop();
 PaintTools.Round(g,Color.FromArgb(246,246,246),0,8,50,50,14);DrawIcon(g,d.Category);const float left=66;string status=d.Status;float statusWidth;using(var sf=PaintTools.Font(18))statusWidth=Math.Max(90,g.MeasureString(status,sf).Width+8);float edge=w-2;if(charging)statusWidth+=20;PaintTools.Text(g,status,18,d.State==DeviceState.Disconnected?Color.FromArgb(155,157,155):PaintTools.Ink,new RectangleF(edge-statusWidth,12,statusWidth-(charging?20:0),30),false,true);
 if(charging){int alpha=SystemInformation.IsMenuAnimationEnabled?(int)(70+185*(.5+.5*Math.Cos(phase))):255;
 float x=edge-12,y=18;using(var brush=new SolidBrush(Color.FromArgb(alpha,0,185,67)))g.FillPolygon(brush,new[]{new PointF(x+6,y),new PointF(x,y+10),new PointF(x+5,y+10),new PointF(x+3,y+20),new PointF(x+12,y+7),new PointF(x+7,y+7)});}

 using(var font=PaintTools.Font(18)){float connectionWidth;using(var cf=PaintTools.Font(15))connectionWidth=g.MeasureString(" · "+d.Connection,cf).Width;float maxName=Math.Max(30,w-left-statusWidth-connectionWidth-12);float nameWidth=Math.Min(maxName,g.MeasureString(d.Name,font).Width);PaintTools.Text(g,d.Name,18,PaintTools.Ink,new RectangleF(left,12,nameWidth+1,30));PaintTools.Text(g," · "+d.Connection,15,PaintTools.Muted,new RectangleF(left+nameWidth,12,connectionWidth,30));}
 float width=edge-left;if(online&&(d.Kind==BatteryKind.Unavailable||d.Kind==BatteryKind.ExternalPowerOnly)){PaintTools.Text(g,"未能识别",12,PaintTools.Muted,new RectangleF(left,43,width,18));return;}Color color=BatteryColor(d);if(animate){int mix=(int)(25+25*Math.Sin(phase));color=Color.FromArgb(50+mix,123+mix,215);}
 if(online&&d.Kind==BatteryKind.DiscreteLevel&&d.LevelCount>0){float gap=4,part=(width-gap*(d.LevelCount-1))/d.LevelCount;for(int i=0;i<d.LevelCount;i++)PaintTools.Round(g,i<d.Level?color:PaintTools.Track,left+i*(part+gap),47,part,6,3);}else{PaintTools.Round(g,PaintTools.Track,left,47,width,6,3);if(online&&(d.Kind==BatteryKind.ExactPercent||d.Kind==BatteryKind.EstimatedPercent)&&d.Percent>0)PaintTools.Round(g,color,left,47,width*(SystemInformation.IsMenuAnimationEnabled?shownPercent:d.Percent)/100f,6,3);}}
 void DrawIcon(Graphics g,string category){using(var p=new Pen(Color.FromArgb(107,109,108),1.6f)){if(category=="Keyboard"){g.DrawRectangle(p,16,27,20,13);for(int y=30;y<38;y+=4)for(int x=19;x<34;x+=4)g.DrawLine(p,x,y,x+1,y);}else if(category=="Audio"){DrawAudioIcon(g,p);}else if(category=="Mouse"){g.DrawEllipse(p,19,23,14,21);g.DrawLine(p,26,26,26,32);}else{using(var path=new GraphicsPath()){path.AddBezier(15,31,16,25,21,25,25,28);path.AddBezier(25,28,26,29,28,29,29,28);path.AddBezier(29,28,33,25,38,25,39,31);path.AddLine(39,31,42,41);path.AddBezier(42,41,43,47,38,47,33,40);path.AddLine(33,40,21,40);path.AddBezier(21,40,16,47,11,47,12,41);path.AddLine(12,41,15,31);path.CloseFigure();p.LineJoin=LineJoin.Round;g.DrawPath(p,path);}g.DrawLine(p,19,33,25,33);g.DrawLine(p,22,30,22,36);g.DrawEllipse(p,31,30,1,1);g.DrawEllipse(p,34,34,1,1);}}}
 void DrawAudioIcon(Graphics g,Pen p){
 if(Data.AudioType==AudioKind.Headphones||Data.AudioType==AudioKind.Headset){
 g.DrawArc(p,17,23,20,20,180,180);g.DrawLine(p,17,32,17,40);g.DrawLine(p,37,32,37,40);
 PaintTools.Round(g,p.Color,15,32,5,12,2);PaintTools.Round(g,p.Color,34,32,5,12,2);
 if(Data.AudioType==AudioKind.Headset){g.DrawArc(p,26,37,12,9,0,90);g.DrawLine(p,27,46,32,46);}
 }else if(Data.AudioType==AudioKind.Speakers){g.DrawRectangle(p,18,22,18,26);g.DrawEllipse(p,22,33,10,10);g.DrawEllipse(p,25,26,4,4);
 }else if(Data.AudioType==AudioKind.SoundCard){g.DrawRectangle(p,14,27,26,17);g.DrawEllipse(p,30,31,6,6);for(int x=18;x<=26;x+=4)g.DrawEllipse(p,x,37,2,2);g.DrawLine(p,18,24,36,24);
 }else{g.DrawLine(p,24,24,24,41);g.DrawLine(p,24,24,35,22);g.DrawLine(p,35,22,35,38);g.DrawEllipse(p,18,39,6,5);g.DrawEllipse(p,29,36,6,5);}
 }
 protected override void Dispose(bool disposing){if(disposing){pulse.Dispose();}base.Dispose(disposing);}
}
static class Program {
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(IntPtr context);
 [STAThread]static void Main(){try{if(!SetProcessDpiAwarenessContext(new IntPtr(-4)))SetProcessDPIAware();}catch(EntryPointNotFoundException){SetProcessDPIAware();}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainWindow());}
}
}


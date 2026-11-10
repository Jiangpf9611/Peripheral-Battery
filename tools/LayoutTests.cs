using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PeriBatt;
// Development-only executable. No fixture switch or data is linked into PeriBatt.exe.
class FixtureWindow:MainWindow {protected override void OnShown(EventArgs e){} public void RecreateForTest(){RecreateHandle();} }
class LayoutTests {
 [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
 static void FrameCheck(Form f){Check((GetWindowLong(f.Handle,-16)&(0x00C00000|0x00040000))==0,"native caption/frame restored");Check(f.Size==f.ClientSize,"unexpected nonclient inset");Check(f.BackColor==Color.White,"white background");}
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static List<Device> Samples(int n){return Enumerable.Range(0,n).Select(i=>new Device{Id="fixture-"+i,Name=i==0?"布局测试 · 超长设备名称 ABCDEFGHIJKLMNOPQRSTUVWXYZ": "布局测试设备 "+(i+1),Category=i==0?"Keyboard":"Mouse",Connection="2.4G",Kind=BatteryKind.ExactPercent,Percent=20+i*13}).ToList();}
 [STAThread]static void Main(string[] args){try{Run(args);}catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);Environment.ExitCode=1;}}
 static void Run(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
 if(args.Length>0){var f=new FixtureWindow();f.Text="PeriBatt 布局测试 · 模拟数据";f.Show();f.ApplySnapshot(Samples(int.Parse(args[0])));Application.Run(f);return;}
 foreach(float scale in new[]{1f,1.25f,1.5f,1.75f})using(var f=new FixtureWindow()){
 f.Show();f.RecreateForTest();FrameCheck(f);foreach(int msg in new[]{0x86,0x31e,0x320,0x1a}){SendMessage(f.Handle,msg,new IntPtr(1),IntPtr.Zero);FrameCheck(f);}
 typeof(MainWindow).GetField("scale",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,scale);
 var header=(Panel)typeof(MainWindow).GetField("header",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);header.Height=(int)Math.Round(52*scale);
 var list=(Panel)typeof(MainWindow).GetField("list",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
 foreach(int n in new[]{0,1,2,3,4,5,6,9,5,2,0}){f.ApplySnapshot(Samples(n));Application.DoEvents();int expected=(int)Math.Round((60+76*Math.Min(Math.Max(n,1),5))*scale)+2;
 Check(f.Height==expected,"height "+scale+" / "+n+": "+f.Height+" != "+expected);Check(f.Width==(int)Math.Round(480*scale),"width");
 FrameCheck(f);Check(!list.HorizontalScroll.Visible,"horizontal scrollbar");Check(list.VerticalScroll.Visible==(n>5),"vertical scrollbar "+scale+" / "+n);
 foreach(var row in list.Controls.OfType<DeviceRow>()){Check(row.Height==(int)Math.Round(76*scale),"row height");if(n<=5)Check(row.Bottom<=list.ClientSize.Height,"clipped row");}
 if(n>5){list.AutoScrollPosition=new Point(0,1000);Application.DoEvents();Check(list.AutoScrollPosition.Y<0,"scroll position");}else Check(list.AutoScrollPosition.Y==0,"scroll reset");
 }f.Close();}
 Console.WriteLine("PASS: 0..6/9 devices, 9 -> 5 -> 2 -> 0, width/height, full rows, scroll/reset at 100/125/150/175% layout scale.");}
}

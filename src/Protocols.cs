using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace PeriBatt {
public enum QueryOutcome { Unmatched, NoReply, InvalidReply, Success, NoBatteryChannel, AccessDenied, IoError }
// Debug build only; release builds omit calls and their argument evaluation.
static class ProtocolLog {
 [System.Diagnostics.Conditional("DEBUG")]
 public static void Add(string text){System.Diagnostics.Debug.WriteLine(text);}
}
// Separate read/write handles; every outstanding buffer remains pinned until cancellation completes.
sealed class HidTransport:IDisposable {
 readonly SafeFileHandle reader,writer;
 [StructLayout(LayoutKind.Sequential)]struct Overlap {public IntPtr internalLow,internalHigh;public uint offset,offsetHigh;public IntPtr evt;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadFile(SafeFileHandle h,IntPtr b,uint n,out uint done,IntPtr ov);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteFile(SafeFileHandle h,IntPtr b,uint n,out uint done,IntPtr ov);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetOverlappedResult(SafeFileHandle h,IntPtr ov,out uint done,bool wait);
 [DllImport("kernel32.dll")]static extern bool CancelIoEx(SafeFileHandle h,IntPtr ov);
 [DllImport("hid.dll")]static extern bool HidD_FlushQueue(SafeFileHandle h);
 public HidTransport(string path){reader=Native.CreateFile(path,0x80000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);int re=Marshal.GetLastWin32Error();writer=Native.CreateFile(path,0x40000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);int we=Marshal.GetLastWin32Error();ProtocolLog.Add("OPEN read="+(!reader.IsInvalid)+" err="+re+" write="+(!writer.IsInvalid)+" err="+we);if(reader.IsInvalid||writer.IsInvalid){Dispose();if(re==5||we==5)throw new UnauthorizedAccessException("查询通道拒绝访问");throw new IOException("无法访问查询通道，Win32="+(reader.IsInvalid?re:we));}HidD_FlushQueue(reader);}
 int Transfer(byte[] buffer,bool write,int timeout,CancellationToken token){token.ThrowIfCancellationRequested();using(var ev=new EventWaitHandle(false,EventResetMode.ManualReset)){var pinned=GCHandle.Alloc(buffer,GCHandleType.Pinned);IntPtr ov=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Overlap)));var h=write?writer:reader;bool pending=false;try{Marshal.StructureToPtr(new Overlap{evt=ev.SafeWaitHandle.DangerousGetHandle()},ov,false);uint done;bool ok=write?WriteFile(h,pinned.AddrOfPinnedObject(),(uint)buffer.Length,out done,ov):ReadFile(h,pinned.AddrOfPinnedObject(),(uint)buffer.Length,out done,ov);if(!ok){int error=Marshal.GetLastWin32Error();if(error!=997)throw new IOException("HID IO Win32="+error);pending=true;int index=WaitHandle.WaitAny(new WaitHandle[]{ev,token.WaitHandle},timeout);if(index!=0){CancelIoEx(h,ov);GetOverlappedResult(h,ov,out done,true);pending=false;token.ThrowIfCancellationRequested();return 0;}if(!GetOverlappedResult(h,ov,out done,false))throw new IOException("HID completion Win32="+Marshal.GetLastWin32Error());pending=false;}return (int)done;}finally{if(pending){CancelIoEx(h,ov);uint ignored;GetOverlappedResult(h,ov,out ignored,true);}Marshal.FreeHGlobal(ov);pinned.Free();}}}
 public void Send(byte[] request,CancellationToken token){int n=Transfer(request,true,300,token);ProtocolLog.Add("WriteFile TX bytes="+n+" "+BitConverter.ToString(request));if(n!=request.Length)throw new IOException("电量查询未完整发送");}
 public byte[] Read(int length,int timeout,CancellationToken token){var buffer=new byte[length];int n=Transfer(buffer,false,timeout,token);if(n==0)return null;Array.Resize(ref buffer,n);ProtocolLog.Add("RX bytes="+n+" "+BitConverter.ToString(buffer));return buffer;}
 public void Dispose(){if(reader!=null)reader.Dispose();if(writer!=null)writer.Dispose();}
}
public static class BatteryParsers {
 public static int MouseVoltagePercent(int millivolts,bool charging){int[] curve={3050,3420,3480,3540,3600,3660,3720,3760,3800,3840,3880,3920,3940,3960,3980,4000,4020,4040,4060,4080,4110};if(millivolts<curve[0]||millivolts>4500)return -1;if(millivolts>=curve[curve.Length-1])return charging?99:100;int upper=Array.FindIndex(curve,v=>v>millivolts);double value=(upper-1)*5+(millivolts-curve[upper-1])*5.0/(curve[upper]-curve[upper-1]);if(value==0||value==15)value++;return (int)Math.Floor(value+0.5);}
 public static bool CalibratedMouse(byte[] r,out int p,out bool c){if(!Compx(r,out p,out c))return false;int mv=(r[8]<<8)|r[9];p=MouseVoltagePercent(mv,c);ProtocolLog.Add("MOUSE raw="+r[6]+" voltage="+mv+"mV calibrated="+p);return p>=0;}
 public static bool Keyboard(byte[] r,out int percent,out bool charging){percent=0;charging=false;if(r==null||r.Length!=64||r[0]!=4||r[1]!=0||r[2]!=0||r[3]!=0x1a||r[4]!=6||r[8]>100)return false;percent=r[8];charging=r[9]!=0;return true;}
 public static bool Compx(byte[] r,out int percent,out bool charging){percent=0;charging=false;if(r==null||r.Length!=17||r[0]!=8||r[1]!=4||r.Sum(b=>(int)b)%256!=0x55||r[5]!=2||r[6]>100)return false;percent=r[6];charging=r[7]==1;return true;}
}
public abstract class QueryProvider:IBatteryProvider {
 public abstract bool CanHandle(HidInterface h);
 protected abstract byte[] Request(); protected abstract bool Parse(byte[] r,out int percent,out bool charging);protected abstract string Source{get;} protected virtual bool Estimated{get{return false;}} protected virtual bool VerifiedPercent{get{return true;}} protected virtual int ReadWindow{get{return 450;}} protected virtual int Attempts{get{return 1;}}
 public Task ReadAsync(Device d,HidInterface h,CancellationToken token){return Task.Run(()=>{bool received=false;d.Source=Source;ProtocolLog.Add("MATCH "+Source+" usage="+h.Page.ToString("X4")+":"+h.Usage.ToString("X4"));try{using(var channel=new HidTransport(h.Path)){for(int attempt=0;attempt<Attempts;attempt++){channel.Send(Request(),token);var time=System.Diagnostics.Stopwatch.StartNew();while(time.ElapsedMilliseconds<ReadWindow){var reply=channel.Read(h.Input,Math.Min(80,ReadWindow-(int)time.ElapsedMilliseconds),token);if(reply==null)continue;received=true;int percent;bool charging;if(!Parse(reply,out percent,out charging)){ProtocolLog.Add("VALIDATION rejected foreign/status/invalid frame");continue;}if(!VerifiedPercent){d.Kind=BatteryKind.Unavailable;d.Outcome=QueryOutcome.InvalidReply;d.Reason="设备原始电量字段与官方读数不一致，精确百分比尚未验证";ProtocolLog.Add("UNVERIFIED raw battery field="+percent+"; not displayed as percent");return;}d.Kind=Estimated?BatteryKind.EstimatedPercent:BatteryKind.ExactPercent;d.Percent=percent;d.Charging=charging;d.ReadTimestamp=DateTime.Now;d.Freshness=Freshness.Unknown;d.Outcome=QueryOutcome.Success;d.Reason=Estimated?"根据设备返回的电压及 VGN 电量曲线估算。官方历史平滑可能造成差异；不是精确实测百分比。":"设备返回电量；测量时间未知，可能为接收器缓存。";ProtocolLog.Add("SUCCESS percent="+percent+" charging="+charging);return;}}}d.Outcome=received?QueryOutcome.InvalidReply:QueryOutcome.NoReply;d.Reason=received?"设备回复未通过电量协议校验":"设备没有回复，使用设备后刷新";}catch(OperationCanceledException){throw;}catch(Exception ex){d.Outcome=ex is UnauthorizedAccessException?QueryOutcome.AccessDenied:QueryOutcome.IoError;d.Reason="电量查询失败，请稍后刷新";ProtocolLog.Add("IO ERROR "+ex.Message);}ProtocolLog.Add("RESULT "+d.Outcome);},token);}
}
public class VgnKeyboardProvider:QueryProvider {
 public override bool CanHandle(HidInterface h){return h.Vid==0x320f&&h.Pid==0x5088&&h.Page==0xff1c&&h.Usage==0x92&&h.Input==64&&h.Output==64&&h.Feature==0;}
 protected override byte[] Request(){var r=new byte[64];r[0]=4;r[3]=0x1a;r[4]=6;return r;}
 protected override bool Parse(byte[] r,out int p,out bool c){return BatteryParsers.Keyboard(r,out p,out c);}
 protected override string Source{get{return "VGN HID 0x1A";}}protected override int Attempts{get{return 3;}}
}
public class CompxMouseProvider:QueryProvider {
 public override bool CanHandle(HidInterface h){return h.Vid==0x391d&&(h.Pid==0x1a05||h.Pid==0x1005)&&h.Page==0xff02&&h.Usage==2&&h.Input==17&&h.Output==17&&h.Feature==0;}
 protected override byte[] Request(){var r=new byte[17];r[0]=8;r[1]=4;r[16]=0x49;return r;}
 protected override bool Parse(byte[] r,out int p,out bool c){return BatteryParsers.CalibratedMouse(r,out p,out c);}
 protected override bool Estimated{get{return true;}} protected override string Source{get{return "Compx voltage / VGN calibration";}}protected override int ReadWindow{get{return 1500;}}
}
}






using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
namespace PeriBatt {
public enum BatteryKind { ExactPercent, EstimatedPercent, DiscreteLevel, Unavailable, ExternalPowerOnly }
public enum DeviceState { Online, Disconnected, Sleeping, Reading, Error }
public enum Freshness { Current, Unknown, Stale }
public class Device {
 public AudioKind AudioType;public string ContainerId;public string Id, Name, Connection="连接方式未知", Category="Other", Source="无可用协议", Reason="该设备已连接，但未提供可读取的电量信息";
 public BatteryKind Kind=BatteryKind.Unavailable; public DeviceState State=DeviceState.Online; public Freshness Freshness=Freshness.Unknown;
 public QueryOutcome Outcome=QueryOutcome.Unmatched;public int Percent, Level, LevelCount; public string LevelLabel; public DateTime? ReadTimestamp; public bool Charging; public bool ConfirmedConnected;
 public string Status { get { if(State==DeviceState.Disconnected)return "未连接"; if(State==DeviceState.Sleeping)return "状态未知"; if(State==DeviceState.Reading)return "读取中"; if(Kind==BatteryKind.EstimatedPercent)return Percent+"%";if(Kind==BatteryKind.ExactPercent)return Percent+"%"; if(Kind==BatteryKind.DiscreteLevel)return LevelLabel; if(Category=="Gamepad"&&State==DeviceState.Online)return "已连接";return "—"; } }
}
public class HidInterface { public AudioKind AudioType;public string ContainerId;public bool Audio;public string Path, Identity, Product; public ushort Vid,Pid,Page,Usage,Input,Output,Feature; public bool Bluetooth; public int? SystemBattery; }
static class Native {
 [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid guid; public uint id; }
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_DevNode_PropertyW(uint node,ref PropertyKey key,out uint type,byte[] data,ref uint size,uint flags);
 static int? SystemBattery(uint node) { var key=new PropertyKey{guid=new Guid("104ea319-6ee2-4701-bd47-8ddbf425bbe5"),id=2};uint type;uint size=4;var bytes=new byte[4];if(CM_Get_DevNode_PropertyW(node,ref key,out type,bytes,ref size,0)==0){if(type==3&&size==1&&bytes[0]<=100)return bytes[0];if(type==7&&size==4){uint value=BitConverter.ToUInt32(bytes,0);if(value<=100)return (int)value;}}return null;}
 [DllImport("hid.dll")] internal static extern void HidD_GetHidGuid(out Guid g);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid g,IntPtr a,IntPtr b,uint f);
 [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInfo(IntPtr set,uint index,ref DevInfo info);
 [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr h,IntPtr d,ref Guid g,uint i,ref InterfaceData data);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr h,ref InterfaceData d,IntPtr p,uint size,out uint needed,ref DevInfo info);
 [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr h);
 [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent,uint node,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_Device_ID(uint node,StringBuilder text,int length,uint flags);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern SafeFileHandle CreateFile(string p,uint access,uint share,IntPtr sec,uint creation,uint flags,IntPtr t);
 [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h,ref Attributes a);
 [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h,out IntPtr p);
 [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
 [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p, out Caps caps);
 [DllImport("hid.dll")] static extern bool HidD_GetProductString(SafeFileHandle h,byte[] b,int n);
 [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int size; public Guid guid; public int flags; public IntPtr reserved; }
 [StructLayout(LayoutKind.Sequential)] struct DevInfo { public int size; public Guid guid; public uint node; public IntPtr reserved; }
 [StructLayout(LayoutKind.Sequential)] struct Attributes { public int size; public ushort vid,pid,version; }
 [StructLayout(LayoutKind.Sequential)] struct Caps {public ushort usage,page,input,output,feature; [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] reserved; public ushort a,b,c,d,e,f,g,h,i,j;}
 [StructLayout(LayoutKind.Sequential)] struct BluetoothSearch {public uint size;[MarshalAs(UnmanagedType.Bool)]public bool authenticated,remembered,unknown,connected,inquiry;public byte timeout;public IntPtr radio;}
 [StructLayout(LayoutKind.Sequential)] struct SystemTime {public ushort y,m,dow,d,h,min,s,ms;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct BluetoothDevice {public uint size;public ulong address;public uint deviceClass;[MarshalAs(UnmanagedType.Bool)]public bool connected,remembered,authenticated;public SystemTime seen,used;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=248)]public string name;}
 [DllImport("BluetoothAPIs.dll")]static extern IntPtr BluetoothFindFirstDevice(ref BluetoothSearch search,ref BluetoothDevice device);
 [DllImport("BluetoothAPIs.dll")]static extern bool BluetoothFindNextDevice(IntPtr find,ref BluetoothDevice device);
 [DllImport("BluetoothAPIs.dll")]static extern bool BluetoothFindDeviceClose(IntPtr find);
 internal static async Task<HashSet<string>> ConnectedBluetoothLe(CancellationToken token){
 var live=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 try{var devices=await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),new[]{"System.Devices.Aep.DeviceAddress"}).AsTask(token);
 foreach(var device in devices){object value;if(device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress",out value)&&value is string)live.Add(((string)value).Replace(":","").Replace("-", ""));}
 }catch(OperationCanceledException){throw;}catch(Exception ex){ProtocolLog.Add("BLE discovery unavailable: "+ex.Message);}
 return live;
 }
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern int CM_Locate_DevNodeW(out uint node,string id,uint flags);
 internal static string EndpointContainer(string endpointId){uint node;return CM_Locate_DevNodeW(out node,"SWD\\MMDEVAPI\\"+endpointId,0)==0?NodeContainer(node):null;}
 static string NodeContainer(uint node){var key=new PropertyKey{guid=new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"),id=2};uint type;uint size=16;var bytes=new byte[16];if(CM_Get_DevNode_PropertyW(node,ref key,out type,bytes,ref size,0)!=0||type!=13)return null;var id=new Guid(bytes);return id==Guid.Empty||id==new Guid("00000000-0000-0000-ffff-ffffffffffff")?null:id.ToString("D");}
 static void AttachIdentity(HidInterface h,uint node){
 h.ContainerId=NodeContainer(node);
 for(int depth=0;depth<12;depth++){var sb=new StringBuilder(512);if(CM_Get_Device_ID(node,sb,512,0)!=0)break;string id=sb.ToString();h.SystemBattery=h.SystemBattery??SystemBattery(node);
 if(id.StartsWith("BTH",StringComparison.OrdinalIgnoreCase)){
 h.Bluetooth=true;if(string.IsNullOrEmpty(h.Identity))h.Identity=id;
 // The radio is not the headset's parent identity. Remote nodes may be siblings.
 if(id.StartsWith("BTH\\",StringComparison.OrdinalIgnoreCase))break;
 }
 if(id.StartsWith("BTHLE\\DEV_",StringComparison.OrdinalIgnoreCase)||id.StartsWith("BTHENUM\\DEV_",StringComparison.OrdinalIgnoreCase)){
 h.Identity=id;h.ContainerId=NodeContainer(node)??h.ContainerId;var name=NodeName(node);if(!string.IsNullOrWhiteSpace(name))h.Product=name;break;}
 if(id.StartsWith("USB\\",StringComparison.OrdinalIgnoreCase)&&!id.Contains("&MI_")){h.Identity=id;h.ContainerId=NodeContainer(node)??h.ContainerId;break;}
 uint parent;if(CM_Get_Parent(out parent,node,0)!=0)break;node=parent;
 }
 }
 internal static List<HidInterface> ContainerNodes(){
 var nodes=new List<HidInterface>();var g=Guid.Empty;var set=SetupDiGetClassDevs(ref g,IntPtr.Zero,IntPtr.Zero,6);
 if(set==new IntPtr(-1))return nodes;
 try{for(uint i=0;;i++){var info=new DevInfo{size=Marshal.SizeOf(typeof(DevInfo))};if(!SetupDiEnumDeviceInfo(set,i,ref info))break;
 var text=new StringBuilder(512);if(CM_Get_Device_ID(info.node,text,512,0)!=0)continue;string id=text.ToString();
 if(!id.StartsWith("BTH",StringComparison.OrdinalIgnoreCase)&&!id.StartsWith("USB\\",StringComparison.OrdinalIgnoreCase))continue;
 string container=NodeContainer(info.node);if(container==null)continue;
 nodes.Add(new HidInterface{Identity=id,ContainerId=container,Product=NodeName(info.node),SystemBattery=SystemBattery(info.node)});
 }}finally{SetupDiDestroyDeviceInfoList(set);}return nodes;
 }
 internal static List<HidInterface> EnumerateAudio(){
 var result=new List<HidInterface>();var g=new Guid("4d36e96c-e325-11ce-bfc1-08002be10318");var set=SetupDiGetClassDevs(ref g,IntPtr.Zero,IntPtr.Zero,2);
 if(set==new IntPtr(-1))return result;
 try{for(uint i=0;;i++){var info=new DevInfo{size=Marshal.SizeOf(typeof(DevInfo))};if(!SetupDiEnumDeviceInfo(set,i,ref info))break;
 var h=new HidInterface{Audio=true,Product=NodeName(info.node),Identity="",Path=""};AttachIdentity(h,info.node);
 if(h.Identity.StartsWith("USB\\",StringComparison.OrdinalIgnoreCase)||h.Identity.StartsWith("BTHLE",StringComparison.OrdinalIgnoreCase)||h.Identity.StartsWith("BTHENUM",StringComparison.OrdinalIgnoreCase)||h.Identity.StartsWith("BTHHFENUM",StringComparison.OrdinalIgnoreCase)){
 if(string.IsNullOrWhiteSpace(h.Product))h.Product="音频设备";
 result.Add(h);}
 }}finally{SetupDiDestroyDeviceInfoList(set);}var resolved=BatteryManager.ResolveAudioContainers(result,ContainerNodes());AudioTypes.Apply(resolved,AudioTypes.ReadEndpoints());return resolved;
 }
 static string NodeName(uint node){var key=new PropertyKey{guid=new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),id=14};uint type;uint size=1024;var bytes=new byte[size];return CM_Get_DevNode_PropertyW(node,ref key,out type,bytes,ref size,0)==0&&type==18?Encoding.Unicode.GetString(bytes,0,(int)size).TrimEnd('\0'):null;}
 internal static HashSet<string> ConnectedBluetooth(){var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var search=new BluetoothSearch{size=(uint)Marshal.SizeOf(typeof(BluetoothSearch)),connected=true};var d=new BluetoothDevice{size=(uint)Marshal.SizeOf(typeof(BluetoothDevice))};var find=BluetoothFindFirstDevice(ref search,ref d);if(find==IntPtr.Zero)return names;try{do{if(d.connected)names.Add(d.address.ToString("x12"));d.size=(uint)Marshal.SizeOf(typeof(BluetoothDevice));}while(BluetoothFindNextDevice(find,ref d));}finally{BluetoothFindDeviceClose(find);}return names;}
 internal static List<HidInterface> Enumerate() {
 var result=new List<HidInterface>(); Guid g; HidD_GetHidGuid(out g); var set=SetupDiGetClassDevs(ref g,IntPtr.Zero,IntPtr.Zero,18);
 if(set==new IntPtr(-1))return result;
 try { for(uint i=0;;i++) { var data=new InterfaceData{size=Marshal.SizeOf(typeof(InterfaceData))}; if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref g,i,ref data))break;
 var info=new DevInfo{size=Marshal.SizeOf(typeof(DevInfo))}; uint n; SetupDiGetDeviceInterfaceDetail(set,ref data,IntPtr.Zero,0,out n,ref info); var mem=Marshal.AllocHGlobal((int)n);
 try {Marshal.WriteInt32(mem,IntPtr.Size==8?8:6); if(!SetupDiGetDeviceInterfaceDetail(set,ref data,mem,n,out n,ref info))continue; string path=Marshal.PtrToStringUni(IntPtr.Add(mem,4));
 using(var handle=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {if(handle.IsInvalid)continue; var a=new Attributes{size=Marshal.SizeOf(typeof(Attributes))}; if(!HidD_GetAttributes(handle,ref a))continue;
 IntPtr pp; if(!HidD_GetPreparsedData(handle,out pp))continue; Caps cap; try{if(HidP_GetCaps(pp,out cap)<0)continue;}finally{HidD_FreePreparsedData(pp);}
 var buf=new byte[512]; string product=HidD_GetProductString(handle,buf,buf.Length)?Encoding.Unicode.GetString(buf).TrimEnd('\0'):"HID 外设";
 var h=new HidInterface{Path=path,Identity=path,Product=product,Vid=a.vid,Pid=a.pid,Page=cap.page,Usage=cap.usage,Input=cap.input,Output=cap.output,Feature=cap.feature};
 AttachIdentity(h,info.node);result.Add(h); }
 }finally{Marshal.FreeHGlobal(mem);} }
 }finally{SetupDiDestroyDeviceInfoList(set);} return result;
 }
 [DllImport("xinput1_4.dll")] internal static extern uint XInputGetState(uint i,byte[] state);
 [DllImport("xinput1_4.dll")] internal static extern uint XInputGetBatteryInformation(uint i,byte type,out XBattery battery);
 [StructLayout(LayoutKind.Sequential)] internal struct XBattery { public byte Type,Level; }
}
public interface IBatteryProvider { bool CanHandle(HidInterface device); Task ReadAsync(Device device,HidInterface path,CancellationToken cancellation); }
public interface ILocalNetworkProvider { Task<IList<Device>> DiscoverAsync(CancellationToken cancellation); }
public static class DeviceNames {
 public static bool IsConfirmedMouse(HidInterface h){if(h==null||h.Vid!=0x391d||h.Pid!=0x1a05||h.Path==null)return false;using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(h.Path.ToLowerInvariant()))).Replace("-","")=="3B84D9DD73833DAB2A26146B2D884472DACC22BB7207B30510A4236728F6D978";}

 public static bool IsConfirmedDirewolf(HidInterface h){
 if(h.Vid!=0x045e||h.Pid!=0x028e||h.Page!=1||h.Usage!=5||h.Product!="Controller (Flydigi Direwolf)"||h.Path==null)return false;
 using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(h.Path.ToLowerInvariant()))).Replace("-","")=="27E31C3120FF27433D4F2DDD4373EA73CEFEBBAA63A9FE11FF81C93E4691F256";
 }
 // User-confirmed physical keyboard: bind the confirmed USB, BLE and receiver identities,
 // never merge all keyboards merely because they share a model name.
 public static string KeyboardIdentity(string transportId){
 if(transportId==null)return null;
 using(var sha=System.Security.Cryptography.SHA256.Create()){
 var hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(transportId.ToLowerInvariant()))).Replace("-","");
 if(hash=="52E90D53170B31FBDCCE5E18FDF046BF636C6311C1352536F6FF70A9BD296DA9"||hash=="676A176C217D391F9CF9A2E57792CC44B6595021C388B28989027915B09C3588"||hash=="2D471D813EEC22C8171F82F5594ED39FD6160FE4D6C76B72B2BB050AD0AC018B")return "keyboard:vgn-v98pro-v2-confirmed";
 }return transportId;
 }
 public static string Friendly(HidInterface h){if(IsConfirmedDirewolf(h))return "飞智冰原狼2";string name=h.Product??"HID 外设";if(name=="Controller (Flydigi Direwolf)")return "飞智冰原狼手柄";if(name.StartsWith("Controller (")&&name.EndsWith(")"))return name.Substring(12,name.Length-13);return name;}
}
public class BatteryManager {
 public static List<HidInterface> ResolveAudioContainers(List<HidInterface> audio,IEnumerable<HidInterface> nodes){
 var metadata=nodes.ToList();
 foreach(var h in audio){Guid container;
 if(!Guid.TryParse(h.ContainerId,out container)||container==Guid.Empty||container==new Guid("00000000-0000-0000-ffff-ffffffffffff"))continue;
 var same=metadata.Where(n=>string.Equals(n.ContainerId,h.ContainerId,StringComparison.OrdinalIgnoreCase)).ToList();
 var remote=same.FirstOrDefault(n=>n.Identity!=null&&(n.Identity.StartsWith("BTHENUM\\DEV_",StringComparison.OrdinalIgnoreCase)||n.Identity.StartsWith("BTHLE\\DEV_",StringComparison.OrdinalIgnoreCase)));
 if(remote!=null){h.Identity=remote.Identity;h.Bluetooth=true;if(!string.IsNullOrWhiteSpace(remote.Product))h.Product=remote.Product;}
 var battery=same.FirstOrDefault(n=>n.SystemBattery.HasValue);
 if(battery!=null)h.SystemBattery=h.SystemBattery??battery.SystemBattery;
 }return audio;
 }
 // Windows container evidence applies equally to mice, controllers, audio and keyboards.
 // No name/model/VID-PID fallback: absent evidence must never collapse different units.
 public static List<Device> SelectSnapshot(IEnumerable<Device> devices){
 var all=devices.ToList();
 var links=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 foreach(var d in all){Guid container;var transport=d.Id;
 if(!string.IsNullOrWhiteSpace(d.ContainerId)&&Guid.TryParse(d.ContainerId,out container)&&container!=Guid.Empty&&container!=new Guid("00000000-0000-0000-ffff-ffffffffffff")){
 string key="container:"+container.ToString("D");string previous;
 if(links.TryGetValue(key,out previous)&&previous!=transport&&previous.StartsWith("keyboard:",StringComparison.Ordinal))continue;
 links[key]=transport.StartsWith("keyboard:",StringComparison.Ordinal)?transport:key;
 }}
 foreach(var d in all){Guid c;if(!string.IsNullOrWhiteSpace(d.ContainerId)&&Guid.TryParse(d.ContainerId,out c)){
 string id;if(links.TryGetValue("container:"+c.ToString("D"),out id))d.Id=id;
 }}
 return all.GroupBy(d=>d.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(d=>d.ConfirmedConnected&&d.State==DeviceState.Online)
 .ThenByDescending(d=>d.Connection=="USB").ThenByDescending(d=>d.Connection=="蓝牙").ThenByDescending(d=>d.Kind==BatteryKind.ExactPercent||d.Kind==BatteryKind.EstimatedPercent||d.Kind==BatteryKind.DiscreteLevel).ThenByDescending(d=>d.Source=="XInput").ThenByDescending(d=>d.Category=="Audio").First()).ToList();
 }

 public static bool BluetoothIsConnected(HidInterface h,HashSet<string> classic,HashSet<string> le){
 if(h.Identity!=null&&h.Identity.StartsWith("BTHLE\\DEV_",StringComparison.OrdinalIgnoreCase))return le.Any(address=>address.Length==12&&h.Identity.IndexOf("DEV_"+address+"\\",StringComparison.OrdinalIgnoreCase)>=0);
 return h.Identity!=null&&classic.Any(address=>address.Length==12&&h.Identity.IndexOf("DEV_"+address+"\\",StringComparison.OrdinalIgnoreCase)>=0);
 }

 readonly SemaphoreSlim queries=new SemaphoreSlim(2);

 async Task ReadAndNotify(IBatteryProvider provider,Device device,HidInterface path,CancellationToken token,Action<Device> notify){await queries.WaitAsync(token);try{await provider.ReadAsync(device,path,token);}finally{queries.Release();device.ConfirmedConnected=device.Outcome==QueryOutcome.Success;device.State=device.ConfirmedConnected?DeviceState.Online:DeviceState.Sleeping;}notify(device);}
 public async Task<List<Device>> ScanAsync(CancellationToken token,Action<Device> discovered){
 var liveBluetooth=await Task.Run(()=>Native.ConnectedBluetooth(),token);var liveLe=await Native.ConnectedBluetoothLe(token);var all=await Task.Run(()=>Native.Enumerate(),token); all.AddRange(await Task.Run(()=>Native.EnumerateAudio(),token));
 var result=new List<Device>(); IBatteryProvider[] providers={new VgnKeyboardProvider(),new CompxMouseProvider()};var pending=new List<Task>();
 foreach(var group in all.GroupBy(h=>h.Identity)) {token.ThrowIfCancellationRequested();var items=group.ToList();var main=items.FirstOrDefault(h=>h.Audio)??items.FirstOrDefault(h=>h.Page==1&&(h.Usage==2||h.Usage==6||h.Usage==5));if(main==null)continue;
 var d=new Device{AudioType=main.AudioType,ContainerId=main.ContainerId,Id=group.Key,Name=DeviceNames.Friendly(main),Category=main.Audio?"Audio":main.Usage==6?"Keyboard":main.Usage==2?"Mouse":"Gamepad",Connection=items.Any(h=>h.Bluetooth)?"蓝牙":"USB"};
 if(main.Bluetooth&&!BluetoothIsConnected(main,liveBluetooth,liveLe))continue;var system=items.FirstOrDefault(h=>h.SystemBattery.HasValue);if(system!=null){d.Kind=BatteryKind.ExactPercent;d.Percent=system.SystemBattery.Value;d.Source="Windows 设备电池属性";d.Outcome=QueryOutcome.Success;d.ReadTimestamp=DateTime.Now;d.Reason="系统报告电量，测量时间未知，可能为缓存。";}var provider=providers.FirstOrDefault(p=>items.Any(p.CanHandle));var known=provider==null?null:items.First(provider.CanHandle);if(known!=null&&provider is VgnKeyboardProvider){d.Name="VGN V98Pro V2";d.Category="Keyboard";d.Connection="2.4G";}
 if(provider is CompxMouseProvider){d.Category="Mouse";d.Connection=DeviceNames.IsConfirmedMouse(known)?"2.4G":"连接方式未知";d.Name="Dragonfly 3 Master+";}
 if(d.Category=="Keyboard"){
 var unified=DeviceNames.KeyboardIdentity(d.Id);
 if(unified!=d.Id){d.Id=unified;d.Name="VGN V98Pro V2";}
 }
 // An XInput interface is represented by its slot until physical correlation is available.
 if(items.Any(h=>h.Path.IndexOf("&ig_",StringComparison.OrdinalIgnoreCase)>=0))continue;
 d.ConfirmedConnected=known==null&&(main.Bluetooth||d.Connection=="USB");if(known!=null)d.State=DeviceState.Reading;result.Add(d);discovered(d);if(known!=null)pending.Add(ReadAndNotify(provider,d,known,token,discovered));
 }
 var slots=new uint[4];for(uint i=0;i<4;i++)slots[i]=Native.XInputGetState(i,new byte[16]); for(uint slot=0;slot<4;slot++){Native.XBattery battery;try{uint state=slots[slot];if(state!=0){ProtocolLog.Add("XInput slot="+slot+" state="+state);continue;}var physical=all.Where(h=>h.Page==1&&h.Usage==5).GroupBy(h=>h.Identity).ToList();var linked=physical.Count==1&&slots.Count(code=>code==0)==1?physical[0].First():null;var d=new Device{ContainerId=linked==null?null:linked.ContainerId,Id=linked==null?"xinput:"+slot:linked.Identity,Name="XInput 手柄 "+(slot+1),Category="Gamepad",Source="XInput"};d.ConfirmedConnected=true;if(linked!=null){d.Name=DeviceNames.Friendly(linked);if(DeviceNames.IsConfirmedDirewolf(linked))d.Connection="2.4G";}ProtocolLog.Add("XInput slot="+slot+" state=0");if(Native.XInputGetBatteryInformation(slot,0,out battery)==0){ProtocolLog.Add("XInput battery type="+battery.Type+" level="+battery.Level);if(battery.Type==1){d.Outcome=QueryOutcome.NoBatteryChannel;d.Kind=BatteryKind.ExternalPowerOnly;d.Reason="XInput 只报告 Wired 供电类型，不代表实体连接方式，也不代表满电";}else if((battery.Type==2||battery.Type==3)&&battery.Level<=3){d.Kind=BatteryKind.DiscreteLevel;d.LevelCount=4;d.Level=new[]{0,1,2,4}[battery.Level];d.LevelLabel=new[]{"已耗尽","电量低","电量中等","电量充足"}[battery.Level];d.Freshness=Freshness.Current;d.ReadTimestamp=DateTime.Now;}}result.Add(d);discovered(d);}catch(DllNotFoundException){break;}}
 await Task.WhenAll(pending);result=SelectSnapshot(result);return result.OrderBy(d=>d.Category=="Keyboard"?0:d.Category=="Mouse"?1:d.Category=="Gamepad"?2:3).ThenBy(d=>d.Id).ToList();
 }
}
}












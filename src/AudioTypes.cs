using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace PeriBatt {
public enum AudioKind { Unknown, Headphones, Headset, Speakers, SoundCard }
public class AudioEndpointFact { public string ContainerId;public int FormFactor; }
public static class AudioTypes {
 public static string Label(AudioKind kind){return kind==AudioKind.Headphones?"耳机":kind==AudioKind.Headset?"耳麦":kind==AudioKind.Speakers?"音箱":kind==AudioKind.SoundCard?"声卡/音频接口":"音频设备";}
 public static AudioKind Classify(IEnumerable<int> forms,string adapterName){
 var f=forms.ToArray();if(f.Contains(5))return AudioKind.Headset;if(f.Contains(3))return AudioKind.Headphones;
 string n=(adapterName??"").ToLowerInvariant();
 if(n.Contains("声卡")||n.Contains("sound card")||n.Contains("audio interface")||n=="usbaudio2.0"||n=="usb audio device"||n=="usb pnp sound device"||n.Contains(" usb dac")||n.StartsWith("dac "))return AudioKind.SoundCard;
 if(f.Contains(1)||f.Contains(2))return AudioKind.Speakers;
 if(f.Contains(0)||f.Contains(8))return AudioKind.SoundCard;
 return AudioKind.Unknown;
 }
 public static void Apply(IEnumerable<HidInterface> devices,IEnumerable<AudioEndpointFact> facts){
 var endpoints=facts.ToList();foreach(var h in devices){
 var forms=string.IsNullOrWhiteSpace(h.ContainerId)?new int[0]:endpoints.Where(f=>string.Equals(f.ContainerId,h.ContainerId,StringComparison.OrdinalIgnoreCase)).Select(f=>f.FormFactor).ToArray();
 h.AudioType=Classify(forms,h.Product);
 }
 }
 [StructLayout(LayoutKind.Sequential)]struct Key {public Guid guid;public uint id;public Key(string g,uint i){guid=new Guid(g);id=i;}}
 // PROPVARIANT occupies 24 bytes on this application's x64 target.
 [StructLayout(LayoutKind.Explicit,Size=24)]struct Value {[FieldOffset(0)]public ushort type;[FieldOffset(8)]public uint number;[FieldOffset(8)]public IntPtr pointer;}
 [DllImport("ole32.dll")]static extern int PropVariantClear(ref Value value);
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Enumerator {
 [PreserveSig]int EnumAudioEndpoints(int flow,uint state,out Collection devices);
 [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out Endpoint device);
 [PreserveSig]int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out Endpoint device);
 [PreserveSig]int RegisterEndpointNotificationCallback(IntPtr client);
 [PreserveSig]int UnregisterEndpointNotificationCallback(IntPtr client);
 }
 [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Collection {
 [PreserveSig]int GetCount(out uint count);[PreserveSig]int Item(uint i,out Endpoint device);
 }
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Endpoint {
 [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr activation,out IntPtr obj);
 [PreserveSig]int OpenPropertyStore(uint mode,out Store store);
 [PreserveSig]int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);
 [PreserveSig]int GetState(out uint state);
 }
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Store {
 [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out Key key);
 [PreserveSig]int GetValue(ref Key key,out Value value);
 [PreserveSig]int SetValue(ref Key key,ref Value value);[PreserveSig]int Commit();
 }
 static void Release(object o){if(o!=null&&Marshal.IsComObject(o))Marshal.ReleaseComObject(o);}
 public static List<AudioEndpointFact> ReadEndpoints(){
 var result=new List<AudioEndpointFact>();Enumerator en=null;Collection collection=null;
 try{en=(Enumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
 // Active render endpoints only: microphones and unplugged historical endpoints cannot classify a speaker.
 if(en.EnumAudioEndpoints(0,1,out collection)!=0)return result;uint count;if(collection.GetCount(out count)!=0)return result;
 for(uint i=0;i<count;i++){Endpoint ep=null;Store store=null;try{
 if(collection.Item(i,out ep)!=0||ep.OpenPropertyStore(0,out store)!=0)continue;
 var formKey=new Key("1da5d803-d492-4edd-8c23-e0c0ffee7f0e",0);Value form;int factor=-1;
 if(store.GetValue(ref formKey,out form)==0){try{if(form.type==19||form.type==3)factor=(int)form.number;}finally{PropVariantClear(ref form);}}
 var containerKey=new Key("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c",2);Value container;string id=null;
 if(store.GetValue(ref containerKey,out container)==0){try{if(container.type==72&&container.pointer!=IntPtr.Zero)id=((Guid)Marshal.PtrToStructure(container.pointer,typeof(Guid))).ToString("D");}finally{PropVariantClear(ref container);}}
 string endpointId;if(ep.GetId(out endpointId)==0)id=Native.EndpointContainer(endpointId)??id;
 ProtocolLog.Add("AUDIO property form="+factor+" container="+(id??"unavailable"));
 if(id!=null&&id!=Guid.Empty.ToString("D")&&id!="00000000-0000-0000-ffff-ffffffffffff"&&factor>=0){result.Add(new AudioEndpointFact{ContainerId=id,FormFactor=factor});ProtocolLog.Add("AUDIO endpoint form="+factor+" container="+id);}
 }finally{Release(store);Release(ep);}}
 }catch(Exception ex){ProtocolLog.Add("Audio endpoint properties unavailable: "+ex.Message);}finally{Release(collection);Release(en);}return result;
 }
}
}

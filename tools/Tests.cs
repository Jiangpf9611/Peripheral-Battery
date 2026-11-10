using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PeriBatt;
class Tests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 [STAThread]static int Main(){try{
 using(var window=new MainWindow()){Check(window.Text=="外设电量"&&window.Icon!=null,"window resource construction");}
 var classic=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase){"VGN V98pro V2 BT1"};
 var le=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase){"d10177fc8dd4"};
 var bt=new HidInterface{Identity=@"BTHLE\DEV_D10177FC8DD4\7&TEST",Product="",Bluetooth=true};
 Check(BatteryManager.BluetoothIsConnected(bt,classic,le),"BLE connects by address even with empty HID name");
 Check(!BatteryManager.BluetoothIsConnected(bt,classic,new System.Collections.Generic.HashSet<string>()),"paired BLE name alone cannot prove connection");
 bt.Identity=@"BTHLE\DEV_001122334455\7&TEST";Check(!BatteryManager.BluetoothIsConnected(bt,classic,le),"other BLE address cannot match");
 string keyboardId=DeviceNames.KeyboardIdentity(@"USB\VID_320F&PID_5088\5&CC3F949&0&7");
 Check(keyboardId==DeviceNames.KeyboardIdentity(@"BTHLE\DEV_D10177FC8DD4\7&B8081D1&2&D10177FC8DD4"),"confirmed keyboard transport identity");
 Check(keyboardId!=DeviceNames.KeyboardIdentity(@"USB\VID_320F&PID_5088\another-unit"),"do not merge other same-model keyboard");
 var receiver=new Device{Id=keyboardId,Connection="2.4G",State=DeviceState.Sleeping};
 var activeBt=new Device{Id=keyboardId,Connection="蓝牙",State=DeviceState.Online,ConfirmedConnected=true,Kind=BatteryKind.ExactPercent,Percent=78};
 var merged=BatteryManager.SelectSnapshot(new[]{receiver,activeBt});Check(merged.Count==1&&object.ReferenceEquals(merged[0],activeBt),"active BLE replaces receiver placeholder");
 receiver.State=DeviceState.Online;receiver.ConfirmedConnected=true;
 Check(BatteryManager.SelectSnapshot(new[]{receiver,activeBt})[0]==activeBt,"BLE wins over possible receiver cache");
 Check(BatteryManager.SelectSnapshot(new[]{receiver})[0].Id==keyboardId,"same row identity after switching back");
 var activeUsb=new Device{Id=DeviceNames.KeyboardIdentity(@"USB\VID_320F&PID_5055\6&2BCCCFA4&0&3"),Connection="USB",State=DeviceState.Online,ConfirmedConnected=true};
 Check(activeUsb.Id==keyboardId,"confirmed USB keyboard retains row identity");
 var usbMerged=BatteryManager.SelectSnapshot(new[]{receiver,activeBt,activeUsb});
 Check(usbMerged.Count==1&&usbMerged[0]==activeUsb,"wired USB wins over receiver and BLE");
 Check(BatteryManager.SelectSnapshot(new[]{receiver,activeBt})[0]==activeBt,"USB removed restores Bluetooth on same ID");
 var secondKeyboard=new Device{Id=DeviceNames.KeyboardIdentity(@"USB\VID_320F&PID_5088\another-unit"),Name="VGN V98Pro V2",Category="Keyboard",Connection="2.4G",ConfirmedConnected=true};
 var pair=BatteryManager.SelectSnapshot(new[]{receiver,activeBt,activeUsb,secondKeyboard});
 Check(pair.Count==2&&pair.Contains(activeUsb)&&pair.Contains(secondKeyboard),"same-model keyboard remains separate beside unified keyboard");
 var mouse1=new Device{Id=@"USB\VID_391D&PID_1A05\unit-one",Name="Dragonfly 3 Master+"};
 var mouse2=new Device{Id=@"USB\VID_391D&PID_1A05\unit-two",Name="Dragonfly 3 Master+"};
 Check(BatteryManager.SelectSnapshot(new[]{mouse1,mouse2}).Count==2,"identical mouse names and VID/PID retain separate instances");
 foreach(string category in new[]{"Mouse","Gamepad","Audio"}){
 string containerA=Guid.NewGuid().ToString(),containerB=Guid.NewGuid().ToString();
 var usbA=new Device{Id=category+":usb-a",ContainerId=containerA,Name="Same model",Category=category,Connection="USB",ConfirmedConnected=true,State=DeviceState.Online,Kind=BatteryKind.Unavailable};
 var btA=new Device{Id=category+":bt-a",ContainerId=containerA,Name="Different transport name",Category=category,Connection="蓝牙",ConfirmedConnected=true,State=DeviceState.Online,Kind=BatteryKind.ExactPercent,Percent=66};
 var btB=new Device{Id=category+":bt-b",ContainerId=containerB,Name="Same model",Category=category,Connection="蓝牙",ConfirmedConnected=true,State=DeviceState.Online,Kind=BatteryKind.ExactPercent,Percent=37};
 var both=BatteryManager.SelectSnapshot(new[]{usbA,btA,btB});
 Check(both.Count==2&&both.Contains(usbA)&&both.Contains(btB),category+" merge transport, preserve second unit");
 string stable=usbA.Id;var after=BatteryManager.SelectSnapshot(new[]{btA,btB});
 Check(after.Count==2&&btA.Id==stable&&btA.Percent==66&&btB.Percent==37,category+" transport switch retains row and independent battery");
 var unknown1=new Device{Id=category+":unknown-1",Name="Same model",Category=category};
 var unknown2=new Device{Id=category+":unknown-2",Name="Same model",Category=category};
 Check(BatteryManager.SelectSnapshot(new[]{unknown1,unknown2}).Count==2,category+" missing identity evidence cannot merge by name");
 }
 var classicLive=new System.Collections.Generic.HashSet<string>{"001122334455"};
 Check(BatteryManager.BluetoothIsConnected(new HidInterface{Identity=@"BTHENUM\DEV_001122334455\test",Product="same name"},classicLive,le),"classic Bluetooth address connection");
 Check(!BatteryManager.BluetoothIsConnected(new HidInterface{Identity=@"BTHENUM\DEV_AABBCCDDEEFF\test",Product="same name"},classicLive,le),"classic same-name offline device excluded");
 var audioContainer=Guid.NewGuid().ToString();
 var audioService=new HidInterface{Audio=true,Identity=@"BTHENUM\audio-service",ContainerId=audioContainer,Product="Headset Hands-Free"};
 var remoteHeadset=new HidInterface{Identity=@"BTHENUM\DEV_286F40D80549\test",ContainerId=audioContainer,Product="OPPO Enco Free3"};
 var batteryService=new HidInterface{Identity=@"BTHENUM\hands-free-ag",ContainerId=audioContainer,SystemBattery=90};
 var unrelatedBattery=new HidInterface{Identity="other",ContainerId=Guid.NewGuid().ToString(),SystemBattery=12};
 BatteryManager.ResolveAudioContainers(new System.Collections.Generic.List<HidInterface>{audioService},new[]{unrelatedBattery,remoteHeadset,batteryService});
 Check(audioService.Identity==remoteHeadset.Identity&&audioService.Product=="OPPO Enco Free3"&&audioService.Bluetooth&&audioService.SystemBattery==90,"audio uses sibling remote identity and battery within its container");
 var pairedOnly=new System.Collections.Generic.HashSet<string>();
 Check(!BatteryManager.BluetoothIsConnected(audioService,pairedOnly,le),"paired headset metadata cannot prove connected");
 pairedOnly.Add("286f40d80549");Check(BatteryManager.BluetoothIsConnected(audioService,pairedOnly,le),"connected headset resolves address from sibling");
 var missingContainer=new HidInterface{Audio=true,Identity="separate",Product="OPPO Enco Free3"};
 BatteryManager.ResolveAudioContainers(new System.Collections.Generic.List<HidInterface>{missingContainer},new[]{remoteHeadset,batteryService});
 Check(!missingContainer.Bluetooth&&!missingContainer.SystemBattery.HasValue,"same name without container cannot inherit headset battery");
 Check(AudioTypes.Classify(new[]{3},"USB Audio Device")==AudioKind.Headphones,"explicit headphones form overrides generic adapter name");
 Check(AudioTypes.Classify(new[]{3,5,1},"headset")==AudioKind.Headset,"headset and stereo endpoints choose headset");
 Check(AudioTypes.Classify(new[]{1},"External Speakers")==AudioKind.Speakers,"speakers endpoint");
 Check(AudioTypes.Classify(new[]{1},"USBAudio2.0")==AudioKind.SoundCard,"generic USB adapter is not its attached speakers");
 Check(AudioTypes.Classify(new[]{0},"Line interface")==AudioKind.SoundCard,"line level interface");
 Check(AudioTypes.Classify(new[]{8},"Digital interface")==AudioKind.SoundCard,"SPDIF interface");
 Check(AudioTypes.Classify(new[]{9},"Display")==AudioKind.Unknown,"display audio is generic");
 Check(AudioTypes.Classify(new[]{4},"Capture device")==AudioKind.Unknown,"microphone is not headphones");
 Check(AudioTypes.Classify(new int[0],"Unknown device")==AudioKind.Unknown,"no evidence uses generic audio");
 var endpointA=new HidInterface{ContainerId=audioContainer,Product="device",Audio=true};
 var endpointB=new HidInterface{ContainerId=Guid.NewGuid().ToString(),Product="device",Audio=true};
 AudioTypes.Apply(new[]{endpointA,endpointB},new[]{new AudioEndpointFact{ContainerId=audioContainer,FormFactor=3}});
 Check(endpointA.AudioType==AudioKind.Headphones&&endpointB.AudioType==AudioKind.Unknown,"endpoint type cannot leak across containers");
 Directory.CreateDirectory("docs/test-artifacts/audio-icons");foreach(AudioKind kind in Enum.GetValues(typeof(AudioKind))){using(var row=new DeviceRow(new Device{Name=AudioTypes.Label(kind),Category="Audio",AudioType=kind,Connection="USB"})){row.Size=new Size(440,76);using(var bitmap=new Bitmap(row.Width,row.Height)){row.DrawToBitmap(bitmap,row.ClientRectangle);bitmap.Save("docs/test-artifacts/audio-icons/"+kind+".png");}}}
 var d=new Device{Kind=BatteryKind.ExactPercent};foreach(int p in new[]{0,19,20,50,51,99,100}){d.Percent=p;var c=DeviceRow.BatteryColor(d);Check(p<20?c.R==210:p<=50?c.R==209:p<100?c.G==151:c.B==215,"threshold "+p);Check(d.Status==p+"%","exact status");}
 d.Kind=BatteryKind.DiscreteLevel;d.LevelLabel="电量充足";d.Level=4;d.LevelCount=4;Check(!d.Status.Contains("%"),"coarse precision");d.Kind=BatteryKind.ExternalPowerOnly;Check(d.Status=="—","external power");d.State=DeviceState.Disconnected;Check(d.Status=="未连接","offline");
 var k=new byte[64];k[0]=4;k[3]=0x1a;k[4]=6;k[8]=77;int percent;bool charging;Check(BatteryParsers.Keyboard(k,out percent,out charging)&&percent==77,"real keyboard response");k[3]=0xff;Check(!BatteryParsers.Keyboard(k,out percent,out charging),"reject FF response");k[3]=0x1a;k[8]=101;Check(!BatteryParsers.Keyboard(k,out percent,out charging),"reject >100");
 byte[] mouse={8,4,0,0,0,2,30,0,14,211,0,0,0,0,0,0,72};Check(BatteryParsers.Compx(mouse,out percent,out charging)&&percent==30&&!charging,"real mouse checksum response");mouse[16]++;Check(!BatteryParsers.Compx(mouse,out percent,out charging),"checksum corruption");mouse[16]--;mouse[5]=0;Check(!BatteryParsers.Compx(mouse,out percent,out charging),"invalid battery status");
 Check(!new VgnKeyboardProvider().CanHandle(new HidInterface{Vid=0x320f,Pid=0x5088,Page=0xff1c,Usage=0x92,Input=65,Output=64}),"HID length guard");Check(!new CompxMouseProvider().CanHandle(new HidInterface{Vid=0x1234,Pid=0x1a05,Page=0xff02,Usage=2,Input=17,Output=17}),"VID guard");
 Check(BatteryParsers.MouseVoltagePercent(3795,false)==39,"official voltage curve 3795mV");Check(BatteryParsers.MouseVoltagePercent(4110,false)==100,"upper endpoint");Check(BatteryParsers.MouseVoltagePercent(4110,true)==99,"charging cap");Check(BatteryParsers.MouseVoltagePercent(0,false)==-1,"invalid voltage");d.Kind=BatteryKind.EstimatedPercent;d.State=DeviceState.Online;d.Percent=39;Check(d.Status=="39%","estimated percent display");Directory.CreateDirectory("docs/test-artifacts/layout");foreach(float scale in new[]{1f,1.25f,1.5f}){using(var bitmap=new Bitmap((int)(760*scale),(int)(116*5*scale)))using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);var samples=new[]{new Device{Name="VGN V98Pro V2",Category="Keyboard",Connection="2.4G",Kind=BatteryKind.ExactPercent,Percent=77},new Device{Name="Dragonfly 3 Master+",Category="Mouse",Connection="2.4G",Kind=BatteryKind.ExactPercent,Percent=30},new Device{Name="飞智 冰原狼2",Category="Gamepad",Connection="2.4G",Kind=BatteryKind.DiscreteLevel,Level=2,LevelCount=4,LevelLabel="电量中等"},new Device{Name="无法读取电量的在线设备",Connection="USB"},new Device{Name="已登记的离线设备",Connection="2.4G",State=DeviceState.Disconnected}};for(int i=0;i<samples.Length;i++)using(var row=new DeviceRow(samples[i],scale)){row.Size=new Size((int)(760*scale),(int)(116*scale));using(var b=new Bitmap(row.Width,row.Height)){row.DrawToBitmap(b,row.ClientRectangle);g.DrawImageUnscaled(b,0,i*row.Height);}}bitmap.Save("docs/test-artifacts/layout/"+(int)(scale*100)+".png");}}
 Check(DeviceNames.Friendly(new HidInterface{Product="Controller (Flydigi Direwolf)",Vid=0x045e,Pid=0x028e,Page=1,Usage=5,Path="other"})=="飞智冰原狼手柄","do not guess generation");
 Check(DeviceNames.Friendly(new HidInterface{Product="Controller (Other Model)"})=="Other Model","clean wrapper");
 Check(!File.ReadAllText("src/Devices.cs").Contains("DeviceRegistry.Apply"),"no history restore");
 Console.WriteLine("PASS: real packet parsers, malformed/status/checksum rejection, precision, thresholds, power/offline states, HID guards; DPI layout renders 100/125/150 generated (test fixtures only).");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}


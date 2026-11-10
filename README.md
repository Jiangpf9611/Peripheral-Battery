# Peripheral Battery · 外设电量

轻量的 Windows 外设电量查看工具。打开即检测，点击刷新更新，关闭即退出。

当前版本：**V2.3.0** · Windows 10/11 x64 · C# / WinForms / .NET Framework 4.8

## 功能

- 自动发现当前可见的键盘、鼠标、手柄和 USB / 蓝牙音频外设。
- 通过 Windows 电池属性、已适配的 HID 协议与 XInput 读取可用电量。
- 区分精确百分比、估算百分比、原生电量档位和未知状态；档位不会换算成百分比。
- 根据可靠设备身份归并重复接口；刷新后移除已确认断开的设备。
- 区分耳机、耳麦、音箱、声卡及通用音频图标。
- 提供当前用户开机自启开关；支持高 DPI、自适应行数和列表滚动。

不需要联网，没有托盘常驻、后台设备轮询、遥测或自动更新，不保存设备历史和电量历史。

## 使用方法

1. 从 [V2.3.0 发布页](https://github.com/Jiangpf9611/Peripheral-Battery/releases/tag/v2.3.0) 下载 [PeriBatt.exe](https://github.com/Jiangpf9611/Peripheral-Battery/releases/download/v2.3.0/PeriBatt.exe)，或按下文从源码构建。
2. 将程序放在固定位置，双击打开；窗口启动及首次检测完成后在屏幕工作区居中，电量条直接显示读数。手动拖动后刷新保留位置。
3. 接入、唤醒或切换设备后，点击右上角刷新按钮重新检测。
4. 按需开启“开机自启”，它会在当前用户登录时正常打开窗口。移动 EXE 后，请重新设置自启路径。
5. 点击关闭按钮或按 `Alt+F4` 退出。

运行需要 Windows 10/11 x64 和 .NET Framework 4.8，不需要 Python 或厂商管理软件。程序使用普通用户权限；当前 EXE 未签名。

## 兼容范围与限制

发现设备与读取电量是两项独立能力。设备能显示名称，不代表该设备提供了可读取的电量协议。

- **键盘**：部分已适配型号支持 2.4G 电池查询；蓝牙通过 Windows 电池属性读取；USB 模式仍可能显示未知电量。
- **鼠标**：部分已适配型号支持严格接口白名单下的只读查询，使用电压曲线估算百分比。界面不加“约”字，但数据仍为估算，可能与官方软件的历史平滑值不同，充电时存在 99% 上限局限。
- **手柄**：通过 XInput 仅显示接口实际提供的原生档位。有线供电类型不代表满电；部分型号或连接模式可能无法读取电量。
- **蓝牙键鼠和音频设备**：取决于 Windows / 驱动是否暴露电量属性；系统读数可能有缓存。
- **音频分类**：取决于活动播放端点及容器信息。3.5mm 耳机可能只暴露声卡，无法识别耳机型号。

不承诺支持所有品牌和型号；尚未实现通用 Wi-Fi 电池协议、BLE GATT Battery Service 主动查询及更多厂商专用协议。2.4G 接收器存在不能证明无线本体在线；没有可靠共同身份时不会跨模式强行合并同型号设备。部分友好名称和跨模式映射来自已确认的具体设备指纹，不能视为通用型号支持。

未知、休眠或查询失败时显示中性状态，不用旧值冒充实时电量。红色为 0–19%，黄色为 20–50%，绿色为 51–99%；只有可信且新鲜的精确 100% 才使用满电动画。

## 从源码构建

准备 Windows x64 构建环境：

- .NET Framework 4.8 Developer Pack（包括参考程序集）。
- Windows 10/11 SDK（包括 `References`、`UnionMetadata`）。
- Python 3 与 [requirements-dev.txt](requirements-dev.txt) 中的 Pillow，用于生成图标。
- Windows PowerShell。

在仓库根目录执行：

```powershell
python -m pip install -r requirements-dev.txt
powershell -ExecutionPolicy Bypass -File ./build.ps1
```

输出：`dist/PeriBatt.exe`，运行资源均嵌入 EXE，无需携带源码或 Python。`dist/` 为本地构建输出，不提交到 Git。

构建脚本使用 Windows 自带的 Framework64 C# 编译器及标准安装路径下的 SDK；SDK 缺失时会报错。现有 WinRT 引用可能产生 CS1701 警告，需结合目标系统运行验证。

## 验证

```powershell
powershell -ExecutionPolicy Bypass -File ./tools/test.ps1
```

脚本重新构建，执行真实报文解析、异常回复拒绝、电量类型与阈值、HID 白名单保护及多行/DPI 布局测试。测试会生成 `docs/test-artifacts/` 夹具图片，这些不是硬件兼容证明。真实连接切换、充电、Windows 10 目标机及多显示器 DPI 切换仍需实机验证。

## 项目结构

```text
assets/                 正式 Logo 母版
src/                    应用源码、manifest 和嵌入资源
  App.cs                窗口、设备行、图标绘制、自启与入口
  Devices.cs            Windows 发现、数据模型、去重与调度
  Protocols.cs          HID 传输与设备协议
  AudioTypes.cs         Core Audio 类型识别
  DeviceRegistry.cs     无持久化的兼容占位
  assets/               多尺寸 EXE 图标、窗口图标、标题 Logo
tools/                 构建辅助与测试
docs/REQUIREMENTS.md     唯一完整需求规格
build.ps1               构建入口
requirements-dev.txt    图标生成依赖
```

## 参与开发

请先阅读 [需求规格](docs/REQUIREMENTS.md) 和 [贡献指南](CONTRIBUTING.md)。报告兼容问题时提供系统版本、设备准确型号、连接模式和实际现象；请隐藏序列号、蓝牙地址及完整本机设备路径。

新增协议须说明公开依据，匹配明确设备与报告结构，只允许已知只读电池查询。不要向未知设备发送探测命令，也不要加入固件、灯效、DPI 或键位写入功能。

## 许可证与致谢

本项目按 [GNU GPL v3.0](LICENSE) 授权，版权 © 2026 风车车。修改、分发及衍生作品的要求以许可证正文为准。

协议研究参考（不作为运行依赖）：[vgn-battery](https://github.com/yangyuuuuu/vgn-battery)、[atk-battery](https://github.com/GyroZeppel1/atk-battery)。当前 C# 实现独立编写；引入第三方代码前须核对其许可证并保留必要声明。

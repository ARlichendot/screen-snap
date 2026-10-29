# ScreenSnap

一个极简的 Windows 截图小工具：**托盘常驻、全局快捷键、框选即存**。

单文件 exe，无第三方依赖，不联网，不写注册表。用系统自带的 .NET Framework 编译，整个程序只有 30 多 KB。

## 功能

- **Ctrl + Alt + S** — 框选截图，拖出区域松手即保存
- **Ctrl + Alt + F** — 全屏截图
- 截图自动存到 `图片\ScreenSnap\`，文件名为 `ScreenSnap_年月日_时分秒.png`
- 截完自动复制到剪贴板，可以直接 Ctrl+V 粘贴
- 窗口/托盘气泡提示保存路径
- 托盘右键菜单：框选 / 全屏 / 打开截图文件夹 / 退出
- 快捷键被占用时自动换用备选组合（`Ctrl+Shift+S`、`Alt+Shift+S`），启动气泡会说明最终生效的是哪一个
- 单实例运行：重复启动不会堆出一堆托盘图标

## 使用

下载 Release 里的 `ScreenSnap.exe`，双击即可。程序没有主窗口，运行后在右下角托盘留下一个图标。

### 保存位置

默认：`C:\Users\<用户名>\Pictures\ScreenSnap\`

想换目录，设置环境变量 `SCREENSNAP_DIR` 指向目标文件夹即可。

### 退出

托盘右键 → 退出。

## 从源码编译

只需要 Windows 自带的 .NET Framework 4.x（Win10/11 默认就有），不需要安装 Visual Studio 或任何 SDK。

```powershell
git clone https://github.com/ARlichendot/screen-snap.git
cd screen-snap
.\build.ps1
```

编译产物是当前目录下的 `ScreenSnap.exe`。

`build.ps1` 内部就是一句调用：

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe `
  /target:winexe /platform:anycpu /optimize+ `
  /out:ScreenSnap.exe /win32icon:icon.ico `
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ScreenSnap.cs
```

## 目录结构

```
ScreenSnap.cs   全部源码，单文件（约 500 行，纯 WinForms + Win32 API）
icon.ico        程序图标
build.ps1       编译脚本
```

源代码里几个关键点：

- `ScreenGrab` — 屏幕抓取、保存、目录降级（`SCREENSNAP_DIR` → 图片 → 文档 → 本地应用数据）
- `RegionForm` — 框选覆盖层。先抓一张全屏位图做暗化底图，选区贴原图，实现「选区外变暗、选区内清晰」
- `HotkeyWindow` — 隐藏窗口 + `RegisterHotKey`，接收全局热键的 `WM_HOTKEY` 消息

## 隐私

- 不联网，没有任何网络请求
- 不上传、不收集任何数据
- 截图只写到本地磁盘

## 系统要求

Windows 10 / Windows 11。

## License

[MIT](LICENSE)

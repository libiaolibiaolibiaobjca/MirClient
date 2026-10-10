using System.Runtime.InteropServices;
using System.Text;

namespace MirClient.Runner.Platform;

/// <summary>
/// 原生系统文件与文件夹选择对话框：纯 P/Invoke Windows Shell API，专用 STA 线程隔离，绝不卡死主窗口
/// </summary>
public static class NativeFileDialog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    private const uint BIF_RETURNONLYFSDIRS   = 0x0001;
    private const uint BIF_NEWDIALOGSTYLE     = 0x0040;
    private const uint BIF_NONEWFOLDERBUTTON  = 0x0200;

    /// <summary>
    /// 在独立的 STA 线程中运行原生文件夹选择器，等待结果返回 (免死锁安全模式)
    /// </summary>
    public static string? SelectFolder(string title = "请用鼠标选择盛大热血传奇十周年客户端根目录")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine($"[平台提示] 当前非 Windows 系统，无法弹出原生文件夹选择器");
            return null;
        }

        string? result = null;
        Thread thread = new(() =>
        {
            result = SelectFolderInternal(title);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        return result;
    }

    /// <summary>
    /// 在独立的 STA 线程中异步唤起原生文件夹选择器，主窗口渲染循环绝不卡死
    /// </summary>
    public static void SelectFolderAsync(string title, Action<string?> onCompleted)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine($"[平台提示] 当前非 Windows 系统，无法弹出原生文件夹选择器");
            onCompleted(null);
            return;
        }

        Thread thread = new(() =>
        {
            string? path = SelectFolderInternal(title);
            onCompleted(path);
        });

        // 极其重要：Windows Shell COM 对话框必须在 STA 单元线程中运行，否则必死锁！
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    private static string? SelectFolderInternal(string title)
    {
        try
        {
            BROWSEINFO bi = new()
            {
                hwndOwner = IntPtr.Zero,
                pidlRoot = IntPtr.Zero,
                pszDisplayName = new string('\0', 260),
                lpszTitle = title,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE | BIF_NONEWFOLDERBUTTON
            };

            IntPtr pidl = SHBrowseForFolder(ref bi);
            if (pidl != IntPtr.Zero)
            {
                StringBuilder sb = new(260);
                if (SHGetPathFromIDList(pidl, sb))
                {
                    CoTaskMemFree(pidl);
                    string selected = sb.ToString();
                    return string.IsNullOrWhiteSpace(selected) ? null : selected;
                }
                CoTaskMemFree(pidl);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[对话框错误] 唤起文件夹对话框异常: {ex.Message}");
        }

        return null;
    }
}

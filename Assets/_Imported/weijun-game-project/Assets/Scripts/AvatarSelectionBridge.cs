using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class AvatarSelectionBridge
{
    public static void Request(MonoBehaviour receiver)
    {
        if (receiver == null)
        {
            return;
        }

#if UNITY_EDITOR
        SendSelectedFile(
            receiver,
            EditorUtility.OpenFilePanelWithFilters(
                "选择头像",
                string.Empty,
                new[] { "图片文件", "png,jpg,jpeg", "所有文件", "*" }));
#elif UNITY_WEBGL
        WJ_WeChatChooseAvatar(receiver.gameObject.name);
#elif UNITY_ANDROID
        RequestAndroidGallery(receiver);
#elif UNITY_IOS
        WJ_LocalAvatarChoose(receiver.gameObject.name);
#elif UNITY_STANDALONE_WIN
        SendSelectedFile(receiver, OpenWindowsFilePicker());
#elif UNITY_STANDALONE_OSX
        SendSelectedFile(receiver, OpenMacFilePicker());
#elif UNITY_STANDALONE_LINUX
        SendSelectedFile(receiver, OpenLinuxFilePicker());
#else
        SendFailure(receiver, "当前平台暂不支持系统图片选择");
#endif
    }

    private static void SendSelectedFile(MonoBehaviour receiver, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            SendFailure(receiver, "头像选择已取消");
            return;
        }

        AvatarFilePayload payload = new AvatarFilePayload
        {
            filePath = filePath,
            fileName = Path.GetFileName(filePath),
            mimeType = GetMimeType(filePath),
            temporaryFile = false
        };
        receiver.SendMessage("OnAvatarSelected", JsonUtility.ToJson(payload), SendMessageOptions.DontRequireReceiver);
    }

    private static void SendFailure(MonoBehaviour receiver, string message)
    {
        receiver.SendMessage("OnAvatarSelectionFailed", message ?? string.Empty, SendMessageOptions.DontRequireReceiver);
    }

    private static string GetMimeType(string filePath)
    {
        return string.Equals(Path.GetExtension(filePath), ".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void RequestAndroidGallery(MonoBehaviour receiver)
    {
        try
        {
            using (AndroidJavaClass picker = new AndroidJavaClass("com.weijun.avatar.AvatarPicker"))
            {
                picker.CallStatic("chooseImage", receiver.gameObject.name);
            }
        }
        catch (Exception exception)
        {
            SendFailure(receiver, "无法打开手机相册：" + exception.Message);
        }
    }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const int WindowsPickerBufferLength = 4096;
    private const int OpenFileMustExist = 0x00001000;
    private const int OpenPathMustExist = 0x00000800;
    private const int OpenNoChangeDirectory = 0x00000008;
    private const int OpenExplorer = 0x00080000;

    private static string OpenWindowsFilePicker()
    {
        IntPtr fileBuffer = Marshal.AllocHGlobal(WindowsPickerBufferLength * sizeof(char));
        try
        {
            for (int index = 0; index < WindowsPickerBufferLength; index++)
            {
                Marshal.WriteInt16(fileBuffer, index * sizeof(char), 0);
            }

            OpenFileName dialog = new OpenFileName
            {
                structureSize = Marshal.SizeOf(typeof(OpenFileName)),
                filter = "图片文件\0*.png;*.jpg;*.jpeg\0所有文件\0*.*\0\0",
                file = fileBuffer,
                maximumFileLength = WindowsPickerBufferLength,
                title = "选择头像",
                defaultExtension = "jpg",
                flags = OpenFileMustExist | OpenPathMustExist | OpenNoChangeDirectory | OpenExplorer
            };
            return GetOpenFileName(ref dialog) ? Marshal.PtrToStringUni(fileBuffer) : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int structureSize;
        public IntPtr ownerWindow;
        public IntPtr instance;
        [MarshalAs(UnmanagedType.LPWStr)] public string filter;
        public IntPtr customFilter;
        public int maximumCustomFilterLength;
        public int filterIndex;
        public IntPtr file;
        public int maximumFileLength;
        public IntPtr fileTitle;
        public int maximumFileTitleLength;
        [MarshalAs(UnmanagedType.LPWStr)] public string initialDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string defaultExtension;
        public IntPtr customData;
        public IntPtr hook;
        public IntPtr templateName;
        public IntPtr reservedPointer;
        public int reservedInteger;
        public int extendedFlags;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName dialog);
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private static string OpenMacFilePicker()
    {
        return RunPickerProcess(
            "/usr/bin/osascript",
            "-e \"POSIX path of (choose file with prompt \\\"选择头像\\\" of type {\\\"public.image\\\"})\"");
    }
#endif

#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
    private static string OpenLinuxFilePicker()
    {
        string selectedPath = RunPickerProcess(
            "/usr/bin/zenity",
            "--file-selection --title=\"选择头像\" --file-filter=\"图片 | *.png *.jpg *.jpeg\"");
        return selectedPath ?? RunPickerProcess(
            "/usr/bin/kdialog",
            "--getopenfilename . \"*.png *.jpg *.jpeg|图片文件\"");
    }
#endif

#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
    private static string RunPickerProcess(string executable, string arguments)
    {
        try
        {
            using (System.Diagnostics.Process process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? output.Trim() : string.Empty;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
#endif

    [Serializable]
    private class AvatarFilePayload
    {
        public string filePath;
        public string fileName;
        public string mimeType;
        public bool temporaryFile;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_WeChatChooseAvatar(string receiverName);
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_LocalAvatarChoose(string receiverName);
#endif
}

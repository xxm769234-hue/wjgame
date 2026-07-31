#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class AvatarIosBuildProcessor
{
    [PostProcessBuild(100)]
    public static void AddPhotoLibraryUsageDescription(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("NSPhotoLibraryUsageDescription", "用于从系统相册选择并更换游戏头像");
        plist.WriteToFile(plistPath);
    }
}
#endif

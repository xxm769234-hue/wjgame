using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StarterCharacterSelectionPreview
{
    [MenuItem("Tools/书屿梦行/预览初始选角界面")]
    public static void OpenPreview()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("选角界面预览", "请先打开 HomeScene 并进入 Play 模式，再打开此预览。当前场景不会自动切换。", "知道了");
            return;
        }
        HomePageUI home = Object.FindObjectOfType<HomePageUI>();
        if (home == null || SceneManager.GetActiveScene().name != "HomeScene")
        {
            EditorUtility.DisplayDialog("选角界面预览", "请在主页 HomeScene 中打开此预览。预览不会替你切换场景或提交角色选择。", "知道了");
            return;
        }
        StarterCharacterSelectionView view = Object.FindObjectOfType<StarterCharacterSelectionView>(true);
        if (view == null) view = StarterCharacterSelectionView.Create(home.transform, null);
        view.Show();
        Selection.activeGameObject = view.gameObject;
    }
}

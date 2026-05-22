// LoadingPanel.cs
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Threading.Tasks;

public class LoadingPanel : UIBase
{
    public Slider sliderProgress; // 💡 进度条 Slider
    public Text txtProgress;      // 💡 进度百分比文本
    public CanvasGroup canvasGroup; // 💡 面板的 CanvasGroup (用于淡出)
    
    protected override void OnShow()
    {
        base.OnShow();
        sliderProgress.value = 0;
        txtProgress.text = "正在唤醒圣光... 0%";
        canvasGroup.alpha = 1; // 恢复不透明

        // 💡 监听 CardMgr 的加载进度事件
        CardMgr.Instance.OnPreloadProgress += OnLoadingProgressUpdate;
    }
    
    private void OnLoadingProgressUpdate(float progress)
    {
        // 💡 核心技巧：用 DOTween 缓动进度条，手感会极其丝滑、不卡顿！
        sliderProgress.DOValue(progress, 0.1f);
        txtProgress.text = $"正在唤醒圣光... {(int)(progress * 100)}%";
    }
    
    // 💡 外部调用：加载完毕后，淡出并彻底释放内存
    public async Task FadeOutAndDestroy()
    {
        // 渐变淡出，播完后销毁
        await canvasGroup.DOFade(0, 0.5f).AsyncWaitForCompletion();
        UIMgr.Instance.DestroyPanel("LoadingPanel");
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 记得注销事件，杜绝内存泄漏！
        CardMgr.Instance.OnPreloadProgress -= OnLoadingProgressUpdate;
    }
    
}

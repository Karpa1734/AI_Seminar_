using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [Header("FPS Settings")]
    [SerializeField] private TextMeshProUGUI fpsText;
    private int frameCount = 0;
    private float prevTime = 0.0f;
    private float fps = 0.0f;

    [Header("Transparency Settings")]
    [SerializeField] private CanvasGroup uiCanvasGroup;   // FPS表示のCanvasGroup
    [SerializeField] private float fadeYThreshold = -3.0f; // 判定のしきい値
    [SerializeField] private float minAlpha = 0.3f;       // 半透明時のAlpha
    [SerializeField] private float fadeDuration = 0.5f;   // 変化にかかる時間

    [Header("Monitoring Targets")]
    [SerializeField] private Transform player1Transform; // 自機
    [SerializeField] private Transform player2Transform; // 敵

    void Start()
    {
        prevTime = Time.realtimeSinceStartup;
    }

    void Update()
    {
        // --- 1. FPSの計算と更新 (0.5秒ごと) ---
        UpdateFPSCalculation();

        // --- 2. 透明度の更新 (毎フレーム) ---
        UpdateUITransparency();
    }

    private void UpdateFPSCalculation()
    {
        frameCount++;
        float time = Time.realtimeSinceStartup - prevTime;

        if (time >= 0.5f)
        {
            fps = frameCount / time;
            if (fpsText != null)
            {
                fpsText.text = $"{fps:F1} FPS";
            }
            frameCount = 0;
            prevTime = Time.realtimeSinceStartup;
        }
    }

    private void UpdateUITransparency()
    {
        if (uiCanvasGroup == null) return;

        // 誰かがしきい値より下にいるか判定
        bool isAnyoneBelow = false;
        if (player1Transform != null && player1Transform.position.y < fadeYThreshold)
        {
            isAnyoneBelow = true;
        }
        if (!isAnyoneBelow && player2Transform != null && player2Transform.position.y < fadeYThreshold)
        {
            isAnyoneBelow = true;
        }

        // 目標の透明度を決定
        float targetAlpha = isAnyoneBelow ? minAlpha : 1.0f;

        // 指定秒数(fadeDuration)で変化する速度を計算
        float fadeSpeed = (1.0f - minAlpha) / Mathf.Max(fadeDuration, 0.01f);

        // 現在の値から目標値へ滑らかに変化
        uiCanvasGroup.alpha = Mathf.MoveTowards(uiCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}
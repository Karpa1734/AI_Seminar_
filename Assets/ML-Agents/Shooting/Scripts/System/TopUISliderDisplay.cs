using UnityEngine;

public class TopUISliderDisplay : MonoBehaviour
{
    [Header("Transparency Settings")]
    [SerializeField] private CanvasGroup uiCanvasGroup;   // スライダーのCanvasGroup
    [SerializeField] private float fadeYThreshold = 3.0f;  // ★ここより上に行くと透過
    [SerializeField] private float minAlpha = 0.3f;       // 半透明時のAlpha
    [SerializeField] private float fadeDuration = 0.5f;   // 変化にかかる時間

    [Header("Monitoring Targets")]
    [SerializeField] private Transform player1Transform;
    [SerializeField] private Transform player2Transform;

    void Update()
    {
        UpdateUITransparency();
    }

    private void UpdateUITransparency()
    {
        if (uiCanvasGroup == null) return;

        // 誰かがしきい値（上部）より上にいるか判定
        bool isAnyoneAbove = false;

        // playerY > fadeYThreshold (上方向の判定)
        if (player1Transform != null && player1Transform.position.y > fadeYThreshold)
        {
            isAnyoneAbove = true;
        }
        if (!isAnyoneAbove && player2Transform != null && player2Transform.position.y > fadeYThreshold)
        {
            isAnyoneAbove = true;
        }

        // 目標の透明度を決定
        float targetAlpha = isAnyoneAbove ? minAlpha : 1.0f;

        // 0.5秒かけて変化させる速度
        float fadeSpeed = (1.0f - minAlpha) / Mathf.Max(fadeDuration, 0.01f);

        // アルファ値を滑らかに変更
        uiCanvasGroup.alpha = Mathf.MoveTowards(uiCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}
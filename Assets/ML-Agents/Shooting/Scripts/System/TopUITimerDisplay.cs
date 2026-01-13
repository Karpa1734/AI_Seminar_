using UnityEngine;
using TMPro;

public class TopUITimerDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Header("Transparency Settings")]
    [SerializeField] private float fadeYThreshold = 3.0f;
    [SerializeField] private float minAlpha = 0.3f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Time Visual Effects")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.red;      // 10秒前
    [SerializeField] private Color criticalColor = new Color(0.8f, 0f, 0f); // 5秒前

    [Header("Bounce Settings")]
    [SerializeField] private float animationDuration = 0.2f; // 弾む時間の長さ
    [SerializeField] private float bounceAmount = 0.2f;      // 弾む大きさ

    [Header("Monitoring Targets")]
    [SerializeField] private Transform player1Transform;
    [SerializeField] private Transform player2Transform;

    private int lastIntTime = -1;
    private float shakeTimer = 0f;
    private float currentFloatTime;

    public void UpdateTimer(float currentTime)
    {
        currentFloatTime = currentTime;
        if (timerText == null) return;

        // 現在の表示秒数（切り上げ）
        int displayTime = Mathf.CeilToInt(currentTime);

        // --- 数字が変化した瞬間を検知 ---
        if (displayTime != lastIntTime)
        {
            // 10秒以下、かつ最初の初期化時以外ならアニメーションをトリガー
            if (lastIntTime != -1 && currentTime <= 10f)
            {
                shakeTimer = animationDuration;
            }

            lastIntTime = displayTime;

            // ★修正ポイント："00" を指定することで、1桁の時に頭に 0 がつきます
            timerText.text = displayTime.ToString("00");
        }

        // --- 色の制御（10秒以下で赤、5秒以下で濃い赤） ---
        if (currentTime <= 5f) timerText.color = criticalColor;
        else if (currentTime <= 10f) timerText.color = warningColor;
        else timerText.color = normalColor;
    }

    void Update()
    {
        UpdateUITransparency();
        UpdateBounceAnimation();
    }

    private void UpdateBounceAnimation()
    {
        if (timerText == null) return;

        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;

            // アニメーションの進捗 (0～1)
            float t = 1f - (shakeTimer / animationDuration);

            // 1周期分のサイン波で Squash & Stretch
            // 0 -> 1(最大) -> 0 と変化させる
            float curve = Mathf.Sin(t * Mathf.PI);

            float scaleX = 1.0f + (curve * bounceAmount);
            float scaleY = 1.0f - (curve * bounceAmount);

            timerText.transform.localScale = new Vector3(scaleX, scaleY, 1.0f);
        }
        else
        {
            // アニメーション終了時は等倍に戻す
            timerText.transform.localScale = Vector3.one;
        }
    }

    private void UpdateUITransparency()
    {
        if (uiCanvasGroup == null) return;
        bool isAnyoneAbove = false;
        if (player1Transform != null && player1Transform.position.y > fadeYThreshold) isAnyoneAbove = true;
        if (!isAnyoneAbove && player2Transform != null && player2Transform.position.y > fadeYThreshold) isAnyoneAbove = true;

        float targetAlpha = isAnyoneAbove ? minAlpha : 1.0f;
        float fadeSpeed = (1.0f - minAlpha) / Mathf.Max(fadeDuration, 0.01f);
        uiCanvasGroup.alpha = Mathf.MoveTowards(uiCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}
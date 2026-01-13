using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillRecastDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BulletSpawner targetSpawner; // リキャスト計算用

    [Header("Recast UI Elements")]
    [SerializeField] private Image[] skillFillImages = new Image[4];

    [Header("Transparency Settings (Player Near Bottom)")]
    [SerializeField] private CanvasGroup uiCanvasGroup;
    [SerializeField] private float fadeYThreshold = -3.0f;
    [SerializeField] private float minAlpha = 0.3f;
    [SerializeField] private float fadeDuration = 0.5f;// ★追加：タイマー表示用のTMP
    [SerializeField] private TextMeshProUGUI[] skillTimerTexts = new TextMeshProUGUI[4];

    [Header("Monitoring Targets")]
    [SerializeField] private Transform player1Transform; // プレイヤー1
    [SerializeField] private Transform player2Transform; // プレイヤー2
    void Update()
    {
        if (targetSpawner == null) return;

        // --- 1. リキャストゲージの更新 ---
        UpdateRecastGauges();

        // --- 2. 自機の位置による透過処理 ---
        UpdateUITransparency();
    }

    private void UpdateRecastGauges()
    {
        for (int i = 0; i < 4; i++)
        {
            // --- 1. ゲージ（FillAmount）の更新 ---
            float progress = targetSpawner.GetRecastProgress(i);
            if (skillFillImages[i] != null)
            {
                skillFillImages[i].fillAmount = progress;
            }

            // --- 2. カウントダウンテキストの更新 ---
            if (skillTimerTexts[i] != null)
            {
                float remainingTime = targetSpawner.GetRemainingRecastTime(i);

                // 残り時間が 0.01秒以上ある場合のみ表示
                if (remainingTime > 0.01f)
                {
                    // オブジェクトが非表示なら表示する
                    if (!skillTimerTexts[i].gameObject.activeSelf)
                    {
                        skillTimerTexts[i].gameObject.SetActive(true);
                    }

                    // 00.00s 形式で表示
                    skillTimerTexts[i].text = remainingTime.ToString("00.00") + "s";
                }
                else
                {
                    // リキャスト完了時はオブジェクトごと非表示にする
                    if (skillTimerTexts[i].gameObject.activeSelf)
                    {
                        skillTimerTexts[i].gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    private void UpdateUITransparency()
    {
        if (uiCanvasGroup == null) return;

        bool isAnyoneBelow = false;

        // プレイヤー1の判定
        if (player1Transform != null && player1Transform.position.y < fadeYThreshold)
        {
            isAnyoneBelow = true;
        }

        // プレイヤー2の判定（プレイヤー1が上でなくても、こちらが下ならtrueになる）
        if (!isAnyoneBelow && player2Transform != null && player2Transform.position.y < fadeYThreshold)
        {
            isAnyoneBelow = true;
        }

        // 目標のアルファ値を決定
        float targetAlpha = isAnyoneBelow ? minAlpha : 1.0f;

        // 0.5秒かけて変化させる
        float fadeSpeed = (1.0f - minAlpha) / Mathf.Max(fadeDuration, 0.01f);
        uiCanvasGroup.alpha = Mathf.MoveTowards(uiCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}
using UnityEngine;
using UnityEngine.UI;

public class PlayerRingRecast : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BulletSpawner spawner;

    [Header("Ring Settings")]
    [SerializeField] private Image[] ringImages = new Image[4]; // Z, X, C, V 用
    [SerializeField] private float fadeDuration = 0.5f;

    void Update()
    {
        if (spawner == null) return;

        for (int i = 0; i < ringImages.Length; i++)
        {
            if (ringImages[i] == null) continue;

            float progress = spawner.GetRecastProgress(i);

            // ゲージの進捗を更新
            ringImages[i].fillAmount = progress;

            // リキャストが終わっている（progress <= 0）なら非表示、そうでなければ表示
            // 自然に消えるように、少し透明度をいじっても良い
            bool isRecasting = progress > 0.01f;

            // リングの透明度（CanvasGroupなどを使わずに直接Colorで制御）
            Color c = ringImages[i].color;
            float targetAlpha = isRecasting ? 0.6f : 0f; // リキャスト中は少し薄く表示
            c.a = Mathf.MoveTowards(c.a, targetAlpha, (1.0f / fadeDuration) * Time.deltaTime);
            ringImages[i].color = c;
        }
    }
}
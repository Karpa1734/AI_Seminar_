using UnityEngine;
using UnityEngine.UI;

public class SkillRecastDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BulletSpawner targetSpawner; // 自分のチームのSpawnerをアサイン

    [Header("UI Elements")]
    [Tooltip("Z, X, C, V の順で明るい方のImageを4つ登録")]
    [SerializeField] private Image[] skillFillImages = new Image[4];

    void Update()
    {
        if (targetSpawner == null) return;

        for (int i = 0; i < 4; i++)
        {
            if (skillFillImages[i] == null) continue;

            // Spawnerの GetRecastProgress は「残り時間 / 最大時間」を返す (1.0 -> 0.0)
            // ユーザーの要望は「貯まるにつれて明るくなる」なので、
            // fillAmount は「1.0 - 進捗」にする
            float progress = targetSpawner.GetRecastProgress(i);

            // リキャスト中（progress > 0）は 0 から 1 へ向かって増える
            // リキャスト完了（progress = 0）なら 1 (全表示)
            skillFillImages[i].fillAmount = 1.0f - progress;
        }
    }
}
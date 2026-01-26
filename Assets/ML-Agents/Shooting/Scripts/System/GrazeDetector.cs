using UnityEngine;

public class GrazeDetector : MonoBehaviour
{
    private PlayerAgent playerAgent;

    void Start()
    {
        // 親オブジェクトについている PlayerAgent を取得
        playerAgent = GetComponentInParent<PlayerAgent>();

        if (playerAgent == null)
        {
            Debug.LogError("GrazeDetector: PlayerAgent が親オブジェクトに見つかりません。");
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        // 敵の弾（タグ：Enemy_Bullet）がこのコライダーに触れたか判定
        if (col.CompareTag("Enemy_Bullet"))
        {
            if (playerAgent != null)
            {
                // PlayerAgent のグレイズカウントを増やす
                playerAgent.RegisterGraze();

                // デバッグ用（動作確認ができたら消してOK）
                // Debug.Log("Graze! Count: " + playerAgent.GrazeCount);
            }

            // 【発展】ボス側にも「プレイヤーに惜しい弾を撃てた」という報酬を与える場合
            // BossAgent boss = Object.FindFirstObjectByType<BossAgent>();
            // if (boss != null) boss.AddReward(0.01f);
        }
    }
}
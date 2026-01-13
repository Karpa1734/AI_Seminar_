using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents; // ML-Agentsを使用している場合

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHp = 100f;
    private float currentHp;

    [Header("UI References")]
    [SerializeField] private Slider hpSlider; // 先ほど用意したライフバー

    private Agent agent; // 自機のAgent（ML-Agents用）

    void Awake()
    {
        agent = GetComponent<Agent>();
        ResetHp();
    }

    // 体力のリセット（ゲーム開始時やエピソード開始時に呼ぶ）
    public void ResetHp()
    {
        currentHp = maxHp;
        UpdateUI();
    }

    // ダメージ処理
    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        UpdateUI();

        if (currentHp <= 0)
        {
            OnDeath();
        }
    }

    private void UpdateUI()
    {
        if (hpSlider != null)
        {
            // Sliderの値を 0.0 ～ 1.0 に変換して反映
            hpSlider.value = currentHp / maxHp;
        }
    }

    private void OnDeath()
    {
        Debug.Log($"{gameObject.name} の体力が0になりました。");

        // ML-Agentsのステップ（エピソード）終了
        if (agent != null)
        {
            // 負けた側に報酬ペナルティを与えるなどの処理
            agent.AddReward(-1.0f);
            agent.EndEpisode();
        }

        // ゲーム全体のリセット処理（例：シーン再読み込みや座標リセット）
        // ここで相手側のAgentも EndEpisode() する必要があります
    }

    // 衝突判定
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 弾に当たったか判定（レイヤーやタグで区別）
        // 自分の撃った弾に当たらないよう注意（レイヤー設定が推奨）
        if (collision.CompareTag("Enemy_Bullet"))
        {
            EnemyBullet bullet = collision.GetComponent<EnemyBullet>();
            if (bullet != null)
            {
                // ダメージを適用
                TakeDamage(bullet.DamageValue);

                // 弾を消滅させる（エフェクトを出してプールに戻す）
                bullet.Deactivate();
            }
        }
    }
}
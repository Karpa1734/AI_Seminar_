using UnityEngine;

public class FadeOutEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private int frameCount = 0;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void FixedUpdate()
    {
        frameCount++;

        // 15フレーム目から透過を開始
        if (frameCount >= 15 && frameCount <= 21)
        {
            // 15〜21の範囲を0〜1の割合に変換
            float t = (float)(frameCount - 15) / (21 - 15);

            // 透明度を 1 → 0 へ
            Color color = sr.color;
            color.a = Mathf.Lerp(1f, 0f, t);
            sr.color = color;
        }

        // 21フレームを過ぎたらオブジェクトを削除
        if (frameCount > 21)
        {
            Destroy(gameObject);
        }
    }
}
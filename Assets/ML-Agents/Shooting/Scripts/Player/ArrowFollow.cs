using UnityEngine;

public class ArrowFollow : MonoBehaviour
{
    public Transform target; // 対戦相手のTransformをセット

    void Update()
    {
        if (target == null) return;

        // 1. 自分から相手への方向ベクトルを計算
        Vector3 direction = target.position - transform.position;

        // 2. 角度（ラジアン）を計算し、度数法（Degree）に変換
        // 2Dの場合、z軸（奥行き）を無視してxとyで計算することが多いです
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 3. 矢印の回転を更新
        // 矢印の「先端」が元々どの方向を向いているかによってオフセットを調整します
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
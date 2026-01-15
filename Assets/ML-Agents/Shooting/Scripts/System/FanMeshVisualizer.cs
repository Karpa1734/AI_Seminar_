using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FanMeshVisualizer : MonoBehaviour
{
    private Mesh mesh;
    [SerializeField] private Material fanMaterial;
    [SerializeField] private int segments = 20;

    // Awake を削除（または残してもOK）し、DrawFan内で初期化チェックを行う
    public void DrawFan(float centerAngle, float spreadAngle, float radius)
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "FanMesh";
            GetComponent<MeshFilter>().mesh = mesh;

            // 2Dで最前面に出すための設定
            var renderer = GetComponent<MeshRenderer>();
            renderer.sortingLayerName = "Middle"; // 存在するレイヤー名に合わせてください
            renderer.sortingOrder = 100;

            if (fanMaterial != null) renderer.material = fanMaterial;
        }

        gameObject.SetActive(true);

        int vertexCount = segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        float startAngle = centerAngle - (spreadAngle / 2f);
        float angleStep = spreadAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = (startAngle + (angleStep * i)) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle), 0) * radius;

            if (i < segments)
            {
                // 2Dで「表」に見える頂点の結び順
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        // ★これを追加：メッシュの向き（法線）を自動計算させる
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TorchFOV : MonoBehaviour
{
    [Header("Vision Settings")]
    public float viewRadius = 4.5f; // 타일 1.5칸 반경 (tileSize 3f 기준)
    public int rayCount = 360;      // 원형 퀄리티 (레이저 개수)
    public LayerMask obstacleMask;  // 벽(Wall) 레이어 지정

    private Mesh mesh;

    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        // 렌더러가 반드시 TorchVision 레이어에서만 보이도록 설정 (매우 중요)
        gameObject.layer = LayerMask.NameToLayer("TorchVision");
    }

    void LateUpdate()
    {
        DrawFOV();
    }

    void DrawFOV()
    {
        float angleStep = 360f / rayCount;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>(); // 그라데이션을 위한 버텍스 컬러

        vertices.Add(Vector3.zero); // 횃불 중심점
        colors.Add(Color.white);    // 중심은 완전한 시야(하얀색)

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = i * angleStep;
            Vector3 dir = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad));

            float hitDistance = viewRadius;
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, viewRadius, obstacleMask))
            {
                hitDistance = hit.distance;
            }

            vertices.Add(transform.InverseTransformPoint(transform.position + dir * hitDistance));

            // 끝부분 그라데이션: 거리에 비례해 하얀색에서 검은색으로 부드럽게 페이드 아웃
            float alpha = 1f - (hitDistance / viewRadius);
            colors.Add(new Color(alpha, alpha, alpha, 1f));

            if (i > 0)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i - 1);
            }
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();
    }
}
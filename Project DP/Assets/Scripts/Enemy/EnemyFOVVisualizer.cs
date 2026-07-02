using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EnemyFOVVisualizer : MonoBehaviour
{
    public EnemyController enemy; // 부모의 EnemyController 연결

    [Header("시각화 설정")]
    public float meshResolution = 1f; // 해상도 (높을수록 곡선이 부드러워짐, 1도당 레이캐스트 수)
    public Vector3 offset = new Vector3(0, 0.2f, 0); // 바닥에 살짝 띄우기 위한 오프셋

    private Mesh viewMesh;
    private MeshFilter meshFilter;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        viewMesh = new Mesh();
        viewMesh.name = "View Mesh";
        meshFilter.mesh = viewMesh;
    }

    private void LateUpdate()
    {
        if (enemy == null) return;
        DrawFieldOfView();
    }

    private void DrawFieldOfView()
    {
        int stepCount = Mathf.RoundToInt(enemy.viewAngle * meshResolution);
        float stepAngleSize = enemy.viewAngle / stepCount;

        // 원점 + 외곽선 정점 수
        Vector3[] vertices = new Vector3[stepCount + 2];
        int[] triangles = new int[stepCount * 3];

        vertices[0] = offset; // 부채꼴의 중심 (몬스터 위치)

        for (int i = 0; i <= stepCount; i++)
        {
            // angle은 이미 enemy의 글로벌 회전값을 포함하고 있으므로
            float angle = enemy.transform.eulerAngles.y - enemy.viewAngle / 2 + stepAngleSize * i;

            // angleIsGlobal 파라미터를 true로 넘겨주어야 이중 회전이 발생하지 않습니다!
            Vector3 dir = DirFromAngle(angle, true);

            // 레이캐스트로 장애물 판별
            Vector3 startPos = enemy.transform.position + offset;
            RaycastHit hit;
            if (Physics.Raycast(startPos, dir, out hit, enemy.detectionRadius, enemy.obstacleLayer))
            {
                // 장애물에 닿으면 닿은 곳까지만 정점 찍기 (로컬 좌표계로 변환)
                vertices[i + 1] = transform.InverseTransformPoint(hit.point);
            }
            else
            {
                // 안 닿으면 최대 사거리까지 찍기
                vertices[i + 1] = transform.InverseTransformPoint(startPos + dir * enemy.detectionRadius);
            }

            // 삼각형 구성
            if (i < stepCount)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }

        viewMesh.Clear();
        viewMesh.vertices = vertices;
        viewMesh.triangles = triangles;
        viewMesh.RecalculateNormals();
    }

    // 각도를 방향 벡터로 변환하는 유틸리티 함수
    private Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            // 만약 로컬 각도라면 몬스터의 Y축 회전값을 더해줍니다.
            angleInDegrees += enemy.transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}
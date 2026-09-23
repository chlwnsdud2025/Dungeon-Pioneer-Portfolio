using UnityEngine;

/// <summary>
/// NPC 한 종류의 정의. 같은 종류의 NPC 여러 명이 이 애셋 하나를 공유합니다.
///
/// '종류'를 클래스 상속이 아니라 데이터로 두는 이유: MerchantNpc : NpcActor 식으로 늘리면
/// '움직이는 상인'에서 바로 다중 상속 문제가 생깁니다. 행동은 브레인 컴포넌트로,
/// 능력은 모듈 컴포넌트로, 수치는 이 애셋으로 조합합니다.
///
/// 레이어 마스크는 여기 두지 않습니다. 레이어는 프로젝트 설정에 묶인 값이라
/// 애셋이 레이어 번호에 종속되기 때문입니다. (NpcActor의 인스펙터에 남겨둡니다)
/// </summary>
[CreateAssetMenu(fileName = "Npc_", menuName = "NPC/Definition")]
public class NpcDefinition : ScriptableObject
{
    [Header("표시")]
    public string displayName = "주민";

    [Header("진영")]
    public FactionId faction = FactionId.Town;

    [Header("스탯")]
    [Min(1f)] public float maxHealth = 20f;
    [Min(0f)] public float moveSpeed = 2f;
    [Min(0f)] public float turnSpeed = 540f;

    [Header("지각")]
    [Min(0f)] public float detectionRadius = 7f;
    [Range(0f, 360f)] public float viewAngle = 120f;
    [Tooltip("시야선(Raycast)을 쏘는 눈높이. 피격 판정이 겨누는 지점이기도 합니다.")]
    [Min(0f)] public float eyeHeight = 1.5f;

    [Header("길찾기 (던전에서만 의미 있음)")]
    [Tooltip("바닥함정 셀의 이동 비용. 1이면 일반 바닥과 같게 취급해 최단 경로로 밟고 지나갑니다.")]
    [Min(0f)] public float trapPathCost = 1f;
    [Tooltip("구덩이에서 인접 셀로 기어오르는 데 걸리는 시간(초)")]
    [Min(0f)] public float pitClimbDuration = 0.6f;
}

/// <summary>
/// 액터가 속한 진영.
///
/// Neutral을 0번에 둔 이유: 인스펙터에서 진영 지정을 잊어도 기본값이 '아무와도 싸우지 않음'이 됩니다.
/// 실수의 결과가 조용한 무해함이 되도록 한 배치입니다 — 0번이 Monster였다면
/// 진영을 안 정한 마을 사람이 플레이어를 공격했을 겁니다.
/// </summary>
public enum FactionId : byte
{
    Neutral = 0,
    Player,
    Monster,
    Delver,   // 던전을 탐험하는 NPC 모험가. 플레이어의 경쟁자이자 잠재적 동료.
    Town,     // 마을 주민·상인·경비병
}

/// <summary>
/// 한 진영이 다른 진영을 보는 태도.
///
/// 공격·탐지의 근거가 되는 것은 Hostile 하나뿐이고, Neutral과 Friendly는 둘 다 '공격하지 않음'입니다.
/// 그럼에도 둘을 나눈 이유는 '도울 것인가'(합류·치유·경고 전파)를 구분해야 하기 때문입니다.
/// Phase 1에서는 Hostile만 실제로 쓰이고, Friendly는 파티 시스템에서 의미를 갖습니다.
/// </summary>
public enum Stance : byte
{
    Neutral = 0,
    Friendly,
    Hostile,
}

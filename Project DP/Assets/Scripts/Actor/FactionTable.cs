using System;
using UnityEngine;

/// <summary>
/// 진영 관계를 답하는 단일 창구. 공격 판정·탐지·합류 판단이 모두 여기를 거칩니다.
///
/// 기본 관계표는 대칭입니다 — 진영 단위로 'A는 B를 적대하는데 B는 A를 적대하지 않는' 관계를
/// 허용하면 일관성이 깨지고 버그가 숨습니다. 비대칭이 필요한 경우(내가 먼저 때린 NPC만
/// 나를 미워함)는 진영이 아니라 개체 단위 예외(FactionOverride)로 표현합니다.
/// </summary>
public static class FactionTable
{
    private static readonly int Count = Enum.GetValues(typeof(FactionId)).Length;

    // 기본 관계표. 대각선(자기 진영)은 Friendly이고, 표 전체가 대칭이어야 합니다.
    //
    // Delver가 Player에게 Neutral인 이유: NPC 모험가는 경쟁자지만 처음부터 칼을 뽑지는 않습니다.
    // 적대로 돌아서는 것을 플레이어의 행동에 따른 개체 예외로 처리하는 편이,
    // 처음부터 Hostile로 두고 예외로 진정시키는 것보다 다루기 쉽습니다.
    //
    //                   Neutral          Player           Monster          Delver           Town
    private static readonly Stance[,] Defaults =
    {
        /* Neutral */ { Stance.Neutral,  Stance.Neutral,  Stance.Neutral,  Stance.Neutral,  Stance.Neutral  },
        /* Player  */ { Stance.Neutral,  Stance.Friendly, Stance.Hostile,  Stance.Neutral,  Stance.Friendly },
        /* Monster */ { Stance.Neutral,  Stance.Hostile,  Stance.Friendly, Stance.Hostile,  Stance.Hostile  },
        /* Delver  */ { Stance.Neutral,  Stance.Neutral,  Stance.Hostile,  Stance.Friendly, Stance.Neutral  },
        /* Town    */ { Stance.Neutral,  Stance.Friendly, Stance.Hostile,  Stance.Neutral,  Stance.Friendly },
    };

    private static Stance[,] current;

    private static Stance[,] Current
    {
        get
        {
            if (current == null) ResetToDefaults();
            return current;
        }
    }

    /// <summary>
    /// 관계표를 기본값으로 되돌립니다.
    ///
    /// 플레이 모드 진입마다 자동 호출됩니다. static 필드는 'Enter Play Mode Options'로
    /// 도메인 리로드를 끄면 플레이 종료 후에도 살아남아, 지난 플레이에서 바꾼 관계가
    /// 다음 플레이로 새어 들어갑니다. 재현이 안 되는 버그의 단골 원인입니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetToDefaults()
    {
        current = new Stance[Count, Count];
        for (int a = 0; a < Count; a++)
            for (int b = 0; b < Count; b++)
                current[a, b] = Defaults[a, b];

#if UNITY_EDITOR
        VerifySymmetry();
#endif
    }

    /// <summary>진영 대 진영의 기본 관계. 개체 예외는 보지 않습니다.</summary>
    public static Stance Get(FactionId a, FactionId b) => Current[(int)a, (int)b];

    /// <summary>
    /// 진영 관계를 런타임에 바꿉니다. 대칭이므로 양쪽이 함께 갱신됩니다.
    /// (퀘스트 실패로 마을 전체가 플레이어를 적대하게 되는 경우 등)
    ///
    /// 이 변경은 자동으로 되돌아가지 않습니다 — 진영 관계는 런이 아니라 세이브에 속하는
    /// 영속 상태라고 보기 때문입니다. 새 게임을 시작할 때만 ResetToDefaults를 부르세요.
    /// </summary>
    public static void SetRelation(FactionId a, FactionId b, Stance stance)
    {
        Current[(int)a, (int)b] = stance;
        Current[(int)b, (int)a] = stance;
    }

    /// <summary>
    /// viewer가 subject를 어떻게 보는지. 방향이 있습니다 —
    /// viewer의 개체 예외가 먼저 적용되므로 GetStance(a,b)와 GetStance(b,a)가 다를 수 있습니다.
    /// </summary>
    public static Stance GetStance(IFactionMember viewer, IFactionMember subject)
    {
        if (viewer == null || subject == null) return Stance.Neutral;
        if (ReferenceEquals(viewer, subject)) return Stance.Friendly;   // 자해 방지

        FactionOverride overrides = viewer.Overrides;
        if (overrides != null && overrides.TryGetStanceToward(subject, out Stance overridden))
            return overridden;

        return Get(viewer.Faction, subject.Faction);
    }

    /// <summary>viewer가 subject를 공격할 근거가 있는지. 공격·탐지 판정은 전부 이 함수를 씁니다.</summary>
    public static bool IsHostile(IFactionMember viewer, IFactionMember subject)
        => GetStance(viewer, subject) == Stance.Hostile;

#if UNITY_EDITOR
    /// <summary>기본표가 대칭인지 검사합니다. 표를 손으로 고치다 한 칸만 틀리는 실수를 잡습니다.</summary>
    private static void VerifySymmetry()
    {
        for (int a = 0; a < Count; a++)
            for (int b = a + 1; b < Count; b++)
                if (Defaults[a, b] != Defaults[b, a])
                    Debug.LogError($"[FactionTable] 기본 관계표가 비대칭입니다: " +
                                   $"{(FactionId)a}→{(FactionId)b}={Defaults[a, b]}, " +
                                   $"{(FactionId)b}→{(FactionId)a}={Defaults[b, a]}");
    }
#endif
}

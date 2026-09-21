using UnityEngine;
using URAND = UnityEngine.Random;

// 테스트용 블럭 셋업 기능. 느리니 나중에 VSH로 수정
public class TEST_DUNGEONBLOCK_SETUP : MonoBehaviour
{
    private void Start()
    {
        Vector3 seedCursor =
            new Vector3(URAND.Range(0, 3) * 90f, URAND.Range(0, 3) * 90f, URAND.Range(0, 3) * 90f);
        transform.eulerAngles = seedCursor;
    }
}

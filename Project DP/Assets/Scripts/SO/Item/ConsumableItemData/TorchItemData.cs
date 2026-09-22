using UnityEngine;

[CreateAssetMenu(fileName = "Torch_", menuName = "Item/Consumable/Torch")]
public class TorchItemData : ConsumableItemData
{
    public override bool CanUse(PlayerController user) => user != null && user.CanPlaceTorchNow();

    public override void Use(PlayerController user)
    {
        // 실제 설치와 모션은 상태가 담당합니다. 개수 차감은 Inventory.UseItem이 이어서 처리합니다.
        user.stateMachine.ChangeState<PlaceTorchState_Player>();
    }
}

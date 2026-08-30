using UnityEngine;

/// <summary>
/// 벽에 설치하는 횃불. 예전에는 PlayerController.torchCount 숫자로 들고 다녔지만,
/// 이제 인벤토리에 들어가는 소모품이라 설치할 때마다 재고에서 1개가 빠집니다.
///
/// 설치는 '벽을 바라보고 있을 때'만 되므로 CanUse로 상황을 검사합니다.
/// CanUse가 false면 Inventory.UseItem이 개수를 차감하지 않으므로, 퀵슬롯에 등록해두고
/// 아무 데서나 눌러도 횃불이 그냥 사라지는 일은 없습니다.
/// </summary>
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

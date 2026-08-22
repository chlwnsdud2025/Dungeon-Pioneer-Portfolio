using UnityEngine;

[CreateAssetMenu(fileName = "Usable_", menuName = "Item/Consumable/Usable")]
public class UsableItemData : ConsumableItemData
{
    [Header("사용템(폭탄 등)")] public GameObject spawnPrefab; public float value;
    public override void Use(PlayerController user)
    {
        if (spawnPrefab != null)
            Instantiate(spawnPrefab, user.transform.position + user.transform.forward, user.transform.rotation);
    }
}
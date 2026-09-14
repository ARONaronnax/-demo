using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Demo/Weapon Data")]
    public class WeaponData : ItemData
    {
        [Header("战斗")]
        public int damage;

        [Header("模型")]
        [Tooltip("手持模型与掉落物模型共用同一个 prefab")]
        public GameObject weaponPrefab;

        [Header("挂到 WeaponSocket 时的本地变换")]
        public Vector3 socketLocalPosition;
        public Vector3 socketLocalEuler;
        public Vector3 socketLocalScale = Vector3.one;

        private void OnValidate()
        {
            itemType = ItemType.Weapon;
        }
    }
}

using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Interaction;

namespace Demo.Drop
{
    /// <summary>
    /// 订阅 EnemyDiedEvent 并生成掉落物。
    /// 怪物本身不知道本类的存在。
    /// 掉落物的碰撞体与 DroppedItem 组件全部在运行时添加，
    /// 不需要用户手工制作掉落物 prefab。
    /// </summary>
    public class DropSpawner : MonoBehaviour
    {
        [SerializeField] private float spawnHeight = 0.5f;
        [SerializeField] private float colliderPadding = 1.2f;

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            if (e.Drop == null || e.Drop.weaponPrefab == null)
            {
                return;
            }

            Spawn(e.Drop, e.Position);
        }

        private void Spawn(WeaponData weapon, Vector3 position)
        {
            GameObject instance = Instantiate(
                weapon.weaponPrefab,
                position + Vector3.up * spawnHeight,
                Quaternion.identity);

            instance.name = "Drop_" + weapon.displayName;

            // 剥掉模型自带的碰撞体，避免与交互查询互相干扰。
            Collider[] existing = instance.GetComponentsInChildren<Collider>();

            for (int i = 0; i < existing.Length; i++)
            {
                Destroy(existing[i]);
            }

            var dropped = instance.AddComponent<DroppedItem>();
            dropped.Setup(weapon, 1);

            // 补一个供交互查询命中的碰撞体。
            var box = instance.AddComponent<BoxCollider>();
            Bounds bounds = CalculateBounds(instance);

            box.center = instance.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size * colliderPadding;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}

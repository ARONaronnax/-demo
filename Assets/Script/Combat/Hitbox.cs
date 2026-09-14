using System.Collections.Generic;
using UnityEngine;
using Demo.Core;

namespace Demo.Combat
{
    public class Hitbox : MonoBehaviour
    {
        [Tooltip("非空则使用 PlayerStats.FinalDamage，否则使用 flatDamage")]
        [SerializeField] private Player.PlayerStats stats;
        [SerializeField] private float flatDamage = 10f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField, Min(0f)] private float radius = 0.8f;
        [SerializeField] private Vector3 localOffset = Vector3.zero;

        private readonly Collider[] _buffer = new Collider[16];
        private readonly HashSet<IDamageable> _hitThisWindow = new HashSet<IDamageable>();

        private bool _windowOpen;
        private bool _knockdown;
        private float _damageMultiplier = 1f;

        public void SetKnockdown(bool value)
        {
            _knockdown = value;
        }

        public void OpenWindow(float multiplier = 1f)
        {
            _damageMultiplier = multiplier;
            _hitThisWindow.Clear();
            _windowOpen = true;
        }

        public void CloseWindow()
        {
            _windowOpen = false;
            _hitThisWindow.Clear();
            _knockdown = false;
            _damageMultiplier = 1f;
        }

        private void OnDisable()
        {
            CloseWindow();
        }

        private void Update()
        {
            if (!_windowOpen)
            {
                return;
            }

            Transform hitboxTransform = transform;
            Transform ownerRoot = hitboxTransform.root;
            Vector3 center = hitboxTransform.TransformPoint(localOffset);
            float damage = ResolveDamage();

            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _buffer,
                targetLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _buffer[i];

                if (col == null)
                {
                    continue;
                }

                IDamageable target = col.GetComponentInParent<IDamageable>();

                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                Transform targetTransform = target.Transform;

                if (targetTransform == null ||
                    targetTransform == ownerRoot ||
                    targetTransform == hitboxTransform)
                {
                    continue;
                }

                if (!_hitThisWindow.Add(target))
                {
                    continue;
                }

                Vector3 hitPoint = col.ClosestPoint(center);
                Vector3 direction = (targetTransform.position - center).normalized;
                var info = new DamageInfo(
                    damage,
                    gameObject,
                    hitPoint,
                    direction,
                    _knockdown);

                target.TakeDamage(info);
            }
        }

        private float ResolveDamage()
        {
            float baseDamage = stats != null ? stats.FinalDamage : flatDamage;
            return baseDamage * _damageMultiplier;
        }
    }
}

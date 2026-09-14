using UnityEngine;
using Demo.Core;
using Demo.Interaction;

namespace Demo.Player
{
    /// <summary>
    /// 每帧在玩家周围做球形重叠查询，取最近的可交互对象。
    /// 不使用 Layer / Tag 过滤，靠 IInteractable 组件判定。
    /// </summary>
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private PlayerController controller;

        private readonly Collider[] _buffer = new Collider[32];
        private IInteractable _current;

        private void Reset()
        {
            controller = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            SetCurrent(null);
        }

        private void Update()
        {
            if (InputLock.IsLocked || (controller != null && controller.IsDead))
            {
                SetCurrent(null);
                return;
            }

            IInteractable found = FindNearest();
            SetCurrent(found);

            if (found != null && Input.GetKeyDown(interactKey))
            {
                if (found.CanInteract(gameObject))
                {
                    found.Interact(gameObject);
                }
            }
        }

        private IInteractable FindNearest()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                _buffer);

            IInteractable best = null;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _buffer[i];

                if (col == null)
                {
                    continue;
                }

                IInteractable candidate = col.GetComponentInParent<IInteractable>();

                if (candidate == null)
                {
                    continue;
                }

                if (!candidate.CanInteract(gameObject))
                {
                    continue;
                }

                float sqrDistance =
                    (candidate.Transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = candidate;
                }
            }

            return best;
        }

        private void SetCurrent(IInteractable next)
        {
            if (ReferenceEquals(_current, next))
            {
                return;
            }

            _current = next;

            if (next == null)
            {
                EventBus.Publish(new InteractionPromptChangedEvent(string.Empty, false));
            }
            else
            {
                EventBus.Publish(new InteractionPromptChangedEvent(next.PromptText, true));
            }
        }
    }
}

using UnityEngine;
using Demo.Core;
using Demo.Interaction;

namespace Demo.Player
{
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float radius = 2.5f;
        [SerializeField] private LayerMask interactableLayers = ~0;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private PlayerController controller;

        private readonly Collider[] _buffer = new Collider[32];
        private IInteractable _current;
        private GameObject _actor;
        private Transform _cachedTransform;

        private void Reset()
        {
            controller = GetComponent<PlayerController>();
        }

        private void Awake()
        {
            _actor = gameObject;
            _cachedTransform = transform;

            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }
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

            if (found != null &&
                Input.GetKeyDown(interactKey) &&
                found.CanInteract(_actor))
            {
                found.Interact(_actor);
            }
        }

        private IInteractable FindNearest()
        {
            Vector3 origin = _cachedTransform.position;
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                radius,
                _buffer,
                interactableLayers,
                QueryTriggerInteraction.Collide);

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

                if (candidate == null || !candidate.CanInteract(_actor))
                {
                    continue;
                }

                Transform candidateTransform = candidate.Transform;

                if (candidateTransform == null)
                {
                    continue;
                }

                float sqrDistance = (candidateTransform.position - origin).sqrMagnitude;

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
            bool isVisible = next != null;
            string prompt = isVisible ? next.PromptText : string.Empty;
            EventBus.Publish(new InteractionPromptChangedEvent(prompt, isVisible));
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Demo.Core;
using Demo.Data;
using Demo.Equipment;

namespace Demo.UI
{
    /// <summary>
    /// 背包中的角色展示窗口。运行时复制玩家的纯外观到隔离舞台，
    /// 通过 RenderTexture 显示；不读取或修改背包、角色移动与战斗逻辑。
    /// </summary>
    public sealed class InventoryCharacterPreview : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private RawImage previewImage;
        [SerializeField] private Transform playerSource;
        [SerializeField] private EquipmentComponent equipment;
        [SerializeField, Min(64)] private int textureSize = 512;
        [SerializeField] private float dragSensitivity = 0.45f;
        [SerializeField] private Vector3 modelEulerOffset = new Vector3(0f, 180f, 0f);
        [SerializeField, Range(0.8f, 2.5f)] private float framing = 1.25f;

        private const int PreviewLayer = 5; // Unity 内置 UI Layer，仅用于隔离预览相机。
        private static readonly Vector3 StagePosition = new Vector3(10000f, 10000f, 10000f);

        private RenderTexture _texture;
        private Camera _camera;
        private Transform _model;
        private Transform _previewWeaponSocket;
        private Vector2 _lastPointer;
        private bool _listening;

        public void Bind(RawImage image, Transform source, EquipmentComponent equipmentSource)
        {
            previewImage = image;
            playerSource = source;
            equipment = equipmentSource;
        }

        private void OnEnable()
        {
            Listen();
            EnsurePreview();
            SyncEquippedWeapon();
            if (_camera != null) _camera.enabled = true;
        }

        private void OnDisable()
        {
            StopListening();
            if (_camera != null) _camera.enabled = false;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            if (_camera != null) Destroy(_camera.gameObject);
            if (_model != null) Destroy(_model.gameObject);
        }

        private void Listen()
        {
            if (_listening) return;
            _listening = true;
            EventBus.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);
        }

        private void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            EventBus.Unsubscribe<WeaponEquippedEvent>(OnWeaponEquipped);
        }

        private void OnWeaponEquipped(WeaponEquippedEvent e)
        {
            ShowWeapon(e.Weapon);
        }

        private void SyncEquippedWeapon()
        {
            if (_model != null && equipment != null) ShowWeapon(equipment.MainHand);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _lastPointer = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_model == null) return;

            float delta = eventData.position.x - _lastPointer.x;
            _lastPointer = eventData.position;
            _model.Rotate(Vector3.up, -delta * dragSensitivity, Space.World);
        }

        private void EnsurePreview()
        {
            if (_camera != null || previewImage == null || playerSource == null) return;

            _texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "InventoryCharacterPreviewRT",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear
            };
            _texture.Create();
            previewImage.texture = _texture;

            GameObject modelObject = Instantiate(playerSource.gameObject);
            modelObject.name = "InventoryPreviewModel";
            _model = modelObject.transform;
            _model.position = StagePosition;
            _model.rotation = Quaternion.Euler(modelEulerOffset);
            StripGameplayComponents(modelObject);
            SetLayerRecursively(modelObject, PreviewLayer);
            _previewWeaponSocket = FindChildByName(_model, "weapon_r");

            Renderer[] renderers = modelObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            GameObject cameraObject = new GameObject("InventoryPreviewCamera");
            cameraObject.layer = PreviewLayer;
            _camera = cameraObject.AddComponent<Camera>();
            _camera.targetTexture = _texture;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * framing;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 50f;
            _camera.transform.position = bounds.center + new Vector3(0f, bounds.extents.y * 0.05f, 8f);
            _camera.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.05f);

            GameObject lightObject = new GameObject("InventoryPreviewLight");
            lightObject.transform.SetParent(cameraObject.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.cullingMask = 1 << PreviewLayer;
        }

        private void ShowWeapon(WeaponData weapon)
        {
            if (_previewWeaponSocket == null) return;

            for (int i = _previewWeaponSocket.childCount - 1; i >= 0; i--)
            {
                GameObject child = _previewWeaponSocket.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            if (weapon == null || weapon.weaponPrefab == null) return;

            GameObject held = Instantiate(weapon.weaponPrefab, _previewWeaponSocket, false);
            held.name = "PreviewHeld_" + weapon.displayName;
            held.transform.localPosition = weapon.socketLocalPosition;
            held.transform.localEulerAngles = weapon.socketLocalEuler;
            held.transform.localScale = weapon.socketLocalScale;
            SetLayerRecursively(held, PreviewLayer);
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == childName) return all[i];
            return null;
        }

        private static void StripGameplayComponents(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++) bodies[i].isKinematic = true;

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = true;
                animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;
                animators[i].applyRootMotion = false;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}

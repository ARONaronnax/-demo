## Task 15: DemoSceneBuilder —— 搭建 Demo 场景

生成可运行的场景。**这一步无法自动化验证**，必须靠 Task 16 的手动验收。

**Files:**
- Create: `Assets/Script/Editor/DemoSceneBuilder.cs`

**Interfaces:**
- Consumes: `Demo.EditorTools.HeroAnimatorBuilder`, `Demo.AnimationClipLocator`
- Produces: `Demo.EditorTools.DemoSceneBuilder.Build()`

> **前置条件**：Task 4–8 必须已执行过，`Assets/Animator/Hero_SwordAndShield.controller` 必须已存在。

- [ ] **Step 1: 实现 DemoSceneBuilder**

`Assets/Script/Editor/DemoSceneBuilder.cs`：

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cinemachine;
using Demo;

namespace Demo.EditorTools
{
    /// <summary>
    /// 生成 Demo_Combat 场景。
    ///
    /// 为什么不用资源包自带的 Desert 演示场景（33000 行）：
    /// 那个场景全场景只有 9 个 MeshCollider，沙丘和村落都能穿过去，
    /// 而且含大量瀑布粒子，性能和手感都不适合做战斗测试。
    /// 这里改用 Desert 的 60 个 prefab 搭一个干净的、有完整碰撞的竞技场。
    /// </summary>
    public static class DemoSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Demo_Combat.unity";

        private const string PrefabRoot = "Assets/Lowpoly Style/Desert/Prefabs";
        private const string GroundMaterial =
            "Assets/Lowpoly Style/Shared Materials and Textures/MAT_MAIN.mat";

        private const float ArenaSize = 60f;

        [MenuItem("Tools/角色/搭建 Demo 场景")]
        public static void BuildFromMenu()
        {
            Build();
            Debug.Log($"[DemoSceneBuilder] 已生成场景 {ScenePath}");
        }

        public static void Build()
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                AnimatorParams.ControllerPath);

            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "先做这一步",
                    "找不到 Hero_SwordAndShield.controller。\n" +
                    "请先执行菜单 工具/角色/生成主角状态机。",
                    "好");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildGround();
            BuildArena();
            BuildProps();

            var player = BuildPlayer(controller);
            BuildCameras(player);
            BuildTargets();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------

        private static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.shadows = LightShadows.Soft;
        }

        private static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            // Unity 的 Plane 默认 10x10，缩放 6 得到 60x60
            ground.transform.localScale = Vector3.one * (ArenaSize / 10f);
            ground.transform.position = Vector3.zero;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterial);
            if (mat != null)
                ground.GetComponent<MeshRenderer>().sharedMaterial = mat;

            ground.isStatic = true;
        }

        private static void BuildArena()
        {
            var root = new GameObject("ArenaWalls");

            // 四面墙，留南侧一个缺口作为入口
            const float half = ArenaSize * 0.5f;
            const float step = 4f;

            for (float x = -half; x <= half; x += step)
            {
                // 北墙
                Place($"{PrefabRoot}/WoodenWall1.prefab", new Vector3(x, 0, half), 0f, root.transform);

                // 东、西墙（跳过缺口）
                if (x > -6f && x < 6f)
                    continue;

                Place($"{PrefabRoot}/WoodenWall1.prefab", new Vector3(x, 0, -half), 180f, root.transform);
            }

            for (float z = -half + step; z < half - step; z += step)
            {
                Place($"{PrefabRoot}/OldWall1.prefab", new Vector3(-half, 0, z), 90f, root.transform);
                Place($"{PrefabRoot}/OldWall1.prefab", new Vector3(half, 0, z), -90f, root.transform);
            }
        }

        private static void BuildProps()
        {
            var root = new GameObject("Props");

            var props = new (string prefab, Vector3 pos, float yaw)[]
            {
                ("TorchBig",        new Vector3(-20f, 0f,  20f),   0f),
                ("TorchBig",        new Vector3( 20f, 0f,  20f),   0f),
                ("TorchBig",        new Vector3(-20f, 0f, -20f),   0f),
                ("TorchBig",        new Vector3( 20f, 0f, -20f),   0f),
                ("SaguaroCactus1",  new Vector3(-26f, 0f,  10f),  30f),
                ("SaguaroCactus2",  new Vector3( 26f, 0f,  -8f), -45f),
                ("RockGrey1",       new Vector3( 12f, 0f,  25f),  15f),
                ("RockGrey2",       new Vector3(-14f, 0f, -25f),  70f),
                ("Palmtree1",       new Vector3(-25f, 0f, -18f),   0f),
                ("Palmtree1",       new Vector3( 25f, 0f,  18f), 120f),
                ("Barrel",          new Vector3(  6f, 0f,  16f),   0f),
                ("Barrel",          new Vector3(  8f, 0f,  17f),  40f),
                ("SandduneLow1",    new Vector3(-30f, 0f,  30f),   0f),
                ("SandduneLow1",    new Vector3( 30f, 0f, -30f),  90f),
            };

            foreach (var (prefab, pos, yaw) in props)
                Place($"{PrefabRoot}/{prefab}.prefab", pos, yaw, root.transform);
        }

        private static GameObject Place(string prefabPath, Vector3 pos, float yaw, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[DemoSceneBuilder] 找不到 prefab {prefabPath}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // ------------------------------------------------------------------

        private static GameObject BuildPlayer(UnityEditor.Animations.AnimatorController controller)
        {
            var mc01 = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC01.prefab");

            if (mc01 == null)
            {
                Debug.LogError("[DemoSceneBuilder] 找不到 MC01.prefab");
                return null;
            }

            // 直接用 MC01 作为玩家根节点，不做多余的父子包装。
            // 原因：OnAnimatorMove 是发给 Animator 所在的那个 GameObject 的，
            // 所以 Animator 与 PlayerMotor / CharacterController 必须在同一个对象上。
            var player = (GameObject)PrefabUtility.InstantiatePrefab(mc01);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0.1f, -8f);
            player.transform.rotation = Quaternion.identity;

            // 复用 MC01 自带的 Animator（它的 Avatar 已经配好了），只换控制器
            var animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[DemoSceneBuilder] MC01 上没有 Animator，无法配置");
                return null;
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;

            // 挂武器
            AttachWeapon(player, "weapon_r",
                "Assets/RPGTinyHeroWavePBR/Prefab/Weapons/OHS03_Sword.prefab");
            AttachWeapon(player, "weapon_l",
                "Assets/RPGTinyHeroWavePBR/Prefab/Weapons/Shield01.prefab");

            // 物理与控制器组件
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerAnimatorDriver>();
            player.AddComponent<PlayerCameraRig>();
            player.AddComponent<PlayerController>();

            return player;
        }

        private static void AttachWeapon(GameObject character, string boneName, string weaponPath)
        {
            var bone = FindDeep(character.transform, boneName);
            if (bone == null)
            {
                Debug.LogWarning($"[DemoSceneBuilder] 角色上没有找到骨骼 {boneName}");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[DemoSceneBuilder] 找不到武器 {weaponPath}");
                return;
            }

            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bone);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }

            return null;
        }

        // ------------------------------------------------------------------

        private static void BuildCameras(GameObject player)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            // 自由视角相机：环绕玩家
            var freeLookGo = new GameObject("CM_FreeLook");
            var freeLook = freeLookGo.AddComponent<CinemachineFreeLook>();
            freeLook.Follow = player.transform;
            freeLook.LookAt = player.transform;
            freeLook.m_XAxis.m_MaxSpeed = 300f;
            freeLook.m_YAxis.m_MaxSpeed = 2f;

            // 锁定相机：始终注视目标
            var lockGo = new GameObject("CM_LockOn");
            var lockCam = lockGo.AddComponent<CinemachineVirtualCamera>();
            lockCam.Follow = player.transform;
            lockCam.m_Lens.FieldOfView = 50f;
            lockGo.SetActive(true);

            // 把两个相机接到 PlayerCameraRig 上
            var rig = player.GetComponent<PlayerCameraRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("freeLookCamera").objectReferenceValue = freeLook;
            so.FindProperty("lockOnCamera").objectReferenceValue = lockCam;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTargets()
        {
            var root = new GameObject("Targets");

            var positions = new[]
            {
                new Vector3(-6f, 0f, 8f),
                new Vector3( 6f, 0f, 10f),
                new Vector3( 0f, 0f, 16f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var go = Place($"{PrefabRoot}/WoodenPole1.prefab", positions[i], 0f, root.transform);
                if (go == null) continue;

                go.name = $"Dummy_{i + 1:00}";
                go.AddComponent<LockOnTarget>();

                // 木桩要有碰撞体才能被 Physics.OverlapSphere 找到
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var col = go.AddComponent<CapsuleCollider>();
                    col.height = 2f;
                    col.radius = 0.3f;
                    col.center = new Vector3(0f, 1f, 0f);
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
```

- [ ] **Step 2: 执行生成器**

Unity 菜单 `Tools/角色/搭建 Demo 场景`。

Expected: 场景生成，Console 无错误。若提示找不到 controller，先执行 `Tools/角色/生成主角状态机`。

- [ ] **Step 3: 目视检查场景层级**

确认 Hierarchy 里有 `Ground` / `ArenaWalls` / `Props` / `PlayerRoot` / `Main Camera` / `CM_FreeLook` / `CM_LockOn` / `Targets`。

- [ ] **Step 4: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/DemoSceneBuilder.cs Assets/Scenes/Demo_Combat.unity \
        Assets/Scenes/Demo_Combat.unity.meta
git commit -m "feat: DemoSceneBuilder 搭建沙漠竞技场 Demo 场景"
```

---


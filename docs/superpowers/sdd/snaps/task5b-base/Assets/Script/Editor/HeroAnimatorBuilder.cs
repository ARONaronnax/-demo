using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Demo;

namespace Demo.EditorTools
{
    /// <summary>
    /// 用 UnityEditor.Animations API 生成主角的 Animator Controller。
    ///
    /// 为什么是"生成"而不是"手工改"：
    /// 原资源控制器有 44 个平铺状态、44 条无条件的退出时间过渡，是作者的宣传片播放器。
    /// 手工把它改成参数化状态机需要在 Inspector 里连上百条线，既慢又容易出错。
    /// 用代码生成则快、可重复、可被测试断言。
    ///
    /// 原控制器 Hero_SwordAndShield.controller 生成到 Assets/Animator/ 下，
    /// Assets/RPGTinyHeroWavePBR/ 下的原文件一个字都不动。
    /// </summary>
    public static class HeroAnimatorBuilder
    {
        /// <summary>连招衔接的退出时间：到达这个进度时允许接下一段。</summary>
        public const float ComboLinkExitTime = 0.55f;

        /// <summary>一次性动作返回 Locomotion 的退出时间。</summary>
        public const float ActionReturnExitTime = 0.90f;

        public const string OutputPath = AnimatorParams.ControllerPath;

        private static readonly Vector3 GroupSpacing = new Vector3(400f, 0f, 0f);

        [MenuItem("Tools/角色/生成主角状态机")]
        public static void BuildFromMenu()
        {
            var ctrl = Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = ctrl;
            Debug.Log($"[HeroAnimatorBuilder] 已生成 {OutputPath}");
        }

        /// <summary>
        /// 生成控制器。幂等：每次调用都会先删除旧资源再重建。
        /// </summary>
        public static AnimatorController Build()
        {
            DeleteExistingAsset();

            EnsureFolder("Assets/Animator");

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
            ctrl.layers[0].name = AnimatorParams.LayerName;

            AddParameters(ctrl);

            var root = ctrl.layers[0].stateMachine;
            root.name = AnimatorParams.LayerName;

            CreateGroupStateMachines(root);

            var groups = GetGroups(root);
            BuildLocomotion(root, groups[AnimatorParams.Groups.Locomotion], ctrl);

            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static void DeleteExistingAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
                AssetDatabase.DeleteAsset(OutputPath);
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

        private static void AddParameters(AnimatorController ctrl)
        {
            void F(string n) => ctrl.AddParameter(n, AnimatorControllerParameterType.Float);
            void B(string n) => ctrl.AddParameter(n, AnimatorControllerParameterType.Bool);
            void T(string n) => ctrl.AddParameter(n, AnimatorControllerParameterType.Trigger);

            F(AnimatorParams.Speed);
            F(AnimatorParams.MoveDirX);
            F(AnimatorParams.MoveDirZ);
            B(AnimatorParams.IsLockedOn);
            B(AnimatorParams.IsBattleStance);
            B(AnimatorParams.IsGrounded);

            T(AnimatorParams.LightAttack);
            T(AnimatorParams.HeavyAttack);
            T(AnimatorParams.Roll);
            T(AnimatorParams.Jump);
            B(AnimatorParams.IsDefending);

            T(AnimatorParams.Hit);
            ctrl.AddParameter(AnimatorParams.HitIndex, AnimatorControllerParameterType.Int);
            T(AnimatorParams.DefendHit);
            T(AnimatorParams.Dizzy);
            T(AnimatorParams.Die);
            T(AnimatorParams.GetUp);
            B(AnimatorParams.IsDead);

            T(AnimatorParams.Victory);
            T(AnimatorParams.Dance);
            T(AnimatorParams.LevelUp);
            T(AnimatorParams.Challenging);
            T(AnimatorParams.SenseSomething);
        }

        /// <summary>建立 6 个空的子状态机，仅作分组容器。</summary>
        private static void CreateGroupStateMachines(AnimatorStateMachine root)
        {
            for (int i = 0; i < AnimatorParams.Groups.All.Length; i++)
            {
                var group = root.AddStateMachine(
                    AnimatorParams.Groups.All[i],
                    new Vector3(300f + i * GroupSpacing.x, 0f, 0f));
                group.name = AnimatorParams.Groups.All[i];
            }
        }

        /// <summary>子状态机名 → 子状态机，供各 BuildXxx 取用自己那一组。</summary>
        private static System.Collections.Generic.Dictionary<string, AnimatorStateMachine> GetGroups(
            AnimatorStateMachine root)
        {
            var map = new System.Collections.Generic.Dictionary<string, AnimatorStateMachine>();
            foreach (var child in root.stateMachines)
                map[child.stateMachine.name] = child.stateMachine;
            return map;
        }

        /// <summary>
        /// 移动组：两个待机状态 + 三套混合树（自由非战斗 / 自由战斗 / 锁定）。
        ///
        /// 锁定混合树是锁定机制的核心：锁定时角色不转向，四个方向的位移只能靠
        /// 四个独立的方向剪辑表达，因此它必须是二维方向混合树而不是原地转身。
        /// </summary>
        private static void BuildLocomotion(AnimatorStateMachine root, AnimatorStateMachine locomotion, AnimatorController ctrl)
        {
            // ---- 两个待机状态 ----
            var idleNormal = locomotion.AddState(AnimatorParams.States.IdleNormal, new Vector3(0, 0, 0));
            idleNormal.motion = AnimationClipLocator.LoadInPlace("Idle_Normal_SwordAndShield");

            var idleBattle = locomotion.AddState(AnimatorParams.States.IdleBattle, new Vector3(0, 80, 0));
            idleBattle.motion = AnimationClipLocator.LoadInPlace("Idle_Battle_SwordAndShiled");

            // ---- 三个混合树 ----
            var treeFreeNormal = Create1DTree(ctrl, AnimatorParams.BlendTrees.FreeNormal, new[]
            {
                (AnimationClipLocator.LoadInPlace("Idle_Normal_SwordAndShield"), 0f),
                (AnimationClipLocator.LoadInPlace("MoveFWD_Normal_InPlace_SwordAndShield"), 0.5f),
            });

            var treeFreeBattle = Create1DTree(ctrl, AnimatorParams.BlendTrees.FreeBattle, new[]
            {
                (AnimationClipLocator.LoadInPlace("Idle_Battle_SwordAndShiled"), 0f),
                (AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield"), 0.5f),
                (AnimationClipLocator.LoadInPlace("SprintFWD_Battle_InPlace_SwordAndShield"), 1f),
            });

            var treeLocked = CreateDirectional2DTree(ctrl, AnimatorParams.BlendTrees.Locked, new[]
            {
                (AnimationClipLocator.LoadInPlace("Idle_Battle_SwordAndShiled"),        new Vector2(0f, 0f)),
                (AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield"), new Vector2(0f, 1f)),
                (AnimationClipLocator.LoadInPlace("MoveBWD_Battle_InPlace_SwordAndShield"), new Vector2(0f, -1f)),
                (AnimationClipLocator.LoadInPlace("MoveLFT_Battle_InPlace_SwordAndShield"), new Vector2(-1f, 0f)),
                (AnimationClipLocator.LoadInPlace("MoveRGT_Battle_InPlace_SwordAndShield"), new Vector2(1f, 0f)),
            });

            // 混合树也要作为状态存在
            var stFreeNormal = locomotion.AddState(AnimatorParams.States.TreeFreeNormal, new Vector3(0, 160, 0));
            stFreeNormal.motion = treeFreeNormal;

            var stFreeBattle = locomotion.AddState(AnimatorParams.States.TreeFreeBattle, new Vector3(0, 240, 0));
            stFreeBattle.motion = treeFreeBattle;

            var stLocked = locomotion.AddState(AnimatorParams.States.TreeLocked, new Vector3(0, 320, 0));
            stLocked.motion = treeLocked;

            locomotion.defaultState = stFreeNormal;

            // ---- 组内转换 ----
            // 非战斗 → 战斗姿态
            AddTransition(stFreeNormal, stFreeBattle, 1f, true,
                (AnimatorParams.IsBattleStance, AnimatorConditionMode.If));

            // 战斗姿态 → 非战斗（必须先停下来）
            AddTransition(stFreeBattle, stFreeNormal, 1f, true,
                (AnimatorParams.IsBattleStance, AnimatorConditionMode.IfNot));

            // 自由 → 锁定
            AddTransition(stFreeBattle, stLocked, 1f, true,
                (AnimatorParams.IsLockedOn, AnimatorConditionMode.If));

            // 锁定 → 自由
            AddTransition(stLocked, stFreeBattle, 1f, true,
                (AnimatorParams.IsLockedOn, AnimatorConditionMode.IfNot));
        }

        private static BlendTree Create1DTree(
            AnimatorController ctrl, string name,
            (AnimationClip clip, float threshold)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = AnimatorParams.Speed,
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            foreach (var (clip, threshold) in children)
            {
                if (clip == null)
                {
                    Debug.LogError($"[HeroAnimatorBuilder] {name} 有剪辑加载失败");
                    continue;
                }
                tree.AddChild(clip, threshold);
            }
            return tree;
        }

        private static BlendTree CreateDirectional2DTree(
            AnimatorController ctrl, string name,
            (AnimationClip clip, Vector2 pos)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = AnimatorParams.MoveDirX,
                blendParameterY = AnimatorParams.MoveDirZ,
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            foreach (var (clip, pos) in children)
            {
                if (clip == null)
                {
                    Debug.LogError($"[HeroAnimatorBuilder] {name} 有剪辑加载失败");
                    continue;
                }
                tree.AddChild(clip, pos);
            }
            return tree;
        }

        private static AnimatorStateTransition AddTransition(
            AnimatorState from, AnimatorState to,
            float exitTime, bool hasExitTime,
            params (string param, AnimatorConditionMode mode)[] conditions)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = hasExitTime;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = 0.15f;
            t.canTransitionToSelf = false;

            foreach (var (param, mode) in conditions)
                t.AddCondition(mode, 0f, param);

            return t;
        }
    }
}

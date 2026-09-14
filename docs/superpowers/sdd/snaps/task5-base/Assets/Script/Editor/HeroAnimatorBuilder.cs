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
    }
}

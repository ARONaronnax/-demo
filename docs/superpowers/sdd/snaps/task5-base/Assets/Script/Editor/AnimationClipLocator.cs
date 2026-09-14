using UnityEditor;
using UnityEngine;

namespace Demo.EditorTools
{
    /// <summary>
    /// 从资源的 fbx 文件中定位并加载 AnimationClip。
    ///
    /// 资源里每个位移类动作都同时提供两套同名剪辑：
    ///   Animation/SwordAndShield/InPlace/&lt;名字&gt;.fbx
    ///   Animation/SwordAndShield/RootMotion/&lt;名字&gt;.fbx
    /// 因此必须显式指定目录，不能只靠名字查找。
    /// </summary>
    public static class AnimationClipLocator
    {
        public const string HeroAnimationRoot = "Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield";
        public const string InPlaceDir = HeroAnimationRoot + "/InPlace";
        public const string RootMotionDir = HeroAnimationRoot + "/RootMotion";

        public static string InPlacePath(string clipName) => $"{InPlaceDir}/{clipName}.fbx";

        public static string RootMotionPath(string clipName) => $"{RootMotionDir}/{clipName}.fbx";

        public static AnimationClip LoadInPlace(string clipName) => LoadClip(InPlacePath(clipName));

        public static AnimationClip LoadRootMotion(string clipName) => LoadClip(RootMotionPath(clipName));

        /// <summary>
        /// 加载 fbx 中的主 AnimationClip。
        /// fbx 里可能含多个子资源（网格、材质、多个 take），
        /// 这里取名字最匹配的那个，并跳过 Unity 生成的 __preview__ 剪辑。
        /// </summary>
        public static AnimationClip LoadClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null || assets.Length == 0)
                return null;

            string wanted = System.IO.Path.GetFileNameWithoutExtension(fbxPath);

            AnimationClip best = null;
            foreach (var asset in assets)
            {
                if (asset is not AnimationClip clip)
                    continue;
                if (clip.name.StartsWith("__preview__"))
                    continue;

                // 完全同名，直接采用
                if (clip.name == wanted)
                    return clip;

                // 否则记住第一个候选，作为兜底
                if (best == null)
                    best = clip;
            }

            return best;
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace Demo.UI
{
    /// <summary>仅提供圆形小地图容器；未来真实地图系统可监听 onMapRequested 并替换 Content。</summary>
    public class MiniMapPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private UnityEvent onMapRequested;

        public RectTransform Content { get { return content; } }
        public void Bind(RectTransform mapContent) { content = mapContent; }
        public void RequestMap() { if (onMapRequested != null) onMapRequested.Invoke(); }
    }
}

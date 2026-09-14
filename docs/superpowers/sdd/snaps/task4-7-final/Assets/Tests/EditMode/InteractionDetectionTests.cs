using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Interaction;
using Demo.Player;

namespace Demo.Tests
{
    /// <summary>
    /// 守护 Phase 02 交互检测链路的三个前提：
    /// 1) 接口类型能被 GetComponentInParent 解析
    /// 2) 父对象上的组件也能被子对象的碰撞体找到
    /// 3) 新建的碰撞体能被 OverlapSphere 查到
    /// 这三条任一不成立，交互提示就不会出现。
    /// </summary>
    public class InteractionDetectionTests
    {
        private GameObject _npc;
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_npc != null)
            {
                Object.DestroyImmediate(_npc);
            }

            if (_player != null)
            {
                Object.DestroyImmediate(_player);
            }
        }

        [Test]
        public void GetComponentInParent_ResolvesIInteractable_OnSameObject()
        {
            _npc = new GameObject("Npc");
            CapsuleCollider col = _npc.AddComponent<CapsuleCollider>();
            _npc.AddComponent<NpcInteractable>();

            Assert.IsNotNull(
                col.GetComponentInParent<IInteractable>(),
                "GetComponentInParent<IInteractable>() 未能解析同一对象上的接口实现");
        }

        [Test]
        public void GetComponentInParent_ResolvesIInteractable_OnParentObject()
        {
            _npc = new GameObject("Npc");
            _npc.AddComponent<NpcInteractable>();

            GameObject body = new GameObject("Body");
            body.transform.SetParent(_npc.transform);
            CapsuleCollider col = body.AddComponent<CapsuleCollider>();

            Assert.IsNotNull(
                col.GetComponentInParent<IInteractable>(),
                "GetComponentInParent<IInteractable>() 未能解析父对象上的接口实现");
        }

        [Test]
        public void OverlapSphere_FindsNpcCollider_WithinRadius()
        {
            _npc = new GameObject("Npc");
            _npc.transform.position = new Vector3(1.5f, 0f, 0f);
            _npc.AddComponent<CapsuleCollider>();
            _npc.AddComponent<NpcInteractable>();

            _player = new GameObject("Player");
            _player.transform.position = Vector3.zero;
            _player.AddComponent<PlayerInteractionDetector>();

            Physics.SyncTransforms();

            Collider[] buffer = new Collider[8];
            int count = Physics.OverlapSphereNonAlloc(Vector3.zero, 2.5f, buffer);

            bool found = false;

            for (int i = 0; i < count; i++)
            {
                if (buffer[i] != null &&
                    buffer[i].GetComponentInParent<IInteractable>() != null)
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found,
                "OverlapSphere 未在半径内找到带 IInteractable 的碰撞体（查到 " + count + " 个碰撞体）");
        }
    }
}

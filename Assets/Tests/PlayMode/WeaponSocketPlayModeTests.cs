using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Demo.Core;
using Demo.Data;
using Demo.Equipment;

namespace Demo.Tests.PlayMode
{
    /// <summary>
    /// 武器挂点在 **Play 模式**下的行为。
    ///
    /// 为什么要单独有一份：EditMode 下 Object.Destroy 是不生效的（只在 Play 模式有效），
    /// 所以 WeaponSocket 里销毁走的是 DestroyImmediate 那条分支，
    /// `Destroy` 那条**一次都没被测到**。而 Destroy 是延迟到帧末执行的 ——
    /// 差了这一帧，才会出现"换了武器旧模型还在手上"。
    ///
    /// 另外这里覆盖真实的 OnEnable / OnDisable 生命周期：EditMode 下
    /// AddComponent 不触发它们，只能靠 Bind 注入，测不到生命周期那几行。
    /// </summary>
    public class WeaponSocketPlayModeTests
    {
        private WeaponData _sword;
        private WeaponData _axe;

        private GameObject _root;
        private GameObject _prefab;
        private Transform _socket;
        private WeaponSocket _weaponSocket;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("LizardWarriorBlade");
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(_prefab.transform, false);

            _sword = ScriptableObject.CreateInstance<WeaponData>();
            _sword.displayName = "蜥蜴战士之刃";
            _sword.weaponPrefab = _prefab;
            _sword.maxStack = 1;

            _axe = ScriptableObject.CreateInstance<WeaponData>();
            _axe.displayName = "战斧";
            _axe.weaponPrefab = _prefab;
            _axe.maxStack = 1;

            _root = new GameObject("WeaponSocketPlayMode");

            _socket = new GameObject("weapon_r").transform;
            _socket.SetParent(_root.transform, false);

            _weaponSocket = _root.AddComponent<WeaponSocket>();
            _weaponSocket.Bind(_socket);
        }

        [TearDown]
        public void TearDown()
        {
            _weaponSocket.StopListening();

            Object.Destroy(_root);
            Object.Destroy(_prefab);
            Object.Destroy(_sword);
            Object.Destroy(_axe);
        }

        // ---------------------------------------------------------
        // 换武器 —— 就是这一条在 Play 模式里出过问题
        // ---------------------------------------------------------

        [UnityTest]
        public IEnumerator EquipAnotherWeapon_LeavesOnlyOneModel()
        {
            _weaponSocket.Show(_sword);
            yield return null;

            Assert.AreEqual(1, _socket.childCount, "前提：第一把武器挂上去了");

            _weaponSocket.Show(_axe);
            yield return null;

            Assert.AreEqual(1, _socket.childCount, "换武器后手上只能有一个模型");
            Assert.AreSame(_axe, _weaponSocket.HeldWeapon);
        }

        [UnityTest]
        public IEnumerator EquipAnotherWeapon_DestroysTheOldInstance()
        {
            _weaponSocket.Show(_sword);
            yield return null;
            GameObject first = _weaponSocket.HeldInstance;

            _weaponSocket.Show(_axe);
            yield return null;

            Assert.IsTrue(first == null, "旧模型要被真的销毁掉，否则会在骨骼底下越堆越多");
        }

        [UnityTest]
        public IEnumerator EquipAnotherWeapon_WhilePaused_LeavesOnlyOneModel()
        {
            // 这条才是真实条件：装备按钮只能在开着背包时点到，
            // 而那时候 Time.timeScale == 0。上面的用例跑在 timeScale=1，
            // 暂停相关的行为它照不到。
            Time.timeScale = 0f;

            try
            {
                _weaponSocket.Show(_sword);
                yield return null;

                Assert.AreEqual(1, _socket.childCount, "前提：暂停时第一把也挂得上去");

                _weaponSocket.Show(_axe);
                yield return null;

                Assert.AreEqual(1, _socket.childCount, "暂停时换武器，旧模型也必须马上消失");
                Assert.AreSame(_axe, _weaponSocket.HeldWeapon);
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator EquipAnotherWeapon_WhilePaused_OldModelIsNotVisible()
        {
            // 就算销毁被推迟，旧模型也**当帧**就不该再看得见 ——
            // 玩家看到的是画面，不是销毁队列
            Time.timeScale = 0f;

            try
            {
                _weaponSocket.Show(_sword);
                yield return null;
                GameObject first = _weaponSocket.HeldInstance;

                _weaponSocket.Show(_axe);

                // 注意这里不 yield：换完立刻检查
                Assert.IsFalse(first.activeInHierarchy, "旧模型当帧就该看不见了，不能等帧末销毁");

                yield return null;
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator WeaponEquippedEvent_SwapsTheModel()
        {
            // 走真实的事件链路：装备系统广播 -> 挂点换模型
            EventBus.Publish(new WeaponEquippedEvent(_sword));
            yield return null;

            Assert.IsNotNull(_weaponSocket.HeldInstance);

            EventBus.Publish(new WeaponEquippedEvent(_axe));
            yield return null;

            Assert.AreEqual(1, _socket.childCount, "换了武器不该留下上一把");
            Assert.AreSame(_axe, _weaponSocket.HeldWeapon);
        }

        [UnityTest]
        public IEnumerator Unequip_RemovesTheModel()
        {
            EventBus.Publish(new WeaponEquippedEvent(_sword));
            yield return null;

            EventBus.Publish(new WeaponEquippedEvent(null));
            yield return null;

            Assert.IsNull(_weaponSocket.HeldInstance);
            Assert.AreEqual(0, _socket.childCount);
        }

        // ---------------------------------------------------------
        // 真实生命周期（EditMode 测不到的那几行）
        // ---------------------------------------------------------

        [UnityTest]
        public IEnumerator OnEnable_SubscribesToTheBus()
        {
            // 没有调 Bind 之外的东西：AddComponent 在 Play 模式会立刻触发 OnEnable
            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.IsNotNull(_weaponSocket.HeldInstance, "OnEnable 里应该已经订阅上了");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnDisable_StopsReactingToTheBus()
        {
            _weaponSocket.enabled = false;
            yield return null;

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.IsNull(_weaponSocket.HeldInstance, "OnDisable 里应该退订了");
        }

        [UnityTest]
        public IEnumerator OnDisableThenEnable_WorksAgain()
        {
            _weaponSocket.enabled = false;
            yield return null;

            _weaponSocket.enabled = true;
            yield return null;

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.IsNotNull(_weaponSocket.HeldInstance, "重新启用后要能重新订阅上");
            Assert.AreEqual(1, _socket.childCount, "重复订阅会让同一次装备挂两次模型");
        }
    }
}

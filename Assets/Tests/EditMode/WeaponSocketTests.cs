using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Equipment;

namespace Demo.Tests
{
    /// <summary>
    /// 武器挂点：装备变了就把模型挂到右手骨骼上。
    ///
    /// MC01 这套骨骼自带 weapon_r（hand_r 的子物体），不用自己造挂点。
    /// 这里用普通 Transform 代替骨骼，测的是"挂了什么、挂在哪、有没有动到挂点本身"。
    /// </summary>
    public class WeaponSocketTests
    {
        private WeaponData _sword;
        private WeaponData _axe;
        private WeaponData _noModel;

        private GameObject _root;
        private GameObject _prefab;
        private GameObject _barePrefab;
        private Transform _socket;
        private WeaponSocket _weaponSocket;

        /// <summary>骨骼自己的本地变换，Show 之后必须原封不动。</summary>
        private static readonly Vector3 SocketPosition = new Vector3(0.096260175f, -0.018506708f, -0.00360484f);
        private static readonly Vector3 SocketEuler = new Vector3(90f, 0f, 0f);

        [SetUp]
        public void SetUp()
        {
            MakePrefabs();

            _sword = MakeWeapon("蜥蜴战士之刃", _prefab);
            _sword.socketLocalPosition = new Vector3(0.01f, 0.02f, 0.03f);
            _sword.socketLocalEuler = new Vector3(0f, 180f, 15f);
            _sword.socketLocalScale = new Vector3(2f, 2f, 2f);

            _axe = MakeWeapon("战斧", _prefab);
            _axe.socketLocalScale = Vector3.one;

            _noModel = MakeWeapon("没模型的武器", null);

            _root = new GameObject("WeaponSocketTest");

            _socket = new GameObject("weapon_r").transform;
            _socket.SetParent(_root.transform, false);
            _socket.localPosition = SocketPosition;
            _socket.localEulerAngles = SocketEuler;

            _weaponSocket = _root.AddComponent<WeaponSocket>();
            _weaponSocket.Bind(_socket);
        }

        private void MakePrefabs()
        {
            _prefab = new GameObject("LizardWarriorBlade");
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(_prefab.transform, false);

            _barePrefab = new GameObject("Bare");
        }

        private static WeaponData MakeWeapon(string name, GameObject prefab)
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.displayName = name;
            weapon.weaponPrefab = prefab;
            weapon.maxStack = 1;
            return weapon;
        }

        [TearDown]
        public void TearDown()
        {
            _weaponSocket.StopListening();

            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_barePrefab);

            Object.DestroyImmediate(_sword);
            Object.DestroyImmediate(_axe);
            Object.DestroyImmediate(_noModel);
        }

        private GameObject Held()
        {
            return _weaponSocket.HeldInstance;
        }

        // ---------------------------------------------------------
        // 挂上去
        // ---------------------------------------------------------

        [Test]
        public void StartsEmpty()
        {
            Assert.IsNull(Held());
            Assert.IsNull(_weaponSocket.HeldWeapon);
        }

        [Test]
        public void Show_InstantiatesTheModel()
        {
            _weaponSocket.Show(_sword);

            Assert.IsNotNull(Held(), "装备了武器就该有模型");
            Assert.AreSame(_sword, _weaponSocket.HeldWeapon);
        }

        [Test]
        public void Show_ParentsTheModelToTheSocket()
        {
            _weaponSocket.Show(_sword);

            Assert.AreSame(_socket, Held().transform.parent, "模型必须挂在右手骨骼底下才跟得上动作");
        }

        [Test]
        public void Show_AppliesTheSocketTransform()
        {
            _weaponSocket.Show(_sword);

            Assert.AreEqual(_sword.socketLocalPosition, Held().transform.localPosition);
            Assert.AreEqual(_sword.socketLocalScale, Held().transform.localScale);

            // 欧拉角存的是四元数，读回来会有精度漂移，只能按容差比
            AssertVectorNear(_sword.socketLocalEuler, Held().transform.localEulerAngles, "socketLocalEuler 没写上去");
        }

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less((expected - actual).magnitude, 0.01f, message + "（期望 " + expected + "，实际 " + actual + "）");
        }

        [Test]
        public void Show_SocketTransformWinsOverThePrefabsOwn()
        {
            // prefab 自己带的变换会被 socketLocal* 覆盖 —— 同一个 prefab
            // 掉在地上和拿在手里本来就需要不同的姿态，落到手上就以 socketLocal* 为准
            _prefab.transform.localPosition = new Vector3(9f, 9f, 9f);
            _prefab.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
            _prefab.transform.localScale = new Vector3(5f, 5f, 5f);

            _weaponSocket.Show(_sword);

            Assert.AreEqual(_sword.socketLocalPosition, Held().transform.localPosition);
            Assert.AreEqual(_sword.socketLocalScale, Held().transform.localScale);
            AssertVectorNear(_sword.socketLocalEuler, Held().transform.localEulerAngles, "prefab 自带的角度应该被覆盖");
        }

        // ---------------------------------------------------------
        // 不要动到骨骼本身
        // ---------------------------------------------------------

        [Test]
        public void Show_DoesNotMoveTheSocket()
        {
            // 挂点只能往上加子物体。动了骨骼自己的变换，
            // 模型会歪，而且可能连带动画表现 —— 这是硬伤
            Vector3 beforePosition = _socket.localPosition;
            Vector3 beforeEuler = _socket.localEulerAngles;
            Vector3 beforeScale = _socket.localScale;

            _weaponSocket.Show(_sword);

            Assert.AreEqual(beforePosition, _socket.localPosition);
            Assert.AreEqual(beforeEuler, _socket.localEulerAngles);
            Assert.AreEqual(beforeScale, _socket.localScale);
        }

        [Test]
        public void Show_DoesNotReparentTheSocket()
        {
            Transform parent = _socket.parent;

            _weaponSocket.Show(_sword);

            Assert.AreSame(parent, _socket.parent);
        }

        [Test]
        public void Clear_DoesNotMoveTheSocket()
        {
            _weaponSocket.Show(_sword);

            _weaponSocket.Clear();

            Assert.AreEqual(SocketPosition, _socket.localPosition);
            AssertVectorNear(SocketEuler, _socket.localEulerAngles, "摘模型不该动到骨骼的角度");
        }

        // ---------------------------------------------------------
        // 换 / 摘
        // ---------------------------------------------------------

        [Test]
        public void Show_AnotherWeapon_ReplacesTheModel()
        {
            _weaponSocket.Show(_sword);
            GameObject first = Held();

            _weaponSocket.Show(_axe);

            Assert.AreSame(_axe, _weaponSocket.HeldWeapon);
            Assert.IsTrue(first == null, "旧模型要被真的销毁掉，不是只把引用换掉");
            Assert.AreEqual(1, _socket.childCount, "换武器不能把旧模型留在手上");
        }

        [Test]
        public void Show_TheSameWeaponTwice_LeavesOneModel()
        {
            _weaponSocket.Show(_sword);
            GameObject first = Held();

            _weaponSocket.Show(_sword);

            Assert.IsTrue(first == null, "重复装备也该把上一个实例销毁掉");
            Assert.AreEqual(1, _socket.childCount);
        }

        [Test]
        public void Clear_RemovesTheModel()
        {
            _weaponSocket.Show(_sword);
            GameObject first = Held();

            _weaponSocket.Clear();

            Assert.IsNull(Held());
            Assert.IsNull(_weaponSocket.HeldWeapon);
            Assert.IsTrue(first == null, "卸下的模型要销毁，否则换几次武器就在骨骼底下堆一堆");
            Assert.AreEqual(0, _socket.childCount, "卸下武器后骨骼底下不该还挂着东西");
        }

        [Test]
        public void Clear_WhenEmpty_DoesNothing()
        {
            _weaponSocket.Clear();

            Assert.IsNull(Held());
            Assert.AreEqual(0, _socket.childCount);
        }

        // ---------------------------------------------------------
        // 美术预摆在挂点底下的模型
        //
        // MC01.prefab 的 weapon_r 底下本来就嵌着一把 OHS09_Sword（左手的 weapon_l 下还有一把）。
        // 它不是本组件实例化的，但换装时必须一起清掉 ——
        // 否则新模型是加进去了，那把原生剑原地不动，右手上就是两把。
        // ---------------------------------------------------------

        private GameObject PutArtModelUnderTheSocket()
        {
            GameObject artModel = new GameObject("OHS09_Sword");
            artModel.transform.SetParent(_socket, false);
            return artModel;
        }

        [Test]
        public void Clear_RemovesAModelTheArtAlreadyPutThere()
        {
            GameObject artModel = PutArtModelUnderTheSocket();
            Assert.AreEqual(1, _socket.childCount, "前提：原生剑确实挂在挂点底下");

            _weaponSocket.Clear();

            Assert.IsTrue(artModel == null, "美术预摆的模型也要销毁，不能只清自己实例化的那个");
            Assert.AreEqual(0, _socket.childCount);
        }

        [Test]
        public void Show_ClearsTheModelTheArtAlreadyPutThere()
        {
            // 真实场景就是这样：骨骼上已有原生剑，这时装上蜥蜴战士之刃
            GameObject artModel = PutArtModelUnderTheSocket();

            _weaponSocket.Show(_sword);

            Assert.IsTrue(artModel == null, "换上别的武器时，美术预摆的那把必须销毁");
            Assert.AreEqual(1, _socket.childCount, "右手上只能留新装的那一把");
            Assert.AreSame(_sword, _weaponSocket.HeldWeapon);
        }

        [Test]
        public void Show_WeaponWithoutModel_AlsoClearsTheArtModel()
        {
            // 换成"还没做模型"的武器也等于空手，原生剑同样得摘掉
            GameObject artModel = PutArtModelUnderTheSocket();

            _weaponSocket.Show(_noModel);

            Assert.IsTrue(artModel == null);
            Assert.AreEqual(0, _socket.childCount);
        }

        [Test]
        public void Show_ArtModel_ThenSwapTwice_LeavesOneModel()
        {
            // 连换两次：原生剑 -> 刀 -> 斧，中途不能在骨骼底下攒东西
            PutArtModelUnderTheSocket();

            _weaponSocket.Show(_sword);
            _weaponSocket.Show(_axe);

            Assert.AreEqual(1, _socket.childCount);
            Assert.AreSame(_axe, _weaponSocket.HeldWeapon);
        }

        // ---------------------------------------------------------
        // 容错
        // ---------------------------------------------------------

        [Test]
        public void Show_WeaponWithoutModel_ShowsNothing()
        {
            // 数据对但还没做模型，不该崩，也不该留个空的 GameObject 在手上
            _weaponSocket.Show(_noModel);

            Assert.IsNull(Held());
            Assert.IsNull(_weaponSocket.HeldWeapon, "没有模型就不算挂在手上");
            Assert.AreEqual(0, _socket.childCount);
        }

        [Test]
        public void Show_WeaponWithoutModel_AfterAnotherWeapon_ClearsTheOld()
        {
            _weaponSocket.Show(_sword);

            _weaponSocket.Show(_noModel);

            Assert.IsNull(Held());
            Assert.AreEqual(0, _socket.childCount, "换成没模型的武器，手上的旧模型要摘掉");
        }

        [Test]
        public void Show_WithoutSocket_DoesNotThrow()
        {
            // 场景里 socket 那个框忘了拖
            _weaponSocket.Bind(null);

            Assert.DoesNotThrow(() => _weaponSocket.Show(_sword));
            Assert.IsNull(Held());
            Assert.IsNull(_weaponSocket.HeldWeapon);
        }

        [Test]
        public void Show_EmptyPrefab_DoesNotThrow()
        {
            WeaponData empty = MakeWeapon("空 prefab", _barePrefab);

            Assert.DoesNotThrow(() => _weaponSocket.Show(empty));
            Assert.IsNotNull(Held(), "prefab 是空的物体也是模型，照样挂上去");

            Object.DestroyImmediate(empty);
        }

        // ---------------------------------------------------------
        // EventBus -> 挂点 这一跳
        // ---------------------------------------------------------

        [Test]
        public void WeaponEquippedEvent_OnBus_ShowsTheModel()
        {
            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.IsNotNull(Held());
            Assert.AreSame(_sword, _weaponSocket.HeldWeapon);
        }

        [Test]
        public void WeaponEquippedEvent_WithNull_ClearsTheModel()
        {
            EventBus.Publish(new WeaponEquippedEvent(_sword));

            EventBus.Publish(new WeaponEquippedEvent(null));

            Assert.IsNull(Held(), "卸下武器要广播 null，挂点收到就该摘模型");
        }

        [Test]
        public void StopListening_StopsReactingToBus()
        {
            _weaponSocket.StopListening();

            EventBus.Publish(new WeaponEquippedEvent(_sword));

            Assert.IsNull(Held(), "退订之后不该还在响应总线事件");
        }
    }
}

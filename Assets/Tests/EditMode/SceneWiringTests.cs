using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Demo.Data;
using Demo.Equipment;
using Demo.Inventory;
using Demo.UI;

namespace Demo.Tests
{
    /// <summary>
    /// 场景接线检查。
    ///
    /// 存在的理由：这个项目已经踩过一次 —— DebugHud.questComponent 在场景里是
    /// {fileID: 0}，也就是 Inspector 里那个框空着。这种问题编译不报错、单元测试
    /// 也照过（它们都是自己 new GameObject 出来测的），只有真正进游戏才发现，
    /// 而且表现是"功能莫名其妙不工作"，很难查。
    ///
    /// 这里用 SerializedObject 直接读私有序列化字段，把空引用挡在上线之前。
    /// </summary>
    public class SceneWiringTests
    {
        private const string ScenePath = "Assets/Lowpoly Style/Desert/DemoScene/Desert.unity";

        private Scene _scene;

        [OneTimeSetUp]
        public void LoadScene()
        {
            // 附加加载，不动当前场景，免得影响别的测试
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void UnloadScene()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        private static T FindInScene<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsNotEmpty(found, "场景里找不到 " + typeof(T).Name);
            return found[0];
        }

        [Test]
        public void Scene_IsLoaded()
        {
            Assert.IsTrue(_scene.IsValid() && _scene.isLoaded, "场景没加载起来，后面的检查都会失去意义");
        }

        // ---------------------------------------------------------
        // 背包
        // ---------------------------------------------------------

        [Test]
        public void InventoryUI_PanelIsWired()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty panel = new SerializedObject(ui).FindProperty("panel");

            Assert.IsNotNull(panel, "InventoryUI 上没有 panel 字段");
            Assert.IsNotNull(panel.objectReferenceValue, "InventoryUI.panel 在场景里是空的");
        }

        [Test]
        public void InventoryUI_InventoryIsWired()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty inventory = new SerializedObject(ui).FindProperty("inventory");

            Assert.IsNotNull(inventory.objectReferenceValue, "InventoryUI.inventory 在场景里是空的");
        }

        [Test]
        public void InventoryUI_HasTwelveSlots_AllWired()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty slots = new SerializedObject(ui).FindProperty("slots");

            Assert.AreEqual(12, slots.arraySize, "背包是 4x3，应该是 12 个格子");

            for (int i = 0; i < slots.arraySize; i++)
            {
                Object slot = slots.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.IsNotNull(slot, "第 " + i + " 个格子是空的");
            }
        }

        [Test]
        public void InventoryUI_SlotContainerIsWired()
        {
            // 物品超过初始格子数时要往这里塞新格子；空了就长不出来
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty container = new SerializedObject(ui).FindProperty("slotContainer");

            Assert.IsNotNull(container.objectReferenceValue, "InventoryUI.slotContainer 在场景里是空的");
        }

        [Test]
        public void InventoryUI_ScrollRectIsWired()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty scroll = new SerializedObject(ui).FindProperty("scrollRect");

            Assert.IsNotNull(scroll.objectReferenceValue, "InventoryUI.scrollRect 在场景里是空的");
        }

        [Test]
        public void Slots_LiveUnderTheSlotContainer()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedObject so = new SerializedObject(ui);

            RectTransform container = so.FindProperty("slotContainer").objectReferenceValue as RectTransform;
            SerializedProperty slots = so.FindProperty("slots");

            Assert.IsNotNull(container);

            for (int i = 0; i < slots.arraySize; i++)
            {
                InventorySlotUI slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as InventorySlotUI;
                Assert.IsNotNull(slot);

                Assert.AreSame(
                    container,
                    slot.transform.parent,
                    "第 " + i + " 个格子不在 slotContainer 底下，扩出来的新格子会排到别处去");
            }
        }

        [Test]
        public void InventoryUI_DetailPaneIsWired()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty detail = new SerializedObject(ui).FindProperty("detail");

            Assert.IsNotNull(detail.objectReferenceValue, "InventoryUI.detail 在场景里是空的");
        }

        [Test]
        public void EverySlotButton_IsWiredToClick()
        {
            // 按钮监听是用持久化监听挂的（序列化进场景），所以这里能直接查到。
            // 漏了这条，表现是"格子点上去没反应"，而且不报任何错。
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty slots = new SerializedObject(ui).FindProperty("slots");

            for (int i = 0; i < slots.arraySize; i++)
            {
                InventorySlotUI slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as InventorySlotUI;
                Assert.IsNotNull(slot);

                Button button = slot.GetComponent<Button>();
                Assert.IsNotNull(button, "第 " + i + " 个格子上没有 Button");
                Assert.Greater(button.onClick.GetPersistentEventCount(), 0, "第 " + i + " 个格子的按钮没接点击");
                Assert.AreEqual("Click", button.onClick.GetPersistentMethodName(0), "第 " + i + " 个格子接错了方法");
            }
        }

        [Test]
        public void EquipButton_IsWiredToEquipClicked()
        {
            WeaponDetailUI detail = FindInScene<WeaponDetailUI>();
            Button equip = new SerializedObject(detail).FindProperty("equipButton").objectReferenceValue as Button;

            Assert.IsNotNull(equip, "详情区的装备按钮没接");
            Assert.Greater(equip.onClick.GetPersistentEventCount(), 0, "装备按钮没接点击");
            Assert.AreEqual("OnEquipClicked", equip.onClick.GetPersistentMethodName(0));
        }

        [Test]
        public void InventoryComponent_IsPresent()
        {
            // InventoryUI 只是投影，数据在 InventoryComponent 里，少一个都不行
            Assert.IsNotNull(FindInScene<InventoryComponent>());
        }

        [Test]
        public void EverySlot_HasItsLabelsWired()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            SerializedProperty slots = new SerializedObject(ui).FindProperty("slots");

            for (int i = 0; i < slots.arraySize; i++)
            {
                InventorySlotUI slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as InventorySlotUI;
                Assert.IsNotNull(slot, "第 " + i + " 个格子不是 InventorySlotUI");

                SerializedObject so = new SerializedObject(slot);
                Assert.IsNotNull(so.FindProperty("iconImage").objectReferenceValue, "第 " + i + " 个格子缺 iconImage");
                Assert.IsNotNull(so.FindProperty("nameLabel").objectReferenceValue, "第 " + i + " 个格子缺 nameLabel");
                Assert.IsNotNull(so.FindProperty("amountLabel").objectReferenceValue, "第 " + i + " 个格子缺 amountLabel");
            }
        }

        [Test]
        public void InventoryPanel_StartsHidden()
        {
            InventoryUI ui = FindInScene<InventoryUI>();
            GameObject panel = new SerializedObject(ui).FindProperty("panel").objectReferenceValue as GameObject;

            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.activeSelf, "进游戏时背包面板应该是收起来的");
        }

        // ---------------------------------------------------------
        // 装备
        // ---------------------------------------------------------

        [Test]
        public void EquipmentComponent_IsPresent()
        {
            // 少了它，详情区的装备按钮就只是往总线喊一声，没人接
            Assert.IsNotNull(FindInScene<EquipmentComponent>());
        }

        [Test]
        public void EquipmentComponent_PlayerStatsIsWired()
        {
            // 这个框空着不会报错，表现是"武器装上了但攻击力没变"——
            // 不痛不痒，最难查的那一类
            EquipmentComponent equipment = FindInScene<EquipmentComponent>();
            SerializedProperty stats = new SerializedObject(equipment).FindProperty("playerStats");

            Assert.IsNotNull(stats, "EquipmentComponent 上没有 playerStats 字段");
            Assert.IsNotNull(stats.objectReferenceValue, "EquipmentComponent.playerStats 在场景里是空的");
        }

        [Test]
        public void EquipmentComponent_IsOnGameSystems()
        {
            // 挂在别处也能跑，但 GameSystems 是这套桥接层统一待的地方
            // （InventoryComponent / QuestComponent / DropSpawner 都在那）
            EquipmentComponent equipment = FindInScene<EquipmentComponent>();

            Assert.AreEqual("GameSystems", equipment.gameObject.name);
        }

        [Test]
        public void PlayerStats_IsPresent()
        {
            // Hitbox 读的就是它，少了它攻击力永远按 flatDamage 算
            Assert.IsNotNull(FindInScene<Demo.Player.PlayerStats>());
        }

        [Test]
        public void WeaponSocket_IsPresent()
        {
            // 少了它，装备能装上、攻击力也涨，但手上永远空着
            Assert.IsNotNull(FindInScene<WeaponSocket>());
        }

        [Test]
        public void WeaponSocket_SocketIsWired()
        {
            WeaponSocket socket = FindInScene<WeaponSocket>();
            SerializedProperty bone = new SerializedObject(socket).FindProperty("socket");

            Assert.IsNotNull(bone, "WeaponSocket 上没有 socket 字段");
            Assert.IsNotNull(bone.objectReferenceValue, "WeaponSocket.socket 在场景里是空的");
        }

        [Test]
        public void WeaponSocket_PointsAtTheHandBone()
        {
            // 光"非空"不够 —— 挂到随便哪个 Transform 上都不报错，
            // 表现是武器飘在角色旁边。必须是 hand_r 底下那个 weapon_r
            WeaponSocket socket = FindInScene<WeaponSocket>();
            Transform bone = new SerializedObject(socket).FindProperty("socket").objectReferenceValue as Transform;

            Assert.IsNotNull(bone);
            Assert.AreEqual("weapon_r", bone.name);
            Assert.IsNotNull(bone.parent, "挂点骨骼不该是根节点");
            Assert.AreEqual("hand_r", bone.parent.name, "挂点必须在右手骨骼底下，否则跟不了手臂动作");
        }

        [Test]
        public void WeaponSocket_IsOnGameSystems()
        {
            Assert.AreEqual("GameSystems", FindInScene<WeaponSocket>().gameObject.name);
        }

        // ---------------------------------------------------------
        // 背包以外，顺手把之前踩过的坑一起钉住
        // ---------------------------------------------------------

        [Test]
        public void DebugHud_ReferencesAreWired()
        {
            // questComponent 曾经是 {fileID: 0}
            Component[] huds = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Component hud = System.Array.Find(huds, c => c != null && c.GetType().Name == "DebugHud");

            Assert.IsNotNull(hud, "场景里找不到 DebugHud");

            SerializedObject so = new SerializedObject(hud);
            foreach (string field in new[] { "questComponent", "inventory" })
            {
                SerializedProperty property = so.FindProperty(field);
                Assert.IsNotNull(property, "DebugHud 上没有 " + field + " 字段");
                Assert.IsNotNull(property.objectReferenceValue, "DebugHud." + field + " 在场景里是空的");
            }
        }

        // ---------------------------------------------------------
        // 开局武器
        //
        // MC01 模型出厂时 weapon_r 底下就嵌着一把 OHS09_Sword。那把剑要算作
        // 玩家开局握着的武器，换成别的武器时它才会退回背包而不是凭空消失。
        // 这几条把"忘了在场景里拖引用"挡在上线之前 —— 漏了的话玩家空手开局，
        // 而且骨骼上那把原生剑会一直挂着。
        // ---------------------------------------------------------

        [Test]
        public void EquipmentComponent_StartingWeaponIsWired()
        {
            EquipmentComponent equipment = FindInScene<EquipmentComponent>();
            SerializedProperty starting = new SerializedObject(equipment).FindProperty("startingWeapon");

            Assert.IsNotNull(starting, "EquipmentComponent 上没有 startingWeapon 字段");
            Assert.IsNotNull(starting.objectReferenceValue, "startingWeapon 在场景里是空的，玩家会空手开局");
        }

        [Test]
        public void EquipmentComponent_StartingWeaponIsTheSwordOnTheBone()
        {
            // 接错资源的话，右手上会出现一把玩家没见过的武器
            EquipmentComponent equipment = FindInScene<EquipmentComponent>();
            WeaponData weapon = new SerializedObject(equipment)
                .FindProperty("startingWeapon").objectReferenceValue as WeaponData;

            Assert.IsNotNull(weapon);
            Assert.AreEqual("OHS09Sword", weapon.id, "开局武器应该就是 MC01 骨骼上预摆的那把剑");
            Assert.IsNotNull(weapon.weaponPrefab, "开局武器没有模型，手上会是空的");
            Assert.AreEqual("OHS09_Sword", weapon.weaponPrefab.name);
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Demo.Data;
using Demo.Equipment;
using Demo.Player;

/// <summary>
/// 把装备系统接进 Desert 场景。
///
/// 和 BuildInventoryUI 分开的理由：那个脚本是"把 UI 拆了重建"，
/// 这个是"往 GameSystems 上补一个组件 + 连一根线"。混在一起的话，
/// 每次重跑背包布局都会顺手动一遍装备接线。
///
/// 幂等：EquipmentComponent 已经在了就复用，只重连引用。
/// </summary>
public static class WireEquipment
{
    private const string ScenePath = "Assets/Lowpoly Style/Desert/DemoScene/Desert.unity";
    private const string SystemsObjectName = "GameSystems";

    /// <summary>MC01 骨骼自带的武器挂点，是 hand_r 的子物体。</summary>
    private const string SocketBoneName = "weapon_r";

    /// <summary>
    /// 开局武器。模型就是 MC01 的 weapon_r 底下预摆的那把 OHS09_Sword，
    /// socketLocal* 用的也是它原本的变换（单位变换），所以换手后看着一模一样。
    /// </summary>
    private const string StartingWeaponPath = "Assets/Weapon_OHS09Sword.asset";

    [MenuItem("Demo/接线装备系统")]
    public static void Wire()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject systems = GameObject.Find(SystemsObjectName);
        if (systems == null)
        {
            Debug.LogError("[WireEquipment] 场景里找不到 " + SystemsObjectName + "，中止");
            return;
        }

        // PlayerStats 挂在玩家身上，可能是个 Prefab 实例，
        // 所以按类型找而不是按名字找
        PlayerStats[] found = Object.FindObjectsByType<PlayerStats>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found.Length != 1)
        {
            Debug.LogError("[WireEquipment] 场景里应该有且只有一个 PlayerStats，实际找到 "
                + found.Length + " 个，中止");
            return;
        }

        PlayerStats stats = found[0];

        Transform socket = FindSocketBone(stats.transform);
        if (socket == null)
        {
            Debug.LogError("[WireEquipment] 在 " + stats.gameObject.name + " 底下找不到 "
                + SocketBoneName + " 骨骼，中止");
            return;
        }

        EquipmentComponent equipment = systems.GetComponent<EquipmentComponent>();
        if (equipment == null)
        {
            equipment = systems.AddComponent<EquipmentComponent>();
        }

        WeaponSocket weaponSocket = systems.GetComponent<WeaponSocket>();
        if (weaponSocket == null)
        {
            weaponSocket = systems.AddComponent<WeaponSocket>();
        }

        // Bind 会顺手订阅 EventBus。那是 Play 模式的运行时行为，
        // 在编辑器里执行完就退订，免得在编辑器域里留一个指向场景对象的处理器。
        equipment.Bind(stats);
        equipment.StopListening();

        weaponSocket.Bind(socket);
        weaponSocket.StopListening();

        // 开局武器：MC01 模型出厂时 weapon_r 底下就嵌着一把 OHS09_Sword，
        // 那把剑得登记成玩家开局握着的武器。不登记的话，换成别的武器时
        // 它只会被挂点销毁 —— 凭空消失、也不会退回背包。
        WeaponData starting = AssetDatabase.LoadAssetAtPath<WeaponData>(StartingWeaponPath);
        if (starting == null)
        {
            Debug.LogError("[WireEquipment] 加载不到 " + StartingWeaponPath + "，开局武器没接上");
            return;
        }

        equipment.SetStartingWeapon(starting);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[WireEquipment] 完成：GameSystems.EquipmentComponent -> "
            + stats.gameObject.name + ".PlayerStats（开局武器 " + starting.displayName + "），"
            + "GameSystems.WeaponSocket -> " + SocketPath(socket));
    }

    /// <summary>
    /// 按名字在玩家层级里找挂点骨骼。
    ///
    /// 名字查找只在**编辑器里**跑这一次，找到的 Transform 会被序列化进场景，
    /// 运行时是直接引用，不存在字符串查找。所以这里用名字是可以接受的。
    /// </summary>
    private static Transform FindSocketBone(Transform playerRoot)
    {
        Transform[] all = playerRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == SocketBoneName)
            {
                return all[i];
            }
        }

        return null;
    }

    private static string SocketPath(Transform bone)
    {
        string path = bone.name;

        for (Transform p = bone.parent; p != null; p = p.parent)
        {
            path = p.name + "/" + path;
        }

        return path;
    }
}

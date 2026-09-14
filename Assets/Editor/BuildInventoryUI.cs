using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Demo.Inventory;
using Demo.UI;

/// <summary>
/// 一次性把背包 UI 搭进 Desert 场景。
///
/// 为什么用脚本而不是手改 .unity：场景有 1.1MB，fileID 引用全是手写的 YAML，
/// 手改一次错一次。让 Unity 自己序列化，出来的东西一定是它认得的。
///
/// 幂等：重复执行会先删掉上次生成的 Canvas / EventSystem 再重建，
/// 所以改完布局参数直接重跑就行。
///
/// 层级：
///     InventoryCanvas
///       InventoryPanel
///         Title
///         ScrollArea (ScrollRect)
///           Viewport (RectMask2D)
///             Content (GridLayoutGroup + ContentSizeFitter)   <- 格子往这里塞
///           Scrollbar
///         WeaponDetail
///           EmptyHint / Icon / Name / Damage / Description / EquipButton
///         Hint
///
/// 按钮的点击用 UnityEventTools.AddPersistentListener 挂成**持久化监听**。
/// 这样它会被序列化进场景，Play 模式直接生效，不依赖 Awake / OnEnable
/// 那套运行时生命周期回调 —— 那套在 EditMode 下不触发，测不了。
/// </summary>
public static class BuildInventoryUI
{
    private const string ScenePath = "Assets/Lowpoly Style/Desert/DemoScene/Desert.unity";
    private const string SystemsObjectName = "GameSystems";

    private const int Columns = 4;
    private const int Rows = 3;
    private const float CellSize = 116f;
    private const float Spacing = 10f;
    private const float SidePadding = 18f;

    private const float PanelWidth = 920f;
    private const float PanelHeight = 480f;
    private const float DetailWidth = 300f;
    private const float DetailGap = 20f;

    private const float TitleHeight = 44f;
    private const float HintHeight = 28f;
    private const float ScrollbarWidth = 12f;

    /// <summary>面板顶 / 底留给标题和提示的空间。</summary>
    private const float TopInset = 60f;
    private const float BottomInset = 40f;

    private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.10f, 0.95f);
    private static readonly Color SlotColor = new Color(0.16f, 0.17f, 0.21f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color MutedColor = new Color(0.60f, 0.63f, 0.70f, 1f);
    private static readonly Color DamageColor = new Color(0.95f, 0.72f, 0.32f, 1f);
    private static readonly Color ButtonColor = new Color(0.24f, 0.42f, 0.72f, 1f);
    private static readonly Color ScrollbarTrackColor = new Color(0.12f, 0.13f, 0.16f, 1f);
    private static readonly Color ScrollbarHandleColor = new Color(0.34f, 0.36f, 0.42f, 1f);

    [MenuItem("Demo/构建背包 UI")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject systems = GameObject.Find(SystemsObjectName);
        if (systems == null)
        {
            Debug.LogError("[BuildInventoryUI] 场景里找不到 " + SystemsObjectName + "，中止");
            return;
        }

        InventoryComponent inventory = systems.GetComponent<InventoryComponent>();
        if (inventory == null)
        {
            Debug.LogError("[BuildInventoryUI] " + SystemsObjectName + " 上没有 InventoryComponent，中止");
            return;
        }

        // 先拆掉上次生成的，保证重复执行结果一致
        DestroyIfExists("InventoryCanvas");
        DestroyIfExists("EventSystem");

        CreateEventSystem();

        GameObject panel = CreateCanvas();
        ScrollRect scroll = CreateScrollArea(panel.transform, out RectTransform content);

        InventorySlotUI[] slots = CreateSlots(content, Columns * Rows);
        WeaponDetailUI detail = CreateDetail(panel.transform);
        CreateTitle(panel.transform);
        CreateHint(panel.transform);

        InventoryUI ui = systems.GetComponent<InventoryUI>();
        if (ui == null)
        {
            ui = systems.AddComponent<InventoryUI>();
        }

        // Bind 内部会把面板置为关闭态，所以存盘后场景里背包就是收起来的
        ui.Bind(panel, inventory, scroll, content, slots, detail);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[BuildInventoryUI] 完成：Canvas + EventSystem + 滚动区 + 详情区 + " + slots.Length + " 个格子");
    }

    // -----------------------------------------------------------------
    // 搭件
    // -----------------------------------------------------------------

    private static void CreateEventSystem()
    {
        // 场景里原本没有 EventSystem，按钮点不动就是缺了它
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static GameObject CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "InventoryCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        rect.anchoredPosition = Vector2.zero;

        panel.GetComponent<Image>().color = PanelColor;

        return panel;
    }

    /// <summary>
    /// 滚动区。物品超过场景里摆好的格子数时，多出来的格子塞进 Content，
    /// ContentSizeFitter 把 Content 撑高，ScrollRect 就能滚了。
    /// </summary>
    private static ScrollRect CreateScrollArea(Transform panel, out RectTransform content)
    {
        GameObject areaObject = new GameObject("ScrollArea", typeof(RectTransform), typeof(ScrollRect));
        areaObject.transform.SetParent(panel, false);

        RectTransform areaRect = areaObject.GetComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0f, 0f);
        areaRect.anchorMax = new Vector2(1f, 1f);
        areaRect.offsetMin = new Vector2(12f, BottomInset);
        areaRect.offsetMax = new Vector2(-(12f + DetailWidth + DetailGap), -TopInset);

        // --- Viewport ---
        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportObject.transform.SetParent(areaObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        // --- Content ---
        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));

        contentObject.transform.SetParent(viewportObject.transform, false);

        content = contentObject.GetComponent<RectTransform>();
        // 竖滚的标准摆法：顶部对齐 + 横向拉伸，高度交给 ContentSizeFitter
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup layout = contentObject.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(CellSize, CellSize);
        layout.spacing = new Vector2(Spacing, Spacing);
        layout.padding = new RectOffset((int)SidePadding, (int)(SidePadding + ScrollbarWidth), 0, 0);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Columns;
        layout.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(areaObject.transform);

        ScrollRect scroll = areaObject.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        return scroll;
    }

    private static Scrollbar CreateScrollbar(Transform parent)
    {
        GameObject scrollbarObject = new GameObject(
            "Scrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar));

        scrollbarObject.transform.SetParent(parent, false);

        RectTransform rect = scrollbarObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(ScrollbarWidth, -4f);
        rect.anchoredPosition = new Vector2(-2f, 0f);

        scrollbarObject.GetComponent<Image>().color = ScrollbarTrackColor;

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarObject.transform, false);

        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = Vector2.zero;
        slidingRect.offsetMax = Vector2.zero;

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingArea.transform, false);

        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(2f, 2f);
        handleRect.offsetMax = new Vector2(-2f, -2f);

        handleObject.GetComponent<Image>().color = ScrollbarHandleColor;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleObject.GetComponent<Image>();

        return scrollbar;
    }

    // -----------------------------------------------------------------
    // 详情区
    // -----------------------------------------------------------------

    private static WeaponDetailUI CreateDetail(Transform panel)
    {
        GameObject detailObject = new GameObject("WeaponDetail", typeof(RectTransform));
        detailObject.transform.SetParent(panel, false);

        RectTransform rect = detailObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(DetailWidth, -(TopInset + BottomInset));
        rect.anchoredPosition = new Vector2(-12f, (BottomInset - TopInset) * 0.5f);

        GameObject hint = MakeText(
            "EmptyHint",
            detailObject.transform,
            "选择一件物品\n查看详情",
            16f,
            TextAlignmentOptions.Center);

        hint.GetComponent<TextMeshProUGUI>().color = MutedColor;
        Stretch(hint.GetComponent<RectTransform>(), 12f, 12f, 12f, 12f);

        // --- 图标 ---
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(detailObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = new Vector2(96f, 96f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = false;

        // --- 名字 ---
        GameObject nameObject = MakeText("Name", detailObject.transform, string.Empty, 20f, TextAlignmentOptions.Center);
        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.sizeDelta = new Vector2(-16f, 30f);
        nameRect.anchoredPosition = new Vector2(0f, -116f);

        // --- 攻击力 ---
        GameObject damageObject = MakeText("Damage", detailObject.transform, string.Empty, 18f, TextAlignmentOptions.Center);
        damageObject.GetComponent<TextMeshProUGUI>().color = DamageColor;

        RectTransform damageRect = damageObject.GetComponent<RectTransform>();
        damageRect.anchorMin = new Vector2(0f, 1f);
        damageRect.anchorMax = new Vector2(1f, 1f);
        damageRect.pivot = new Vector2(0.5f, 1f);
        damageRect.sizeDelta = new Vector2(-16f, 26f);
        damageRect.anchoredPosition = new Vector2(0f, -150f);

        // --- 描述 ---
        GameObject descriptionObject = MakeText("Description", detailObject.transform, string.Empty, 14f, TextAlignmentOptions.TopLeft);
        descriptionObject.GetComponent<TextMeshProUGUI>().color = MutedColor;

        RectTransform descriptionRect = descriptionObject.GetComponent<RectTransform>();
        descriptionRect.anchorMin = new Vector2(0f, 0f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.offsetMin = new Vector2(12f, 68f);
        descriptionRect.offsetMax = new Vector2(-12f, -184f);

        // --- 装备按钮 ---
        GameObject buttonObject = new GameObject(
            "EquipButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(detailObject.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(DetailWidth - 48f, 44f);
        buttonRect.anchoredPosition = new Vector2(0f, 14f);

        Image buttonBackground = buttonObject.GetComponent<Image>();
        buttonBackground.color = ButtonColor;

        TextMeshProUGUI buttonLabel = MakeText(
            "Label",
            buttonObject.transform,
            "装备",
            18f,
            TextAlignmentOptions.Center).GetComponent<TextMeshProUGUI>();

        Stretch(buttonLabel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        Button equipButton = buttonObject.GetComponent<Button>();
        equipButton.targetGraphic = buttonBackground;

        WeaponDetailUI detail = detailObject.AddComponent<WeaponDetailUI>();
        detail.Bind(
            hint,
            iconImage,
            nameObject.GetComponent<TextMeshProUGUI>(),
            descriptionObject.GetComponent<TextMeshProUGUI>(),
            damageObject.GetComponent<TextMeshProUGUI>(),
            equipButton);

        // 持久化监听：序列化进场景，Play 模式直接生效
        UnityEventTools.AddPersistentListener(equipButton.onClick, detail.OnEquipClicked);

        return detail;
    }

    // -----------------------------------------------------------------
    // 格子
    // -----------------------------------------------------------------

    private static InventorySlotUI[] CreateSlots(Transform content, int count)
    {
        InventorySlotUI[] slots = new InventorySlotUI[count];
        for (int i = 0; i < count; i++)
        {
            slots[i] = CreateSlot(content, i);
        }

        return slots;
    }

    private static InventorySlotUI CreateSlot(Transform parent, int index)
    {
        GameObject slotObject = new GameObject(
            "Slot" + index,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        slotObject.transform.SetParent(parent, false);

        Image background = slotObject.GetComponent<Image>();
        background.color = SlotColor;

        Button button = slotObject.GetComponent<Button>();
        button.targetGraphic = background;

        // --- 图标 ---
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        iconRect.anchoredPosition = new Vector2(0f, -10f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = false;

        // --- 名字 ---
        GameObject nameObject = MakeText("Name", slotObject.transform, string.Empty, 15f, TextAlignmentOptions.Center);
        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.sizeDelta = new Vector2(-8f, 40f);
        nameRect.anchoredPosition = new Vector2(0f, 6f);

        // --- 数量 ---
        GameObject amountObject = MakeText("Amount", slotObject.transform, string.Empty, 14f, TextAlignmentOptions.BottomRight);
        RectTransform amountRect = amountObject.GetComponent<RectTransform>();
        amountRect.anchorMin = new Vector2(1f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0f);
        amountRect.pivot = new Vector2(1f, 0f);
        amountRect.sizeDelta = new Vector2(40f, 22f);
        amountRect.anchoredPosition = new Vector2(-4f, 4f);

        InventorySlotUI slot = slotObject.AddComponent<InventorySlotUI>();
        slot.Bind(iconImage, nameObject.GetComponent<TextMeshProUGUI>(), amountObject.GetComponent<TextMeshProUGUI>());

        // 持久化监听：点格子 -> InventoryUI 会转发给详情区
        UnityEventTools.AddPersistentListener(button.onClick, slot.Click);

        return slot;
    }

    // -----------------------------------------------------------------

    private static void CreateTitle(Transform parent)
    {
        GameObject title = MakeText("Title", parent, "背包", 28f, TextAlignmentOptions.Center);
        RectTransform rect = title.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-24f, TitleHeight);
        rect.anchoredPosition = new Vector2(0f, -12f);
    }

    private static void CreateHint(Transform parent)
    {
        GameObject hint = MakeText("Hint", parent, "按 I 或 ESC 关闭", 16f, TextAlignmentOptions.Center);
        hint.GetComponent<TextMeshProUGUI>().color = MutedColor;

        RectTransform rect = hint.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(-24f, HintHeight);
        rect.anchoredPosition = new Vector2(0f, 8f);
    }

    private static GameObject MakeText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = TextColor;
        tmp.raycastTarget = false;

        return go;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void DestroyIfExists(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }
}

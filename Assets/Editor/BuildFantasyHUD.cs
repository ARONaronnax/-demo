#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Demo.Combat;
using Demo.Debugging;
using Demo.Dialogue;
using Demo.Quest;
using Demo.UI;

/// <summary>
/// 生成可替换 Sprite 的 Fantasy HUD Prefab，并只在场景层做引用接线。
/// 可从 Tools/Demo UI/Rebuild Fantasy HUD 重复执行。
/// </summary>
public static class BuildFantasyHUD
{
    private const string ScenePath = "Assets/Lowpoly Style/Desert/DemoScene/Desert.unity";
    private const string AtlasPath = "Assets/UI/FantasyHUD/Source/FantasyHUD_Atlas.png";
    private const string PortraitPath = "Assets/UI/FantasyHUD/Source/FantasyHUD_Portraits.png";
    private const string PrefabFolder = "Assets/UI/FantasyHUD/Prefabs";
    private const string FontPath = "Assets/UI/Fonts/NotoSansSC SDF.asset";

    private static TMP_FontAsset _font;
    private static readonly Color Ink = new Color(0.20f, 0.105f, 0.055f, 1f);
    private static readonly Color Cream = new Color(1f, 0.91f, 0.72f, 1f);

    [InitializeOnLoadMethod]
    private static void AutoBuildWhenEditorIsOpen()
    {
        // 项目正在 Editor 中打开时，命令行实例会被 Unity 的项目锁挡住。
        // 首次脚本编译成功后自动构建一次；Prefab 已存在时不重复改场景。
        EditorApplication.delayCall += () =>
        {
            if (!Application.isBatchMode &&
                (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/GameHUD.prefab") == null ||
                 AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/InteractionPromptPanel.prefab") == null))
            {
                Build();
            }
        };
    }

    [MenuItem("Tools/Demo UI/Rebuild Fantasy HUD")]
    public static void Build()
    {
        EnsureFolder("Assets/UI/FantasyHUD");
        EnsureFolder(PrefabFolder);
        ConfigureAtlas();
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject player = SavePrefab("PlayerStatusPanel", BuildPlayerStatus());
        GameObject menu = SavePrefab("TopRightMenu", BuildTopMenu());
        GameObject minimap = SavePrefab("MiniMapPanel", BuildMiniMap());
        GameObject dialogue = SavePrefab("DialoguePanel", BuildDialogue());
        GameObject quest = SavePrefab("QuestSummaryPanel", BuildQuestSummary());
        GameObject interaction = SavePrefab("InteractionPromptPanel", BuildInteractionPrompt());
        GameObject hud = SavePrefab("GameHUD", BuildHud(player, menu, minimap, dialogue, quest, interaction));

        WireScene(hud);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildFantasyHUD] 已生成 7 个 UI Prefab 并接入 Desert 场景。");
    }

    [MenuItem("Tools/Demo UI/Capture Fantasy HUD Preview")]
    public static void CapturePreview()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Canvas canvas = GameObject.Find("GameHUD").GetComponent<Canvas>();
        Camera camera = UnityEngine.Object.FindObjectsOfType<Camera>(true).FirstOrDefault(c => c.gameObject.activeInHierarchy);
        if (camera == null) throw new InvalidOperationException("场景中找不到可用 Camera");

        DialoguePanel dialogue = canvas.GetComponentInChildren<DialoguePanel>(true);
        FindChild(dialogue.transform, "Visual").gameObject.SetActive(true);
        FindChild(dialogue.transform, "Option0").gameObject.SetActive(false);
        FindChild(dialogue.transform, "Option1").gameObject.SetActive(false);
        FindChild(dialogue.transform, "Option2").gameObject.SetActive(false);

        RenderMode oldMode = canvas.renderMode;
        Camera oldWorldCamera = canvas.worldCamera;
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/FantasyHUD-preview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = oldTarget;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldWorldCamera;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(rt);
        }
        Debug.Log("[BuildFantasyHUD] Preview: Artifacts/FantasyHUD-preview.png");
    }

    private static void ConfigureAtlas()
    {
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter atlas = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        atlas.textureType = TextureImporterType.Sprite;
        atlas.spriteImportMode = SpriteImportMode.Single;
        atlas.alphaIsTransparency = true;
        atlas.mipmapEnabled = false;
        atlas.filterMode = FilterMode.Bilinear;
        atlas.npotScale = TextureImporterNPOTScale.None;
        atlas.SaveAndReimport();
        Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        int atlasWidth = atlasTexture.width;
        int atlasHeight = atlasTexture.height;
        atlas = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        atlas.spriteImportMode = SpriteImportMode.Multiple;

        string[] names = {
            "DialogueFrame", "HpFrame", "PortraitFrame", "MiniMapFrame",
            "ButtonFrame", "HpFill", "Heart", "Backpack",
            "Quest", "Map", "Settings", "ContinueArrow",
            "LeafCorner", "Nameplate", "Rivet", "Tooltip"
        };
        Vector4[] borders = {
            new Vector4(62,54,62,54), new Vector4(64,48,64,48), Vector4.zero, Vector4.zero,
            new Vector4(48,48,48,48), new Vector4(60,36,60,36), Vector4.zero, Vector4.zero,
            Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero,
            Vector4.zero, new Vector4(54,34,54,34), Vector4.zero, new Vector4(50,50,50,50)
        };
        SpriteMetaData[] sheet = new SpriteMetaData[16];
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 4; col++)
        {
            int i = row * 4 + col;
            int x0 = Mathf.RoundToInt(atlasWidth * col / 4f);
            int x1 = Mathf.RoundToInt(atlasWidth * (col + 1) / 4f);
            int y0 = Mathf.RoundToInt(atlasHeight * (3 - row) / 4f);
            int y1 = Mathf.RoundToInt(atlasHeight * (4 - row) / 4f);
            sheet[i] = new SpriteMetaData {
                name = names[i], rect = new Rect(x0, y0, x1 - x0, y1 - y0),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f,.5f), border = borders[i]
            };
        }
        atlas.spritesheet = sheet;
        atlas.SaveAndReimport();

        AssetDatabase.ImportAsset(PortraitPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter portraits = (TextureImporter)AssetImporter.GetAtPath(PortraitPath);
        portraits.textureType = TextureImporterType.Sprite;
        portraits.spriteImportMode = SpriteImportMode.Single;
        portraits.alphaIsTransparency = true;
        portraits.mipmapEnabled = false;
        portraits.filterMode = FilterMode.Bilinear;
        portraits.npotScale = TextureImporterNPOTScale.None;
        portraits.SaveAndReimport();
        Texture2D portraitTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath);
        int portraitHalfWidth = portraitTexture.width / 2;
        int portraitHeight = portraitTexture.height;
        portraits = (TextureImporter)AssetImporter.GetAtPath(PortraitPath);
        portraits.spriteImportMode = SpriteImportMode.Multiple;
        portraits.spritesheet = new[] {
            new SpriteMetaData { name="PlayerPortrait", rect=new Rect(0,0,portraitHalfWidth,portraitHeight), alignment=(int)SpriteAlignment.Center, pivot=new Vector2(.5f,.5f) },
            new SpriteMetaData { name="NpcPortrait", rect=new Rect(portraitHalfWidth,0,portraitTexture.width-portraitHalfWidth,portraitHeight), alignment=(int)SpriteAlignment.Center, pivot=new Vector2(.5f,.5f) }
        };
        portraits.SaveAndReimport();
    }

    private static GameObject BuildPlayerStatus()
    {
        GameObject root = RectObject("PlayerStatusPanel", null, new Vector2(690,220), new Vector2(28,24), new Vector2(0,0), new Vector2(0,0));
        PlayerStatusPanel view = root.AddComponent<PlayerStatusPanel>();

        Image frame = ImageObject("HpWoodFrame", root.transform, S("HpFrame"), new Vector2(520,122), new Vector2(182,36), new Vector2(0,0));
        frame.type = Image.Type.Sliced;
        Image fill = ImageObject("HpFill", frame.transform, S("HpFill"), new Vector2(430,52), new Vector2(45,4), new Vector2(0,0));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;

        Image portraitFrame = ImageObject("PortraitFrame", root.transform, S("PortraitFrame"), new Vector2(202,202), new Vector2(0,18), new Vector2(0,0));
        portraitFrame.preserveAspect = true;
        portraitFrame.raycastTarget = false;
        Image portrait = ImageObject("PlayerPortrait", root.transform, S("PlayerPortrait", PortraitPath), new Vector2(154,154), new Vector2(24,46), new Vector2(0,0));
        portrait.preserveAspect = true;
        ImageObject("Heart", root.transform, S("Heart"), new Vector2(74,74), new Vector2(174,57), new Vector2(0,0)).preserveAspect = true;

        TMP_Text hp = TextObject("HpValue", frame.transform, "100 / 100", 31, Color.white, TextAlignmentOptions.Center,
            new Vector2(420,54), new Vector2(51,3), new Vector2(0,0));
        AddTextOutline(hp, new Color(0.18f,0.05f,0.03f,1f), new Vector2(2,-2));

        Image levelBg = ImageObject("LevelNameplate", root.transform, S("Nameplate"), new Vector2(150,58), new Vector2(26,4), new Vector2(0,0));
        levelBg.type = Image.Type.Sliced;
        TMP_Text level = TextObject("Level", levelBg.transform, "Lv.10", 25, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, new Vector2(.5f,.5f));
        Stretch(level.rectTransform, new Vector2(12,4), new Vector2(-12,-4));
        AddTextOutline(level, Ink, new Vector2(1.5f,-1.5f));
        view.Bind(null, fill, hp, portrait);
        return root;
    }

    private static GameObject BuildTopMenu()
    {
        GameObject root = RectObject("TopRightMenu", null, new Vector2(600,150), new Vector2(-285,-24), Vector2.one, Vector2.one);
        TopRightMenu view = root.AddComponent<TopRightMenu>();
        string[] names = { "InventoryButton", "QuestButton", "MapButton", "SettingsButton" };
        string[] icons = { "Backpack", "Quest", "Map", "Settings" };
        string[] labels = { "背包", "任务", "地图", "设置" };
        for (int i=0; i<4; i++)
        {
            Button button = MenuButton(names[i], root.transform, icons[i], labels[i], new Vector2(i*140,0));
            if (i==0) UnityEventTools.AddPersistentListener(button.onClick, view.OpenInventory);
            else if (i==1) UnityEventTools.AddPersistentListener(button.onClick, view.ToggleQuest);
            else if (i==2) UnityEventTools.AddPersistentListener(button.onClick, view.RequestMap);
            else UnityEventTools.AddPersistentListener(button.onClick, view.RequestSettings);
        }
        view.Bind(null, null);
        return root;
    }

    private static GameObject BuildMiniMap()
    {
        GameObject root = RectObject("MiniMapPanel", null, new Vector2(280,280), new Vector2(-18,-16), Vector2.one, Vector2.one);
        MiniMapPanel view = root.AddComponent<MiniMapPanel>();
        Image map = ImageObject("MapContent", root.transform, S("MiniMapFrame"), new Vector2(260,260), Vector2.zero, new Vector2(0,1));
        map.preserveAspect = true;
        RectTransform content = map.rectTransform;
        TMP_Text marker = TextObject("PlayerMarker", map.transform, "▲", 34, new Color(1f,.78f,.12f,1f), TextAlignmentOptions.Center,
            new Vector2(50,50), new Vector2(0,-4), new Vector2(.5f,.5f));
        AddTextOutline(marker, Ink, new Vector2(1,-1));
        view.Bind(content);
        return root;
    }

    private static GameObject BuildDialogue()
    {
        GameObject root = RectObject("DialoguePanel", null, new Vector2(930,300), new Vector2(34,-30), new Vector2(0,1), new Vector2(0,1));
        DialoguePanel view = root.AddComponent<DialoguePanel>();
        GameObject visual = RectObject("Visual", root.transform, new Vector2(930,300), Vector2.zero, Vector2.zero, Vector2.zero);

        Image paper = ImageObject("Parchment", visual.transform, S("DialogueFrame"), new Vector2(720,230), new Vector2(190,18), new Vector2(0,0));
        paper.type = Image.Type.Sliced;
        Button advance = paper.gameObject.AddComponent<Button>();
        advance.targetGraphic = paper;
        advance.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = advance.colors; cb.highlightedColor = new Color(1f,.98f,.86f,1f); cb.pressedColor = new Color(.9f,.82f,.68f,1f); advance.colors = cb;
        UnityEventTools.AddPersistentListener(advance.onClick, view.Advance);

        Image pframe = ImageObject("PortraitFrame", visual.transform, S("PortraitFrame"), new Vector2(240,240), new Vector2(-6,9), new Vector2(0,0));
        pframe.preserveAspect = true; pframe.raycastTarget = false;
        Image portrait = ImageObject("NpcPortrait", visual.transform, S("NpcPortrait", PortraitPath), new Vector2(182,182), new Vector2(23,38), new Vector2(0,0));
        portrait.preserveAspect = true;

        Image nameplate = ImageObject("Nameplate", visual.transform, S("Nameplate"), new Vector2(310,64), new Vector2(245,218), new Vector2(0,0));
        nameplate.type = Image.Type.Sliced;
        TMP_Text speaker = TextObject("Speaker", nameplate.transform, "旅行的法师", 27, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, new Vector2(.5f,.5f));
        Stretch(speaker.rectTransform, new Vector2(18,5), new Vector2(-18,-5)); AddTextOutline(speaker, Ink, new Vector2(1.5f,-1.5f));
        TMP_Text line = TextObject("DialogueText", paper.transform, "这里的沙漠看起来很平静，\n但危险可能就在风沙之后。", 27, Ink, TextAlignmentOptions.TopLeft,
            new Vector2(590,125), new Vector2(78,-48), new Vector2(0,1));
        line.enableWordWrapping = true; line.lineSpacing = 8;

        Image arrow = ImageObject("ContinueHint", paper.transform, S("ContinueArrow"), new Vector2(44,44), new Vector2(-62,25), new Vector2(1,0));
        arrow.preserveAspect = true; arrow.raycastTarget = false;

        Button[] options = new Button[3];
        for (int i=0; i<3; i++)
        {
            Image bg = ImageObject("Option"+i, visual.transform, S("Tooltip"), new Vector2(520,55), new Vector2(315,-42-i*60), new Vector2(0,1));
            bg.type = Image.Type.Sliced;
            options[i] = bg.gameObject.AddComponent<Button>(); options[i].targetGraphic = bg;
            TMP_Text label = TextObject("Label", bg.transform, "选项", 21, Ink, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, new Vector2(.5f,.5f));
            Stretch(label.rectTransform, new Vector2(18,4), new Vector2(-18,-4));
        }
        UnityEventTools.AddPersistentListener(options[0].onClick, view.SelectOption0);
        UnityEventTools.AddPersistentListener(options[1].onClick, view.SelectOption1);
        UnityEventTools.AddPersistentListener(options[2].onClick, view.SelectOption2);
        view.Bind(visual, speaker, line, portrait, S("NpcPortrait", PortraitPath), arrow.gameObject, options, null);
        return root;
    }

    private static GameObject BuildQuestSummary()
    {
        GameObject root = RectObject("QuestSummaryPanel", null, new Vector2(420,154), new Vector2(236,72), new Vector2(0,.5f), new Vector2(0,.5f));
        QuestSummaryPanel view = root.AddComponent<QuestSummaryPanel>();
        GameObject visual = RectObject("Visual", root.transform, new Vector2(420,154), Vector2.zero, Vector2.zero, Vector2.zero);
        Image bg = ImageObject("Parchment", visual.transform, S("Tooltip"), new Vector2(420,154), Vector2.zero, Vector2.zero);
        bg.type = Image.Type.Sliced;
        TMP_Text title = TextObject("Title", bg.transform, "当前任务", 23, Cream, TextAlignmentOptions.Center,
            new Vector2(172,38), new Vector2(25,-9), new Vector2(0,1));
        title.fontStyle = FontStyles.Bold; AddTextOutline(title, Ink, new Vector2(1.5f,-1.5f));
        TMP_Text summary = TextObject("Summary", bg.transform, "暂无任务", 20, Ink, TextAlignmentOptions.TopLeft,
            new Vector2(352,72), new Vector2(34,-62), new Vector2(0,1)); summary.enableWordWrapping = true;
        view.Bind(visual, summary, null);
        return root;
    }

    private static GameObject BuildInteractionPrompt()
    {
        GameObject root = RectObject("InteractionPromptPanel", null, new Vector2(520,82), new Vector2(0,92), new Vector2(.5f,0), new Vector2(.5f,0));
        InteractionPromptPanel view = root.AddComponent<InteractionPromptPanel>();
        GameObject visual = RectObject("Visual", root.transform, new Vector2(520,82), Vector2.zero, Vector2.zero, Vector2.zero);
        Image bg = ImageObject("Frame", visual.transform, S("Tooltip"), new Vector2(520,82), Vector2.zero, Vector2.zero);
        bg.type = Image.Type.Sliced;
        TMP_Text prompt = TextObject("PromptText", bg.transform, "<b>[ E ]</b>  交互", 24, Ink, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, new Vector2(.5f,.5f));
        Stretch(prompt.rectTransform, new Vector2(34,12), new Vector2(-34,-12));
        view.Bind(visual, prompt);
        return root;
    }

    private static GameObject BuildHud(params GameObject[] panels)
    {
        GameObject root = new GameObject("GameHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
        foreach (GameObject prefab in panels) PrefabUtility.InstantiatePrefab(prefab, root.transform);
        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        return root;
    }

    private static void WireScene(GameObject hudPrefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject old = GameObject.Find("GameHUD"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
        GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);

        HealthComponent health = UnityEngine.Object.FindObjectsOfType<HealthComponent>(true).FirstOrDefault(h => h.IsPlayer);
        DialogueRunner runner = UnityEngine.Object.FindObjectOfType<DialogueRunner>(true);
        QuestComponent quest = UnityEngine.Object.FindObjectOfType<QuestComponent>(true);
        InventoryUI inventory = UnityEngine.Object.FindObjectOfType<InventoryUI>(true);

        PlayerStatusPanel statusView = hud.GetComponentInChildren<PlayerStatusPanel>(true);
        Image fill = FindChild(statusView.transform,"HpFill").GetComponent<Image>();
        TMP_Text hpText = FindChild(statusView.transform,"HpValue").GetComponent<TMP_Text>();
        Image playerPortrait = FindChild(statusView.transform,"PlayerPortrait").GetComponent<Image>();
        statusView.Bind(health, fill, hpText, playerPortrait);

        DialoguePanel dialogueView = hud.GetComponentInChildren<DialoguePanel>(true);
        Transform d = dialogueView.transform;
        GameObject visual = FindChild(d,"Visual").gameObject;
        Button[] choices = { FindChild(d,"Option0").GetComponent<Button>(), FindChild(d,"Option1").GetComponent<Button>(), FindChild(d,"Option2").GetComponent<Button>() };
        dialogueView.Bind(visual, FindChild(d,"Speaker").GetComponent<TMP_Text>(), FindChild(d,"DialogueText").GetComponent<TMP_Text>(),
            FindChild(d,"NpcPortrait").GetComponent<Image>(), S("NpcPortrait",PortraitPath), FindChild(d,"ContinueHint").gameObject, choices, runner);

        QuestSummaryPanel questView = hud.GetComponentInChildren<QuestSummaryPanel>(true);
        questView.Bind(FindChild(questView.transform,"Visual").gameObject, FindChild(questView.transform,"Summary").GetComponent<TMP_Text>(), quest);
        hud.GetComponentInChildren<TopRightMenu>(true).Bind(inventory, questView);

        InteractionPromptPanel interactionView = hud.GetComponentInChildren<InteractionPromptPanel>(true);
        interactionView.Bind(FindChild(interactionView.transform,"Visual").gameObject,
            FindChild(interactionView.transform,"PromptText").GetComponent<TMP_Text>());

        GameObject inventoryCanvasObject = GameObject.Find("InventoryCanvas");
        Canvas inventoryCanvas = inventoryCanvasObject != null ? inventoryCanvasObject.GetComponent<Canvas>() : null;
        if (inventoryCanvas != null) inventoryCanvas.sortingOrder = 30;
        DebugHud debug = UnityEngine.Object.FindObjectOfType<DebugHud>(true); if (debug != null) debug.enabled = false;

        if (UnityEngine.Object.FindObjectOfType<EventSystem>(true) == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BuildFantasyHUD] Scene wiring: Health="+(health!=null)+", Dialogue="+(runner!=null)+", Quest="+(quest!=null)+", Inventory="+(inventory!=null));
    }

    private static Button MenuButton(string name, Transform parent, string iconName, string labelText, Vector2 position)
    {
        GameObject go = RectObject(name, parent, new Vector2(126,142), position, new Vector2(0,1), new Vector2(0,1));
        Image bg = go.AddComponent<Image>(); bg.sprite = S("ButtonFrame"); bg.type = Image.Type.Sliced;
        Button button = go.AddComponent<Button>(); button.targetGraphic = bg;
        Image icon = ImageObject("Icon", go.transform, S(iconName), new Vector2(92,92), new Vector2(17,-4), new Vector2(0,1)); icon.preserveAspect = true; icon.raycastTarget = false;
        TMP_Text label = TextObject("Label", go.transform, labelText, 24, Color.white, TextAlignmentOptions.Center,
            new Vector2(120,38), new Vector2(3,2), new Vector2(0,0)); AddTextOutline(label, Ink, new Vector2(1.5f,-1.5f));
        return button;
    }

    private static GameObject SavePrefab(string name, GameObject root)
    {
        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        string path = PrefabFolder + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static Sprite S(string name, string path = AtlasPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == name);
    }

    private static GameObject RectObject(string name, Transform parent, Vector2 size, Vector2 pos, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform; rt.anchorMin=anchorMin; rt.anchorMax=anchorMax; rt.pivot=anchorMin==Vector2.one?Vector2.one:anchorMin;
        rt.sizeDelta=size; rt.anchoredPosition=pos; return go;
    }

    private static Image ImageObject(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos, Vector2 anchor)
    {
        GameObject go = RectObject(name,parent,size,pos,anchor,anchor); Image image=go.AddComponent<Image>(); image.sprite=sprite; return image;
    }

    private static TMP_Text TextObject(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment, Vector2 rectSize, Vector2 pos, Vector2 anchor)
    {
        GameObject go = RectObject(name,parent,rectSize,pos,anchor,anchor); TextMeshProUGUI tmp=go.AddComponent<TextMeshProUGUI>();
        tmp.font=_font; tmp.text=text; tmp.fontSize=size; tmp.color=color; tmp.alignment=alignment; tmp.raycastTarget=false; return tmp;
    }

    private static void Stretch(RectTransform rt, Vector2 minOffset, Vector2 maxOffset)
    {
        rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.pivot=new Vector2(.5f,.5f); rt.offsetMin=minOffset; rt.offsetMax=maxOffset;
    }

    private static void AddTextOutline(TMP_Text text, Color color, Vector2 distance)
    {
        Outline outline=text.gameObject.AddComponent<Outline>(); outline.effectColor=color; outline.effectDistance=distance;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true)) if (child.name==name) return child;
        throw new InvalidOperationException("找不到 UI 子节点: "+name);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/'); string current=parts[0];
        for(int i=1;i<parts.Length;i++){ string next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]); current=next; }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if(layer<0) return; go.layer=layer; foreach(Transform child in go.transform) SetLayerRecursively(child.gameObject,layer);
    }
}
#endif

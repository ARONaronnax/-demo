#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Demo.Inventory;
using Demo.Equipment;
using Demo.Player;
using Demo.UI;

/// <summary>按参考图重建背包 View；库存与装备数据系统保持不变。</summary>
public static class BuildInventoryUI
{
    private const string ScenePath = "Assets/Lowpoly Style/Desert/DemoScene/Desert.unity";
    private const string AtlasPath = "Assets/UI/FantasyHUD/Source/InventoryUI_Atlas.png";
    private const int Columns = 6;
    private const int Rows = 4;
    private static TMP_FontAsset _font;
    private static readonly Color Ink = new Color(.25f,.13f,.06f,1f);
    private static readonly Color Cream = new Color(1f,.92f,.72f,1f);

    [InitializeOnLoadMethod]
    private static void RebuildOnceForNewAtlas()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || SceneManager.GetActiveScene().path != ScenePath) return;
            GameObject panel = GameObject.Find("InventoryPanel");
            if (panel != null && panel.transform.Find("CharacterPreviewArea") != null && panel.transform.Find("CategoryTabs") != null) return;
            Build();
        };
    }

    [MenuItem("Demo/构建背包 UI")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        // 图集已由用户在 Sprite Editor 中手动切片；这里只读，绝不修改 importer/.meta。
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/NotoSansSC SDF.asset");

        GameObject systems = GameObject.Find("GameSystems");
        InventoryComponent inventory = systems != null ? systems.GetComponent<InventoryComponent>() : null;
        EquipmentComponent equipment = systems != null ? systems.GetComponent<EquipmentComponent>() : null;
        PlayerStats player = Object.FindObjectsOfType<PlayerStats>(true).FirstOrDefault();
        if (systems == null || inventory == null || player == null)
        {
            Debug.LogError("[BuildInventoryUI] 缺少 GameSystems、InventoryComponent 或 PlayerStats。");
            return;
        }

        Destroy("InventoryCanvas");
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;

        Image dim = CreateImage("Dimmer", canvasGo.transform, null, new Vector2(0,0), Vector2.zero, Vector2.zero);
        Stretch(dim.rectTransform,0,0,0,0); dim.color = new Color(0,0,0,.32f);

        Image panel = CreateImage("InventoryPanel", canvasGo.transform, S("WindowFrame"), new Vector2(1580,820), Vector2.zero, new Vector2(.5f,.5f));
        panel.type = Image.Type.Sliced;

        TMP_Text title = Text("Title", panel.transform, "背  包", 42, Cream, TextAlignmentOptions.Center,
            new Vector2(310,72), new Vector2(90,-34), new Vector2(0,1)); Outline(title);

        Button close = Button("CloseButton", panel.transform, S("Close"), new Vector2(86,86), new Vector2(-18,-18), new Vector2(1,1), false);
        close.image.preserveAspect = true;

        InventoryCharacterPreview preview = CreateCharacterPreview(panel.transform, player.transform, equipment);
        CreateTabs(panel.transform, out Button all, out Button weapons, out Button consumables, out Button quest);
        ScrollRect scroll = CreateGrid(panel.transform, out RectTransform content);
        InventorySlotUI[] slots = CreateSlots(content, Columns * Rows);
        WeaponDetailUI detail = CreateDetail(panel.transform);
        TMP_Text capacity = Text("Capacity", panel.transform, "0 / 40", 24, Ink, TextAlignmentOptions.Left,
            new Vector2(180,44), new Vector2(565,42), new Vector2(0,0));

        InventoryUI ui = systems.GetComponent<InventoryUI>();
        if (ui == null) ui = systems.AddComponent<InventoryUI>();
        ui.Bind(panel.gameObject, inventory, scroll, content, slots, detail);
        ui.BindPresentation(capacity, 40);
        ui.BindCategories(new[] { all, weapons, consumables, quest }, S("TabNormal"), S("TabSelected"));
        UnityEventTools.AddPersistentListener(close.onClick, ui.Close);
        UnityEventTools.AddPersistentListener(all.onClick, ui.ShowAll);
        UnityEventTools.AddPersistentListener(weapons.onClick, ui.ShowWeapons);
        UnityEventTools.AddPersistentListener(consumables.onClick, ui.ShowConsumables);
        UnityEventTools.AddPersistentListener(quest.onClick, ui.ShowQuestItems);

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[BuildInventoryUI] RPG 背包 UI 已重建：角色预览、拖动旋转、分类、容量、详情与装备接线完成。");
    }

    private static InventoryCharacterPreview CreateCharacterPreview(Transform parent, Transform player, EquipmentComponent equipment)
    {
        Image area = CreateImage("CharacterPreviewArea", parent, S("Parchment"), new Vector2(500,620), new Vector2(52,-130), new Vector2(0,1));
        area.type = Image.Type.Sliced;
        RawImage raw = new GameObject("CharacterViewport", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        raw.transform.SetParent(area.transform,false); Stretch(raw.rectTransform,35,78,35,30); raw.color = Color.white;
        TMP_Text hint = Text("RotateHint",area.transform,"↔  按住鼠标左右拖动，旋转角色",18,Ink,TextAlignmentOptions.Center,
            new Vector2(420,46),new Vector2(40,24),new Vector2(0,0));
        InventoryCharacterPreview preview = area.gameObject.AddComponent<InventoryCharacterPreview>();
        preview.Bind(raw,player,equipment);
        return preview;
    }

    private static void CreateTabs(Transform parent, out Button all, out Button weapons, out Button consumables, out Button quest)
    {
        GameObject tabs = Rect("CategoryTabs",parent,new Vector2(860,72),new Vector2(575,-112),new Vector2(0,1));
        all = Tab("All",tabs.transform,"全部",0);
        weapons = Tab("Weapons",tabs.transform,"武器",1);
        consumables = Tab("Consumables",tabs.transform,"道具",2);
        quest = Tab("Quest",tabs.transform,"任务物品",3);
    }

    private static Button Tab(string name, Transform parent, string label, int index)
    {
        Button b = Button(name,parent,S(index==0?"TabSelected":"TabNormal"),new Vector2(205,64),new Vector2(index*215,0),new Vector2(0,1));
        TMP_Text t = Text("Label",b.transform,label,23,Ink,TextAlignmentOptions.Center,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f)); Stretch(t.rectTransform,8,6,8,6);
        return b;
    }

    private static ScrollRect CreateGrid(Transform parent, out RectTransform content)
    {
        Image paper = CreateImage("ItemsPanel",parent,S("Parchment"),new Vector2(930,545),new Vector2(570,-190),new Vector2(0,1)); paper.type=Image.Type.Sliced;
        GameObject area = Rect("ScrollArea",paper.transform,Vector2.zero,Vector2.zero,Vector2.zero); Stretch(area.GetComponent<RectTransform>(),24,24,24,24);
        ScrollRect scroll = area.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped;
        GameObject viewport=Rect("Viewport",area.transform,Vector2.zero,Vector2.zero,Vector2.zero); Stretch(viewport.GetComponent<RectTransform>(),0,0,0,0); viewport.AddComponent<RectMask2D>();
        GameObject c=Rect("Content",viewport.transform,Vector2.zero,Vector2.zero,new Vector2(0,1));
        content=c.GetComponent<RectTransform>(); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
        GridLayoutGroup grid=c.AddComponent<GridLayoutGroup>(); grid.cellSize=new Vector2(136,116); grid.spacing=new Vector2(12,8); grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=Columns; grid.childAlignment=TextAnchor.UpperCenter;
        ContentSizeFitter fitter=c.AddComponent<ContentSizeFitter>(); fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport.GetComponent<RectTransform>(); scroll.content=content;
        return scroll;
    }

    private static InventorySlotUI[] CreateSlots(Transform parent,int count)
    {
        InventorySlotUI[] result=new InventorySlotUI[count];
        for(int i=0;i<count;i++)
        {
            GameObject root=Rect("Slot"+i,parent,new Vector2(136,116),Vector2.zero,Vector2.zero);
            Button b=root.AddComponent<Button>();
            Image background=CreateImage("Background",root.transform,S("SlotNormal"),new Vector2(108,108),Vector2.zero,new Vector2(.5f,.5f));
            background.type=Image.Type.Simple; background.preserveAspect=true; b.targetGraphic=background;
            Image icon=CreateImage("Icon",root.transform,null,new Vector2(70,70),Vector2.zero,new Vector2(.5f,.5f)); icon.preserveAspect=true; icon.enabled=false; icon.raycastTarget=false;
            TMP_Text name=Text("Name",root.transform,"",1,new Color(1,1,1,0),TextAlignmentOptions.Center,new Vector2(1,1),Vector2.zero,new Vector2(.5f,.5f));
            TMP_Text amount=Text("Amount",root.transform,"",18,Color.white,TextAlignmentOptions.BottomRight,new Vector2(42,25),new Vector2(-15,10),new Vector2(1,0)); Outline(amount);
            InventorySlotUI slot=root.AddComponent<InventorySlotUI>(); slot.Bind(icon,name,amount); slot.BindVisuals(background,S("SlotNormal"),S("SlotSelected")); UnityEventTools.AddPersistentListener(b.onClick,slot.Click); result[i]=slot;
        }
        return result;
    }

    private static WeaponDetailUI CreateDetail(Transform parent)
    {
        Image bg=CreateImage("ItemDetail",parent,S("Parchment"),new Vector2(930,112),new Vector2(570,40),new Vector2(0,0)); bg.type=Image.Type.Sliced;
        GameObject empty=Text("EmptyHint",bg.transform,"选择物品查看详情",18,Ink,TextAlignmentOptions.Center,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f)).gameObject; Stretch(empty.GetComponent<RectTransform>(),20,15,20,15);
        Image icon=CreateImage("Icon",bg.transform,null,new Vector2(76,76),new Vector2(22,18),new Vector2(0,0)); icon.enabled=false; icon.preserveAspect=true;
        TMP_Text name=Text("Name",bg.transform,"",22,Ink,TextAlignmentOptions.TopLeft,new Vector2(230,30),new Vector2(112,-14),new Vector2(0,1));
        TMP_Text damage=Text("Damage",bg.transform,"",17,new Color(.72f,.28f,.08f,1),TextAlignmentOptions.Left,new Vector2(220,26),new Vector2(112,24),new Vector2(0,0));
        TMP_Text desc=Text("Description",bg.transform,"",15,Ink,TextAlignmentOptions.TopLeft,new Vector2(330,72),new Vector2(350,-20),new Vector2(0,1));
        Button equip=Button("EquipButton",bg.transform,S("RedButton"),new Vector2(190,62),new Vector2(-28,25),new Vector2(1,0));
        TMP_Text label=Text("Label",equip.transform,"装备",23,Color.white,TextAlignmentOptions.Center,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f)); Stretch(label.rectTransform,5,5,5,5); Outline(label);
        WeaponDetailUI detail=bg.gameObject.AddComponent<WeaponDetailUI>(); detail.Bind(empty,icon,name,desc,damage,equip); UnityEventTools.AddPersistentListener(equip.onClick,detail.OnEquipClicked); return detail;
    }

    private static Sprite S(string name){return AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().FirstOrDefault(x=>x.name==name);}
    private static GameObject Rect(string name,Transform parent,Vector2 size,Vector2 pos,Vector2 anchor){GameObject go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);RectTransform r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.sizeDelta=size;r.anchoredPosition=pos;return go;}
    private static Image CreateImage(string name,Transform parent,Sprite sprite,Vector2 size,Vector2 pos,Vector2 anchor){GameObject go=Rect(name,parent,size,pos,anchor);Image image=go.AddComponent<Image>();image.sprite=sprite;return image;}
    private static Button Button(string name,Transform parent,Sprite sprite,Vector2 size,Vector2 pos,Vector2 anchor,bool sliced=true){Image image=CreateImage(name,parent,sprite,size,pos,anchor);image.type=sliced?Image.Type.Sliced:Image.Type.Simple;Button b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;return b;}
    private static TMP_Text Text(string name,Transform parent,string value,float size,Color color,TextAlignmentOptions align,Vector2 rectSize,Vector2 pos,Vector2 anchor){GameObject go=Rect(name,parent,rectSize,pos,anchor);TextMeshProUGUI t=go.AddComponent<TextMeshProUGUI>();t.text=value;t.fontSize=size;t.color=color;t.alignment=align;t.font=_font;t.raycastTarget=false;return t;}
    private static void Stretch(RectTransform r,float l,float b,float rr,float t){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-rr,-t);}
    private static void Outline(TMP_Text t){t.outlineColor=Ink;t.outlineWidth=.18f;}
    private static void Destroy(string name){GameObject go=GameObject.Find(name);if(go!=null)Object.DestroyImmediate(go);}
    private static void EnsureEventSystem(){if(Object.FindObjectOfType<EventSystem>(true)==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));}
}
#endif

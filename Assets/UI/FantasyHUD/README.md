# Fantasy HUD 换肤说明

UI 由 `Tools > Demo UI > Rebuild Fantasy HUD` 生成，设计分辨率为 1920×1080，Canvas Scaler 使用 `Scale With Screen Size`。

## Prefab

- `GameHUD.prefab`：总 Canvas
- `PlayerStatusPanel.prefab`：玩家头像、HP
- `TopRightMenu.prefab`：背包 / 任务 / 地图 / 设置
- `MiniMapPanel.prefab`：圆形小地图容器
- `DialoguePanel.prefab`：NPC 头像、名称、正文、继续与选项
- `QuestSummaryPanel.prefab`：现有任务系统的只读摘要

## 正式图片替换位

`FantasyHUD_Atlas.png` 是 4×4 临时图集，Sprite 名称固定为：

`DialogueFrame`, `HpFrame`, `PortraitFrame`, `MiniMapFrame`, `ButtonFrame`, `HpFill`, `Heart`, `Backpack`, `Quest`, `Map`, `Settings`, `ContinueArrow`, `LeafCorner`, `Nameplate`, `Rivet`, `Tooltip`。

`FantasyHUD_Portraits.png` 提供 `PlayerPortrait` 与 `NpcPortrait`。正式 NPC 头像也可以直接填写在每个 `DialogueData.portrait` 字段，不需要改对话逻辑。

## 需要 9-Slice 的图片

- `DialogueFrame`：建议 Border 62 / 54 / 62 / 54
- `HpFrame`：建议 Border 64 / 48 / 64 / 48
- `ButtonFrame`：建议 Border 48 / 48 / 48 / 48
- `HpFill`：建议 Border 60 / 36 / 60 / 36
- `Nameplate`：建议 Border 54 / 34 / 54 / 34
- `Tooltip`：建议 Border 50 / 50 / 50 / 50

替换正式贴图时，最稳妥的方式是保留上述 Sprite 名称和边界设置，然后重新执行构建菜单。也可以打开各 Prefab，直接替换对应 Image 的 Source Image。地图与设置按钮的接口在 `TopRightMenu` 的 `onMapRequested` / `onSettingsRequested`；MiniMap 的真实渲染内容以后放到 `MiniMapPanel.Content`。

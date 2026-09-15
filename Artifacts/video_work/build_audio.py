import asyncio
import json
import os
from pathlib import Path

import edge_tts


OUT = Path(__file__).resolve().parent
CUES = [
    (4.0, 11.0, "这是一个基于 Unity 开发的第三人称 RPG 功能演示。"),
    (11.0, 18.5, "进入场景后，角色支持相机相对移动，界面同步显示生命值、小地图和功能入口。"),
    (18.5, 27.0, "靠近 NPC 会出现交互提示。按下 E 键进入对话，并接受讨伐魔物任务。"),
    (27.0, 39.5, "任务系统以数据资产驱动，通过事件总线解耦对话、任务和界面；左上角会实时显示目标与完成进度。"),
    (39.5, 56.5, "进入怪物区域后，敌人会根据距离在待机、追击、攻击和死亡状态之间切换。玩家可使用连续攻击与翻滚进行战斗。"),
    (56.5, 64.0, "受到攻击时，生命值会即时更新；背包界面则保持角色状态与物品数据同步。"),
    (64.0, 81.0, "战斗伤害由独立组件计算，攻击判定只在动画有效帧内开启。敌人死亡后发布事件，任务进度和掉落逻辑分别响应。"),
    (81.0, 93.0, "击败目标后，掉落物可以通过场景交互拾取，并自动进入背包，无需让敌人直接依赖背包系统。"),
    (93.0, 103.0, "完成规定的击杀数量后，任务状态自动更新。返回 NPC 附近，即可继续完成任务流程。"),
    (103.0, 149.2, "最后打开背包，可以查看获得的武器与恢复药剂，并进行装备或使用。以上就是本次 RPG 纵向切片的完整演示。"),
]


async def main():
    manifest = []
    for i, (start, end, text) in enumerate(CUES, 1):
        target = OUT / f"voice_{i:02d}.mp3"
        await edge_tts.Communicate(text, "zh-CN-YunxiNeural", rate="+8%", volume="+5%").save(str(target))
        manifest.append({"index": i, "start": start, "end": end, "text": text, "file": target.name})
    (OUT / "voice_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


asyncio.run(main())

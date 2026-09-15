Add-Type -AssemblyName System.Speech
$outDir = Join-Path (Get-Location) 'Artifacts\video_work'
$cues = @(
  '这是一个基于 Unity 开发的第三人称 RPG 功能演示。',
  '进入场景后，角色支持相机相对移动，界面同步显示生命值、小地图和功能入口。',
  '靠近 NPC 会出现交互提示。按下 E 键进入对话，并接受讨伐魔物任务。',
  '任务系统以数据资产驱动，通过事件总线解耦对话、任务和界面；左上角会实时显示目标与完成进度。',
  '进入怪物区域后，敌人会根据距离在待机、追击、攻击和死亡状态之间切换。玩家可使用连续攻击与翻滚进行战斗。',
  '受到攻击时，生命值会即时更新；背包界面则保持角色状态与物品数据同步。',
  '战斗伤害由独立组件计算，攻击判定只在动画有效帧内开启。敌人死亡后发布事件，任务进度和掉落逻辑分别响应。',
  '击败目标后，掉落物可以通过场景交互拾取，并自动进入背包，无需让敌人直接依赖背包系统。',
  '完成规定的击杀数量后，任务状态自动更新。返回 NPC 附近，即可继续完成任务流程。',
  '最后打开背包，可以查看获得的武器与恢复药剂，并进行装备或使用。以上就是本次 RPG 纵向切片的完整演示。'
)
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
$speaker.SelectVoice('Microsoft Kangkang')
$speaker.Rate = 1
$speaker.Volume = 100
for ($i = 0; $i -lt $cues.Count; $i++) {
  $path = Join-Path $outDir ('voice_{0:D2}.wav' -f ($i + 1))
  $speaker.SetOutputToWaveFile($path)
  $speaker.Speak($cues[$i])
  $speaker.SetOutputToNull()
}
$speaker.Dispose()

# Task 3 Report: AnimationClipLocator 剪辑定位

**Status:** DONE

## What I implemented

Two new files, exactly as specified in the brief — no extra members, no added lookup/caching/helper API.

**`Assets/Script/Editor/AnimationClipLocator.cs`** (2nd file in assembly `Demo.Editor`, namespace `Demo.EditorTools`)

Static class with the 5 produced members from the brief plus the 3 path constants:

| Member | Signature |
|---|---|
| `HeroAnimationRoot` | `const string` = `Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield` |
| `InPlaceDir` | `const string` = `HeroAnimationRoot + "/InPlace"` |
| `RootMotionDir` | `const string` = `HeroAnimationRoot + "/RootMotion"` |
| `InPlacePath` | `string InPlacePath(string clipName)` |
| `RootMotionPath` | `string RootMotionPath(string clipName)` |
| `LoadInPlace` | `AnimationClip LoadInPlace(string clipName)` |
| `LoadRootMotion` | `AnimationClip LoadRootMotion(string clipName)` |
| `LoadClip` | `AnimationClip LoadClip(string fbxPath)` |

`LoadClip` uses `AssetDatabase.LoadAllAssetsAtPath`, skips `__preview__` clips, prefers the clip whose name equals the fbx file name, and falls back to the first real clip otherwise. Returns `null` (never throws) when the path yields nothing.

**`Assets/Tests/EditMode/AnimationClipLocatorTests.cs`** — the 6 tests from the brief, verbatim. Both path branches are covered: `InPlacePath` asserts the path contains `/InPlace/` and ends `.fbx`; `RootMotionPath` asserts it contains `/RootMotion/`. The load tests hit a **real fbx on disk** (not a synthetic clip), so they genuinely exercise `AssetDatabase` against the pack.

## Evidence the fbx paths resolve on disk

The brief asserts a specific directory layout and specific clip names. I verified all of it against the real files **before** trusting anything.

```
$ cd "E:/unity/求职demo" && for f in \
    "Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/MoveFWD_Battle_InPlace_SwordAndShield.fbx" \
    "Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield/RootMotion/RollFWD_Battle_RM_SwordAndShield.fbx" \
    "Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield/RootMotion/MoveFWD_Battle_RM_SwordAndShield.fbx"; do
  [ -f "$f" ] && echo "EXISTS: $f" || echo "MISSING: $f"; done
EXISTS: Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/MoveFWD_Battle_InPlace_SwordAndShield.fbx
EXISTS: Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield/RootMotion/RollFWD_Battle_RM_SwordAndShield.fbx
EXISTS: Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield/RootMotion/MoveFWD_Battle_RM_SwordAndShield.fbx
```

All three paths the brief depends on exist. Both `InPlace/` and `RootMotion/` directories are populated.

I additionally confirmed each fbx's **internal clip name** matches its file name (this is what `LoadClip`'s exact-name match relies on). From the `.fbx.meta` `internalIDToNameTable`:

```
MoveFWD_Battle_InPlace_SwordAndShield.fbx.meta -> second: MoveFWD_Battle_InPlace_SwordAndShield
RollFWD_Battle_RM_SwordAndShield.fbx.meta      -> second: RollFWD_Battle_RM_SwordAndShield
MoveFWD_Battle_RM_SwordAndShield.fbx.meta      -> second: MoveFWD_Battle_RM_SwordAndShield
```

No path in the brief was missing, so the brief's stopping condition ("if a path does not exist, stop and report") did not trigger. I did not substitute any path.

## TDD Evidence

### RED

Command: `"E:/unity/求职demo/docs/superpowers/sdd/run-tests" task3` (with the test file written, implementation absent)

```
Aborting batchmode due to failure:
Scripts have compiler errors.

✗ 测试没有产生结果文件。日志：E:/unity/求职demo/docs/superpowers/sdd/test-task3.log
EXIT_CODE=2
```

Exit code **2** = never ran, which is the expected RED for this TDD task. The log shows the reason is precisely the missing type, not an unrelated error:

```
Assets\Tests\EditMode\AnimationClipLocatorTests.cs(12,21): error CS0103: The name 'AnimationClipLocator' does not exist in the current context
Assets\Tests\EditMode\AnimationClipLocatorTests.cs(20,21): error CS0103: The name 'AnimationClipLocator' does not exist in the current context
Assets\Tests\EditMode\AnimationClipLocatorTests.cs(27,24): error CS0103: The name 'AnimationClipLocator' does not exist in the current context
Assets\Tests\EditMode\AnimationClipLocatorTests.cs(35,24): error CS0103: The name 'AnimationClipLocator' does not exist in the current context
Assets\Tests\EditMode\AnimationClipLocatorTests.cs(42,24): error CS0103: The name 'AnimationClipLocator' does not exist in the current context
Assets\Tests\EditMode\AnimationClipLocatorTests.cs(49,21): error CS0103: The name 'AnimationClipLocator' does not exist in the current context
Assets\Tests\EditMode\AnimationClipLocatorTests.cs(50,21): error CS0103: The name 'AnimationClipLocator' does not exist in the current context
```

7 error sites (one per call site), all `CS0103 The name 'AnimationClipLocator' does not exist`. RED evidence is preserved at `docs/superpowers/sdd/test-task3.log.prev` (55 KB). Note that no `results-task3.xml.prev` from the RED run exists — consistent with the script's design, since a compile failure produces no results XML at all.

### GREEN

Command: same script, after implementing `AnimationClipLocator`.

```
total=13  passed=13  failed=0  result=Passed
结果文件：E:/unity/求职demo/docs/superpowers/sdd/results-task3.xml
EXIT_CODE=0
```

Exit code **0**. All 13 EditMode tests pass (6 new `AnimationClipLocatorTests` + 2 `AssemblySmokeTests` + 5 `AnimatorParamsTests`), so Tasks 1–2 remain green.

The 6 new tests, all present and passing in `results-task3.xml`:

```
AnimationClipLocatorTests.InPlace路径_指向InPlace目录
AnimationClipLocatorTests.RootMotion路径_指向RootMotion目录
AnimationClipLocatorTests.加载InPlace剪辑_返回非空且名称匹配
AnimationClipLocatorTests.加载RootMotion剪辑_返回非空
AnimationClipLocatorTests.加载不存在的剪辑_返回null而非抛异常
AnimationClipLocatorTests.同名的InPlace与RootMotion剪辑_是两个不同对象
```

Note the load tests are only meaningful because they passed: `Assert.IsNotNull(clip)` on a real path proves the fbx really resolved and really contained a matching clip. Had the brief's path been wrong, `加载InPlace剪辑` would have failed rather than silently passing.

**Output pristine:** in the GREEN log, `grep -c "error CS"` = 0, `grep -ci "AssertionException\|Test Failed"` = 0, and there is no `Debug.Log` noise from the class (0 occurrences of `AnimationClipLocator` in the log). The only "failed" lines in the log are Unity licensing handshake messages, unrelated to this code.

## Files changed

- **Created** `E:/unity/求职demo/Assets/Script/Editor/AnimationClipLocator.cs`
- **Created** `E:/unity/求职demo/Assets/Tests/EditMode/AnimationClipLocatorTests.cs`
- **Created** `E:/unity/求职demo/docs/superpowers/sdd/task-3-report.md` (this report)

No existing file was modified. In particular the asmdefs already referenced `Demo.Editor` from the test assembly and `Demo.Runtime` from the editor assembly, so no asmdef change was needed. Nothing under `Assets/RPGTinyHeroWavePBR/` or `Assets/RPGMonsterWave02PBR/` was written to — those files were only read.

Per the global constraint, **`git` was not run**; the brief's Step 5 (提交) was skipped entirely.

## Self-review findings

- **Completeness:** all 5 members from the brief's Produces list are present with exact signatures, plus the 3 path constants the brief's reference implementation declares. Nothing missing, nothing renamed.
- **Test coverage of both branches:** yes. `InPlacePath` → asserts `/InPlace/` + `.fbx`; `RootMotionPath` → asserts `/RootMotion/`. Both the `LoadInPlace` and `LoadRootMotion` paths are additionally exercised against real assets, and `同名的InPlace与RootMotion剪辑_是两个不同对象` proves the two directories yield genuinely different clip objects (`AreNotSame`), which is the whole point of the class.
- **Real behaviour, not mocks:** every load test goes through `AssetDatabase.LoadAllAssetsAtPath` on a real `.fbx`. The suite fails if the pack layout changes, which is the intended tripwire.
- **YAGNI:** no extra members. I deliberately did not add the debugging `foreach (var a in ...) Debug.Log(...)` snippet the brief offers in Step 4, since it was conditional ("若...失败") and would have added console noise.
- **Name hardcoding constraint:** the class hardcodes fbx *asset paths*, which are not animator parameter/state names; `Demo.AnimatorParams` has no fbx-path constants and nothing in it is applicable here. No animator name strings are hardcoded.
- **Idempotence:** the class is a read-only static utility that creates no assets, so it is trivially idempotent.
- **Style:** matches the existing files — Chinese `///` summaries, block-form `namespace Demo.EditorTools { }`, and Chinese test method names consistent with `AnimatorParamsTests`.
- **Known asymmetry preserved:** I did not normalise `DashRHT_*` vs `DashRGT_*` or `RollRGT_Battle_RM_*`; this task touches none of them (the brief's tests use `MoveFWD` and `RollFWD` only).

## Issues or concerns

1. **The test-runner script's process cleanup is broken on this machine.** `run-tests` reaches its summary and exits correctly, but its fallback cleanup uses `wmic`, which is removed/non-functional on this Windows 11 build (`wmic process where ...` returns nothing). The result is that the batchmode `Unity.exe` — which the script's own header documents as hanging during shutdown — is left alive holding `Temp/UnityLockfile`. I hit this twice: after both the GREEN runs, a stale `-batchmode` Unity process was still running and holding the lock. I killed them with `taskkill //PID <pid> //T //F` and removed `Temp/UnityLockfile` myself; no Unity process is running now. **This will affect every later task in this plan**, and a stale lock makes the next `run-tests` fail with "another Unity instance has this project open". Worth fixing the script to use PowerShell (`Get-CimInstance Win32_Process`) instead of `wmic`, or to `taskkill` by image name filtered on `-batchmode`.

2. **A single `run-tests` invocation can exceed a 10-minute wall clock on the first run** (cold `Library`, fbx import). The script's own internal budget is 20 minutes. Not a defect, just a timing note: budget accordingly, and do not assume a missing result after 10 minutes means failure.

3. **`LoadClip`'s fallback is order-dependent.** When no clip name equals the file name, it returns the *first* non-preview `AnimationClip` in `AssetDatabase`'s order. This is exactly what the brief specifies and is fine for the single-take fbx files in this pack, but it would be non-deterministic for a multi-take fbx. I flag it only as a latent property of the specified design, not as a defect; every clip this task loads matched by exact name. The most likely place it could matter is `Combo05_InPlaceWithRMHeight_SwordAndShield.fbx`, which Task 4+ may consume.

4. **No behavioural risk found in the two files themselves.** Nothing was blocked, nothing needed an architectural decision, and no deviation from the brief was required.

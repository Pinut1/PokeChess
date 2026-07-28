# 씬 병합 도구 (fileID 블록 단위 3-way)

`GameSceneTest.unity` 병합 충돌을 텍스트 3-way 대신 **fileID 블록 단위**로 푸는 Node 스크립트.
왜 텍스트 3-way를 쓰면 안 되는지, 전체 절차와 검증 항목은 `Docs/HANDOFF_2026-07-28_final.md` §4 참조.

실적: 2026-07-28 `master ← haein_UI` 병합에서 git 보고 충돌 19곳 → 실제 충돌 1곳으로 판별,
같은 날 통신교환 브랜치(741 blocks)에도 재사용.

## 사용법 (git 충돌 상태에서)

```powershell
# 1. 세 버전 추출
$base = git merge-base HEAD MERGE_HEAD
git show "${base}:Assets/Scenes/GameSceneTest.unity"      | Set-Content -Encoding utf8 base.unity
git show "HEAD:Assets/Scenes/GameSceneTest.unity"         | Set-Content -Encoding utf8 ours.unity
git show "MERGE_HEAD:Assets/Scenes/GameSceneTest.unity"   | Set-Content -Encoding utf8 theirs.unity

# 2. 병합 — conflicts 배열이 비어 있지 않으면 해당 fileID만 사람이 판단
node merge_scene.mjs base.unity ours.unity theirs.unity merged.unity

# 3. 검증 — 세 파일 모두 넣어 병합본이 양쪽 부모와 같은 기준을 통과하는지 비교
node check_scene.mjs ours.unity theirs.unity merged.unity

# 4. 설치
Copy-Item merged.unity Assets/Scenes/GameSceneTest.unity -Force
git add Assets/Scenes/GameSceneTest.unity
```

## 주의

- `SceneRoots`(&9223372036854775807)는 양쪽이 루트를 추가하면 both-modified 충돌로 뜬다 — `m_Roots` 목록을 합집합으로 손수 합칠 것 (7/28 두 번 모두 이 케이스)
- 병합 후 Unity에서 씬을 열 때 에디터가 그 씬을 이미 열어둔 상태면 **반드시 Reload** (Save를 고르면 병합 결과 유실)
- 검증 통과 ≠ 완료. Unity에서 Missing Script 0 확인 + Play 진입까지 볼 것

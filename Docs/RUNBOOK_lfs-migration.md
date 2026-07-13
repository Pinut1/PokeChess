# RUNBOOK — Git LFS 히스토리 마이그레이션

> 작성: 2026-07-13 (영욱). 배경: 주간보고 "대용량 에셋이 LFS 없이 커밋되며 레포가 무거워지는 중" → 확인 작업(`Assets/_Project/LFS.md`) 후속 실행 절차서입니다.
> **작업 도중 인수인계될 수 있으므로, 각 단계 완료 시 아래 진행 체크박스를 갱신해 주세요.**

## ⚠️ 2026-07-13 실측 결론 — 히스토리 재작성 보류 권고

리허설(백업 미러 클론 + 히스토리 blob 실측)을 돌린 결과, **아래 실행 절차는 당분간 불필요**하다고 판단합니다. 근거:

| 지표 | 실측값 | 의미 |
|---|---|---|
| 원격 히스토리 `.pack` (압축 후) | **20MB** | clone 시 실제로 받는 크기 — 건강함 |
| 히스토리 대용량 blob 논리 총합 | 261MB | LFS.md가 우려한 수치(비압축 기준) |
| 로컬 `.git` 442MB 중 `.git/lfs` | 414MB | **옛 버전 쓰레기 아님** — 여러 브랜치가 실참조하는 LFS 파일(`git lfs prune` 대상 0) |

- `.unity`/`.prefab`은 YAML 텍스트라 delta+zlib 압축이 잘 먹어, 261MB어치가 원격에선 20MB로 압축돼 있음. **filter-repo로 히스토리를 다 지워도 실절감은 수 MB 수준.**
- 반면 재작성 비용은 큼: `--everything`이 **아트 활성 브랜치(art/vfx, art/prefab_model)·해인(AugmentManager_HHI)·태욱 브랜치까지 전부 재작성** → force-push + 팀 5명 재클론 강제. 리스크 대비 실익이 거의 없음.
- 로컬이 무겁게 느껴지는 건 실제 LFS 에셋이 많아서(정상)이며, 히스토리 문제가 아님.

**결론**: 지금은 실행하지 않음. 아래 절차는 (1) 정말 원격이 비대해지거나 (2) 라이브 서비스 전환으로 히스토리 슬림화가 필요해질 때를 위해 보존. 재발 방지(11번 pre-commit hook)만 선택적으로 도입 가치 있음.

---

## (이하) 실행 절차 — 보류 중, 필요 시 사용

## 현황 요약 (2026-07-13 실측)

- `.gitattributes` LFS 필터는 정상 동작 중 — **현재 master HEAD는 깨끗함** (패턴 매칭 파일 121개 전부 LFS 추적)
- 문제는 **`.gitattributes` 도입 전 일반 커밋으로 들어간 히스토리 속 블롭** (.git ≈ 427MB, 환경에 따라 ±)
- 히스토리 최대 블롭: Maps_Ground 7.3MB / Lana Studio VFX 텍스처 1~2.2MB급 다수 / PunCockpit-Scene.unity 2.1MB / PhotonNetworking-Documentation.chm 1.5MB
- 전략: **② 안 쓰는 파일 히스토리 제거 → ① 나머지 LFS migrate → ③ gc** 를 한 번의 재작성으로 묶어서 진행 (근거: LFS.md)

## 진행 체크리스트

- [ ] 0. 팀 공지 (아래 공지 템플릿 참고) + 작업 일시 확정
- [ ] 1. 전원 로컬 변경 커밋+푸시 완료 확인 / 오픈 PR 0개 확인
- [ ] 2. 미머지 브랜치 소유자 조율 — 재작성 대상: `taewook-item-inventory-flow`(태욱), `AugmentManager_HHI`(해인), art 브랜치 2개(아트)
- [ ] 3. 백업 미러 클론 생성
- [ ] 4. 삭제 대상 GUID 참조 확인 (특히 PUN Demos)
- [ ] 5. filter-repo로 불필요 파일 히스토리 제거
- [ ] 6. lfs migrate로 잔여 바이너리 히스토리 LFS 전환
- [ ] 7. gc + 용량 비교 기록
- [ ] 8. force-push
- [ ] 9. LFS 쿼터 확인 (GitHub Settings → Billing)
- [ ] 10. 팀 전원 재클론 안내 + 완료 확인
- [ ] 11. pre-commit hook 배포 (재발 방지)

## 상세 절차

### 3. 백업 (필수 — 실수 시 유일한 복구 수단)

```bash
git clone --mirror https://github.com/Pinut1/PokeChess.git PokeChess-backup.git
```

작업이 완전히 끝나고 전원 재클론 확인 후에도 최소 1~2주 보관 권장.

### 4. 삭제 대상 GUID 참조 확인

삭제 후보는 `Assets/_Project/LFS.md`의 ①Demo 폴더(41MB) ②문서 파일(10.8MB) 표 참고.
삭제 전, 실게임 씬/프리팹이 해당 폴더의 에셋을 참조하지 않는지 확인:

```bash
# 예: PUN Demos 내 에셋의 guid를 뽑아 실게임 씬/프리팹에서 검색
grep -h "guid:" "Assets/Photon/PhotonUnityNetworking/Demos" -r --include="*.meta" | sort -u > demo_guids.txt
# GameSceneTest.unity / LobbyScene.unity / _Project 하위 프리팹에서 위 guid가 검색되면 참조 있음 → 삭제 제외
```

가장 확실한 건 삭제 후 Unity 배치모드 컴파일 + GameSceneTest 플레이 1판 (콘솔에 Missing 참조 에러 없는지).

### 5. 히스토리에서 불필요 파일 제거 (git filter-repo)

⚠️ **filter-repo는 새로 받은 클론(fresh clone)에서 실행할 것** (기존 워킹 클론 아님).

```bash
pip install git-filter-repo   # 미설치 시

git clone https://github.com/Pinut1/PokeChess.git PokeChess-rewrite
cd PokeChess-rewrite

git filter-repo \
  --invert-paths \
  --path "Assets/Photon/PhotonUnityNetworking/Demos" \
  --path "Assets/Photon/PhotonChat/Demos" \
  --path "Assets/Photon/PhotonRealtime/Demos" \
  --path "Assets/Photon/PhotonNetworking-Documentation.chm" \
  --path "Assets/_ImportAsset/Maps_Ground/Low Poly Modular Terrain Pack/_READ_ME" \
  --path "Assets/Art_VFX/Click VFX/Demo Scenes" \
  --path "Assets/Art_VFX/Lana Studio/Casual RPG VFX/Demo" \
  --path "Assets/Eugatnom/GritlineToonShader/Demo"
```

(4번 확인 결과에 따라 목록 가감. 폴더를 지우면 대응 .meta도 함께 지워야 하므로 `--path "...Demos.meta"` 식으로 짝 확인.)

### 6. 잔여 바이너리 히스토리 LFS 전환

```bash
git lfs migrate import \
  --include="*.png,*.jpg,*.jpeg,*.psd,*.tga,*.tif,*.tiff,*.exr,*.fbx,*.FBX,*.obj,*.blend,*.wav,*.mp3,*.ogg,*.aiff,*.mp4,*.mov" \
  --everything
```

⚠️ 콤마 뒤 **공백 없이** (공백이 들어가면 패턴이 매칭되지 않을 수 있음). `.gitattributes`의 패턴 목록과 동일하게 유지.

### 7. 정리 + 용량 기록

```bash
git reflog expire --expire=now --all
git gc --prune=now --aggressive
# 전/후 .git 크기를 이 문서 하단 "결과 기록"에 남길 것
```

### 8. force-push

```bash
git push origin --force --all
git push origin --force --tags
```

### 10. 팀 재클론 안내

기존 클론은 히스토리가 갈라져 pull 불가 — **반드시 새로 클론**:

```bash
git clone https://github.com/Pinut1/PokeChess.git
```

(레포 특성상 체크아웃 2분+ 소요. 로컬에 미푸시 작업이 있었다면 기존 클론에서 패치로 뽑아 새 클론에 적용: `git diff > my.patch` → `git apply my.patch`)

### 11. 재발 방지 pre-commit hook

`.git/hooks/pre-commit` (팀 각자 설치, 또는 저장소에 `Tools/hooks/`로 두고 안내):

```bash
#!/bin/sh
# 5MB 이상인데 LFS 포인터가 아닌 파일 커밋 차단
limit=5242880
fail=0
for f in $(git diff --cached --name-only --diff-filter=AM); do
  [ -f "$f" ] || continue
  size=$(wc -c < "$f")
  if [ "$size" -gt "$limit" ]; then
    if ! git check-attr filter -- "$f" | grep -q "filter: lfs"; then
      echo "[pre-commit] ${f} (${size}B) — 5MB 초과인데 LFS 미적용. .gitattributes에 패턴 추가 후 커밋하세요."
      fail=1
    fi
  fi
done
exit $fail
```

## 공지 템플릿 (팀 단톡용)

> [공지] Git 히스토리 정리(LFS 마이그레이션)를 O/O(O) OO시에 진행합니다.
> - 그 전까지: 로컬 작업 전부 커밋+푸시 부탁드립니다 (미푸시 작업은 히스토리 재작성 후 pull이 안 됩니다)
> - 작업 후: 기존 클론은 버리고 **새로 클론**해야 합니다 (방법은 Docs/RUNBOOK_lfs-migration.md 참고)
> - 소요: 작업 자체 ~1시간 + 각자 재클론 시간

## 결과 기록 (작업자가 채울 것)

- 작업일:
- 작업자:
- .git 크기 전/후:
- LFS 스토리지 사용량 (Billing):
- 특이사항:

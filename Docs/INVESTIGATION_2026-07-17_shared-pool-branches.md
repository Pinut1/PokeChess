---
name: janghanna-2026-07-17-shared-pool-branch-investigation
description: "장한나, 2026-07-17 - 김영욱 7/16 devlog가 남긴 ⚠️미해결 3건(유닛풀 동기화 담당경계 충돌/4코오픈 스펙불일치/채석가 보너스 미반영)을 git 브랜치 직접 조사로 확인. 2건은 코드상 해소 확인(단 미병합), 1건(경쟁 브랜치 중복구현)은 신규 미해결 이슈로 격상"
metadata:
  node_type: memory
  type: project
  originSessionId: 077e909e-7b3f-43f7-9614-cab5a539d770
---

> 저장소 반영 메모(2026-07-17): 아래 내용은 조사 당시의 스냅샷이다. 이후 `feature/shared-shop-pool`에 7/17 후속 커밋(유예 만료 수정·데브로그)이 추가됐으므로, 최종 판단 전 원격 브랜치와 PR 상태를 다시 확인한다.

[[devlog-2026-07-16-kimyounguk]]/[[devlog-2026-07-16-kimtaewook]]/[[design-log-2026-07-16-hwanghaein-augment-table-v2]]에서 나온 ⚠️ 3건(유닛풀 동기화 담당경계 충돌, 4코 상점오픈 스펙 불일치, 채석가 보너스 미반영)의 실제 코드 상태를 로컬 클론에서 `git fetch --all` 후 직접 조사. 로컬 master가 origin보다 8커밋 뒤처져 있어 먼저 `git merge --ff-only`로 최신화(`7a875e66`→`60cd4282`).

## 조사 결과

### 해소 확인 (코드는 존재, 단 master 미병합)

- **4코 상점 오픈 스펙**: `feature/shared-shop-pool` 브랜치(PR #40, 커밋 작성자 Pinut1)의 `EconomyShopAugment.cs`에 v2 스펙(100% 확정 2회 발동: 즉시+1~3라운드 중 1회, 매 발동 4골드) 정확히 구현됨. master의 `ShopManager.ForceOpenCostFour()`는 여전히 구버전(15% PLACEHOLDER 확률, 골드 지급 없음).
- **채석가 보너스**: 같은 브랜치 `GambleStoneAugment.cs`에 "최초 지급 시 재조합기+2골드" 로직 정확히 구현됨. master의 `QuarryAugment.cs`는 여전히 보너스 없는 구버전.

### 미해결로 격상 — 경쟁 브랜치 중복 구현 (신규)

- 김태욱의 `feature/shared-unit-pool-sync`(작성자 ktw1306, `NetworkManager.cs` 774줄 직접 수정·PunRPC 5종 이상 신규 작성 확인)와 김영욱의 `feature/shared-shop-pool`(PR #40, MasterClient 권위·revision 기반 스냅샷 방식)이 **같은 기능(2인 공용 상점/유닛풀)을 서로 다른 설계로 각자 구현**한 상태.
- 영욱은 자신의 devlog(`feature/shared-shop-pool` 브랜치의 `Docs/DEVLOG_2026-07-16.md`, memory엔 아직 미저장)에서 PR #40을 "태욱님이 구현·2인 검증완료·Open/Clean/Mergeable"이라 적어놨으나, **실제 커밋 작성자는 영욱 본인**이고 API 설계도 태욱 브랜치와 다름 — 두 브랜치의 실제 관계(리뷰만 한 건지, 통째로 재구현한 건지)가 기록만으로는 불명확.
- 두 브랜치 모두 `ShopManager.cs`/`NetworkManager.cs`를 광범위하게 수정해서, 정리 없이 그대로 두면 나중에 병합 충돌 위험.
- 조사 시점(2026-07-17) 기준 `git log --all --since=2026-07-17` 결과 0건 — 이후 진전 없음, 두 브랜치 다 미병합 상태로 정체 중.

## 참고: 조사 방법

`gh` CLI 미설치라 PR 상태(리뷰/코멘트 등 실시간 GitHub 정보)는 확인 못 함 — 브랜치/커밋/diff는 전부 `git fetch`+로컬 clone 기준. "PR #40 Open/Clean/Mergeable"이라는 상태 서술은 GitHub API가 아니라 영욱이 자기 devlog 파일에 적어놓은 텍스트를 그대로 인용한 것 — 실시간 정확성 보장 안 됨.

**Why:** [[devlog-2026-07-16-kimyounguk]]/[[devlog-2026-07-16-kimtaewook]]가 남긴 3개 ⚠️ 항목 중 2개(스펙 문제)는 실제로 이미 해결돼 있었다는 걸 코드로 확인한 사례 — devlog만으로는 "미반영"처럼 보였지만 실제로는 다른 브랜치에 이미 정답이 구현돼 있었음. 반면 유닛풀 동기화는 오히려 조사할수록 더 복잡해진 케이스(중복 구현) — [[hwanghaein-left-development-2026-07-15]] 이후 개발 인력이 사실상 영욱·태욱 2인으로 줄면서 같은 작업을 따로 진행하다 겹친 것으로 보임.

**How to apply:** 다음에 "4코 상점 오픈"이나 "채석가" 관련 질문이 오면 스펙 자체는 확정·구현 완료 상태(PR #40)이고 남은 건 병합뿐이라고 답할 것 — devlog의 "미확정 PLACEHOLDER" 서술을 그대로 인용하지 말 것. 유닛풀/공용상점 관련 질문에는 **두 개의 경쟁 브랜치(`feature/shared-unit-pool-sync` vs `feature/shared-shop-pool`/PR #40) 중 어느 쪽을 채택할지 팀 결정이 안 난 상태**임을 전제로 답할 것. 이후 진전 확인 시 `git fetch --all` + `git merge-base --is-ancestor <branch> origin/master`로 병합 여부부터 재확인.

<#
.SYNOPSIS
    PokeChess 일일 DevLog를 git 이력에서 생성해 로컬 .md로 저장하고 Notion에 게시한다.

.DESCRIPTION
    지정한 날짜(기본: 오늘)에 PokeChess 레포에 들어간 커밋을 모아 DevLog를 만든다.
    기존 자동 로그가 "끝난 팀 멀티게임 레포"를 보던 문제를 해결하기 위해, 대상 레포를
    PokeChess로 고정한 새 스크립트.

.PARAMETER Date
    대상 날짜 (yyyy-MM-dd). 기본값: 오늘.

.PARAMETER RepoPath
    git 이력을 읽을 레포 경로. 기본값: PokeChess.

.PARAMETER Author
    특정 작성자(이름 부분일치)만 필터링. 비우면 전체.

.PARAMETER DryRun
    Notion 게시 없이 콘솔 미리보기 + 로컬 .md 저장만.

.NOTES
    Notion 게시에는 환경변수 NOTION_TOKEN(통합 토큰)이 필요하다. 최초 1회 설정:
      1) https://www.notion.so/my-integrations 에서 통합 생성 → "Internal Integration Secret" 복사
      2) Notion에서 'DevLog' 페이지 → 우측 ... → 연결(Connections) → 만든 통합 추가(공유)
      3) PowerShell:  setx NOTION_TOKEN "secret_xxxxx"   (새 터미널부터 적용)

    실행 예:
      .\Generate-DevLog.ps1                 # 오늘 DevLog 생성 + Notion 게시
      .\Generate-DevLog.ps1 -Date 2026-06-13 -DryRun
#>
[CmdletBinding()]
param(
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
    [string]$RepoPath = 'C:\Users\mbc-16\Documents\Claude\Projects\PokeChess',
    [string]$ParentPageId = '35f2391f7a83815f97a4d5574efa8b55',  # Notion 'DevLog' 페이지
    [string]$Author = '',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# ── 0. 사전 점검 ────────────────────────────────────────────
if (-not (Test-Path (Join-Path $RepoPath '.git'))) {
    throw "git 레포가 아님: $RepoPath"
}
try { $null = Get-Date $Date } catch { throw "잘못된 날짜 형식(yyyy-MM-dd): $Date" }
$nextDate = (Get-Date $Date).AddDays(1).ToString('yyyy-MM-dd')

function Git { git -C $RepoPath @args }

# ── 1. 해당 날짜 커밋 수집 ──────────────────────────────────
# --all: 어느 브랜치든 그날 들어간 커밋 포함(해시 기준 자동 dedupe). 머지커밋 제외.
$raw = Git log --all --no-merges --after="$Date 00:00:00" --before="$nextDate 00:00:00" --date=format:'%H:%M' --pretty=format:'%h|%an|%ad|%s'

$commits = @()
foreach ($line in ($raw -split "`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $p = $line -split '\|', 4
    if ($p.Count -lt 4) { continue }
    if ($Author -and $p[1] -notlike "*$Author*") { continue }
    $commits += [pscustomobject]@{ Hash = $p[0]; Author = $p[1]; Time = $p[2]; Subject = $p[3] }
}

# ── 2. 깃 상태 요약 ─────────────────────────────────────────
$uncommitted = @(Git status --porcelain).Where({ $_ }).Count
$unpushed    = @(Git log --branches --not --remotes --oneline).Where({ $_ }).Count
$stash       = @(Git stash list).Where({ $_ }).Count

# ── 3. 마크다운 본문 구성 ───────────────────────────────────
$titleDate = (Get-Date $Date -Format 'yyyy년 M월 d일')
$pageTitle = "DevLog - $titleDate"

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("## 📝 오늘 커밋")
if ($commits.Count -eq 0) {
    [void]$md.AppendLine("오늘 커밋 없음")
} else {
    foreach ($c in $commits) {
        [void]$md.AppendLine("- ``$($c.Hash)`` ($($c.Time)) $($c.Subject)  — _$($c.Author)_")
    }
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## ⚠️ 깃 상태 확인")
[void]$md.AppendLine("- 미커밋 변경사항: $uncommitted 건")
[void]$md.AppendLine("- 언푸시 커밋: $unpushed 건")
[void]$md.AppendLine("- 스태시: $stash 건")
[void]$md.AppendLine("")
[void]$md.AppendLine("## 📌 내일 할 일")
[void]$md.AppendLine("- (다음 작업을 여기에 추가하세요)")
$markdown = $md.ToString()

# ── 4. 로컬 .md 백업 저장 ──────────────────────────────────
$outDir = Join-Path $RepoPath 'Docs\devlogs'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outFile = Join-Path $outDir "DevLog_$Date.md"
"# $pageTitle`r`n`r`n$markdown" | Set-Content -Path $outFile -Encoding UTF8
Write-Host "[저장] $outFile" -ForegroundColor Green

Write-Host "`n----- 미리보기 -----" -ForegroundColor Cyan
Write-Host $markdown

# ── 5. Notion 게시 ─────────────────────────────────────────
if ($DryRun) { Write-Host "[DryRun] Notion 게시 생략" -ForegroundColor Yellow; return }
if (-not $env:NOTION_TOKEN) {
    Write-Warning "NOTION_TOKEN 환경변수가 없어 Notion 게시를 건너뜁니다. (.md는 저장됨)"
    return
}

# 마크다운 → Notion 블록 변환 (heading_2 / bulleted_list_item)
function New-RichText($t) { ,@(@{ type = 'text'; text = @{ content = "$t" } }) }
$children = @()
foreach ($line in ($markdown -split "`n")) {
    $l = $line.TrimEnd()
    if ($l -eq '') { continue }
    if ($l -like '## *') {
        $children += @{ object = 'block'; type = 'heading_2'; heading_2 = @{ rich_text = (New-RichText $l.Substring(3)) } }
    } elseif ($l -like '- *') {
        $children += @{ object = 'block'; type = 'bulleted_list_item'; bulleted_list_item = @{ rich_text = (New-RichText $l.Substring(2)) } }
    } else {
        $children += @{ object = 'block'; type = 'paragraph'; paragraph = @{ rich_text = (New-RichText $l) } }
    }
}

$body = @{
    parent     = @{ page_id = $ParentPageId }
    icon       = @{ type = 'emoji'; emoji = '🎮' }
    properties = @{ title = @{ title = (New-RichText $pageTitle) } }
    children   = $children
}
$json  = $body | ConvertTo-Json -Depth 25
$bytes = [System.Text.Encoding]::UTF8.GetBytes($json)   # 한글 깨짐 방지(PS5.1)

$headers = @{
    'Authorization'  = "Bearer $env:NOTION_TOKEN"
    'Notion-Version' = '2022-06-28'
}

try {
    $irm = @{
        Uri         = 'https://api.notion.com/v1/pages'
        Method      = 'Post'
        Headers     = $headers
        ContentType = 'application/json; charset=utf-8'
        Body        = $bytes
    }
    $resp = Invoke-RestMethod @irm
    Write-Host "[Notion] 게시 완료 → $($resp.url)" -ForegroundColor Green
} catch {
    Write-Error "[Notion] 게시 실패: $($_.Exception.Message)"
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
}

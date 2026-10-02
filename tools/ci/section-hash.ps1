# ═══════════════════════════════════════════════════════════════
#  section-hash.ps1 — 기획 문서의 "🛠 개발 설계안 (Claude)" 섹션 지문
# ═══════════════════════════════════════════════════════════════
# spec-cycle 신뢰 경계: 설계 트랙이 설계안을 쓴 직후 이 해시를 `설계 해시` 속성에 저장하고,
# 구현 트랙(⑨)이 승인된 문서를 다시 받아 같은 해시인지 대조한다. 다르면 승인 이후 누군가
# 설계안 섹션을 고친 것이므로 구현하지 않는다.
#
# 입력: Notion fetch 로 받은 페이지 마크다운을 저장한 파일 (-File) — 항상 fetch 결과로 계산할 것
#       (쓴 내용으로 계산하면 Notion 렌더링 차이 때문에 대조가 어긋난다)
# 범위: "## 🛠 개발 설계안 (Claude)" 줄부터 "## 📋 구현 리포트" 줄 직전(없으면 끝)까지
# 정규화: 모든 공백 제거 후 sha256 → 앞 12자리
# 출력 마지막 줄: HASH=<12자리> 또는 HASH=none (섹션 없음)

[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$File)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$text = [System.IO.File]::ReadAllText((Resolve-Path $File), [System.Text.Encoding]::UTF8)
$text = $text -replace '\\n', "`n"          # JSON 이스케이프된 fetch 결과도 허용
$start = $text.IndexOf('## 🛠 개발 설계안 (Claude)')
if ($start -lt 0) { Write-Host "HASH=none"; exit 0 }
$end = $text.IndexOf('## 📋 구현 리포트', $start)
$section = if ($end -gt $start) { $text.Substring($start, $end - $start) } else { $text.Substring($start) }
$norm = [regex]::Replace($section, '\s+', '')
$sha = [System.Security.Cryptography.SHA256]::Create()
$hex = -join ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($norm)) | ForEach-Object { $_.ToString('x2') })
Write-Host "CHARS=$($norm.Length)"
Write-Host "HASH=$($hex.Substring(0, 12))"

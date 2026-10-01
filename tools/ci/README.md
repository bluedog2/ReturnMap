# Unity CI (배치모드)

에디터를 켜지 않아도, 혹은 메인 프로젝트를 에디터로 열어둔 채로도 컴파일·데이터 무결성·테스트를 검증한다.
CI는 **별도 git worktree**(`..\ReturnMap-ci`)에서 돌기 때문에 메인 프로젝트의 Unity 잠금과 충돌하지 않는다.

## 처음 한 번

```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\setup-ci-worktree.ps1 -CopyLibrary
```
`-CopyLibrary`는 메인 `Library`를 복사해 첫 임포트 시간을 줄인다(생략 가능, 대신 첫 실행이 길어짐).

## 실행

```powershell
# 현재 작업트리(미커밋·신규 파일 포함) 검증
powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1

# 특정 브랜치/커밋 검증 + EditMode 테스트
powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1 -Ref spec/SPEC-9 -Tests
```

| 옵션 | 기본값 | 설명 |
|---|---|---|
| `-Ref` | (없음 = 작업트리 스냅샷) | 검증할 브랜치/커밋 |
| `-Tests` | off | `-runTests -testPlatform EditMode` 추가 실행 (Unity 1회 더 기동) |
| `-TimeoutMinutes` | 40 | Unity 1회 실행 제한 시간 |
| `-CiPath` | `..\ReturnMap-ci` | CI worktree 경로 |
| `-OutDir` | `Logs\ci` | 결과 출력 폴더 (`Logs/`는 gitignore) |

작업트리 스냅샷은 **임시 인덱스**로 커밋 객체만 만들어 CI worktree에 체크아웃한다. 메인 작업트리·인덱스·브랜치는 건드리지 않는다.
CI worktree는 전용 공간이라 매 실행마다 `checkout --force` + `clean -fd`로 초기화된다(직접 작업하지 말 것).

| `-HarvestMetaTo` | (없음) | Unity가 CI worktree에 새로 만든 `.meta`를 지정 worktree로 복사 (spec 브랜치 GUID 고정용) |

## 기획별 구현 worktree (spec-cycle 3단계)

```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\spec-worktree.ps1 -Spec SPEC-9          # ..\ReturnMap-spec\SPEC-9 (spec/SPEC-9, main 기준)
powershell -ExecutionPolicy Bypass -File tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Remove  # worktree만 제거, 브랜치 유지
```
코드 편집 전용이라 Unity로 열지 않는다. 검증은 `unity-ci.ps1 -Ref spec/SPEC-9 -HarvestMetaTo ..\ReturnMap-spec\SPEC-9`.

## 결과

| 파일 | 내용 |
|---|---|
| `Logs/ci/ci-summary.json` | 최종 요약 (result, commit, compileErrors, checks, tests) — 자동화가 읽는 파일 |
| `Logs/ci/ci-result.json` | `CIRunner` 원본 리포트 |
| `Logs/ci/unity-ci.log`, `unity-tests.log` | Unity 로그 |
| `Logs/ci/editmode-results.xml` | NUnit 테스트 결과 |

종료 코드: `0` PASS · `1` FAIL(검사 오류/테스트 실패) · `2` COMPILE_ERROR · `3` INFRA_ERROR(잠금·라이선스·타임아웃 등)

## CIRunner 검사 항목 (`Assets/_Project/Editor/CI/CIRunner.cs`)

| 검사 | 오류(실패) | 경고 |
|---|---|---|
| compile | 스크립트 컴파일 실패 | — |
| maps | JSON 파싱 실패, `MapData.Validate` 실패, 맵 0개 | `MapAuthoringValidator` 경고, mapId≠파일명 |
| stageCatalog | 에셋 없음, mapId 중복/빈 값, JSON 없는 mapId | 카탈로그 미등록 JSON, 스폰 테이블 미지정 |
| trapDefinitions | 에셋 0개, 로드 실패 | 누락 참조 |
| prefabs | Missing Script | 누락 참조 |
| scenes | Missing Script, 씬 열기 실패 | 누락 참조 |

에디터에서도 `ReTrap → Dev → CI 검증 실행 (에디터)` 메뉴로 같은 검사를 돌릴 수 있다(종료 없음, Unity MCP에서도 호출 가능).

## 아직 없는 것

- **PlayMode 스모크(실제 맵 로드·페이즈 전환)** — 플레이 모드가 필요해 PlayMode 테스트로 할 예정. 런타임 asmdef 도입 결정이 선행돼야 함(설계 리뷰 필요).
- **EditMode 테스트 어셈블리** — 아직 테스트가 없어 `-Tests`는 0건으로 끝날 수 있다.

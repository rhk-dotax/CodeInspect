# CodeInspect 개발 히스토리

## 프로젝트 개요

Windows 오프라인(폐쇄망) 환경에서 설치 없이 동작하는 소스코드 취약점 정적분석 도구.
`.NET Framework`(Windows 내장) + `csc.exe`(Windows 내장 컴파일러)로 빌드되어 추가 런타임 설치가 불필요함.

- **지원 언어**: C, C++, Java, C#
- **UI**: WinForms
- **빌드**: `csc.exe` (Windows 10/11 기본 내장)
- **타겟**: .NET Framework 4.x (C# 5 호환)

---

## 변경 이력

### v0.1 (초기 설계) — Python 시도 → 포기
- 초안으로 Python + tkinter 구현 시도
- **폐쇄망 요구사항** 때문에 Python 런타임 설치도 피해야 한다는 판단으로 포기
- `analyzer.py` 삭제

### v0.2 — .NET WinForms 전환
- Windows 내장 `.NET Framework` + `csc.exe` 활용 → 추가 설치 불필요
- 파일 구조:
  - `VulnerabilityRules.cs`: 취약점 규칙 정의 (60+ 개, CWE 기반)
  - `Analyzer.cs`: 분석 엔진 + 로그 기록 유틸
  - `MainForm.cs`: WinForms GUI
  - `Program.cs`: 엔트리포인트 + `--hook` 모드
  - `build.bat` / `build.rsp`: 빌드 스크립트
  - `install_hook.bat`: Git pre-commit hook 설치 스크립트

### v0.3 — C# 5 호환성 이슈 해결
- `csc.exe` v4.8 (Framework 4.0.30319)는 **C# 5까지만 지원**
- 수정 사항:
  - Auto-property initializer (`public RegexOptions Options { get; set; } = ...`) → 명시적 프로퍼티 + 생성자
  - Null-conditional operator (`e.Value?.ToString()`) → `e.Value != null ? ... : ""`

### v0.4 — build.bat 인코딩/옵션 이슈
- 증상: `build.bat` 실행 시 `??` 표시 + csc.exe 옵션 파싱 실패
- 원인 1: 한글 메시지가 cmd.exe 기본 코드페이지(CP949)와 충돌
- 원인 2: csc.exe 옵션에서 `/utf8output` + caret(`^`) 줄바꿈 조합 파싱 실패
- 해결:
  - `build.bat` 메시지를 영문으로 변경
  - csc.exe 옵션을 `build.rsp` response 파일로 분리 (`csc.exe @build.rsp`)
  - `/utf8output` 제거

### v0.5 — 요구사항 검증
- 검증 #2: GUI의 "분석 시작" 버튼 → `AnalyzeDirectory` 호출 (Git 무관하게 독립 동작 확인)
- 검증 #3: pre-commit hook 설치 후 commit 테스트
  - 취약점 12건 검출 + 로그 생성 + commit 정상 진행 확인
- 검증 #4: 로그 형식 (일자/검출파일/검출라인/검출사유) 4개 필수 항목 모두 포함 확인

### v0.6 — GUI 분석 시 텍스트 로그 추가
- 기존: GUI 분석 완료 시 CSV 로그만 생성 + 검출 건수 0이면 로그 생략
- 변경: `.log`(텍스트) + `.csv` 모두 생성 + 검출 건수 0이어도 로그 기록

### v0.7 — 다중 프로젝트 지원
- **멀티 프로젝트** 관리 기능 추가
  - 좌측 패널에 `ListBox`로 A/B/C 등 여러 프로젝트 경로 관리
  - "프로젝트 추가", "프로젝트 제거" 버튼
  - "선택 프로젝트 분석" / "전체 프로젝트 분석" 두 모드
  - "Git Hook 설치 (선택 프로젝트)" 버튼으로 프로젝트별 hook 설치
  - 프로젝트 목록은 `codeinspect_projects.txt`에 영속화

### v0.8 — Git Hook 제거 기능
- "Git Hook 제거" 버튼 추가 (빨간색, 설치 버튼 옆)
- 안전장치:
  - 제거 전 `.removed_YYYYMMDD_HHMMSS` 파일로 자동 백업
  - CodeInspect가 설치한 hook이 아니면 확인 대화상자 표시
- 선택된 여러 프로젝트에 대해 일괄 처리, 결과를 OK/SKIP/FAIL로 구분 요약

### v0.9 — 분석 룰셋 외부화 및 UI 관리
**배경**: `VulnerabilityRules.cs`에 60+개 규칙이 하드코딩되어 있어 사용자가 규칙을 추가/수정/갱신하려면 재빌드 필요했음. 폐쇄망에서도 CWE 업데이트와 현장 맞춤화가 가능하도록 외부화.

- 신규 `RuleStore.cs` — 외부 파일(`codeinspect_rules/rules.config`) 기반 룰셋 저장소
  - INI 스타일 포맷(`[RULE]` 블록 + `key=value`)으로 notepad 편집 가능
  - 정규식의 `\`를 이스케이프 없이 원본 그대로 저장 (가독성)
  - 로드/저장/파싱, URL 다운로드(WebClient + TLS 1.2), 백업, 검증(Regex 컴파일) 포함
  - 첫 실행 시 `DefaultRules`로 자동 시드
- 신규 `RulesEditorForm.cs` — 3개 UI 폼 포함
  - `RulesEditorForm`: ListView 기반 룰 관리 (검색/언어 필터/심각도 색상)
  - `RuleEditDialog`: 단일 룰 편집 (언어 체크박스, 정규식 실시간 테스트 영역)
  - `RulesUpdateDialog`: URL 다운로드 또는 내장 기본 복원, 병합/교체 방식 선택
- `VulnerabilityRules.cs` 수정
  - 기존 하드코딩 `Rules` 리스트를 `DefaultRules`로 이름 변경 (시드/복원용 보존)
  - `Rules`는 `RuleStore.Load()`를 호출하는 프로퍼티로 전환
- `MainForm.cs` 수정
  - 좌측 패널 하단에 "분석 룰셋" 섹션 추가 (룰 개수 표시)
  - 3개 버튼: 룰셋 관리 / 룰셋 보기(notepad) / 룰셋 업데이트
  - `_analyzer` `readonly` 제거, 분석 시점마다 재생성하여 최신 룰 반영
  - 폼 높이 850 → 920 (좌측 패널 확장 수용)
- `build.rsp` 수정: `RuleStore.cs`, `RulesEditorForm.cs` 추가

**자동 백업**: `RuleStore.Save`, `RestoreDefaults`, `DownloadAndApply`, notepad 편집 전 모두 `backup/rules_yyyyMMdd_HHmmss.txt` 생성. 동일 초 중복 시 `_N` 접미사로 회피.

**빌드**: `csc.exe @build.rsp` 성공, 경고 0건 (`RuleEditDialog.Load` → `LoadFromRule`로 개명하여 `Form.Load` shadow 경고 제거).

**C# 5 호환성 유지**: `$""`, `?.`, pattern matching, `out var`, `nameof` 미사용 확인.

### v0.9.1 — 룰셋 파일 확장자 변경 (.txt → .config)
- `codeinspect_rules/rules.config`: 메인 룰셋 파일 확장자 변경
- `codeinspect_rules/backup/rules_yyyyMMdd_HHmmss.config`: 백업 파일 확장자 변경
- 변경 파일: `RuleStore.cs` (RulesFile, Backup 경로), `VulnerabilityRules.cs` (주석)

### v0.9.2 — 확장자 변경 반영 + 기본 URL/다운로드 오류 개선
**배경**: v0.9.1의 확장자 변경이 소스에는 반영됐으나 EXE를 재빌드하지 않아 런타임에선 여전히 `rules.txt` 생성. 또한 `RulesUpdateDialog`의 기본 URL이 존재하지 않는 가상 주소로 설정되어 있어 실행 시 404 발생.

- **EXE 재빌드** — 확장자 변경이 실제 런타임에 반영되도록 `CodeInspect.exe` 재컴파일
- **레거시 파일 자동 마이그레이션** — `RuleStore.Load()`에서 `rules.config`가 없고 `rules.txt`만 있으면 자동으로 `File.Move` (실패 시 `File.Copy` 폴백). 기존 사용자의 룰 편집 내용 보존
- **기본 업데이트 URL을 공란으로 변경** — 폐쇄망 전제이므로 공개 URL을 강제하지 않음. 사용자가 사내 서버 주소를 직접 입력
- **URL 입력 사전 검증 추가** — `Uri.TryCreate`로 스킴(http/https/file) 검증 후 네트워크 호출
- **`WebException` 상세 처리** — HTTP 상태코드(404 등), DNS 실패, 연결 실패, 타임아웃, TLS 실패를 각각 한글 메시지로 구분
- **다이얼로그 UX 개선**:
  - URL 공란이면 "내장 기본 CWE 룰셋으로 복원" 라디오를 기본 선택 (안전한 진입점)
  - 안내 문구에 "rules.config 파일", "[RULE] 블록 포맷" 명시
  - 레이아웃 Y 좌표 재조정 (다이얼로그 높이 380 → 400)

### v0.10 — Semgrep / OWASP ASVS 내장 룰팩 추가 (2026-04-22)
**배경**: 폐쇄망 환경에서는 외부 룰셋 다운로드가 제한적이므로, 공인된 OSS/국제표준 기반 룰셋을 EXE에 **내장**하여 즉시 적용 가능하도록 제공. 각 룰에 출처를 명시하여 감사/추적성 확보.

- **신규 `Reference` 필드** — `VulnerabilityRule` 클래스에 `Reference` 프로퍼티 추가 (출처 인용: `semgrep:...`, `OWASP ASVS v4.0.3 Vx.y.z`)
- **신규 `RulePacks.cs`** — 두 종의 내장 룰팩 제공
  - `GetSemgrepPack()` — Semgrep 공개 OSS 룰셋(github.com/returntocorp/semgrep-rules) 기반 **27개 룰**, Java/C/C++/C#. Semgrep의 AST 매칭이 아닌 CodeInspect의 line-regex 엔진용으로 정규식 포맷 재구현. 각 룰 `Reference = semgrep:<rule-id>`
  - `GetOwaspAsvsPack()` — OWASP ASVS v4.0.3 요구사항 기반 **22개 룰**. 각 룰 `Reference = OWASP ASVS v4.0.3 Vx.y.z` 로 요구사항 번호 인용
  - `GetCombinedPack()` — 두 팩 합본(RuleId 기준 중복 제거), 총 **49개 룰**
  - 검증: 49개 룰 모두 정규식 컴파일 성공, INI 직렬화/역직렬화 Round-trip에서 Reference 필드 보존 확인
- **`RulesUpdateDialog` 확장** — 업데이트 소스로 3개 옵션 추가
  - "Semgrep 공개 룰팩 (내장)"
  - "OWASP ASVS v4.0.3 룰팩 (내장)"
  - "Semgrep + OWASP ASVS 통합 룰팩 (내장)"
  - URL이 비어있을 때 기본 진입점이 **통합 룰팩**으로 변경 (폐쇄망에서 가장 유용)
  - 적용 방식(병합/전체 교체)은 URL/내장 팩 공통 적용
  - 다이얼로그 높이 400 → 555, GroupBox 확대로 6개 라디오 수용
- **`RuleStore.ApplyPack(pack, merge)`** — 내장 룰팩 적용 헬퍼. 기존 룰과 병합 시 RuleId 기준 덮어쓰기, 전체 교체 시 해당 팩으로 초기화. 파일 변경 전 backup 자동 생성
- **`RulesEditorForm` UI 확장**
  - ListView에 "출처" 컬럼 추가 (너비 180px)
  - 검색 대상에 Reference 필드 포함
- **`RuleEditDialog` UI 확장** — "출처" TextBox 추가, 예시 안내 라벨 포함. 폼 높이 620 → 680
- **INI 포맷 확장** — `reference=` 키 지원. `Reference` 값이 비어있으면 직렬화 시 생략(기존 룰 하위호환)
- **`build.rsp` 업데이트** — `RulePacks.cs` 포함
- **EXE 재빌드 완료** — CodeInspect.exe 약 80KB → 107KB로 증가

**C# 5 호환성**: 모든 추가 코드에서 `$""`, `?.`, pattern matching, `out var`, `nameof`, auto-property initializer 미사용 확인. 빌드 성공(csc.exe exit 0, 경고 0건).

**출처 투명성 원칙**: 내장 룰팩의 각 룰은 Semgrep 룰 ID 또는 OWASP ASVS 요구사항 번호를 Reference로 기록하므로, 감사 시 근거 제시 가능. Semgrep 룰은 LGPL-2.1 라이선스이며 알고리즘(패턴 의도)을 참고하여 CodeInspect의 regex 엔진용으로 재구현했음.

### v0.11 — 전역 예외 로깅 (2026-04-22)
**배경**: 폐쇄망 운영 환경에서는 장애 원인을 재현하기 어려우므로, 처리되지 못한 예외가 발생했을 때 **발생 위치를 실행 디렉토리에 자동 기록**하여 사후 분석이 가능하도록 함.

- **신규 `ErrorLogger.cs`** — 정적 유틸 클래스, 실행 디렉토리(`AppDomain.BaseDirectory`)에 `yyyy-MM-dd-HH-mm-ss.log` 파일명으로 기록
  - `Install()` — `AppDomain.CurrentDomain.UnhandledException` + `Application.ThreadException` 핸들러 등록 (WinForms UI 스레드 / 백그라운드 스레드 모두 포착)
  - `Log(ex, context)` — 시각 / 컨텍스트 / 스레드 ID / 예외형식 / 메시지 / 발생위치(클래스.메서드 + 파일:라인) / 스택트레이스 / InnerException 체인 기록
  - 동시 기록 충돌 방지: 내부 `lock` 으로 직렬화
  - 로그 기록 자체 실패 시 예외 삼킴(재귀 예외 방지)
  - 인코딩: UTF-8 (BOM 없음)
- **`Program.Main` 수정** — 진입 즉시 `ErrorLogger.Install()` 호출, 최상위 `try/catch` 로 예기치 못한 예외를 로깅 후 exit 1
- **`Program.RunHookMode` / `MainForm` 보강** — 기존의 무응답 `catch { }` 블록들에 `ErrorLogger.Log()` 추가
  - `MainForm.UpdateRuleCount` / `SaveProjects` / `LoadProjects` / `InstallHookForProject` / `RemoveHookForProject` / `BtnViewRules_Click`
  - BackgroundWorker 오류(`RunAnalysis.RunWorkerCompleted`)도 로깅
- **`build.rsp` 변경**
  - `ErrorLogger.cs` 컴파일 포함
  - `/debug:pdbonly` 추가 — 최적화를 유지하면서 PDB 생성, 스택트레이스에 **파일:라인 정보** 포함
- **검증** — 테스트 하네스로 예외 강제 발생 → `2026-04-22-16-39-28.log` 생성 및 컨텍스트/메서드/스택 정상 기록 확인. 정상 경로(hook 모드 no-findings)에서는 로그 파일 미생성(부작용 없음).

**로그 예시**:
```
[시각]     2026-04-22 16:39:28.197
[컨텍스트] MainForm.RunAnalysis (BackgroundWorker)
[스레드]   5
[예외형식] System.IO.IOException
[메시지]   사용 중인 다른 프로세스가 파일에 액세스하고 있습니다.
[발생위치]
  CodeInspect.CodeAnalyzer.AnalyzeFile()
  at C:\src\claude\codeinspect\Analyzer.cs:line 87
[스택트레이스]
   ...
```

### v1.3 — LLM 기반 분석 옵션 (2026-04-27)
**배경**: 정규식 룰셋은 빠르지만 새로운/문맥적 취약점을 놓칠 수 있어, 사용자의 로컬에서 구동되는 LLM(Ollama, LM Studio)을 통해 의미 기반 분석을 보완 옵션으로 추가. 폐쇄망 제약을 위해 외부 클라우드 API는 일절 호출하지 않고, 동일 머신/LAN의 로컬 LLM 엔드포인트만 호출하도록 설계.

- **신규 `LLMAnalyzer.cs`** — Ollama / LM Studio HTTP 호출 및 응답 파싱
  - `LLMConfig` (동일 파일 내) — `codeinspect_rules/llm_config.txt`에 INI 스타일 저장. 필드: provider(ollama|lmstudio), endpoint, model, timeoutSec(기본 120), temperature(기본 0.1), maxFileSizeKB(기본 50)
  - `LLMAnalyzer.AnalyzeFile / AnalyzeDirectory` — `CodeAnalyzer`와 동일 시그니처로 다형성 확보. 디렉토리 제외 정책(.git/bin/obj 등)도 동일
  - **큰 파일 처리** — `MaxFileSizeKB` 초과 시 LOW 심각도 'LLM-SKIP' Finding 1건 추가 후 스킵
  - **HTTP 클라이언트** — `HttpWebRequest` 사용 (.NET 4.0 호환, 신규 DLL 의존성 없음)
  - **JSON** — `System.Web.Script.Serialization.JavaScriptSerializer` (`System.Web.Extensions.dll`, .NET Framework 표준 포함)
  - **Provider별 엔드포인트**:
    - Ollama: `POST /api/generate` (body `{model,prompt,stream:false,options:{temperature}}`), 응답 `.response`
    - LM Studio: `POST /v1/chat/completions` (OpenAI 호환), 응답 `.choices[0].message.content`
  - **모델 자동 조회** — `LLMAnalyzer.ListModels(provider, endpoint, ...)` 정적 메서드 (Ollama `/api/tags`, LM Studio `/v1/models`)
  - **응답 파싱** — 마크다운 코드블록 제거, JSON 배열만 추출. 각 항목을 `Finding`으로 매핑(severity 4단계 검증, MatchedCode는 실제 파일 라인에서 추출)
  - **에러 격리** — HTTP/파싱 실패 시 'LLM-ERROR' Finding 1건 남기고 다음 파일로 계속(분석 전체 중단 안 함)
- **신규 `LLMConfigForm.cs`** — 모달 설정 다이얼로그
  - Provider 라디오(Ollama/LM Studio), 엔드포인트, 모델 ComboBox + '모델 목록' 버튼, Timeout/Temperature/MaxFileKB NumericUpDown, '연결 테스트' 버튼, 상태 로그 영역
  - 프로바이더 변경 시 엔드포인트 기본값 자동 변경(Ollama=11434, LM Studio=1234)
  - OK 클릭 시 `LLMConfig.Save()`로 저장, Cancel은 변경 무시
- **`MainForm.cs` 수정**
  - 좌측 패널 Git Hook 바로 아래에 LLM 섹션 추가: `chkUseLLM` 체크박스 + `btnLLMConfig` 설정 버튼 + `lblLLMStatus` 상태 라벨. 룰셋 섹션은 그 아래로 이동
  - 폼 크기: 920 → 1020 (높이만 확대), MinimumSize 780 → 880, 타이틀 v1.2 → v1.3
  - `RunAnalysis(...)` 분기 — 체크박스 ON 시 `LLMAnalyzer`로 분석, OFF 시 기존 `CodeAnalyzer` 사용 (룰셋 모드와 LLM 모드는 상호 배타)
  - 체크박스 ON인데 LLM 미설정이면 `LLMConfigForm`을 자동으로 띄우고, Cancel이면 분석 취소
  - `ChkUseLLM_CheckedChanged` — LLM 모드 시 룰셋 관련 버튼 시각적 비활성화(Enabled=false)
  - `UpdateLLMStatusLabel()` — 라벨에 "● ollama : llama3:8b" 또는 "(미설정 - ...)" 표시. 활성 상태일 땐 녹색
  - 진행 상태/완료 메시지에 "[LLM]" 또는 "[룰셋]" 접두사로 모드 표시
- **`build.rsp` 변경**
  - `/reference:System.Web.Extensions.dll` 추가 (JavaScriptSerializer 용)
  - `LLMAnalyzer.cs`, `LLMConfigForm.cs` 컴파일 포함
- **결과 매핑** — 기존 `Finding` 클래스 그대로 사용:
  - `RuleId` = `"LLM-{model}-{seq:D3}"` (예: `LLM-llama3-001`)
  - `Severity` = LLM 응답값(CRITICAL/HIGH/MEDIUM/LOW로 검증, 잘못되면 MEDIUM)
  - `Category` = LLM 응답값 (예: "CWE-89 SQL Injection")
  - `Reason` = LLM 한국어 설명
  - `MatchedCode` = LLM이 보고한 라인 번호로 실제 파일에서 직접 추출 (LLM 위변조 방지)
- **재사용** — DataGridView 컬럼/심각도 색상/CSV 내보내기/요약 패널/필터/로그 디렉토리/`BackgroundWorker` 패턴/`LogWriter`/`ErrorLogger` 모두 변경 없이 그대로 사용
- **기본값 / 호환성** — 체크박스는 기본 OFF, 미사용자는 변경 없이 기존 동작. Git Hook 모드(`--hook`)는 LLM 모드 사용하지 않음(차후 확장 여지)
- **빌드** — csc.exe @build.rsp 정상 빌드(exit 0, 경고 0건). EXE 130KB

**C# 5 호환성**: `$""`, `?.`, pattern matching, `out var`, `nameof`, auto-property initializer 미사용. `BackgroundWorker` 패턴 유지(async/await 미사용).

---

## 현재 파일 구조

```
codeinspect/
├── VulnerabilityRules.cs    # 기본 룰셋(DefaultRules) + 룰 타입 정의 (Reference 필드 포함, v0.10~)
├── RuleStore.cs             # 외부 파일 기반 룰셋 저장소 (v0.9~) + ApplyPack (v0.10~)
├── RulePacks.cs             # 내장 룰팩: Semgrep/OWASP ASVS (v0.10~)
├── RulesEditorForm.cs       # 룰셋 관리/편집/업데이트 UI (v0.9~) + 출처 컬럼 (v0.10~)
├── Analyzer.cs              # 분석 엔진 + 로그 기록
├── MainForm.cs              # WinForms GUI (멀티 프로젝트 + 룰셋 + LLM 옵션 v1.3~)
├── Program.cs               # 엔트리포인트 (GUI/Hook 듀얼 모드) + 전역 예외 훅 (v0.11~)
├── ErrorLogger.cs           # 실행 디렉토리에 yyyy-MM-dd-HH-mm-ss.log 기록 (v0.11~)
├── LLMAnalyzer.cs           # LLM(Ollama/LM Studio) 분석 엔진 + LLMConfig (v1.3~)
├── LLMConfigForm.cs         # LLM 모델/엔드포인트 설정 다이얼로그 (v1.3~)
├── build.bat                # 빌드 런처
├── build.rsp                # csc.exe 옵션 파일
├── install_hook.bat         # CLI hook 설치 스크립트
├── CodeInspect.exe          # 빌드 산출물 (~130KB)
├── codeinspect_rules/       # 런타임 생성 (첫 실행 시)
│   ├── rules.config         # 사용자 편집 가능 룰셋 파일
│   ├── update_url.txt       # 다운로드 URL 저장
│   ├── llm_config.txt       # LLM 분석 설정 (v1.3~)
│   └── backup/              # 룰 변경 시 이전 버전 보관
├── codeinspect_projects.txt # 등록된 프로젝트 경로 목록
├── test_samples/            # 샘플 취약점 코드 (C/Java/C#)
├── dev_history.md           # 개발 히스토리 (본 문서)
└── plan.md                  # 개발 계획
```

---

## 알려진 제약사항

1. **C# 5 한정**: csc.exe(Framework 4.x)는 C# 5까지만 지원
   - 사용 금지: auto-property initializer, null-conditional, string interpolation (`$""`), expression-bodied members, `nameof`, pattern matching
2. **정규식 기반 분석**: AST 수준 분석이 아니므로 false positive 존재 가능
3. **cmd.exe 인코딩**: 한글 출력은 CP949 기준으로 작성 (`chcp 65001` 사용 시 깨짐 주의)

---

## 빌드 명령

```bat
build.bat
```

또는 직접:
```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe @build.rsp
```

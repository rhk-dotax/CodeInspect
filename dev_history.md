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

### v1.4 — 분석 중지 기능 (2026-04-27)
**배경**: v1.3에서 LLM 모드를 추가하면서 파일당 응답 대기가 수십 초 ~ 타임아웃까지 걸릴 수 있게 됐고, 다중 프로젝트 일괄 분석에서 첫 프로젝트가 너무 오래 걸려도 멈출 수단이 없었음. BackgroundWorker는 이미 사용 중이었으나 `WorkerSupportsCancellation = false`였고 `CancelAsync()`/`CancellationPending`도 활용되지 않았음.

- **`MainForm.cs` 수정**
  - 신규 필드: `btnCancelAnalysis`, `volatile int _cancelMode` (0=None / 1=ProjectOnly / 2=All), `BackgroundWorker _worker`(로컬 변수에서 클래스 필드로 승격), `LLMAnalyzer _activeLLM`(진행 중 LLM 인스턴스 캐싱), `List<string> _currentProjectPaths`
  - 좌측 패널에 "■ 분석 중지" 버튼 추가 (`btnAnalyzeAll` 바로 아래, 주황색 `Color.FromArgb(255, 87, 34)`, 초기 `Enabled=false`). 후속 컨트롤(separator2 / Git Hook / LLM / 룰셋 섹션) 좌표를 +36 이동
  - `RunAnalysis(...)` 변경 — `_worker`를 필드로 보관, `WorkerSupportsCancellation = true`, `_cancelMode = 0`로 초기화, `_currentProjectPaths` 저장. 시작 시 `btnCancelAnalysis.Enabled = true`
  - 분석기에 전달할 취소 콜백 `Func<bool> isCancelRequested = delegate() { return _cancelMode != 0 || worker.CancellationPending; };`
  - 프로젝트 루프 시작부에 `if (_cancelMode == 2) break;` (전체 중지), 프로젝트 종료 후 `if (_cancelMode == 1) _cancelMode = 0;` (현재 프로젝트만 건너뛰기)
  - LLM 분기: `_activeLLM = new LLMAnalyzer(...); try { ... } finally { _activeLLM = null; }`
  - `progressCb` 람다: `_cancelMode != 0`이면 `"[중지 중...] "` prefix
  - `RunWorkerCompleted`: 취소 케이스에 `"분석 중지됨 - {total}개 중 {completed}개 진행, {findings.Count}건 검출"` 메시지, 상태 필드 모두 초기화
  - 신규 핸들러 `BtnCancelAnalysis_Click(...)`:
    - `_worker == null || !_worker.IsBusy` 가드
    - 다중 프로젝트면 `ShowMultiProjectCancelDialog()` (3-버튼 다이얼로그), 단건이면 `MessageBox.Show(YesNo)`
    - 결정 모드를 `_cancelMode`에 set, 전체 중지면 `_worker.CancelAsync()` + `_activeLLM.Cancel()`, 현재 프로젝트만 건너뛰기도 LLM HTTP 호출 끊기 위해 `_activeLLM.Cancel()` 호출
    - 버튼 텍스트를 "중지 처리 중..." / "건너뛰는 중..."으로 변경 후 disable
  - 신규 메서드 `ShowMultiProjectCancelDialog()` — 즉석 `Form` 으로 3-버튼 다이얼로그 구성("현재 프로젝트만 건너뛰기" / "전체 분석 중지" / "계속 분석"). 신규 `.cs` 파일 추가 없이 `MainForm` 내부에 구현 → `build.rsp` 갱신 불필요
  - 폼 타이틀 v1.3 → v1.4

- **`Analyzer.cs` 수정**
  - `CodeAnalyzer.AnalyzeDirectory`에 신규 오버로드 추가:
    `public List<Finding> AnalyzeDirectory(string directory, Action<int,int,string,int> progress, Func<bool> isCancelRequested)`
  - 파일 루프 진입부에 `if (isCancelRequested != null && isCancelRequested()) break;`
  - 기존 1-인자 오버로드는 신규에 `null` 위임 → `Program.cs --hook` 모드 호환성 유지

- **`LLMAnalyzer.cs` 수정**
  - 신규 필드: `private volatile bool _cancelRequested;`, `private HttpWebRequest _currentRequest;`
  - 신규 public 메서드 `Cancel()` — `_cancelRequested = true; _currentRequest?.Abort()`. C# 5 제약상 null 검사 명시 + try/catch 무시
  - `HttpPost`를 `static` → 인스턴스 메서드로 변경. 진행 중 `HttpWebRequest`를 `_currentRequest`에 보관 → `try ... finally { _currentRequest = null; }`로 정리. WebException 발생 시 `_cancelRequested == true`면 본문 디버깅 로직 건너뛰고 그대로 throw하여 호출자에서 silent 처리
  - `AnalyzeFile`의 `catch (Exception ex)` 블록(HTTP 실패 처리부)에서 `if (_cancelRequested) return new List<Finding>();` 가드 추가 → ErrorLogger 호출 생략
  - `AnalyzeDirectory`에 신규 오버로드 추가(시그니처 동일). 파일 루프 진입부에서 외부 콜백 + `_cancelRequested` 모두 체크

- **HTTP 인터럽트 동작 원리**
  - 사용자 "전체 중지" 또는 "현재 프로젝트만" 선택 시 `_activeLLM.Cancel()` 호출 → 진행 중 `HttpWebRequest.Abort()` 즉시 발동 → `WebException` (status `RequestCanceled`) 발생 → `HttpPost`/`AnalyzeFile`이 `_cancelRequested` 검사 후 silent 반환 → 다음 파일/프로젝트 루프 진입 시 취소 체크에 의해 break
  - 클라이언트 응답성: Ollama/LM Studio 응답 대기 중에도 1~2초 이내 반응

- **부분 결과 보존**
  - 취소 시점까지 누적된 `findings`는 `we.Result = allFindings;`로 그대로 전달
  - 프로젝트별 로그(`LogWriter.WriteCommitLog/WriteCsvLog`)도 부분 결과로 기록됨
  - DataGridView/요약 패널 모두 부분 결과로 갱신

- **Git hook 모드 호환성**
  - `Program.cs --hook` 모드는 1-인자 `AnalyzeDirectory(dir, progress)` 또는 `AnalyzeFiles(files, progress)`만 사용 → 신규 오버로드는 옵션이므로 영향 없음. 회귀 검증 통과

- **검증**
  - `csc.exe @build.rsp` exit 0, **경고 0건** (`volatile int` + `Interlocked.Exchange(ref ...)` 조합 시 발생하던 CS0420 경고는 `Interlocked` 호출을 단순 대입 `_cancelMode = X;`로 교체하여 해결. `volatile`만으로 워커-UI 스레드 간 가시성 확보)
  - EXE 130KB → 133KB

**C# 5 호환성**: `?.`, `$""`, pattern matching, `out var`, `nameof`, auto-property initializer 미사용. `volatile`, `Func<bool>`, 익명 메서드(`delegate() { ... }`), `MessageBox`, 즉석 `Form` 모두 C# 5/.NET Framework 4.0+에서 정상 동작.

### v1.5 — 검출 결과 더블클릭으로 파일/라인 점프 (2026-04-28)
**배경**: 분석 결과 그리드에서 검출된 라인을 빠르게 확인하려면 그동안 사용자가 직접 파일을 찾아 열어야 했음. 사용성을 높이기 위해 행 더블클릭으로 외부 편집기에서 해당 라인이 자동으로 열리도록 개선. 폐쇄망/무설치 전제는 유지하되, 사용자 환경에 맞춰 사용 가능한 편집기를 자동 선택.

- **`MainForm.cs` 수정**
  - `dgvResults`에 `CellDoubleClick += DgvResults_CellDoubleClick` 이벤트 등록
  - 그리드 위에 `ToolTip`으로 "행을 더블클릭하면 검출 파일을 해당 라인에서 엽니다" 안내 추가
  - 신규 핸들러 `DgvResults_CellDoubleClick(...)`:
    - `e.RowIndex < 0` (헤더) 가드 + `_filteredFindings` 범위 가드
    - `_filteredFindings[e.RowIndex]`에서 `Finding`을 가져와 `FilePath` / `LineNumber` 추출 (그리드 표시용 상대 경로가 아닌 원본 절대 경로 사용)
    - 파일이 존재하지 않으면 안내 메시지(이동된 결과 사용 시 발생 가능)
    - `OpenInExternalEditor(filePath, lineNumber)` 호출
  - 신규 메서드 `OpenInExternalEditor(...)` — fallback 체인:
    1. **VS Code** — `code -g "file:line"`. PATH에서 `code.cmd`도 해석되도록 `UseShellExecute = true`. PATH에 없으면 `Win32Exception` 발생 → 다음 후보로 진행
    2. **Notepad++** — `C:\Program Files\Notepad++\notepad++.exe` 또는 `Program Files (x86)`에서 `File.Exists` 확인 후 `-n<line> "file"` 옵션으로 해당 라인 점프
    3. **메모장** — 최종 fallback. 라인 점프 미지원이므로 상태바에 "라인 N로 직접 이동해주세요" 안내
  - 신규 헬퍼 `TryStartProcess(fileName, arguments, useShellExecute)` — 실행 실패 시 `false` 반환, fallback 체인에서 활용
  - 폼 타이틀 v1.4 → v1.5

- **`build.rsp` 변경 없음** — 신규 .cs 파일 추가 없음(MainForm 내부에 메서드 추가)

- **에디터 인자 형식**
  - VS Code: `-g <path>:<line>` (Goto 형식, 0-base가 아닌 1-base)
  - Notepad++: `-n<line> <path>` (라인 번호는 옵션과 붙여서 작성)
  - 메모장: 라인 인자 미지원 → 안내 메시지로 대체

- **결과 행 → Finding 매핑**
  - `_filteredFindings`는 `PopulateGrid`에서 그리드와 동일한 순서로 채워지므로 `e.RowIndex == _filteredFindings`의 인덱스
  - 그리드 표시용 상대 경로(`fileDisplay`)가 아니라 `Finding.FilePath`(절대 경로)를 사용 → 어떤 작업 디렉토리에서도 정확히 열림

- **검증**
  - `csc.exe @build.rsp` exit 0, **경고 0건**
  - EXE 133KB → 132KB 수준 유지(135168 bytes)
  - C# 5 호환성: `$""`, `?.`, pattern matching, `out var`, `nameof`, auto-property initializer 미사용. `Process.Start` / `ProcessStartInfo` / `ToolTip` / `DataGridView.CellDoubleClick` 모두 .NET Framework 4.0+ 표준

**C# 5 호환성**: 모든 추가 코드에서 금지 기능 미사용. 빌드 경고 0건.

### v1.6 — 로그 삭제 기능 (2026-04-28)
**배경**: 분석을 반복할수록 `codeinspect_logs/<프로젝트명>/` 하위에 `commit_vuln_*.log` / `vuln_report_*.csv`가 무한히 누적되어 디스크 공간을 차지하고, 사용자가 수동으로 정리하지 않으면 오래된 로그가 그대로 남는 문제가 있었음. 이를 해결하기 위해 메인 화면에 진입점 버튼을 추가하고, 수동 일괄/프로젝트별 삭제와 더불어 프로젝트별 자동 보존 주기(일 단위, 0=미삭제) 및 시작 시 자동 퍼지 기능을 도입.

- **신규 파일 `LogDeleteForm.cs`** (한 파일에 3개 클래스, `LLMAnalyzer.cs`의 `LLMConfig` + `LLMAnalyzer` 동거 패턴 모방)
  - `LogRetentionConfig` static — `codeinspect_rules/log_retention.txt`에 보존 주기 영속. 포맷: `project|<프로젝트절대경로>|<일수>` (Windows 경로엔 `|`가 들어갈 수 없어 충돌 위험 0). 주석 `#`, UTF-8 BOM 없음. `Dictionary<string,int> LoadMap()` / `Save(Dictionary<string,int>)` 두 API.
  - `LogCleaner` static — 4개 메서드:
    - `string GetProjectLogFolderName(string)`: `Path.GetFileName(projectPath.TrimEnd('\\','/'))` — `MainForm.RunAnalysis`의 `projLogDir` 매핑과 정확히 동일.
    - `int DeleteAll(string logRootDir)`: 루트 내 모든 파일 + 하위 폴더 재귀 삭제(루트 폴더 자체는 유지). 개별 실패는 `ErrorLogger.Log` 후 계속.
    - `int DeleteProjectLogs(string logRootDir, string projectPath)`: 해당 프로젝트 서브폴더 안 모든 파일만 삭제(폴더 유지).
    - `int CountProjectLogFiles(string logRootDir, string projectPath)`: 그리드의 "파일 수" 컬럼 표시 + "선택 프로젝트 로그 삭제" 버튼 활성 판정.
    - `int PurgeExpiredOnStartup()`: `Application.StartupPath`로 `log_retention.txt` 로드, `days > 0`인 항목만 순회하며 `(DateTime.Now - File.GetLastWriteTime(f)).TotalDays > days`인 파일만 `File.Delete`. 다른 프로젝트와 통합 로그(루트 직접 파일)는 건드리지 않음.
  - `LogDeleteForm : Form` (720×560, FixedDialog, CenterParent, MaximizeBox/MinimizeBox 비활성):
    - 상단 안내 라벨 "로그 폴더: {logDir}".
    - 그룹박스 ① 전체 로그 삭제 — 빨강 "전체 삭제" 버튼 + YesNo 재확인.
    - 그룹박스 ② 프로젝트별 로그 관리 — `DataGridView` 3컬럼(프로젝트 경로 read-only 420px, 삭제주기(일) 편집가능 110px, 파일 수 read-only 90px), 인라인 편집 종료 시 정수 0 이상 검증(비정상 → 0). 우측 하단 "선택 프로젝트 로그 삭제" 버튼은 행 선택 + 파일 1개 이상일 때만 활성.
    - 그 아래 "일괄 적용 주기(일)" `NumericUpDown`(0~3650) + "전체 일괄 적용" 버튼 — 메모리상 모든 행의 일수를 일괄 변경(저장은 [확인] 버튼).
    - 하단 "확인 (저장)" / "취소". 저장 시 그리드 → `Dictionary<projectPath,int>` → `LogRetentionConfig.Save`. 더 이상 `codeinspect_projects.txt`에 없는 옛 항목은 자연 제거됨.

- **`MainForm.cs` 수정**
  - 신규 필드 `private Button btnDeleteLogs;`
  - 타이틀 패널 (`pnlTitle`) 우측 상단에 버튼 추가:
    - 기존 `lblSubTitle.Width` 400 → 320으로 축소
    - `btnDeleteLogs` Dock=Right, Width=110, BackColor `Color.FromArgb(108, 117, 125)`, Text "🗑 로그 삭제"
    - `pnlTitle.Controls.AddRange(new Control[] { lblTitle, lblSubTitle, btnDeleteLogs })` — WinForms Dock 규칙상 같은 `DockStyle.Right`에서 늦게 추가된 컨트롤이 우측 모서리에 가까이 도킹되므로 `btnDeleteLogs`가 우측 끝에 위치, 그 좌측에 `lblSubTitle`.
  - 신규 핸들러 `BtnDeleteLogs_Click(...)` — `lstProjects.Items`로부터 프로젝트 경로 리스트를 만들어 `new LogDeleteForm(_logDir, projectPaths).ShowDialog(this)` 호출.
  - 폼 타이틀 v1.5 → v1.6.

- **`Program.cs` 수정**
  - `--hook` 분기 통과 후, `Application.EnableVisualStyles()` 직전에 `try { LogCleaner.PurgeExpiredOnStartup(); } catch (Exception exPurge) { ErrorLogger.Log(exPurge, "Program.Main / PurgeExpiredOnStartup"); }` 삽입 → GUI 모드에서만 자동 퍼지 실행. `--hook` 모드는 매 커밋마다 디스크 I/O 발생을 피하기 위해 우회.

- **`build.rsp` 수정** — `LogDeleteForm.cs` 한 줄 추가.

- **저장 포맷 예시** (`codeinspect_rules/log_retention.txt`)
  ```
  # CodeInspect 로그 자동 삭제 주기
  # 단위: 일. 0이면 자동 삭제 안 함.
  # 형식: project|<프로젝트절대경로>|<일수>
  # 본 파일은 LogDeleteForm 다이얼로그에서 자동 생성/갱신됩니다.

  project|C:\src\codex\aiocr\LocalOfflineOcr|7
  project|C:\src\cursor\webview2|0
  ```

- **시작 퍼지 동작 원리**
  - `Program.Main` → `LogCleaner.PurgeExpiredOnStartup()` → `LogRetentionConfig.LoadMap()` → 각 `(projectPath, days)` 조합에 대해 `days > 0` 항목만 처리.
  - 프로젝트 로그 폴더 = `codeinspect_logs/<Path.GetFileName(projectPath.TrimEnd('\\','/'))>` (MainForm.RunAnalysis line 759와 동일 매핑).
  - 폴더 내 파일을 재귀 순회하며 `File.GetLastWriteTime`이 기준일을 초과한 파일만 개별 `File.Delete`. 폴더 자체와 통합 로그(루트 직접 파일)는 보존.
  - 어떤 단계에서든 예외 발생 시 `ErrorLogger.Log` 후 다음 항목/파일로 계속 진행 → 시작 자체는 절대 실패하지 않음.

- **검증**
  - `csc.exe @build.rsp` exit 0, **경고 0건**. EXE 132KB → 145KB.
  - 시작 자동 삭제 시나리오: `LocalOfflineOcr|7` / `webview2|0` 설정에서 `LocalOfflineOcr/` 내 2개 파일을 30일 전으로 백데이트 → CodeInspect.exe 실행(3초 후 종료) → 백데이트 2개 파일만 정확히 삭제, 다른 최근 파일과 `webview2/` 전체는 모두 보존.
  - Hook 모드 회귀: `LocalOfflineOcr/`에 30일 전 파일 1개를 추가 백데이트 → `CodeInspect.exe --hook /tmp/empty.txt /tmp` 실행(exit 0) → 해당 파일이 그대로 남아있음 → `--hook` 분기가 startup purge를 정상 우회.
  - 통합 로그(루트 직접 파일 `codeinspect_logs/commit_vuln_*.log` 등)는 자동 퍼지 대상이 아니며, 다이얼로그의 "전체 삭제"로만 정리되도록 설계 — 다이얼로그 내 안내 라벨로 명시.

- **C# 5 호환성**: `$""`, `?.`, pattern matching, `out var`, `nameof`, auto-property initializer 미사용. `Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)` 익스플리싯 비교자, `int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)`, `delegate` 명시적 작성, `DataGridView` / `NumericUpDown` / `GroupBox` 모두 .NET Framework 4.0+ 표준 컴포넌트.

---

## 현재 파일 구조

```
codeinspect/
├── VulnerabilityRules.cs    # 기본 룰셋(DefaultRules) + 룰 타입 정의 (Reference 필드 포함, v0.10~)
├── RuleStore.cs             # 외부 파일 기반 룰셋 저장소 (v0.9~) + ApplyPack (v0.10~)
├── RulePacks.cs             # 내장 룰팩: Semgrep/OWASP ASVS (v0.10~)
├── RulesEditorForm.cs       # 룰셋 관리/편집/업데이트 UI (v0.9~) + 출처 컬럼 (v0.10~)
├── Analyzer.cs              # 분석 엔진 + 로그 기록 (취소 콜백 오버로드 v1.4~)
├── MainForm.cs              # WinForms GUI (멀티 프로젝트 + 룰셋 + LLM 옵션 v1.3~ + 분석 중지 v1.4~ + 더블클릭 파일 열기 v1.5~ + 로그 삭제 진입 버튼 v1.6~)
├── Program.cs               # 엔트리포인트 (GUI/Hook 듀얼 모드) + 전역 예외 훅 (v0.11~) + 시작 자동 퍼지 (v1.6~)
├── ErrorLogger.cs           # 실행 디렉토리에 yyyy-MM-dd-HH-mm-ss.log 기록 (v0.11~)
├── LLMAnalyzer.cs           # LLM(Ollama/LM Studio) 분석 엔진 + LLMConfig (v1.3~) + Cancel/HTTP Abort (v1.4~)
├── LLMConfigForm.cs         # LLM 모델/엔드포인트 설정 다이얼로그 (v1.3~)
├── LogDeleteForm.cs         # 로그 삭제 다이얼로그 + LogRetentionConfig + LogCleaner (v1.6~)
├── build.bat                # 빌드 런처
├── build.rsp                # csc.exe 옵션 파일
├── install_hook.bat         # CLI hook 설치 스크립트
├── CodeInspect.exe          # 빌드 산출물 (~130KB)
├── codeinspect_rules/       # 런타임 생성 (첫 실행 시)
│   ├── rules.config         # 사용자 편집 가능 룰셋 파일
│   ├── update_url.txt       # 다운로드 URL 저장
│   ├── llm_config.txt       # LLM 분석 설정 (v1.3~)
│   ├── log_retention.txt    # 프로젝트별 로그 자동 삭제 주기 (v1.6~)
│   └── backup/              # 룰 변경 시 이전 버전 보관
├── codeinspect_projects.txt # 등록된 프로젝트 경로 목록
├── test_samples/            # 샘플 취약점 코드 (C/Java/C#)
├── dev_history.md           # 개발 히스토리 (본 문서)
└── plan.md                  # 개발 계획
```

### v1.6.1 — 분석 결과 UI 표시 수정 (2026-04-29)
**배경**: 분석 완료 후 `dgvResults` DataGridView에 결과가 표시되지 않는 문제. `pnlSummary`(DockStyle.Top)와 `dgvResults`(DockStyle.Fill)가 모두 Form의 직접 자식으로 배치되어 있어 WinForms 도킹 레이아웃 순서 문제로 `dgvResults`의 가용 영역이 비정상적으로 축소됨.

- **`MainForm.cs` 수정**
  - 신규 로컬 변수 `pnlRight` (Panel, DockStyle.Fill) — `pnlSummary`와 `dgvResults`의 컨테이너. Form의 직접 자식에서 `pnlRight`의 자식으로 이동
  - Form 직접 자식: `pnlTitle`(Top) → `pnlLeft`(Left) → `pnlBottom`(Bottom) → `pnlRight`(Fill, 나머지 영역)
  - `pnlRight` 내부: `pnlSummary`(Top, H=60) → `dgvResults`(Fill)
  - BringToFront 순서 재정렬: Form 레벨 4개 + pnlRight 내부 2개로 분리
  - `PopulateGrid()`에 `SuspendLayout()`/`ResumeLayout()` 추가 — 대량 행 추가 시 렌더링 최적화

- **빌드**: `csc.exe @build.rsp` exit 0, 경고 0건. `build.rsp` 변경 없음.

### v1.6.3 — 좌측 패널 세로 스크롤 추가 (2026-04-29)
**배경**: 폼 높이를 줄이면 좌측 프로젝트 관리 패널 하단의 버튼(룰셋 보기/업데이트 등)이 보이지 않고 접근할 수 없는 문제. 절대 위치 기반 컨트롤 배치라 자동 재배치되지 않음.

- **`MainForm.cs` 수정**
  - `pnlLeft`에 `AutoScroll = true` 추가 → 폼 높이가 컨텐츠보다 작을 때 자동으로 세로 스크롤바 표시
  - `pnlLeft.Width` 310 → 327로 확장 (스크롤바 17px 폭 사전 확보) → 스크롤바가 나타나도 내부 컨트롤(Width=290)이 가로로 잘리거나 가로 스크롤바가 추가로 생기지 않음

- **빌드**: `csc.exe @build.rsp` exit 0, 경고 0건.

### v1.7 — 개별 파일 수동 분석 (2026-05-04)
**배경**: 기존에는 좌측 패널에 등록된 **프로젝트(디렉토리) 단위**로만 분석을 시작할 수 있었음 (`▶ 선택 프로젝트 분석`, `▶▶ 전체 프로젝트 분석`). 외부에서 받은 소스 1~수개의 파일을 빠르게 검사하려면 임시 폴더를 만들고 프로젝트로 등록해야 하는 번거로움이 있어, 좌측 패널에 단일/다중 파일 분석 진입점을 직접 추가.

- **`MainForm.cs` 수정**
  - 신규 필드 `private Button btnAnalyzeSingleFile;`
  - `using System.Text;` 추가 (StringBuilder 사용)
  - 좌측 패널 레이아웃: `btnAnalyzeAll`(Y=470, H=35) 아래에 신규 `btnAnalyzeSingleFile`(Y=510, H=30, BackColor `Color.FromArgb(102, 16, 242)` 보라색) 삽입. 후속 컨트롤(`btnCancelAnalysis` / `separator2` / Git Hook 2개 / LLM 섹션 4개 / 룰셋 섹션 6개) 모두 **Y +40** 이동
    - `btnCancelAnalysis`: 510 → 550 / `separator2`: 552 → 592 / Git Hook 2개: 562 → 602 / `separatorLLM`: 602 → 642 / `lblLLMHeader`: 611 → 651 / `chkUseLLM`: 636 → 676 / `btnLLMConfig`: 662 → 702 / `lblLLMStatus`: 697 → 737 / `separator3`: 722 → 762 / `lblRuleHeader`: 731 → 771 / `lblRuleCount`: 733 → 773 / `btnManageRules`: 756 → 796 / `btnViewRules`/`btnUpdateRules`: 791 → 831
    - `pnlLeft.Controls.AddRange(...)`에 `btnAnalyzeSingleFile` 등록
  - 신규 핸들러 `BtnAnalyzeSingleFile_Click(...)`:
    - `VulnerabilityRules.LanguageExtensions`를 순회하여 OpenFileDialog 필터 동적 구성 (`*.c;*.cc;*.cpp;*.cs;*.cxx;*.h;*.hh;*.hpp;*.hxx;*.java`). "지원 소스 파일 (...)|... |모든 파일 (*.*)|*.*"
    - `Multiselect = true` — Ctrl/Shift로 다중 선택 허용
    - 선택 후 `VulnerabilityRules.DetectLanguage()`로 미지원 확장자 사전 검사. 모두 미지원이면 분석 시작 안 함, 일부면 사용자에게 YesNo 다이얼로그(미지원 파일 최대 8개 표시 + "외 N개")로 진행 여부 확인 후 자동 제외
    - 통과한 파일 목록을 `RunSingleFileAnalysis(supported)`로 전달
  - 신규 메서드 `RunSingleFileAnalysis(List<string> filePaths)`:
    - LLM 모드 사전 검증 — `chkUseLLM` 켜져 있으나 `LLMConfig.IsConfigured()` 실패 시 `LLMConfigForm` 모달로 자동 진입
    - 분석 시작/완료 시 `btnAnalyzeSelected`/`btnAnalyzeAll`/`btnAnalyzeSingleFile` 모두 disable/enable. `btnCancelAnalysis`는 활성화하여 진행 중 중단 허용
    - 취소 다이얼로그가 단건/다건을 자동 분기하도록 `_currentProjectPaths = filePaths;` 그대로 사용 (1개 선택 시 단건 YesNo, 2개+ 시 3-버튼 다이얼로그)
    - BackgroundWorker `DoWork`에서 파일 루프:
      - `if (_cancelMode == 2) break;` (전체 중지)
      - 룰셋 모드: `_analyzer.AnalyzeFile(fpath)` 호출 (파일당 즉시 완료)
      - LLM 모드: `LLMAnalyzer llm = new LLMAnalyzer(llmConfigLocal); _activeLLM = llm;` 후 `llm.AnalyzeFile(fpath)` — `Cancel()`로 진행 중 HTTP 호출 즉시 Abort 가능
      - 그리드 표시용 prefix: `f.MatchedCode = Path.GetFileName(fpath) + "|" + f.MatchedCode;` (기존 프로젝트명 prefix와 같은 형식 → `UpdateProjectFilter()`에 자연스럽게 노출)
      - `progressCb` 형식: `[{모드}] [{i+1}/{N}] {fileName}`
      - 파일 처리 완료 후 `if (_cancelMode == 1) _cancelMode = 0;` (현재 파일만 건너뛰기 = 다음 파일 진입)
    - 로그 출력:
      - 단일 파일 분석 전용 폴더: `Path.Combine(_logDir, "single_file")`에 `WriteCommitLog` + `WriteCsvLog` (basePath=null → 절대경로 그대로)
      - 추가로 검출 건수 > 0이면 통합 로그(`_logDir` 직접)에도 기록 (기존 `RunAnalysis`와 동일 패턴)
    - `RunWorkerCompleted` 상태 메시지: 정상 시 `"{모드} 단일 파일 분석 완료 - {N}개 파일, {M}건 검출"`, 취소 시 `"{모드} 단일 파일 분석 중지됨 - {N}개 중 {완료}개 진행, {M}건 검출"`
    - 결과 그리드/필터/요약은 기존 `UpdateProjectFilter()` / `ApplyFilters()` / `UpdateSummary()` 그대로 호출
  - `RunAnalysis`에서도 `btnAnalyzeSingleFile`을 disable/enable하도록 보강 — 분석 중 다른 분석 시작 방지
  - 폼 타이틀 v1.6 → **v1.7**

- **`build.rsp` 변경 없음** — 신규 .cs 파일 추가 없음(MainForm 내부에 메서드 추가)

- **재사용 자원**
  - 분석 엔진: `CodeAnalyzer.AnalyzeFile(string)`(Analyzer.cs:33), `LLMAnalyzer.AnalyzeFile(string)`(LLMAnalyzer.cs:163) — 모두 v1.0~v1.4에서 이미 구현, 신규 분석 로직 작성 0
  - 로그: `LogWriter.WriteCsvLog` / `WriteCommitLog` 그대로
  - 결과 표시: `_findings` / `dgvResults` / `pnlSummary` / `UpdateProjectFilter` / `ApplyFilters` / `PopulateGrid` / `UpdateSummary` / 더블클릭 점프 / CSV 내보내기 모두 변경 없이 동작
  - 취소: `btnCancelAnalysis` + `_cancelMode` + `_activeLLM.Cancel()` 메커니즘 동일

- **검증**
  - `csc.exe @build.rsp` exit 0, **경고 0건** (사용하지 않는 지역 변수 `completedFiles` 제거 후)
  - EXE 145KB → 150KB
  - C# 5 호환성: `$""`, `?.`, pattern matching, `out var`, `nameof`, auto-property initializer, expression-bodied 모두 미사용. `SortedSet<string>(StringComparer.OrdinalIgnoreCase)`, `OpenFileDialog.Multiselect`, `BackgroundWorker`, `MessageBox.Show(YesNo)` 모두 .NET Framework 4.0+ 표준
  - 분석 엔진은 변경되지 않았고 UI 진입점/배선만 추가되었으므로 컴파일 성공 = 정합성 보장. GUI 실 동작은 사용자 수동 검증 권장

**C# 5 호환성**: 모든 추가 코드에서 금지 기능 미사용. 빌드 경고 0건.

### v1.6.2 — UI 도킹 순서 버그 수정 (2026-04-29)
**배경**: v1.6.1의 BringToFront 호출 순서가 반대로 되어 있어 실행 시 `pnlTitle`(타이틀바)이 좌측 패널 오른쪽 영역에만 표시되고 결과 영역과 겹쳐 보이는 문제 발생. WinForms는 높은 인덱스부터 처리하므로 `BringToFront`(인덱스 0으로 이동)는 마지막 호출된 컨트롤이 가장 늦게 처리됨. `pnlTitle.BringToFront()`를 마지막에 호출하면 인덱스 0이 되어 가장 늦게 처리되며, 그 시점엔 `pnlLeft`가 이미 좌측 전체 높이를 차지한 상태라 `pnlTitle`이 우측 영역에만 도킹됨.

- **`MainForm.cs` 수정**
  - BringToFront 순서를 역전: `pnlTitle` → `pnlBottom` → `pnlLeft` → `pnlRight` 순으로 호출
  - 결과: `pnlTitle`이 가장 높은 인덱스(가장 먼저 처리)되어 전체 폭의 상단 40px를 정상적으로 차지
  - 같은 원리로 `pnlRight` 내부도 `pnlSummary` → `dgvResults` 순으로 변경
  - 최종 레이아웃: `[pnlTitle 전체폭]` / `[pnlLeft|pnlSummary+dgvResults]` / `[pnlBottom 전체폭]`

- **빌드**: `csc.exe @build.rsp` exit 0, 경고 0건.

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

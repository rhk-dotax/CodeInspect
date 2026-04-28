# CodeInspect 개발 계획

## 개발 원칙 (모든 작업에 적용)

### 1. 환경 제약
- **폐쇄망/오프라인**: 외부 패키지 매니저(NuGet, pip, npm) 사용 금지
- **무설치**: Windows 내장 `.NET Framework` + `csc.exe`만 사용
- **추가 런타임 금지**: Python, Node.js, .NET SDK 등 별도 런타임 설치 불가

### 2. 코드 제약 (C# 5 호환 필수)
csc.exe v4.8(Framework 4.0.30319)은 C# 5까지만 지원하므로 아래 기능 사용 금지:

| 금지 기능 | 사용 금지 예시 | 대체 방법 |
|---|---|---|
| Auto-property initializer | `public int X { get; set; } = 5;` | 생성자에서 초기화 |
| Null-conditional (`?.`) | `obj?.Method()` | `(obj != null) ? obj.Method() : ...` |
| String interpolation (`$""`) | `$"{name} is {age}"` | `string.Format(...)` 또는 `+` 연결 |
| Expression-bodied members | `int M() => x + 1;` | `int M() { return x + 1; }` |
| `nameof` | `nameof(x)` | `"x"` 문자열 리터럴 |
| Pattern matching | `if (obj is Foo f)` | `var f = obj as Foo; if (f != null)` |
| `out var` | `int.TryParse(s, out var n)` | `int n; int.TryParse(s, out n);` |
| Tuple literal | `(int, int) t = (1, 2);` | `Tuple.Create(1, 2)` |

### 3. 빌드 체계
- 소스 추가/수정 시 반드시 `build.rsp`에 파일명 추가 (이미 등록되어 있으면 생략)
- 빌드 확인: `C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe @build.rsp`
- 빌드 실패 시 에러 메시지 기준으로 C# 5 호환성 수정 후 재빌드

### 4. 인코딩
- `.bat` 파일: 영문 메시지만 사용 (한글은 cmd.exe 기본 CP949에서 깨짐)
- `.cs` 파일: UTF-8 (소스/문자열 리터럴은 한글 가능)
- 로그 파일:
  - `.log`(텍스트): UTF-8 (BOM 없음)
  - `.csv`: UTF-8 with BOM (Excel 한글 호환)

### 5. 문서화 규칙
- **모든 기능 변경 시 `dev_history.md`에 이력 추가** (버전/변경 내용/수정 이유)
- **새 기능 계획은 `plan.md`에 먼저 기록** 후 구현
- 사용자 요구사항 변경이 있으면 이 `plan.md`를 먼저 업데이트

### 6. 검증 절차 (필수)
기능 추가/수정 후 반드시 아래 순서로 검증:
1. `csc.exe @build.rsp`로 빌드 성공 확인
2. 실제 테스트 케이스(test_samples 또는 임시 Git 저장소)로 end-to-end 검증
3. 검증용 임시 파일/폴더는 작업 완료 후 삭제

---

## 완료된 기능 (v1.6 기준)

- [x] GUI 소스 경로 선택 + C/C++/Java/C# 분석
- [x] Git pre-commit hook 설치 (커밋 시 분석 + 로그 기록 + 커밋 진행)
- [x] 로그 형식: 일자 / 검출파일 / 검출라인 / 검출사유
- [x] 다중 프로젝트 동시 관리 (A/B/C 프로젝트 일괄 분석)
- [x] Git hook 제거 기능 (백업 포함)
- [x] 분석 룰셋 외부 파일화 (`codeinspect_rules/rules.config`) — v0.9
- [x] 사용자 정의 규칙 추가/수정/삭제 UI (리스트 기반) — v0.9
- [x] 룰셋 보기(notepad) 및 직접 편집 지원 — v0.9
- [x] 룰셋 URL 다운로드 + 내장 기본 CWE 복원 + 병합/교체 — v0.9
- [x] 룰셋 변경 시 이전 버전 자동 백업 (`backup/rules_yyyyMMdd_HHmmss.config`) — v0.9
- [x] 레거시 `rules.txt` → `rules.config` 자동 마이그레이션 — v0.9.2
- [x] 다운로드 URL 사전 검증 및 HTTP 상태코드/네트워크 오류 한글 메시지 — v0.9.2
- [x] Semgrep 공개 룰팩 / OWASP ASVS v4.0.3 룰팩 / 통합 룰팩 내장 (오프라인 적용) — v0.10
- [x] 각 룰에 출처(Reference) 필드 추가 (semgrep:<id>, OWASP ASVS Vx.y.z) — v0.10
- [x] 룰셋 관리 UI에 "출처" 컬럼 표시, 편집 다이얼로그에 출처 입력란 추가 — v0.10
- [x] 전역 예외 로깅 — 처리되지 않은 예외를 실행 디렉토리의 `yyyy-MM-dd-HH-mm-ss.log`에 자동 기록, 발생 위치(파일/라인) 포함 — v0.11
- [x] **LLM 기반 분석 옵션** — 좌측 패널에 LLM 분석 섹션(체크박스 + 설정 버튼 + 상태 라벨) 추가, Ollama / LM Studio 로컬 엔드포인트 연동, 모델 자동 조회, 큰 파일(>50KB) SKIP 처리, LLM 모드 시 룰셋 분석 비활성화. 결과는 기존 `Finding` 구조로 매핑되어 DataGridView/CSV/심각도 요약/필터를 그대로 재사용 — v1.3
- [x] **분석 중지 기능** — 좌측 패널에 "■ 분석 중지" 버튼 추가, 다중 프로젝트 분석 중에는 "현재 프로젝트만 건너뛰기 / 전체 분석 중지 / 계속 분석" 3-버튼 다이얼로그로 선택. 룰셋 모드는 파일 루프 진입부 취소 체크로 즉시 멈춤. LLM 모드는 진행 중 `HttpWebRequest.Abort()`로 HTTP 호출 즉시 인터럽트. 부분 결과(중간까지 검출된 Finding) 보존 — v1.4
- [x] **검출 결과 더블클릭 → 파일/라인 점프** — DataGridView 행을 더블클릭하면 외부 편집기에서 해당 라인을 연다. 자동 fallback: VS Code(`code -g`) → Notepad++(`-n<line>`) → 기본 메모장. 그리드 툴팁으로 사용 안내 — v1.5
- [x] **로그 삭제 / 보존 주기** — 메인 폼 우측 상단 "🗑 로그 삭제" 버튼 → 다이얼로그에서 ①전체 삭제(`codeinspect_logs/` 내부 모든 파일+하위 폴더) ②선택 프로젝트 로그 삭제 ③프로젝트별 자동 삭제 주기(일, 0=미삭제) ④일괄 적용. 시작 시 만료 파일 자동 퍼지(GUI 모드만, `--hook` 모드는 우회). 설정은 `codeinspect_rules/log_retention.txt`에 영속 — v1.6

---

## 향후 개선 계획 (우선순위순)

### P1 — 사용성 개선
- [x] 검출 결과에서 더블클릭 시 해당 파일/라인으로 이동 (VS Code / Notepad++ / 메모장 자동 fallback) — v1.5
- [ ] 프로젝트별 "제외 경로" 설정 (예: `third_party/`, `generated/`)
- [ ] 규칙 활성/비활성 토글 UI (체크박스로 삭제 없이 OFF)
- [ ] 룰셋 관리 UI에서 백업 파일 목록 보기 및 "이 버전으로 복원"
- [ ] 분석 중지 후 부분 분석 로그 파일에 "INTERRUPTED" 마커 기록 (현재는 부분 결과만 저장됨)
- [ ] 분석 중지 후 부분 결과를 기준으로 즉시 CSV/HTML 내보내기

### P2 — 분석 정확도
- [ ] 주석 내 매칭 제외 (현재 `//`, `/* */` 안의 코드도 검출됨)
- [ ] 문자열 리터럴 내 매칭 제외 옵션
- [ ] 규칙별 라인 단위 `// NOSONAR` 스타일 억제 주석 지원

### P3 — 보고서
- [ ] HTML 리포트 생성 (심각도별 색상, 파일별 그룹핑)
- [ ] 이전 분석 결과와 비교 (신규 검출 / 해결된 검출)
- [ ] 분석 히스토리 목록 뷰

### P4 — 확장
- [ ] Python / JavaScript 언어 지원 (룰셋 포맷은 그대로, `LanguageExtensions`에 매핑 추가)
- [ ] 룰셋 파일 import/export 포맷을 JSON으로 선택 지원 (현재 INI 스타일)
- [ ] 공식 CWE 룰셋 배포 저장소 구축 (업데이트 기본 URL 제공)

### P5 — 통합
- [ ] Jenkins/GitLab CI에서 CLI 모드로 실행 가능한 옵션 추가 (`--scan [path] --output [file]`)
- [ ] SARIF 포맷 출력 (GitHub Code Scanning 호환)

---

## 변경 요청 처리 흐름

사용자로부터 새로운 요구사항을 받으면:

1. **요구사항 파악** — 정확히 무엇을 원하는지 명확화 (필요시 질문)
2. **plan.md 업데이트** — 해당 항목을 "향후 개선 계획"에서 "진행 중"으로 이동 또는 신규 항목 추가
3. **구현** — 위 개발 원칙(C# 5 호환, 폐쇄망 등) 준수
4. **빌드 + 검증** — csc.exe 빌드 + 실제 시나리오 테스트
5. **dev_history.md 추가** — 새 버전 태그로 변경 내역 기록
6. **완료 보고** — 변경 요약을 사용자에게 간결하게 전달

---

## 참조 파일 관계도

```
사용자 요구사항
    ↓
plan.md 업데이트 ── (계획 수립)
    ↓
소스 수정
    ↓
build.rsp 갱신 (파일 추가 시)
    ↓
csc.exe @build.rsp (빌드)
    ↓
test_samples/ 또는 임시 Git repo로 검증
    ↓
dev_history.md 기록 (이력 보존)
    ↓
사용자에게 보고
```

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace CodeInspect
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // 전역 예외 핸들러 등록: 처리되지 않은 모든 예외를
            // 실행 디렉토리의 yyyy-MM-dd-HH-mm-ss.log 파일로 기록
            ErrorLogger.Install();

            try
            {
                // --hook 모드: git pre-commit hook에서 호출
                if (args.Length >= 2 && args[0] == "--hook")
                {
                    return RunHookMode(args);
                }

                // 시작 시 만료된 로그 자동 삭제 (실패해도 앱 시작은 계속)
                try { LogCleaner.PurgeExpiredOnStartup(); }
                catch (Exception exPurge) { ErrorLogger.Log(exPurge, "Program.Main / PurgeExpiredOnStartup"); }

                // GUI 모드
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "Program.Main (최상위)");
                return 1;
            }
        }

        /// <summary>
        /// Hook 모드: 스테이징된 파일 분석 후 로그 기록
        /// 사용법: CodeInspect.exe --hook [파일목록경로] [저장소루트경로]
        /// 항상 exit 0 (커밋 차단 없음)
        /// </summary>
        static int RunHookMode(string[] args)
        {
            try
            {
                string fileListPath = args[1];
                string repoRoot = args.Length >= 3 ? args[2] : Directory.GetCurrentDirectory();

                if (!File.Exists(fileListPath))
                {
                    Console.Error.WriteLine("[CodeInspect] 파일 목록을 읽을 수 없습니다: " + fileListPath);
                    return 0; // 커밋은 진행
                }

                // 파일 목록 읽기
                string[] lines = File.ReadAllLines(fileListPath);
                var fileList = new List<string>();
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // 상대 경로 → 절대 경로
                    string fullPath = Path.IsPathRooted(trimmed)
                        ? trimmed
                        : Path.Combine(repoRoot, trimmed);

                    if (File.Exists(fullPath))
                        fileList.Add(fullPath);
                }

                if (fileList.Count == 0)
                {
                    Console.WriteLine("[CodeInspect] 분석 대상 파일이 없습니다.");
                    return 0;
                }

                Console.WriteLine("[CodeInspect] " + fileList.Count + "개 파일 분석 중...");

                var analyzer = new CodeAnalyzer();
                var findings = analyzer.AnalyzeFiles(fileList);

                // 로그 디렉토리: 저장소 루트 하위
                string logDir = Path.Combine(repoRoot, "codeinspect_logs");
                string logFile = LogWriter.WriteCommitLog(findings, logDir, repoRoot);

                if (findings.Count > 0)
                {
                    // 요약 통계
                    var summary = AnalysisSummary.Create(findings);
                    Console.WriteLine("[CodeInspect] ──────────────────────────────────");
                    Console.WriteLine("[CodeInspect] 취약점 검출: " + findings.Count + "건");
                    Console.WriteLine("[CodeInspect]   CRITICAL: " + summary.Critical +
                                      "  HIGH: " + summary.High +
                                      "  MEDIUM: " + summary.Medium +
                                      "  LOW: " + summary.Low);
                    Console.WriteLine("[CodeInspect] 로그 저장: " + logFile);
                    Console.WriteLine("[CodeInspect] ──────────────────────────────────");

                    // CSV 로그도 함께 생성
                    LogWriter.WriteCsvLog(findings, logDir, repoRoot);

                    return 1; // 취약점 검출됨 (하지만 hook에서 exit 0으로 처리)
                }
                else
                {
                    Console.WriteLine("[CodeInspect] 취약점이 검출되지 않았습니다.");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "Program.RunHookMode");
                Console.Error.WriteLine("[CodeInspect] 분석 중 오류: " + ex.Message);
                return 0; // 오류가 발생해도 커밋은 진행
            }
        }
    }
}

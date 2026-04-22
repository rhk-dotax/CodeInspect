using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace CodeInspect
{
    /// <summary>
    /// 예외 상황을 실행 디렉토리에 yyyy-MM-dd-HH-mm-ss.log 형태로 기록.
    /// 전역 핸들러(Application.ThreadException / AppDomain.UnhandledException)와
    /// 개별 catch 블록에서 공통으로 사용한다.
    /// </summary>
    public static class ErrorLogger
    {
        private static readonly object _sync = new object();

        /// <summary>전역 예외 핸들러 등록. Program.Main 진입 시 1회 호출.</summary>
        public static void Install()
        {
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;
            System.Windows.Forms.Application.ThreadException += OnWinFormsThreadException;

            try
            {
                System.Windows.Forms.Application.SetUnhandledExceptionMode(
                    System.Windows.Forms.UnhandledExceptionMode.CatchException);
            }
            catch { }
        }

        /// <summary>예외를 로그 파일에 기록. 로그 기록 자체가 실패해도 예외를 재발생시키지 않는다.</summary>
        public static void Log(Exception ex, string context)
        {
            if (ex == null) return;
            try
            {
                string dir = GetLogDir();
                string fileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture) + ".log";
                string path = Path.Combine(dir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("════════════════════════════════════════");
                sb.AppendLine("[시각]     " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                sb.AppendLine("[컨텍스트] " + (string.IsNullOrEmpty(context) ? "(없음)" : context));
                sb.AppendLine("[스레드]   " + Thread.CurrentThread.ManagedThreadId);
                sb.AppendLine("[예외형식] " + ex.GetType().FullName);
                sb.AppendLine("[메시지]   " + ex.Message);
                sb.AppendLine("[발생위치]");
                sb.AppendLine(FormatLocation(ex));
                sb.AppendLine("[스택트레이스]");
                sb.AppendLine(ex.StackTrace ?? "(스택 없음)");

                Exception inner = ex.InnerException;
                int depth = 1;
                while (inner != null)
                {
                    sb.AppendLine("---- Inner Exception #" + depth + " ----");
                    sb.AppendLine("[예외형식] " + inner.GetType().FullName);
                    sb.AppendLine("[메시지]   " + inner.Message);
                    sb.AppendLine(inner.StackTrace ?? "(스택 없음)");
                    inner = inner.InnerException;
                    depth++;
                }

                sb.AppendLine();

                lock (_sync)
                {
                    File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
                }
            }
            catch
            {
                // 로그 기록 실패는 무시 (재귀 예외 방지)
            }
        }

        private static string GetLogDir()
        {
            // 1순위: 실행 파일 위치 (Application.StartupPath 와 동일)
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
                    return baseDir;
            }
            catch { }

            // 2순위: 현재 작업 디렉토리
            return Directory.GetCurrentDirectory();
        }

        /// <summary>스택 최상단 프레임에서 파일/라인/메서드를 추출.</summary>
        private static string FormatLocation(Exception ex)
        {
            try
            {
                var trace = new System.Diagnostics.StackTrace(ex, true);
                if (trace.FrameCount == 0) return "  (프레임 정보 없음)";

                var frame = trace.GetFrame(0);
                var method = frame.GetMethod();
                string file = frame.GetFileName();
                int line = frame.GetFileLineNumber();
                int col = frame.GetFileColumnNumber();

                string declaring = (method != null && method.DeclaringType != null)
                    ? method.DeclaringType.FullName : "?";
                string methodName = (method != null) ? method.Name : "?";

                string loc = "  " + declaring + "." + methodName + "()";
                if (!string.IsNullOrEmpty(file))
                    loc += Environment.NewLine + "  at " + file + ":line " + line + (col > 0 ? (":col " + col) : "");
                return loc;
            }
            catch
            {
                return "  (위치 추출 실패)";
            }
        }

        private static void OnAppDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Log(ex, "AppDomain.UnhandledException (IsTerminating=" + e.IsTerminating + ")");
        }

        private static void OnWinFormsThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log(e.Exception, "Application.ThreadException (WinForms UI 스레드)");
        }
    }
}

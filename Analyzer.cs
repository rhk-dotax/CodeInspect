using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeInspect
{
    /// <summary>소스코드 취약점 정적 분석 엔진</summary>
    public class CodeAnalyzer
    {
        private readonly List<VulnerabilityRule> _rules;
        private readonly Dictionary<string, Regex> _compiled;

        // 제외할 디렉토리
        private static readonly HashSet<string> ExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".svn", ".hg", "bin", "obj", "out", "build", "target",
            "node_modules", "__pycache__", ".vs", ".idea", "packages", "Debug", "Release"
        };

        public CodeAnalyzer()
        {
            _rules = VulnerabilityRules.Rules;
            _compiled = new Dictionary<string, Regex>();
            foreach (var rule in _rules)
            {
                _compiled[rule.RuleId] = new Regex(rule.Pattern, rule.Options | RegexOptions.Compiled);
            }
        }

        /// <summary>단일 파일 분석</summary>
        public List<Finding> AnalyzeFile(string filePath)
        {
            var findings = new List<Finding>();
            string lang = VulnerabilityRules.DetectLanguage(filePath);
            if (string.IsNullOrEmpty(lang)) return findings;

            string content;
            try
            {
                content = File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch
            {
                return findings;
            }

            string[] lines = content.Split(new[] { '\n' }, StringSplitOptions.None);

            foreach (var rule in _rules)
            {
                bool langMatch = false;
                foreach (var l in rule.Languages)
                {
                    if (l == lang) { langMatch = true; break; }
                }
                if (!langMatch) continue;

                Regex regex = _compiled[rule.RuleId];
                MatchCollection matches = regex.Matches(content);

                foreach (Match match in matches)
                {
                    // 매치 위치 → 라인 번호 계산
                    int lineNum = 1;
                    for (int i = 0; i < match.Index && i < content.Length; i++)
                    {
                        if (content[i] == '\n') lineNum++;
                    }

                    string matchedLine = (lineNum <= lines.Length) ? lines[lineNum - 1].Trim() : "";
                    if (matchedLine.Length > 200) matchedLine = matchedLine.Substring(0, 200);

                    findings.Add(new Finding
                    {
                        FilePath = filePath,
                        LineNumber = lineNum,
                        RuleId = rule.RuleId,
                        Severity = rule.Severity,
                        Category = rule.Category,
                        Reason = rule.Description,
                        MatchedCode = matchedLine
                    });
                }
            }

            return findings;
        }

        /// <summary>디렉토리 전체 분석</summary>
        public List<Finding> AnalyzeDirectory(string directory, Action<int, int, string, int> progress = null)
        {
            return AnalyzeDirectory(directory, progress, null);
        }

        /// <summary>디렉토리 전체 분석 (취소 콜백 지원)</summary>
        public List<Finding> AnalyzeDirectory(string directory, Action<int, int, string, int> progress, Func<bool> isCancelRequested)
        {
            var allFindings = new List<Finding>();
            var targetExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in VulnerabilityRules.LanguageExtensions)
            {
                foreach (var ext in kv.Value) targetExts.Add(ext);
            }

            var files = CollectFiles(directory, targetExts);
            int total = files.Count;

            for (int i = 0; i < total; i++)
            {
                if (isCancelRequested != null && isCancelRequested()) break;
                var findings = AnalyzeFile(files[i]);
                allFindings.AddRange(findings);
                if (progress != null) progress(i + 1, total, files[i], findings.Count);
            }

            return allFindings;
        }

        /// <summary>파일 목록 분석 (git hook용)</summary>
        public List<Finding> AnalyzeFiles(List<string> fileList, Action<int, int, string, int> progress = null)
        {
            var allFindings = new List<Finding>();
            int total = fileList.Count;

            for (int i = 0; i < total; i++)
            {
                if (!File.Exists(fileList[i])) continue;
                var findings = AnalyzeFile(fileList[i]);
                allFindings.AddRange(findings);
                if (progress != null) progress(i + 1, total, fileList[i], findings.Count);
            }

            return allFindings;
        }

        /// <summary>대상 파일 수집</summary>
        private List<string> CollectFiles(string directory, HashSet<string> extensions)
        {
            var result = new List<string>();
            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (extensions.Contains(ext)) result.Add(file);
                }

                foreach (var dir in Directory.GetDirectories(directory))
                {
                    string dirName = Path.GetFileName(dir);
                    if (ExcludedDirs.Contains(dirName)) continue;
                    if (dirName.StartsWith(".")) continue;
                    result.AddRange(CollectFiles(dir, extensions));
                }
            }
            catch { /* 접근 권한 없는 디렉토리 무시 */ }

            return result;
        }
    }

    /// <summary>로그 기록 유틸리티</summary>
    public static class LogWriter
    {
        /// <summary>CSV 로그 파일 생성 (GUI 분석용)</summary>
        public static string WriteCsvLog(List<Finding> findings, string logDir, string basePath = null)
        {
            Directory.CreateDirectory(logDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFile = Path.Combine(logDir, "vuln_report_" + timestamp + ".csv");

            using (var sw = new StreamWriter(logFile, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("일자,심각도,규칙ID,검출파일,검출라인,CWE분류,검출사유,검출코드");
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                foreach (var f in findings)
                {
                    string fileDisplay = f.FilePath;
                    if (!string.IsNullOrEmpty(basePath))
                    {
                        try { fileDisplay = GetRelativePath(basePath, f.FilePath); }
                        catch { }
                    }

                    sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                        CsvEscape(now),
                        CsvEscape(f.Severity),
                        CsvEscape(f.RuleId),
                        CsvEscape(fileDisplay),
                        f.LineNumber,
                        CsvEscape(f.Category),
                        CsvEscape(f.Reason),
                        CsvEscape(f.MatchedCode)
                    ));
                }
            }

            return logFile;
        }

        /// <summary>텍스트 로그 파일 생성 (git hook용)</summary>
        public static string WriteCommitLog(List<Finding> findings, string logDir, string basePath = null)
        {
            Directory.CreateDirectory(logDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFile = Path.Combine(logDir, "commit_vuln_" + timestamp + ".log");
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            using (var sw = new StreamWriter(logFile, false, Encoding.UTF8))
            {
                sw.WriteLine("=== CodeInspect Commit Vulnerability Report ===");
                sw.WriteLine("검사 일시: " + now);
                sw.WriteLine("검출 건수: " + findings.Count);
                sw.WriteLine(new string('=', 60));
                sw.WriteLine();

                if (findings.Count == 0)
                {
                    sw.WriteLine("취약점이 검출되지 않았습니다.");
                }
                else
                {
                    foreach (var f in findings)
                    {
                        string fileDisplay = f.FilePath;
                        if (!string.IsNullOrEmpty(basePath))
                        {
                            try { fileDisplay = GetRelativePath(basePath, f.FilePath); }
                            catch { }
                        }

                        sw.WriteLine("일자: " + now);
                        sw.WriteLine("검출파일: " + fileDisplay);
                        sw.WriteLine("검출라인: " + f.LineNumber);
                        sw.WriteLine("심각도: " + f.Severity);
                        sw.WriteLine("검출사유: [" + f.Category + "] " + f.Reason);
                        sw.WriteLine("코드: " + f.MatchedCode);
                        sw.WriteLine(new string('-', 60));
                    }
                }
            }

            return logFile;
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            if (!basePath.EndsWith("\\")) basePath += "\\";
            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', '\\'));
        }
    }

    /// <summary>분석 결과 요약</summary>
    public class AnalysisSummary
    {
        public int Total { get; set; }
        public int Critical { get; set; }
        public int High { get; set; }
        public int Medium { get; set; }
        public int Low { get; set; }
        public Dictionary<string, int> ByCategory { get; set; }
        public Dictionary<string, int> ByFile { get; set; }

        public static AnalysisSummary Create(List<Finding> findings)
        {
            var summary = new AnalysisSummary
            {
                Total = findings.Count,
                ByCategory = new Dictionary<string, int>(),
                ByFile = new Dictionary<string, int>()
            };

            foreach (var f in findings)
            {
                switch (f.Severity)
                {
                    case "CRITICAL": summary.Critical++; break;
                    case "HIGH": summary.High++; break;
                    case "MEDIUM": summary.Medium++; break;
                    case "LOW": summary.Low++; break;
                }

                if (!summary.ByCategory.ContainsKey(f.Category))
                    summary.ByCategory[f.Category] = 0;
                summary.ByCategory[f.Category]++;

                if (!summary.ByFile.ContainsKey(f.FilePath))
                    summary.ByFile[f.FilePath] = 0;
                summary.ByFile[f.FilePath]++;
            }

            return summary;
        }
    }
}

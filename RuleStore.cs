using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CodeInspect
{
    /// <summary>외부 파일 기반 취약점 규칙 저장소.
    /// notepad 등으로 직접 편집 가능한 INI 스타일 포맷 사용.
    /// 변경 시 자동으로 이전 버전을 backup 폴더에 타임스탬프로 보관.</summary>
    public static class RuleStore
    {
        public static string RulesDir
        {
            get { return Path.Combine(Application.StartupPath, "codeinspect_rules"); }
        }
        public static string RulesFile
        {
            get { return Path.Combine(RulesDir, "rules.config"); }
        }
        public static string BackupDir
        {
            get { return Path.Combine(RulesDir, "backup"); }
        }
        public static string UrlConfigFile
        {
            get { return Path.Combine(RulesDir, "update_url.txt"); }
        }

        /// <summary>기본 업데이트 URL은 공란 — 사용자가 자신의 사내 서버/저장소 주소를 입력하여 사용.
        /// 폐쇄망 환경을 기본 전제로 하므로 공개 URL을 강제하지 않음.</summary>
        public const string DefaultUpdateUrl = "";

        /// <summary>레거시 파일명 (v0.9 초기 버전 호환).</summary>
        private static string LegacyRulesFile
        {
            get { return Path.Combine(RulesDir, "rules.txt"); }
        }

        // ───────────────────────────────────────────────────────────
        //  로드 / 저장 / 백업
        // ───────────────────────────────────────────────────────────

        /// <summary>규칙 목록 로드. 파일이 없으면 기본 룰로 자동 시드.
        /// 레거시 rules.txt가 있으면 rules.config로 자동 마이그레이션.</summary>
        public static List<VulnerabilityRule> Load()
        {
            EnsureDirectories();

            // 레거시 파일 마이그레이션: rules.txt만 존재하는 경우 rules.config로 이동
            if (!File.Exists(RulesFile) && File.Exists(LegacyRulesFile))
            {
                try { File.Move(LegacyRulesFile, RulesFile); }
                catch
                {
                    try { File.Copy(LegacyRulesFile, RulesFile, false); }
                    catch { }
                }
            }

            if (!File.Exists(RulesFile))
            {
                WriteFile(VulnerabilityRules.DefaultRules);
            }
            try
            {
                return Parse(File.ReadAllText(RulesFile, Encoding.UTF8));
            }
            catch
            {
                return new List<VulnerabilityRule>(VulnerabilityRules.DefaultRules);
            }
        }

        /// <summary>현재 파일을 백업한 후 새 규칙 저장.</summary>
        public static void Save(List<VulnerabilityRule> rules)
        {
            EnsureDirectories();
            if (File.Exists(RulesFile)) Backup();
            WriteFile(rules);
        }

        private static void WriteFile(List<VulnerabilityRule> rules)
        {
            EnsureDirectories();
            File.WriteAllText(RulesFile, Serialize(rules), new UTF8Encoding(false));
        }

        /// <summary>현재 룰 파일을 backup 디렉토리에 타임스탬프로 복사.</summary>
        public static string Backup()
        {
            EnsureDirectories();
            if (!File.Exists(RulesFile)) return null;
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dest = Path.Combine(BackupDir, "rules_" + ts + ".config");
            // 동일 초에 여러 번 호출되는 경우 숫자 접미사로 중복 회피
            int n = 1;
            while (File.Exists(dest))
            {
                dest = Path.Combine(BackupDir,
                    "rules_" + ts + "_" + n.ToString() + ".config");
                n++;
            }
            File.Copy(RulesFile, dest, true);
            return dest;
        }

        public static void EnsureDirectories()
        {
            if (!Directory.Exists(RulesDir)) Directory.CreateDirectory(RulesDir);
            if (!Directory.Exists(BackupDir)) Directory.CreateDirectory(BackupDir);
        }

        // ───────────────────────────────────────────────────────────
        //  URL 설정
        // ───────────────────────────────────────────────────────────

        public static string GetUpdateUrl()
        {
            EnsureDirectories();
            try
            {
                if (File.Exists(UrlConfigFile))
                {
                    string s = File.ReadAllText(UrlConfigFile, Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            catch { }
            return DefaultUpdateUrl;
        }

        public static void SetUpdateUrl(string url)
        {
            EnsureDirectories();
            File.WriteAllText(UrlConfigFile, url ?? "", new UTF8Encoding(false));
        }

        // ───────────────────────────────────────────────────────────
        //  다운로드 / 복원
        // ───────────────────────────────────────────────────────────

        /// <summary>URL에서 룰셋 다운로드 후 적용. null=성공, string=오류 메시지.</summary>
        public static string DownloadAndApply(string url, bool merge, out int downloadedCount)
        {
            downloadedCount = 0;
            if (string.IsNullOrEmpty(url)) return "URL이 비어있습니다.";

            // URL 형식 검증
            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed)
                || (parsed.Scheme != "http" && parsed.Scheme != "https" && parsed.Scheme != "file"))
            {
                return "올바른 URL 형식이 아닙니다 (http:// 또는 https://로 시작).";
            }

            try
            {
                // TLS 1.2 활성화 (.NET Framework 4.0+에서 기본 비활성인 경우 대비)
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
                catch { }

                string content;
                using (var wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "CodeInspect/1.2");
                    content = wc.DownloadString(url);
                }

                var downloaded = Parse(content);
                if (downloaded == null || downloaded.Count == 0)
                    return "다운로드된 룰셋이 비어있거나 올바른 포맷이 아닙니다. ([RULE] 블록과 key=value 형식 확인 필요)";

                List<VulnerabilityRule> final;
                if (merge)
                {
                    final = Load();
                    var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < final.Count; i++)
                        if (!string.IsNullOrEmpty(final[i].RuleId))
                            idx[final[i].RuleId] = i;

                    foreach (var r in downloaded)
                    {
                        if (!string.IsNullOrEmpty(r.RuleId) && idx.ContainsKey(r.RuleId))
                            final[idx[r.RuleId]] = r;
                        else
                            final.Add(r);
                    }
                }
                else
                {
                    final = downloaded;
                }

                Save(final);
                downloadedCount = downloaded.Count;
                SetUpdateUrl(url);
                return null;
            }
            catch (WebException wex)
            {
                var http = wex.Response as HttpWebResponse;
                if (http != null)
                {
                    int code = (int)http.StatusCode;
                    return string.Format("HTTP {0} {1} — URL에서 룰셋 파일을 찾을 수 없습니다.\n경로와 파일명을 확인하세요.",
                        code, http.StatusCode);
                }
                if (wex.Status == WebExceptionStatus.NameResolutionFailure)
                    return "도메인을 확인할 수 없습니다 (DNS 실패). URL 주소를 확인하세요.";
                if (wex.Status == WebExceptionStatus.ConnectFailure)
                    return "서버에 연결할 수 없습니다. 네트워크/방화벽을 확인하세요.";
                if (wex.Status == WebExceptionStatus.Timeout)
                    return "응답 시간이 초과되었습니다.";
                if (wex.Status == WebExceptionStatus.TrustFailure
                    || wex.Status == WebExceptionStatus.SecureChannelFailure)
                    return "SSL/TLS 연결 실패. https 인증서를 확인하세요.";
                return "네트워크 오류: " + wex.Message;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>내장 기본 CWE 룰셋으로 복원 (현재 파일은 백업됨).</summary>
        public static void RestoreDefaults()
        {
            Save(new List<VulnerabilityRule>(VulnerabilityRules.DefaultRules));
        }

        /// <summary>내장 룰팩(Semgrep/OWASP ASVS 등) 적용. 현재 파일은 백업됨.
        /// merge=true면 기존 룰과 병합(RuleId 중복은 팩 버전으로 덮어쓰기), false면 전체 교체.</summary>
        public static int ApplyPack(List<VulnerabilityRule> pack, bool merge)
        {
            if (pack == null || pack.Count == 0) return 0;

            List<VulnerabilityRule> final;
            if (merge)
            {
                final = Load();
                var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < final.Count; i++)
                    if (!string.IsNullOrEmpty(final[i].RuleId))
                        idx[final[i].RuleId] = i;

                foreach (var r in pack)
                {
                    if (!string.IsNullOrEmpty(r.RuleId) && idx.ContainsKey(r.RuleId))
                        final[idx[r.RuleId]] = r;
                    else
                        final.Add(r);
                }
            }
            else
            {
                final = new List<VulnerabilityRule>(pack);
            }

            Save(final);
            return pack.Count;
        }

        // ───────────────────────────────────────────────────────────
        //  직렬화 / 역직렬화 (INI 스타일, notepad 편집 가능)
        // ───────────────────────────────────────────────────────────

        public static string Serialize(List<VulnerabilityRule> rules)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ════════════════════════════════════════════");
            sb.AppendLine("#  CodeInspect 취약점 검출 규칙");
            sb.AppendLine("# ════════════════════════════════════════════");
            sb.AppendLine("#");
            sb.AppendLine("#  형식:");
            sb.AppendLine("#    - 주석은 '#' 으로 시작");
            sb.AppendLine("#    - 각 규칙은 [RULE] 블록으로 구분");
            sb.AppendLine("#    - key=value 한 줄 (값 안의 '=' 허용)");
            sb.AppendLine("#");
            sb.AppendLine("#  필드:");
            sb.AppendLine("#    id          : 규칙 식별자 (고유)");
            sb.AppendLine("#    languages   : c, cpp, java, csharp 중 하나 이상 (콤마 구분)");
            sb.AppendLine("#    severity    : CRITICAL | HIGH | MEDIUM | LOW");
            sb.AppendLine("#    category    : CWE 분류 (예: CWE-120 Buffer Overflow)");
            sb.AppendLine("#    description : 한글 설명 (한 줄)");
            sb.AppendLine("#    pattern     : 정규식 패턴 (한 줄, 이스케이프 없음)");
            sb.AppendLine("#    options     : None | IgnoreCase | Multiline | Singleline (콤마 구분, 기본 Multiline)");
            sb.AppendLine("#    reference   : 참조 출처 (선택, 예: semgrep:..., OWASP ASVS V6.2.5)");
            sb.AppendLine("#");
            sb.AppendLine("#  생성일시: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("#  규칙 수: " + rules.Count);
            sb.AppendLine();

            foreach (var r in rules)
            {
                sb.AppendLine("[RULE]");
                sb.AppendLine("id=" + Clean(r.RuleId));
                sb.AppendLine("languages=" + (r.Languages != null && r.Languages.Length > 0
                    ? string.Join(",", r.Languages) : ""));
                sb.AppendLine("severity=" + Clean(r.Severity));
                sb.AppendLine("category=" + Clean(r.Category));
                sb.AppendLine("description=" + Clean(r.Description));
                sb.AppendLine("pattern=" + Clean(r.Pattern));
                sb.AppendLine("options=" + SerializeOptions(r.Options));
                if (!string.IsNullOrEmpty(r.Reference))
                    sb.AppendLine("reference=" + Clean(r.Reference));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static List<VulnerabilityRule> Parse(string content)
        {
            var result = new List<VulnerabilityRule>();
            if (string.IsNullOrEmpty(content)) return result;

            VulnerabilityRule cur = null;
            string[] lines = content.Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#")) continue;
                if (trimmed.StartsWith(";")) continue;

                if (trimmed.Equals("[RULE]", StringComparison.OrdinalIgnoreCase))
                {
                    if (cur != null && IsValid(cur)) result.Add(cur);
                    cur = new VulnerabilityRule();
                    continue;
                }

                if (cur == null) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                string key = line.Substring(0, eq).Trim().ToLower();
                string value = line.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "id":
                    case "ruleid":
                        cur.RuleId = value; break;
                    case "languages":
                    case "lang":
                    case "language":
                        cur.Languages = SplitCsv(value); break;
                    case "severity":
                        cur.Severity = value.ToUpper(); break;
                    case "category":
                        cur.Category = value; break;
                    case "description":
                    case "desc":
                        cur.Description = value; break;
                    case "pattern":
                        cur.Pattern = value; break;
                    case "options":
                    case "option":
                        cur.Options = ParseOptions(value); break;
                    case "reference":
                    case "ref":
                    case "source":
                        cur.Reference = value; break;
                }
            }

            if (cur != null && IsValid(cur)) result.Add(cur);
            return result;
        }

        private static bool IsValid(VulnerabilityRule r)
        {
            if (r == null) return false;
            if (string.IsNullOrEmpty(r.RuleId)) return false;
            if (string.IsNullOrEmpty(r.Pattern)) return false;
            if (r.Languages == null || r.Languages.Length == 0) return false;
            if (string.IsNullOrEmpty(r.Severity)) return false;
            // 정규식 컴파일 검증
            try { new Regex(r.Pattern, r.Options); }
            catch { return false; }
            return true;
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        private static string[] SplitCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return new string[0];
            string[] parts = value.Split(',');
            var list = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string t = parts[i].Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        private static RegexOptions ParseOptions(string value)
        {
            RegexOptions opts = RegexOptions.None;
            if (string.IsNullOrEmpty(value)) return RegexOptions.Multiline;

            string[] parts = value.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            bool any = false;
            foreach (var p in parts)
            {
                string t = p.Trim();
                if (t.Length == 0) continue;
                if (t.Equals("None", StringComparison.OrdinalIgnoreCase)) { any = true; continue; }
                if (t.Equals("IgnoreCase", StringComparison.OrdinalIgnoreCase)) { opts |= RegexOptions.IgnoreCase; any = true; }
                else if (t.Equals("Multiline", StringComparison.OrdinalIgnoreCase)) { opts |= RegexOptions.Multiline; any = true; }
                else if (t.Equals("Singleline", StringComparison.OrdinalIgnoreCase)) { opts |= RegexOptions.Singleline; any = true; }
                else if (t.Equals("IgnorePatternWhitespace", StringComparison.OrdinalIgnoreCase)) { opts |= RegexOptions.IgnorePatternWhitespace; any = true; }
                else if (t.Equals("ExplicitCapture", StringComparison.OrdinalIgnoreCase)) { opts |= RegexOptions.ExplicitCapture; any = true; }
            }
            if (!any) return RegexOptions.Multiline;
            return opts;
        }

        public static string SerializeOptions(RegexOptions opts)
        {
            var list = new List<string>();
            if ((opts & RegexOptions.IgnoreCase) != 0) list.Add("IgnoreCase");
            if ((opts & RegexOptions.Multiline) != 0) list.Add("Multiline");
            if ((opts & RegexOptions.Singleline) != 0) list.Add("Singleline");
            if ((opts & RegexOptions.IgnorePatternWhitespace) != 0) list.Add("IgnorePatternWhitespace");
            if ((opts & RegexOptions.ExplicitCapture) != 0) list.Add("ExplicitCapture");
            if (list.Count == 0) return "None";
            return string.Join(",", list.ToArray());
        }
    }
}

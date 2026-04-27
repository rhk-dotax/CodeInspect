using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodeInspect
{
    // ════════════════════════════════════════════════════════════════
    //  LLMConfig — 설정 저장/로드 (codeinspect_rules/llm_config.txt)
    // ════════════════════════════════════════════════════════════════
    public class LLMConfig
    {
        public string Provider { get; set; }       // "ollama" | "lmstudio"
        public string Endpoint { get; set; }       // 예: http://localhost:11434
        public string Model { get; set; }          // 예: llama3:8b
        public int TimeoutSeconds { get; set; }    // 기본 120
        public double Temperature { get; set; }    // 기본 0.1
        public int MaxFileSizeKB { get; set; }     // 기본 50

        public LLMConfig()
        {
            Provider = "ollama";
            Endpoint = "http://localhost:11434";
            Model = "";
            TimeoutSeconds = 120;
            Temperature = 0.1;
            MaxFileSizeKB = 50;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(Provider)
                && !string.IsNullOrEmpty(Endpoint)
                && !string.IsNullOrEmpty(Model);
        }

        public static string GetConfigPath()
        {
            string baseDir = System.Windows.Forms.Application.StartupPath;
            string ruleDir = Path.Combine(baseDir, "codeinspect_rules");
            return Path.Combine(ruleDir, "llm_config.txt");
        }

        public static string DefaultEndpointFor(string provider)
        {
            if (string.Equals(provider, "lmstudio", StringComparison.OrdinalIgnoreCase))
                return "http://localhost:1234";
            return "http://localhost:11434";
        }

        public static LLMConfig Load()
        {
            var cfg = new LLMConfig();
            string path = GetConfigPath();
            if (!File.Exists(path)) return cfg;

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLower();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "provider":      cfg.Provider = val.ToLower(); break;
                        case "endpoint":      cfg.Endpoint = val; break;
                        case "model":         cfg.Model = val; break;
                        case "timeoutsec":
                        case "timeoutseconds":
                            int t; if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) cfg.TimeoutSeconds = t;
                            break;
                        case "temperature":
                            double tp; if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out tp)) cfg.Temperature = tp;
                            break;
                        case "maxfilesizekb":
                            int m; if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out m)) cfg.MaxFileSizeKB = m;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LLMConfig.Load");
            }
            return cfg;
        }

        public void Save()
        {
            string path = GetConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var sb = new StringBuilder();
            sb.AppendLine("# CodeInspect LLM 분석 설정");
            sb.AppendLine("# 본 파일은 LLMConfigForm 다이얼로그에서 자동 생성/갱신됩니다.");
            sb.AppendLine("# 수동 편집도 가능 (provider=ollama|lmstudio).");
            sb.AppendLine();
            sb.AppendLine("provider=" + (Provider ?? ""));
            sb.AppendLine("endpoint=" + (Endpoint ?? ""));
            sb.AppendLine("model=" + (Model ?? ""));
            sb.AppendLine("timeoutSec=" + TimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("temperature=" + Temperature.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("maxFileSizeKB=" + MaxFileSizeKB.ToString(CultureInfo.InvariantCulture));

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  LLMAnalyzer — Ollama / LM Studio HTTP 호출 및 응답 파싱
    // ════════════════════════════════════════════════════════════════
    public class LLMAnalyzer
    {
        private readonly LLMConfig _config;
        private int _findingSeq;

        // CodeAnalyzer와 동일한 디렉토리 제외 정책
        private static readonly HashSet<string> ExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".svn", ".hg", "bin", "obj", "out", "build", "target",
            "node_modules", "__pycache__", ".vs", ".idea", "packages", "Debug", "Release"
        };

        public LLMAnalyzer(LLMConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            _config = config;
            _findingSeq = 0;
        }

        // ── 단일 파일 분석 ──
        public List<Finding> AnalyzeFile(string filePath)
        {
            var findings = new List<Finding>();
            string lang = VulnerabilityRules.DetectLanguage(filePath);
            if (string.IsNullOrEmpty(lang)) return findings;

            // 파일 크기 검사
            long sizeKb;
            try { sizeKb = new FileInfo(filePath).Length / 1024L; }
            catch { return findings; }

            if (sizeKb > _config.MaxFileSizeKB)
            {
                findings.Add(new Finding
                {
                    FilePath = filePath,
                    LineNumber = 1,
                    RuleId = "LLM-SKIP",
                    Severity = "LOW",
                    Category = "LLM-Skip File Too Large",
                    Reason = string.Format("파일 크기 {0}KB가 LLM 분석 한계({1}KB)를 초과하여 건너뜀", sizeKb, _config.MaxFileSizeKB),
                    MatchedCode = "(skipped)"
                });
                return findings;
            }

            string content;
            try { content = File.ReadAllText(filePath, Encoding.UTF8); }
            catch { return findings; }

            string[] lines = content.Split(new[] { '\n' }, StringSplitOptions.None);

            // LLM 호출
            string responseText;
            try
            {
                string prompt = BuildPrompt(filePath, lang, lines);
                responseText = CallLLM(prompt);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LLMAnalyzer.AnalyzeFile (HTTP) - " + filePath);
                findings.Add(new Finding
                {
                    FilePath = filePath,
                    LineNumber = 1,
                    RuleId = "LLM-ERROR",
                    Severity = "LOW",
                    Category = "LLM-Error",
                    Reason = "LLM 호출 실패: " + ex.Message,
                    MatchedCode = "(error)"
                });
                return findings;
            }

            // 응답 파싱
            try
            {
                var parsed = ParseFindings(responseText, filePath, lines);
                findings.AddRange(parsed);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LLMAnalyzer.AnalyzeFile (Parse) - " + filePath);
                findings.Add(new Finding
                {
                    FilePath = filePath,
                    LineNumber = 1,
                    RuleId = "LLM-ERROR",
                    Severity = "LOW",
                    Category = "LLM-Error",
                    Reason = "LLM 응답 파싱 실패: " + ex.Message,
                    MatchedCode = TruncateForDisplay(responseText, 200)
                });
            }
            return findings;
        }

        // ── 디렉토리 분석 (CodeAnalyzer와 동일 시그니처) ──
        public List<Finding> AnalyzeDirectory(string directory, Action<int, int, string, int> progress = null)
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
                var fs = AnalyzeFile(files[i]);
                allFindings.AddRange(fs);
                if (progress != null) progress(i + 1, total, files[i], fs.Count);
            }
            return allFindings;
        }

        // ── 연결 테스트 (Provider별 가벼운 헬스체크) ──
        public string TestConnection()
        {
            try
            {
                string testPrompt = "ping";
                string body = BuildRequestBody(testPrompt);
                string url = BuildEndpointUrl();
                int saved = _config.TimeoutSeconds;
                try
                {
                    // 테스트는 최대 30초로 제한
                    var tmp = new LLMConfig
                    {
                        Provider = _config.Provider, Endpoint = _config.Endpoint, Model = _config.Model,
                        TimeoutSeconds = Math.Min(saved, 30), Temperature = _config.Temperature, MaxFileSizeKB = _config.MaxFileSizeKB
                    };
                    HttpPost(url, body, tmp.TimeoutSeconds);
                }
                finally { /* nothing */ }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ── 모델 목록 조회 (다이얼로그용 정적 메서드) ──
        public static List<string> ListModels(string provider, string endpoint, int timeoutSec, out string error)
        {
            error = null;
            var result = new List<string>();
            try
            {
                if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
                {
                    string url = TrimEnd(endpoint) + "/api/tags";
                    string resp = HttpGet(url, timeoutSec);
                    var ser = new JavaScriptSerializer();
                    var obj = ser.DeserializeObject(resp) as Dictionary<string, object>;
                    if (obj != null && obj.ContainsKey("models"))
                    {
                        var arr = obj["models"] as object[];
                        if (arr != null)
                        {
                            foreach (var m in arr)
                            {
                                var md = m as Dictionary<string, object>;
                                if (md != null && md.ContainsKey("name"))
                                    result.Add(md["name"].ToString());
                            }
                        }
                    }
                }
                else // LM Studio (OpenAI 호환)
                {
                    string url = TrimEnd(endpoint) + "/v1/models";
                    string resp = HttpGet(url, timeoutSec);
                    var ser = new JavaScriptSerializer();
                    var obj = ser.DeserializeObject(resp) as Dictionary<string, object>;
                    if (obj != null && obj.ContainsKey("data"))
                    {
                        var arr = obj["data"] as object[];
                        if (arr != null)
                        {
                            foreach (var m in arr)
                            {
                                var md = m as Dictionary<string, object>;
                                if (md != null && md.ContainsKey("id"))
                                    result.Add(md["id"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════
        //  내부 유틸
        // ════════════════════════════════════════════════════════════

        private string BuildPrompt(string filePath, string lang, string[] lines)
        {
            var sb = new StringBuilder();
            sb.AppendLine("당신은 보안 코드 리뷰어입니다. 아래 " + lang + " 소스코드의 보안 취약점을 찾아주세요.");
            sb.AppendLine();
            sb.AppendLine("엄격한 출력 규칙:");
            sb.AppendLine("- JSON 배열로만 응답. 다른 텍스트, 마크다운 코드블록, 설명 금지.");
            sb.AppendLine("- 각 항목 필드:");
            sb.AppendLine("  lineNumber (정수, 1부터),");
            sb.AppendLine("  severity (\"CRITICAL\" | \"HIGH\" | \"MEDIUM\" | \"LOW\"),");
            sb.AppendLine("  category (예: \"CWE-89 SQL Injection\"),");
            sb.AppendLine("  reason (한국어 1~2문장)");
            sb.AppendLine("- 취약점이 없으면 빈 배열 [].");
            sb.AppendLine();
            sb.AppendLine("[파일: " + Path.GetFileName(filePath) + "]");

            int max = lines.Length;
            for (int i = 0; i < max; i++)
            {
                sb.Append((i + 1).ToString());
                sb.Append(": ");
                sb.AppendLine(lines[i]);
            }
            return sb.ToString();
        }

        private string BuildEndpointUrl()
        {
            string baseUrl = TrimEnd(_config.Endpoint);
            if (string.Equals(_config.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
                return baseUrl + "/api/generate";
            return baseUrl + "/v1/chat/completions";
        }

        private string BuildRequestBody(string prompt)
        {
            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            if (string.Equals(_config.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                var req = new Dictionary<string, object>();
                req["model"] = _config.Model;
                req["prompt"] = prompt;
                req["stream"] = false;
                var opts = new Dictionary<string, object>();
                opts["temperature"] = _config.Temperature;
                req["options"] = opts;
                return ser.Serialize(req);
            }
            else
            {
                var msg = new Dictionary<string, object>();
                msg["role"] = "user";
                msg["content"] = prompt;
                var req = new Dictionary<string, object>();
                req["model"] = _config.Model;
                req["messages"] = new object[] { msg };
                req["temperature"] = _config.Temperature;
                req["stream"] = false;
                return ser.Serialize(req);
            }
        }

        private string CallLLM(string prompt)
        {
            string url = BuildEndpointUrl();
            string body = BuildRequestBody(prompt);
            string raw = HttpPost(url, body, _config.TimeoutSeconds);

            // Provider별 응답에서 텍스트 추출
            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            var obj = ser.DeserializeObject(raw) as Dictionary<string, object>;
            if (obj == null) throw new Exception("응답이 JSON 객체가 아님");

            if (string.Equals(_config.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                if (!obj.ContainsKey("response"))
                    throw new Exception("Ollama 응답에 'response' 필드 없음");
                return obj["response"].ToString();
            }
            else
            {
                var choices = obj.ContainsKey("choices") ? obj["choices"] as object[] : null;
                if (choices == null || choices.Length == 0)
                    throw new Exception("LM Studio 응답에 'choices' 없음");
                var first = choices[0] as Dictionary<string, object>;
                if (first == null || !first.ContainsKey("message"))
                    throw new Exception("LM Studio 응답에 'message' 없음");
                var msg = first["message"] as Dictionary<string, object>;
                if (msg == null || !msg.ContainsKey("content"))
                    throw new Exception("LM Studio 응답에 'content' 없음");
                return msg["content"].ToString();
            }
        }

        private List<Finding> ParseFindings(string responseText, string filePath, string[] lines)
        {
            var result = new List<Finding>();
            if (string.IsNullOrEmpty(responseText)) return result;

            // 모델이 마크다운 코드블록을 둘렀을 가능성 → JSON 배열만 추출
            string json = ExtractJsonArray(responseText);
            if (string.IsNullOrEmpty(json)) return result;

            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            var arr = ser.DeserializeObject(json) as object[];
            if (arr == null) return result;

            string modelTag = SanitizeModelName(_config.Model);

            foreach (var item in arr)
            {
                var d = item as Dictionary<string, object>;
                if (d == null) continue;

                int line = 0;
                if (d.ContainsKey("lineNumber"))
                {
                    var v = d["lineNumber"];
                    if (v is int) line = (int)v;
                    else if (v != null) int.TryParse(v.ToString(), out line);
                }
                if (line < 1) line = 1;
                if (line > lines.Length) line = lines.Length > 0 ? lines.Length : 1;

                string sev = d.ContainsKey("severity") && d["severity"] != null ? d["severity"].ToString().ToUpper() : "MEDIUM";
                if (sev != "CRITICAL" && sev != "HIGH" && sev != "MEDIUM" && sev != "LOW") sev = "MEDIUM";

                string cat = d.ContainsKey("category") && d["category"] != null ? d["category"].ToString() : "LLM-Detected";
                string reason = d.ContainsKey("reason") && d["reason"] != null ? d["reason"].ToString() : "";

                // MatchedCode: 실제 파일에서 추출 (LLM 위변조 방지)
                string code = "";
                if (line >= 1 && line <= lines.Length)
                {
                    code = lines[line - 1].Trim();
                    if (code.Length > 200) code = code.Substring(0, 200);
                }

                _findingSeq++;
                result.Add(new Finding
                {
                    FilePath = filePath,
                    LineNumber = line,
                    RuleId = string.Format("LLM-{0}-{1:D3}", modelTag, _findingSeq),
                    Severity = sev,
                    Category = cat,
                    Reason = reason,
                    MatchedCode = code
                });
            }
            return result;
        }

        private static string ExtractJsonArray(string text)
        {
            // 1) 가장 단순한 케이스: 그냥 배열로 시작하는지
            string trimmed = text.Trim();
            if (trimmed.StartsWith("[")) return trimmed;

            // 2) ```json ... ``` 또는 ``` ... ``` 코드블록
            var m = Regex.Match(text, @"```(?:json)?\s*(\[.*?\])\s*```", RegexOptions.Singleline);
            if (m.Success) return m.Groups[1].Value;

            // 3) 본문 중 첫 [ 부터 마지막 ] 까지
            int start = text.IndexOf('[');
            int end = text.LastIndexOf(']');
            if (start >= 0 && end > start) return text.Substring(start, end - start + 1);

            return "";
        }

        private static string SanitizeModelName(string model)
        {
            if (string.IsNullOrEmpty(model)) return "model";
            // 콜론, 슬래시 등을 제거하고 식별자로
            var sb = new StringBuilder();
            foreach (char c in model)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                else if (c == ':' || c == '/' || c == '.') sb.Append('-');
            }
            string s = sb.ToString();
            if (s.Length > 24) s = s.Substring(0, 24);
            return s.Length == 0 ? "model" : s;
        }

        private static string TruncateForDisplay(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (s.Length > max) s = s.Substring(0, max) + "...";
            return s;
        }

        private static string TrimEnd(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            return url.TrimEnd('/');
        }

        // ── HTTP I/O (HttpWebRequest 기반, .NET 4.0 호환) ──

        private static string HttpGet(string url, int timeoutSec)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = timeoutSec * 1000;
            req.ReadWriteTimeout = timeoutSec * 1000;
            req.Accept = "application/json";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static string HttpPost(string url, string jsonBody, int timeoutSec)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Accept = "application/json";
            req.Timeout = timeoutSec * 1000;
            req.ReadWriteTimeout = timeoutSec * 1000;

            byte[] data = Encoding.UTF8.GetBytes(jsonBody);
            req.ContentLength = data.Length;
            using (var rs = req.GetRequestStream())
            {
                rs.Write(data, 0, data.Length);
            }

            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                // 본문 메시지를 함께 전달해 디버깅 도움
                if (wex.Response != null)
                {
                    using (var sr = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        string body = sr.ReadToEnd();
                        throw new Exception("HTTP 오류: " + wex.Message + " | 본문: " + TruncateForDisplay(body, 300), wex);
                    }
                }
                throw;
            }
        }

        // ── 파일 수집 (CodeAnalyzer.CollectFiles와 동일 정책) ──
        private static List<string> CollectFiles(string directory, HashSet<string> extensions)
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
            catch { /* 권한 없는 디렉토리 무시 */ }
            return result;
        }
    }
}

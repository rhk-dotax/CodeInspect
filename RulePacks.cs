using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CodeInspect
{
    /// <summary>내장 큐레이션 룰팩 (폐쇄망 환경 대응).
    /// 외부 다운로드 대신 EXE에 내장된 룰팩으로 제공.
    /// 각 룰에는 출처(Semgrep 룰 ID / OWASP ASVS 요구사항 번호)를 Reference로 기록.
    ///
    /// 출처:
    ///   - Semgrep 공개 룰셋: github.com/returntocorp/semgrep-rules (LGPL-2.1)
    ///     정규식 포맷으로 재구현 (Semgrep의 AST 매칭이 아닌 CodeInspect의 line-regex 엔진용)
    ///   - OWASP ASVS v4.0.3: owasp.org/www-project-application-security-verification-standard
    ///     요구사항을 정적분석 가능한 패턴으로 변환
    /// </summary>
    public static class RulePacks
    {
        // ═══════════════════════════════════════════════════════════
        //  Semgrep 공개 룰팩 (returntocorp/semgrep-rules 기반)
        // ═══════════════════════════════════════════════════════════

        public static List<VulnerabilityRule> GetSemgrepPack()
        {
            var list = new List<VulnerabilityRule>();

            // ── Java ──────────────────────────────────────────────

            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-001", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-327 Broken Crypto",
                Description = "MD5 메시지 다이제스트 사용 (Semgrep: use-of-md5)",
                Pattern = @"MessageDigest\.getInstance\s*\(\s*""MD5""",
                Reference = "semgrep:java.lang.security.audit.crypto.use-of-md5.use-of-md5"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-002", Languages = new[]{"java"}, Severity = "MEDIUM",
                Category = "CWE-328 Weak Hash",
                Description = "SHA-1 메시지 다이제스트 사용 (Semgrep: use-of-sha1)",
                Pattern = @"MessageDigest\.getInstance\s*\(\s*""SHA-?1""",
                Reference = "semgrep:java.lang.security.audit.crypto.use-of-sha1.use-of-sha1"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-003", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-327 Broken Crypto",
                Description = "DES/3DES 암호 알고리즘 사용 (Semgrep: use-of-des)",
                Pattern = @"Cipher\.getInstance\s*\(\s*""(?:DES|DESede)",
                Reference = "semgrep:java.lang.security.audit.crypto.use-of-des.use-of-des"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-004", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-327 Broken Crypto",
                Description = "AES/ECB 모드 사용 (Semgrep: ecb-cipher)",
                Pattern = @"Cipher\.getInstance\s*\(\s*""[^""]*\/ECB\/",
                Reference = "semgrep:java.lang.security.audit.crypto.ecb-cipher.ecb-cipher"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-005", Languages = new[]{"java"}, Severity = "MEDIUM",
                Category = "CWE-330 Insufficient Randomness",
                Description = "보안 비관련 Random 사용 (Semgrep: weak-random)",
                Pattern = @"new\s+java\.util\.Random\s*\(|new\s+Random\s*\(",
                Reference = "semgrep:java.lang.security.audit.crypto.weak-random.weak-random"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-006", Languages = new[]{"java"}, Severity = "CRITICAL",
                Category = "CWE-89 SQL Injection",
                Description = "executeQuery/Update에 문자열 연결 (Semgrep: formatted-sql-string)",
                Pattern = @"\.(?:executeQuery|executeUpdate|execute)\s*\(\s*(?:""[^""]*""\s*\+|\w+\s*\+\s*"")",
                Reference = "semgrep:java.lang.security.audit.formatted-sql-string.formatted-sql-string"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-007", Languages = new[]{"java"}, Severity = "CRITICAL",
                Category = "CWE-502 Deserialization",
                Description = "ObjectInputStream.readObject (Semgrep: object-deserialization)",
                Pattern = @"ObjectInputStream\s*\(|\.readObject\s*\(",
                Reference = "semgrep:java.lang.security.audit.object-deserialization.object-deserialization"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-008", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-611 XXE",
                Description = "DocumentBuilderFactory DTD 처리 미설정 (Semgrep: xxe)",
                Pattern = @"DocumentBuilderFactory\.newInstance\s*\(",
                Reference = "semgrep:java.lang.security.audit.xxe.xxe"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-009", Languages = new[]{"java"}, Severity = "CRITICAL",
                Category = "CWE-78 OS Command Injection",
                Description = "Runtime.exec 사용 (Semgrep: dangerous-exec)",
                Pattern = @"Runtime\.getRuntime\s*\(\s*\)\s*\.\s*exec\s*\(",
                Reference = "semgrep:java.lang.security.audit.command-injection-formatted-runtime-call"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-010", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-22 Path Traversal",
                Description = "new File에 요청 파라미터 전달 (Semgrep: tainted-path)",
                Pattern = @"new\s+File\s*\(\s*request\.",
                Reference = "semgrep:java.lang.security.audit.tainted-file-system.tainted-file-system"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-011", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-295 Certificate Validation",
                Description = "TrustAllX509TrustManager 사용 (Semgrep: trust-all-tm)",
                Pattern = @"TrustManager\s*\[\s*\]|X509TrustManager\s*\(\s*\)",
                Reference = "semgrep:java.lang.security.audit.crypto.ssl.insecure-trust-manager"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-012", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-295 Certificate Validation",
                Description = "HostnameVerifier 모든 호스트 허용 (Semgrep: allow-all-hostname)",
                Pattern = @"setHostnameVerifier\s*\(\s*new\s+HostnameVerifier|ALLOW_ALL_HOSTNAME",
                Reference = "semgrep:java.lang.security.audit.crypto.ssl.insecure-hostname-verifier"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-013", Languages = new[]{"java"}, Severity = "MEDIUM",
                Category = "CWE-117 Log Injection",
                Description = "로그에 사용자 입력 직접 출력 (Semgrep: tainted-log)",
                Pattern = @"(?:logger|log)\.(?:info|warn|error|debug)\s*\(\s*.*\+\s*request\.",
                Reference = "semgrep:java.lang.security.audit.tainted-log.tainted-log"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-JAVA-014", Languages = new[]{"java"}, Severity = "HIGH",
                Category = "CWE-94 Code Injection",
                Description = "ScriptEngine.eval 호출 (Semgrep: eval-injection)",
                Pattern = @"ScriptEngine\s*\w*\s*\.\s*eval\s*\(",
                Reference = "semgrep:java.lang.security.audit.script-engine-injection"
            });

            // ── Python ────────────────────────────────────────────
            // (※ Python 언어 지원은 P4 계획이지만, 룰 데이터 자체는 미리 포함)

            // ── JavaScript ────────────────────────────────────────
            // (※ JS 언어 지원은 P4 계획)

            // ── C/C++ ─────────────────────────────────────────────

            list.Add(new VulnerabilityRule {
                RuleId = "SG-C-001", Languages = new[]{"c","cpp"}, Severity = "CRITICAL",
                Category = "CWE-120 Buffer Overflow",
                Description = "gets() 사용 (Semgrep: insecure-gets)",
                Pattern = @"\bgets\s*\(",
                Reference = "semgrep:c.lang.security.insecure-use-gets-fn.insecure-use-gets-fn"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-C-002", Languages = new[]{"c","cpp"}, Severity = "HIGH",
                Category = "CWE-120 Buffer Overflow",
                Description = "strcpy/strcat 사용 (Semgrep: insecure-use-strcat-fn)",
                Pattern = @"\b(?:strcpy|strcat)\s*\(",
                Reference = "semgrep:c.lang.security.insecure-use-strcat-fn.insecure-use-strcat-fn"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-C-003", Languages = new[]{"c","cpp"}, Severity = "HIGH",
                Category = "CWE-134 Format String",
                Description = "sprintf 사용 (Semgrep: insecure-use-sprintf)",
                Pattern = @"\bsprintf\s*\(",
                Reference = "semgrep:c.lang.security.insecure-use-sprintf-fn.insecure-use-sprintf-fn"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-C-004", Languages = new[]{"c","cpp"}, Severity = "MEDIUM",
                Category = "CWE-120 Buffer Overflow",
                Description = "scanf에 %s 제한 없이 사용 (Semgrep: insecure-use-scanf)",
                Pattern = @"\bscanf\s*\(\s*""[^""]*%s[^0-9]",
                Reference = "semgrep:c.lang.security.insecure-use-scanf-fn.insecure-use-scanf-fn"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-C-005", Languages = new[]{"c","cpp"}, Severity = "HIGH",
                Category = "CWE-377 Insecure Temp File",
                Description = "tmpnam/tempnam 사용 (Semgrep: insecure-tempnam)",
                Pattern = @"\b(?:tmpnam|tempnam|mktemp)\s*\(",
                Reference = "semgrep:c.lang.security.insecure-use-mktemp-fn.insecure-use-mktemp-fn"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-C-006", Languages = new[]{"c","cpp"}, Severity = "CRITICAL",
                Category = "CWE-78 OS Command Injection",
                Description = "system() 호출 (Semgrep: no-system)",
                Pattern = @"\bsystem\s*\(",
                Reference = "semgrep:c.lang.security.insecure-use-system-fn.insecure-use-system-fn"
            });

            // ── C# ────────────────────────────────────────────────

            list.Add(new VulnerabilityRule {
                RuleId = "SG-CS-001", Languages = new[]{"csharp"}, Severity = "CRITICAL",
                Category = "CWE-502 Deserialization",
                Description = "BinaryFormatter 역직렬화 (Semgrep: binary-formatter)",
                Pattern = @"\bBinaryFormatter\b",
                Reference = "semgrep:csharp.lang.security.insecure-deserialization.binary-formatter"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-CS-002", Languages = new[]{"csharp"}, Severity = "HIGH",
                Category = "CWE-502 Deserialization",
                Description = "TypeNameHandling.All 사용 (Semgrep: json-type-handling)",
                Pattern = @"TypeNameHandling\s*\.\s*(?:All|Auto|Objects|Arrays)",
                Reference = "semgrep:csharp.lang.security.insecure-deserialization.typenamehandling"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-CS-003", Languages = new[]{"csharp"}, Severity = "HIGH",
                Category = "CWE-327 Broken Crypto",
                Description = "DES/TripleDES Provider (Semgrep: weak-cipher)",
                Pattern = @"\b(?:DESCryptoServiceProvider|TripleDESCryptoServiceProvider)\b",
                Reference = "semgrep:csharp.lang.security.audit.weak-cipher"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-CS-004", Languages = new[]{"csharp"}, Severity = "HIGH",
                Category = "CWE-328 Weak Hash",
                Description = "MD5/SHA1 사용 (Semgrep: weak-hash)",
                Pattern = @"\b(?:MD5|SHA1)\s*\.\s*Create\s*\(",
                Reference = "semgrep:csharp.lang.security.audit.weak-hash"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-CS-005", Languages = new[]{"csharp"}, Severity = "HIGH",
                Category = "CWE-79 XSS",
                Description = "Html.Raw 사용 (Semgrep: html-raw)",
                Pattern = @"Html\.Raw\s*\(",
                Reference = "semgrep:csharp.lang.security.audit.xss.html-raw"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-CS-006", Languages = new[]{"csharp"}, Severity = "HIGH",
                Category = "CWE-611 XXE",
                Description = "XmlDocument DtdProcessing 허용 (Semgrep: xxe-xmldocument)",
                Pattern = @"DtdProcessing\s*=\s*DtdProcessing\.Parse",
                Reference = "semgrep:csharp.lang.security.audit.xxe.xmldocument"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "SG-CS-007", Languages = new[]{"csharp"}, Severity = "CRITICAL",
                Category = "CWE-89 SQL Injection",
                Description = "EF ExecuteSqlRaw에 보간 문자열 (Semgrep: ef-raw-sql)",
                Pattern = @"(?:ExecuteSqlRaw|FromSqlRaw)\s*\(\s*\$""",
                Reference = "semgrep:csharp.lang.security.audit.sqli.raw-sql"
            });

            // Reference 필드를 모든 룰에 강제 설정 (null 방어)
            foreach (var r in list)
            {
                if (r.Options == RegexOptions.None) r.Options = RegexOptions.Multiline;
            }
            return list;
        }

        // ═══════════════════════════════════════════════════════════
        //  OWASP ASVS v4.0.3 룰팩
        // ═══════════════════════════════════════════════════════════

        public static List<VulnerabilityRule> GetOwaspAsvsPack()
        {
            var list = new List<VulnerabilityRule>();

            // V2 Authentication
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V2-1-1", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-521 Weak Password Requirements",
                Description = "하드코딩된 패스워드 감지 (ASVS V2.10.2 서비스 계정 자격 증명 금지)",
                Pattern = @"(?:password|passwd|pwd)\s*=\s*""[^""]{3,}""",
                Reference = "OWASP ASVS v4.0.3 V2.10.2",
                Options = RegexOptions.Multiline | RegexOptions.IgnoreCase
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V2-3-1", Languages = new[]{"java","csharp","c","cpp"}, Severity = "HIGH",
                Category = "CWE-798 Hard-coded Credentials",
                Description = "하드코딩된 API 키/시크릿/토큰 (ASVS V2.10.4)",
                Pattern = @"(?:api[_-]?key|secret[_-]?key|access[_-]?token|auth[_-]?token)\s*=\s*""[^""]{8,}""",
                Reference = "OWASP ASVS v4.0.3 V2.10.4",
                Options = RegexOptions.Multiline | RegexOptions.IgnoreCase
            });

            // V5 Validation, Sanitization and Encoding
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V5-2-4", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-95 Code Injection via eval",
                Description = "eval/동적 코드 실행 API (ASVS V5.2.4 eval 금지)",
                Pattern = @"ScriptEngine\s*\w*\s*\.\s*eval\s*\(|CSharpScript\.(?:Run|Evaluate)",
                Reference = "OWASP ASVS v4.0.3 V5.2.4"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V5-3-4", Languages = new[]{"java","csharp"}, Severity = "CRITICAL",
                Category = "CWE-89 SQL Injection",
                Description = "매개변수화된 쿼리 미사용 - 문자열 연결 SQL (ASVS V5.3.4)",
                Pattern = @"(?:executeQuery|executeUpdate|ExecuteSqlRaw|FromSqlRaw|CommandText)\s*(?:=|\()\s*(?:""[^""]*""\s*\+|\$"")",
                Reference = "OWASP ASVS v4.0.3 V5.3.4"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V5-3-3", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-79 XSS",
                Description = "출력 인코딩 없이 HTML 직접 작성 (ASVS V5.3.3)",
                Pattern = @"(?:Html\.Raw|Response\.Write|getWriter\s*\(\s*\)\s*\.\s*(?:print|write))\s*\(",
                Reference = "OWASP ASVS v4.0.3 V5.3.3"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V5-3-8", Languages = new[]{"java","csharp","c","cpp"}, Severity = "CRITICAL",
                Category = "CWE-78 OS Command Injection",
                Description = "OS 명령 실행에 문자열 조립 사용 (ASVS V5.3.8)",
                Pattern = @"(?:Runtime\.getRuntime\s*\(\s*\)\s*\.\s*exec|Process\.Start|\bsystem)\s*\(\s*(?:""[^""]*""\s*\+|\$""|[a-zA-Z_]\w*\s*\+)",
                Reference = "OWASP ASVS v4.0.3 V5.3.8"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V5-5-1", Languages = new[]{"java","csharp"}, Severity = "CRITICAL",
                Category = "CWE-502 Deserialization",
                Description = "안전하지 않은 역직렬화 (ASVS V5.5.1)",
                Pattern = @"\b(?:BinaryFormatter|ObjectInputStream|JavaScriptSerializer|XMLDecoder)\b",
                Reference = "OWASP ASVS v4.0.3 V5.5.1"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V5-5-2", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-611 XXE",
                Description = "XML 파서 외부 엔티티 처리 검토 필요 (ASVS V5.5.2)",
                Pattern = @"(?:DocumentBuilderFactory|SAXParserFactory|XMLInputFactory|XmlTextReader|XmlDocument)\b",
                Reference = "OWASP ASVS v4.0.3 V5.5.2"
            });

            // V6 Stored Cryptography
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V6-2-2", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-327 Broken Crypto",
                Description = "승인되지 않은 암호 알고리즘 (DES/3DES/RC4) (ASVS V6.2.2)",
                Pattern = @"\b(?:DES|DESede|RC4|DESCryptoServiceProvider|TripleDESCryptoServiceProvider)\b",
                Reference = "OWASP ASVS v4.0.3 V6.2.2"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V6-2-5", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-327 Broken Crypto",
                Description = "ECB 모드는 기밀성 제공하지 않음 (ASVS V6.2.5)",
                Pattern = @"(?:Cipher\.getInstance\s*\(\s*""[^""]*\/ECB\/|CipherMode\.ECB)",
                Reference = "OWASP ASVS v4.0.3 V6.2.5"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V6-2-7", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-328 Weak Hash",
                Description = "취약한 해시(MD5/SHA1) 사용 (ASVS V6.2.7)",
                Pattern = @"(?:MD5|SHA-?1)\s*\.\s*Create\s*\(|MessageDigest\.getInstance\s*\(\s*""(?:MD5|SHA-?1)""",
                Reference = "OWASP ASVS v4.0.3 V6.2.7"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V6-3-1", Languages = new[]{"java","csharp","c","cpp"}, Severity = "MEDIUM",
                Category = "CWE-330 Insufficient Randomness",
                Description = "보안용도로 부적절한 PRNG (ASVS V6.3.1 CSPRNG 요구)",
                Pattern = @"new\s+(?:java\.util\.)?Random\s*\(|\b(?:rand|srand)\s*\(",
                Reference = "OWASP ASVS v4.0.3 V6.3.1"
            });

            // V7 Error Handling and Logging
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V7-1-1", Languages = new[]{"java","csharp"}, Severity = "MEDIUM",
                Category = "CWE-532 Info Exposure Through Log",
                Description = "로그에 민감 정보(password/token) 기록 가능성 (ASVS V7.1.1)",
                Pattern = @"(?:log|logger)\.(?:info|debug|warn|error|trace)\s*\(.*(?:password|token|secret|pwd|credential)",
                Reference = "OWASP ASVS v4.0.3 V7.1.1",
                Options = RegexOptions.Multiline | RegexOptions.IgnoreCase
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V7-4-1", Languages = new[]{"java","csharp"}, Severity = "MEDIUM",
                Category = "CWE-209 Error Info Exposure",
                Description = "예외 메시지 클라이언트에 직접 반환 (ASVS V7.4.1)",
                Pattern = @"(?:return|Response\.Write|getWriter\s*\(\s*\)\s*\.\s*(?:print|write))\s*\(?\s*(?:ex|exception|e)\.(?:Message|StackTrace|ToString|getMessage)",
                Reference = "OWASP ASVS v4.0.3 V7.4.1"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V7-1-2", Languages = new[]{"java"}, Severity = "MEDIUM",
                Category = "CWE-209 Error Info Exposure",
                Description = "printStackTrace() 사용 - 표준 로깅 사용 필요 (ASVS V7.1.2)",
                Pattern = @"\.printStackTrace\s*\(\s*\)",
                Reference = "OWASP ASVS v4.0.3 V7.1.2"
            });

            // V8 Data Protection
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V8-3-4", Languages = new[]{"java","csharp"}, Severity = "MEDIUM",
                Category = "CWE-200 Sensitive Info Exposure",
                Description = "HTTP GET 파라미터로 민감 데이터 송신 가능성 (ASVS V8.3.4)",
                Pattern = @"\?\s*(?:password|token|secret|api_key)\s*=",
                Reference = "OWASP ASVS v4.0.3 V8.3.4",
                Options = RegexOptions.Multiline | RegexOptions.IgnoreCase
            });

            // V9 Communication
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V9-1-2", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-295 Certificate Validation",
                Description = "모든 인증서 신뢰하는 TrustManager/HostnameVerifier (ASVS V9.1.2)",
                Pattern = @"(?:ServerCertificateValidationCallback\s*=\s*delegate|ALLOW_ALL_HOSTNAME_VERIFIER|TrustAllCerts|checkServerTrusted\s*\([^)]*\)\s*\{\s*\})",
                Reference = "OWASP ASVS v4.0.3 V9.1.2"
            });
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V9-1-1", Languages = new[]{"java","csharp"}, Severity = "MEDIUM",
                Category = "CWE-319 Cleartext Transmission",
                Description = "평문 HTTP URL 사용 (ASVS V9.1.1 TLS 필수)",
                Pattern = @"""http://(?!(?:localhost|127\.0\.0\.1|0\.0\.0\.0))",
                Reference = "OWASP ASVS v4.0.3 V9.1.1"
            });

            // V10 Malicious Code
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V10-3-2", Languages = new[]{"java","csharp"}, Severity = "MEDIUM",
                Category = "CWE-489 Active Debug Code",
                Description = "TODO/FIXME/HACK/XXX 주석 (ASVS V10.3.2 개발 잔존물 제거)",
                Pattern = @"(?://|/\*|#)\s*(?:TODO|FIXME|HACK|XXX|BUG|DEBUG)\b",
                Reference = "OWASP ASVS v4.0.3 V10.3.2",
                Options = RegexOptions.Multiline | RegexOptions.IgnoreCase
            });

            // V12 Files and Resources
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V12-3-1", Languages = new[]{"java","csharp"}, Severity = "HIGH",
                Category = "CWE-22 Path Traversal",
                Description = "사용자 입력을 경로에 직접 연결 (ASVS V12.3.1)",
                Pattern = @"(?:new\s+File|Path\.Combine|FileInputStream|FileReader)\s*\(.*(?:request\.getParameter|Request\.Query|Request\.Form|userInput)",
                Reference = "OWASP ASVS v4.0.3 V12.3.1",
                Options = RegexOptions.Multiline | RegexOptions.IgnoreCase
            });

            // V13 API and Web Service
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V13-2-3", Languages = new[]{"csharp"}, Severity = "MEDIUM",
                Category = "CWE-352 CSRF",
                Description = "HttpPost 액션에 CSRF 방어 토큰 누락 (ASVS V13.2.3)",
                Pattern = @"\[HttpPost\](?!\s*\r?\n\s*\[ValidateAntiForgeryToken\])",
                Reference = "OWASP ASVS v4.0.3 V13.2.3"
            });

            // V14 Configuration
            list.Add(new VulnerabilityRule {
                RuleId = "ASVS-V14-3-2", Languages = new[]{"csharp"}, Severity = "HIGH",
                Category = "CWE-11 ASP.NET Debug Mode",
                Description = "운영 환경 디버그 모드 활성화 감지 (ASVS V14.3.2)",
                Pattern = @"<compilation\s+debug\s*=\s*""true""",
                Reference = "OWASP ASVS v4.0.3 V14.3.2",
                Options = RegexOptions.Multiline | RegexOptions.IgnoreCase
            });

            // 정규식 기본 옵션 보정
            foreach (var r in list)
            {
                if (r.Options == RegexOptions.None) r.Options = RegexOptions.Multiline;
            }
            return list;
        }

        // ═══════════════════════════════════════════════════════════
        //  통합 룰팩 (Semgrep + OWASP ASVS, RuleId 중복 제거)
        // ═══════════════════════════════════════════════════════════

        public static List<VulnerabilityRule> GetCombinedPack()
        {
            var result = new List<VulnerabilityRule>();
            var seen = new Dictionary<string, bool>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var r in GetSemgrepPack())
            {
                if (!string.IsNullOrEmpty(r.RuleId) && !seen.ContainsKey(r.RuleId))
                {
                    seen[r.RuleId] = true;
                    result.Add(r);
                }
            }
            foreach (var r in GetOwaspAsvsPack())
            {
                if (!string.IsNullOrEmpty(r.RuleId) && !seen.ContainsKey(r.RuleId))
                {
                    seen[r.RuleId] = true;
                    result.Add(r);
                }
            }
            return result;
        }

        /// <summary>내장 룰팩 ID → 룰 목록 반환.</summary>
        public static List<VulnerabilityRule> GetPack(string packId)
        {
            if (string.IsNullOrEmpty(packId)) return null;
            string key = packId.Trim().ToLower();
            if (key == "semgrep") return GetSemgrepPack();
            if (key == "owasp" || key == "asvs" || key == "owasp-asvs") return GetOwaspAsvsPack();
            if (key == "combined" || key == "all") return GetCombinedPack();
            return null;
        }
    }
}

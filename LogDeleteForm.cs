using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CodeInspect
{
    // ════════════════════════════════════════════════════════════════
    //  LogRetentionConfig — 프로젝트별 로그 삭제 주기 영속
    //  파일: codeinspect_rules/log_retention.txt
    //  포맷: project|<프로젝트절대경로>|<일수>   (0=미삭제)
    // ════════════════════════════════════════════════════════════════
    public static class LogRetentionConfig
    {
        public static string GetConfigPath()
        {
            string baseDir = Application.StartupPath;
            string ruleDir = Path.Combine(baseDir, "codeinspect_rules");
            return Path.Combine(ruleDir, "log_retention.txt");
        }

        /// <summary>key=프로젝트 절대 경로, value=일수(0=미삭제). 파일 없으면 빈 Dictionary.</summary>
        public static Dictionary<string, int> LoadMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string path = GetConfigPath();
            if (!File.Exists(path)) return map;

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    // project|<경로>|<일수>
                    if (!line.StartsWith("project|", StringComparison.OrdinalIgnoreCase)) continue;
                    int firstBar = line.IndexOf('|');
                    int lastBar = line.LastIndexOf('|');
                    if (firstBar < 0 || lastBar <= firstBar) continue;

                    string projPath = line.Substring(firstBar + 1, lastBar - firstBar - 1).Trim();
                    string daysStr = line.Substring(lastBar + 1).Trim();
                    if (projPath.Length == 0) continue;

                    int days;
                    if (!int.TryParse(daysStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
                        continue;
                    if (days < 0) days = 0;

                    map[projPath] = days;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LogRetentionConfig.LoadMap");
            }
            return map;
        }

        public static void Save(Dictionary<string, int> map)
        {
            string path = GetConfigPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var sb = new StringBuilder();
                sb.AppendLine("# CodeInspect 로그 자동 삭제 주기");
                sb.AppendLine("# 단위: 일. 0이면 자동 삭제 안 함.");
                sb.AppendLine("# 형식: project|<프로젝트절대경로>|<일수>");
                sb.AppendLine("# 본 파일은 LogDeleteForm 다이얼로그에서 자동 생성/갱신됩니다.");
                sb.AppendLine();

                if (map != null)
                {
                    foreach (var kv in map)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        int d = kv.Value < 0 ? 0 : kv.Value;
                        sb.Append("project|").Append(kv.Key).Append("|").Append(d.ToString(CultureInfo.InvariantCulture)).AppendLine();
                    }
                }

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LogRetentionConfig.Save");
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  LogCleaner — 로그 삭제 / 시작 시 자동 퍼지
    // ════════════════════════════════════════════════════════════════
    public static class LogCleaner
    {
        /// <summary>프로젝트 경로의 마지막 폴더명을 로그 서브폴더명으로 사용 (MainForm.RunAnalysis와 동일 매핑).</summary>
        public static string GetProjectLogFolderName(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return "";
            return Path.GetFileName(projectPath.TrimEnd('\\', '/'));
        }

        /// <summary>codeinspect_logs 내부의 모든 파일/하위 폴더를 삭제. 폴더 자체는 유지. 반환=삭제 파일 수.</summary>
        public static int DeleteAll(string logRootDir)
        {
            int deleted = 0;
            if (string.IsNullOrEmpty(logRootDir) || !Directory.Exists(logRootDir)) return 0;

            try
            {
                string[] files = Directory.GetFiles(logRootDir, "*", SearchOption.AllDirectories);
                foreach (string f in files)
                {
                    try { File.Delete(f); deleted++; }
                    catch (Exception exFile) { ErrorLogger.Log(exFile, "LogCleaner.DeleteAll/File"); }
                }

                string[] dirs = Directory.GetDirectories(logRootDir, "*", SearchOption.TopDirectoryOnly);
                foreach (string d in dirs)
                {
                    try { Directory.Delete(d, true); }
                    catch (Exception exDir) { ErrorLogger.Log(exDir, "LogCleaner.DeleteAll/Dir"); }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LogCleaner.DeleteAll");
            }
            return deleted;
        }

        /// <summary>특정 프로젝트의 로그 서브폴더 안의 모든 파일을 삭제. 폴더 자체는 유지. 반환=삭제 파일 수.</summary>
        public static int DeleteProjectLogs(string logRootDir, string projectPath)
        {
            int deleted = 0;
            if (string.IsNullOrEmpty(logRootDir)) return 0;
            string projName = GetProjectLogFolderName(projectPath);
            if (string.IsNullOrEmpty(projName)) return 0;

            string projLogDir = Path.Combine(logRootDir, projName);
            if (!Directory.Exists(projLogDir)) return 0;

            try
            {
                string[] files = Directory.GetFiles(projLogDir, "*", SearchOption.AllDirectories);
                foreach (string f in files)
                {
                    try { File.Delete(f); deleted++; }
                    catch (Exception exFile) { ErrorLogger.Log(exFile, "LogCleaner.DeleteProjectLogs/File"); }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LogCleaner.DeleteProjectLogs");
            }
            return deleted;
        }

        /// <summary>해당 프로젝트의 현재 로그 파일 수.</summary>
        public static int CountProjectLogFiles(string logRootDir, string projectPath)
        {
            if (string.IsNullOrEmpty(logRootDir)) return 0;
            string projName = GetProjectLogFolderName(projectPath);
            if (string.IsNullOrEmpty(projName)) return 0;

            string projLogDir = Path.Combine(logRootDir, projName);
            if (!Directory.Exists(projLogDir)) return 0;

            try
            {
                return Directory.GetFiles(projLogDir, "*", SearchOption.AllDirectories).Length;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LogCleaner.CountProjectLogFiles");
                return 0;
            }
        }

        /// <summary>
        /// 시작 시 자동 퍼지: log_retention.txt를 읽어 days>0인 프로젝트의 로그 폴더를 순회하며,
        /// 마지막 수정시간이 days를 초과한 파일만 삭제. 다른 프로젝트/통합 로그는 건드리지 않음.
        /// 반환 = 삭제된 파일 수.
        /// </summary>
        public static int PurgeExpiredOnStartup()
        {
            int deleted = 0;
            try
            {
                string baseDir = Application.StartupPath;
                string logRootDir = Path.Combine(baseDir, "codeinspect_logs");
                if (!Directory.Exists(logRootDir)) return 0;

                Dictionary<string, int> map = LogRetentionConfig.LoadMap();
                if (map == null || map.Count == 0) return 0;

                DateTime now = DateTime.Now;
                foreach (var kv in map)
                {
                    int days = kv.Value;
                    if (days <= 0) continue;
                    string projName = GetProjectLogFolderName(kv.Key);
                    if (string.IsNullOrEmpty(projName)) continue;

                    string projLogDir = Path.Combine(logRootDir, projName);
                    if (!Directory.Exists(projLogDir)) continue;

                    string[] files;
                    try { files = Directory.GetFiles(projLogDir, "*", SearchOption.AllDirectories); }
                    catch (Exception exList)
                    {
                        ErrorLogger.Log(exList, "LogCleaner.PurgeExpiredOnStartup/List");
                        continue;
                    }

                    foreach (string f in files)
                    {
                        try
                        {
                            DateTime mtime = File.GetLastWriteTime(f);
                            double age = (now - mtime).TotalDays;
                            if (age > days)
                            {
                                File.Delete(f);
                                deleted++;
                            }
                        }
                        catch (Exception exFile)
                        {
                            ErrorLogger.Log(exFile, "LogCleaner.PurgeExpiredOnStartup/File");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LogCleaner.PurgeExpiredOnStartup");
            }
            return deleted;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  LogDeleteForm — 로그 삭제 다이얼로그
    // ════════════════════════════════════════════════════════════════
    public class LogDeleteForm : Form
    {
        private readonly string _logDir;
        private readonly List<string> _projectPaths;

        private DataGridView dgv;
        private Button btnDeleteAll;
        private Button btnDeleteSelectedProject;
        private NumericUpDown numBulkDays;
        private Button btnApplyBulk;
        private Button btnOK;
        private Button btnCancel;
        private Label lblLogDirInfo;
        private Label lblFootNote;

        // 컬럼 인덱스
        private const int COL_PROJECT = 0;
        private const int COL_DAYS = 1;
        private const int COL_FILES = 2;

        public LogDeleteForm(string logDir, IList<string> projectPaths)
        {
            _logDir = logDir ?? "";
            _projectPaths = new List<string>();
            if (projectPaths != null)
            {
                foreach (var p in projectPaths)
                {
                    if (!string.IsNullOrEmpty(p)) _projectPaths.Add(p);
                }
            }

            InitializeUI();
            LoadGrid();
        }

        private void InitializeUI()
        {
            this.Text = "로그 삭제";
            this.Size = new Size(720, 560);
            this.MinimumSize = new Size(720, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("맑은 고딕", 9F);

            // ── 상단 안내 ──
            lblLogDirInfo = new Label
            {
                Text = "로그 폴더: " + _logDir,
                Location = new Point(15, 12),
                AutoSize = false,
                Size = new Size(680, 18),
                ForeColor = Color.FromArgb(73, 80, 87),
                Font = new Font("맑은 고딕", 9F)
            };

            // ── 그룹박스 ① 전체 삭제 ──
            var grpAll = new GroupBox
            {
                Text = "  전체 로그 삭제  ",
                Location = new Point(15, 38),
                Size = new Size(680, 70),
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold)
            };

            var lblAll = new Label
            {
                Text = "codeinspect_logs 폴더 내 모든 파일과 하위 폴더가 삭제됩니다.",
                Location = new Point(12, 28),
                AutoSize = true,
                Font = new Font("맑은 고딕", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(73, 80, 87)
            };

            btnDeleteAll = new Button
            {
                Text = "전체 삭제",
                Location = new Point(540, 25),
                Size = new Size(125, 30),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold)
            };
            btnDeleteAll.FlatAppearance.BorderSize = 0;
            btnDeleteAll.Click += BtnDeleteAll_Click;

            grpAll.Controls.AddRange(new Control[] { lblAll, btnDeleteAll });

            // ── 그룹박스 ② 프로젝트별 관리 ──
            var grpProj = new GroupBox
            {
                Text = "  프로젝트별 로그 관리  ",
                Location = new Point(15, 118),
                Size = new Size(680, 320),
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold)
            };

            dgv = new DataGridView
            {
                Location = new Point(12, 28),
                Size = new Size(656, 200),
                Font = new Font("맑은 고딕", 9F),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };

            var colProj = new DataGridViewTextBoxColumn
            {
                HeaderText = "프로젝트 경로",
                Width = 420,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colDays = new DataGridViewTextBoxColumn
            {
                HeaderText = "삭제주기(일)",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colDays.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            var colFiles = new DataGridViewTextBoxColumn
            {
                HeaderText = "파일 수",
                Width = 90,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colFiles.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colFiles.DefaultCellStyle.ForeColor = Color.Gray;

            dgv.Columns.Add(colProj);
            dgv.Columns.Add(colDays);
            dgv.Columns.Add(colFiles);

            dgv.CellEndEdit += Dgv_CellEndEdit;
            dgv.SelectionChanged += Dgv_SelectionChanged;

            // 선택 프로젝트 로그 삭제 버튼
            btnDeleteSelectedProject = new Button
            {
                Text = "선택 프로젝트 로그 삭제",
                Location = new Point(12, 238),
                Size = new Size(180, 30),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("맑은 고딕", 9F),
                Enabled = false
            };
            btnDeleteSelectedProject.FlatAppearance.BorderSize = 0;
            btnDeleteSelectedProject.Click += BtnDeleteSelectedProject_Click;

            // 일괄 적용
            var lblBulk = new Label
            {
                Text = "일괄 적용 주기(일):",
                Location = new Point(220, 245),
                AutoSize = true,
                Font = new Font("맑은 고딕", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            numBulkDays = new NumericUpDown
            {
                Location = new Point(345, 242),
                Size = new Size(70, 25),
                Minimum = 0,
                Maximum = 3650,
                Value = 0,
                TextAlign = HorizontalAlignment.Right
            };

            btnApplyBulk = new Button
            {
                Text = "전체 일괄 적용",
                Location = new Point(425, 240),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("맑은 고딕", 9F)
            };
            btnApplyBulk.FlatAppearance.BorderSize = 0;
            btnApplyBulk.Click += BtnApplyBulk_Click;

            lblFootNote = new Label
            {
                Text = "ⓘ 0 = 자동 삭제 안 함. 변경값은 [확인] 버튼을 눌러야 저장됩니다.   "
                     + "통합 로그(루트 직접 파일)는 자동 삭제 대상이 아니며, 전체 삭제로만 정리됩니다.",
                Location = new Point(12, 278),
                AutoSize = false,
                Size = new Size(656, 32),
                Font = new Font("맑은 고딕", 8.5F, FontStyle.Regular),
                ForeColor = Color.Gray
            };

            grpProj.Controls.AddRange(new Control[] {
                dgv, btnDeleteSelectedProject,
                lblBulk, numBulkDays, btnApplyBulk,
                lblFootNote
            });

            // ── OK / Cancel ──
            btnOK = new Button
            {
                Text = "확인 (저장)",
                Location = new Point(490, 460),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "취소",
                Location = new Point(595, 460),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.System,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] {
                lblLogDirInfo, grpAll, grpProj, btnOK, btnCancel
            });

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void LoadGrid()
        {
            Dictionary<string, int> map = LogRetentionConfig.LoadMap();

            dgv.Rows.Clear();
            foreach (string projPath in _projectPaths)
            {
                int days = 0;
                if (map.ContainsKey(projPath)) days = map[projPath];
                int fileCount = LogCleaner.CountProjectLogFiles(_logDir, projPath);

                int row = dgv.Rows.Add(new object[] {
                    projPath,
                    days.ToString(CultureInfo.InvariantCulture),
                    fileCount.ToString(CultureInfo.InvariantCulture)
                });
                dgv.Rows[row].Tag = projPath;
            }

            UpdateSelectedDeleteButtonState();
        }

        // 셀 편집 종료 시 정수 검증
        private void Dgv_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgv.Rows.Count) return;
            if (e.ColumnIndex != COL_DAYS) return;

            object val = dgv.Rows[e.RowIndex].Cells[COL_DAYS].Value;
            string s = (val == null) ? "" : val.ToString().Trim();

            int n;
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 0)
            {
                n = 0;
            }
            dgv.Rows[e.RowIndex].Cells[COL_DAYS].Value = n.ToString(CultureInfo.InvariantCulture);
        }

        private void Dgv_SelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectedDeleteButtonState();
        }

        private void UpdateSelectedDeleteButtonState()
        {
            if (dgv == null || btnDeleteSelectedProject == null) return;
            bool enabled = false;
            if (dgv.SelectedRows.Count > 0)
            {
                var row = dgv.SelectedRows[0];
                string projPath = row.Tag as string;
                if (!string.IsNullOrEmpty(projPath))
                {
                    int cnt = LogCleaner.CountProjectLogFiles(_logDir, projPath);
                    enabled = cnt > 0;
                }
            }
            btnDeleteSelectedProject.Enabled = enabled;
        }

        // ── 핸들러: 전체 삭제 ──
        private void BtnDeleteAll_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_logDir) || !Directory.Exists(_logDir))
            {
                MessageBox.Show("로그 폴더가 존재하지 않습니다.\n" + _logDir,
                    "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ans = MessageBox.Show(
                "codeinspect_logs 폴더 안의 모든 로그 파일과 하위 폴더가 영구 삭제됩니다.\n\n계속 진행하시겠습니까?",
                "전체 삭제 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (ans != DialogResult.Yes) return;

            int deleted = LogCleaner.DeleteAll(_logDir);
            RefreshFileCounts();
            UpdateSelectedDeleteButtonState();

            MessageBox.Show(string.Format("삭제 완료: 파일 {0}건", deleted),
                "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── 핸들러: 선택 프로젝트 로그 삭제 ──
        private void BtnDeleteSelectedProject_Click(object sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0) return;
            var row = dgv.SelectedRows[0];
            string projPath = row.Tag as string;
            if (string.IsNullOrEmpty(projPath)) return;

            string projName = LogCleaner.GetProjectLogFolderName(projPath);
            string projLogDir = Path.Combine(_logDir, projName);

            if (!Directory.Exists(projLogDir))
            {
                MessageBox.Show("선택한 프로젝트의 로그 폴더가 없습니다.\n" + projLogDir,
                    "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ans = MessageBox.Show(
                string.Format("다음 폴더 안의 모든 로그 파일이 삭제됩니다:\n\n{0}\n\n계속 진행하시겠습니까?", projLogDir),
                "프로젝트 로그 삭제 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (ans != DialogResult.Yes) return;

            int deleted = LogCleaner.DeleteProjectLogs(_logDir, projPath);
            RefreshFileCounts();
            UpdateSelectedDeleteButtonState();

            MessageBox.Show(string.Format("삭제 완료: 파일 {0}건", deleted),
                "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── 핸들러: 일괄 적용 ──
        private void BtnApplyBulk_Click(object sender, EventArgs e)
        {
            int days = (int)numBulkDays.Value;
            string daysStr = days.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                dgv.Rows[i].Cells[COL_DAYS].Value = daysStr;
            }
        }

        // ── 핸들러: 확인(저장) ──
        private void BtnOK_Click(object sender, EventArgs e)
        {
            // 편집 중인 셀을 강제 커밋
            if (dgv.IsCurrentCellInEditMode) dgv.EndEdit();

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                string projPath = dgv.Rows[i].Tag as string;
                if (string.IsNullOrEmpty(projPath)) continue;

                object val = dgv.Rows[i].Cells[COL_DAYS].Value;
                string s = (val == null) ? "" : val.ToString().Trim();

                int n;
                if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 0)
                    n = 0;

                map[projPath] = n;
            }

            LogRetentionConfig.Save(map);
            // DialogResult.OK는 버튼에 이미 설정되어 있어 자동 close
        }

        private void RefreshFileCounts()
        {
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                string projPath = dgv.Rows[i].Tag as string;
                if (string.IsNullOrEmpty(projPath)) continue;
                int cnt = LogCleaner.CountProjectLogFiles(_logDir, projPath);
                dgv.Rows[i].Cells[COL_FILES].Value = cnt.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}

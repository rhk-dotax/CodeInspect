using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CodeInspect
{
    /// <summary>CodeInspect 메인 GUI - 다중 프로젝트 지원</summary>
    public class MainForm : Form
    {
        // ── 프로젝트 관리 패널 ──
        private ListBox lstProjects;
        private Button btnAddProject;
        private Button btnRemoveProject;
        private Button btnAnalyzeSelected;
        private Button btnAnalyzeAll;
        private Button btnInstallHookSelected;
        private Button btnRemoveHookSelected;
        private Button btnManageRules;
        private Button btnViewRules;
        private Button btnUpdateRules;
        private Label lblRuleCount;
        private Label lblProjectCount;

        // ── 결과 영역 ──
        private DataGridView dgvResults;
        private Label lblStatus;
        private ProgressBar progressBar;
        private Panel pnlSummary;
        private Label lblTotal, lblCritical, lblHigh, lblMedium, lblLow;
        private ComboBox cboSeverityFilter;
        private ComboBox cboProjectFilter;
        private TextBox txtSearchFilter;
        private Button btnExportCsv;
        private Button btnClear;
        private Label lblLogDir;

        // ── 데이터 ──
        private List<Finding> _findings = new List<Finding>();
        private List<Finding> _filteredFindings = new List<Finding>();
        private CodeAnalyzer _analyzer;
        private string _logDir;

        // 프로젝트 설정 파일 경로
        private string _configPath;

        public MainForm()
        {
            InitializeComponent();
            _logDir = Path.Combine(Application.StartupPath, "codeinspect_logs");
            _configPath = Path.Combine(Application.StartupPath, "codeinspect_projects.txt");
            LoadProjects();
            UpdateRuleCount();
        }

        private void UpdateRuleCount()
        {
            try
            {
                var rules = RuleStore.Load();
                lblRuleCount.Text = string.Format("({0}개)", rules.Count);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "MainForm.UpdateRuleCount");
                lblRuleCount.Text = "(?)";
            }
        }

        private void InitializeComponent()
        {
            this.Text = "CodeInspect - 코드 취약점 분석기 v1.2";
            this.Size = new Size(1300, 920);
            this.MinimumSize = new Size(1000, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("맑은 고딕", 9F);

            // ════════════════════════════════════════
            //  상단: 타이틀
            // ════════════════════════════════════════
            var pnlTitle = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(52, 58, 64) };
            var lblTitle = new Label
            {
                Text = "  CodeInspect - 소스코드 취약점 정적분석 도구",
                Font = new Font("맑은 고딕", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblSubTitle = new Label
            {
                Text = "C, C++, Java, C#  |  오프라인 무설치  |  다중 프로젝트 지원  ",
                Font = new Font("맑은 고딕", 9F),
                ForeColor = Color.FromArgb(173, 181, 189),
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 400,
                TextAlign = ContentAlignment.MiddleRight
            };
            pnlTitle.Controls.AddRange(new Control[] { lblTitle, lblSubTitle });
            this.Controls.Add(pnlTitle);

            // ════════════════════════════════════════
            //  왼쪽: 프로젝트 관리 패널
            // ════════════════════════════════════════
            var pnlLeft = new Panel
            {
                Dock = DockStyle.Left,
                Width = 310,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(248, 249, 250),
                BorderStyle = BorderStyle.None
            };

            var lblProjHeader = new Label
            {
                Text = "프로젝트 목록",
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                Location = new Point(8, 8),
                AutoSize = true
            };

            lblProjectCount = new Label
            {
                Text = "(0개)",
                Font = new Font("맑은 고딕", 9F),
                ForeColor = Color.Gray,
                Location = new Point(110, 10),
                AutoSize = true
            };

            lstProjects = new ListBox
            {
                Location = new Point(8, 32),
                Size = new Size(290, 340),
                Font = new Font("맑은 고딕", 9F),
                SelectionMode = SelectionMode.MultiExtended,
                HorizontalScrollbar = true
            };
            lstProjects.SelectedIndexChanged += LstProjects_SelectedIndexChanged;

            btnAddProject = new Button
            {
                Text = "+ 프로젝트 추가",
                Location = new Point(8, 378),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAddProject.Click += BtnAddProject_Click;

            btnRemoveProject = new Button
            {
                Text = "- 선택 제거",
                Location = new Point(155, 378),
                Size = new Size(143, 30),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRemoveProject.Click += BtnRemoveProject_Click;

            // 구분선
            var separator1 = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(8, 418),
                Size = new Size(290, 2)
            };

            btnAnalyzeSelected = new Button
            {
                Text = "▶ 선택 프로젝트 분석",
                Location = new Point(8, 428),
                Size = new Size(290, 35),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold)
            };
            btnAnalyzeSelected.Click += BtnAnalyzeSelected_Click;

            btnAnalyzeAll = new Button
            {
                Text = "▶▶ 전체 프로젝트 분석",
                Location = new Point(8, 470),
                Size = new Size(290, 35),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold)
            };
            btnAnalyzeAll.Click += BtnAnalyzeAll_Click;

            // 구분선
            var separator2 = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(8, 516),
                Size = new Size(290, 2)
            };

            btnInstallHookSelected = new Button
            {
                Text = "Git Hook 설치 (선택 프로젝트)",
                Location = new Point(8, 526),
                Size = new Size(142, 30),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnInstallHookSelected.Click += BtnInstallHookSelected_Click;

            btnRemoveHookSelected = new Button
            {
                Text = "Git Hook 제거",
                Location = new Point(156, 526),
                Size = new Size(142, 30),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRemoveHookSelected.Click += BtnRemoveHookSelected_Click;

            // 구분선 + 분석 룰셋 섹션
            var separator3 = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(8, 566),
                Size = new Size(290, 2)
            };

            var lblRuleHeader = new Label
            {
                Text = "분석 룰셋",
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                Location = new Point(8, 575),
                AutoSize = true
            };
            lblRuleCount = new Label
            {
                Text = "(0개)",
                Font = new Font("맑은 고딕", 9F),
                ForeColor = Color.Gray,
                Location = new Point(80, 577),
                AutoSize = true
            };

            btnManageRules = new Button
            {
                Text = "룰셋 관리 (리스트 추가/수정/삭제)",
                Location = new Point(8, 600),
                Size = new Size(290, 30),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnManageRules.Click += BtnManageRules_Click;

            btnViewRules = new Button
            {
                Text = "룰셋 보기 (notepad)",
                Location = new Point(8, 635),
                Size = new Size(142, 30),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnViewRules.Click += BtnViewRules_Click;

            btnUpdateRules = new Button
            {
                Text = "룰셋 업데이트",
                Location = new Point(156, 635),
                Size = new Size(142, 30),
                BackColor = Color.FromArgb(23, 162, 184),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnUpdateRules.Click += BtnUpdateRules_Click;

            pnlLeft.Controls.AddRange(new Control[] {
                lblProjHeader, lblProjectCount, lstProjects,
                btnAddProject, btnRemoveProject,
                separator1, btnAnalyzeSelected, btnAnalyzeAll,
                separator2, btnInstallHookSelected, btnRemoveHookSelected,
                separator3, lblRuleHeader, lblRuleCount,
                btnManageRules, btnViewRules, btnUpdateRules
            });
            this.Controls.Add(pnlLeft);

            // ════════════════════════════════════════
            //  중앙 + 우측: 결과 영역
            // ════════════════════════════════════════

            // 요약 패널
            pnlSummary = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(8), BackColor = Color.White };

            lblTotal = CreateSummaryLabel("전체: 0", Color.FromArgb(33, 37, 41), 10, 5);
            lblCritical = CreateSummaryLabel("CRITICAL: 0", Color.FromArgb(220, 53, 69), 120, 5);
            lblHigh = CreateSummaryLabel("HIGH: 0", Color.FromArgb(255, 128, 0), 270, 5);
            lblMedium = CreateSummaryLabel("MEDIUM: 0", Color.FromArgb(255, 193, 7), 380, 5);
            lblLow = CreateSummaryLabel("LOW: 0", Color.FromArgb(108, 117, 125), 510, 5);

            var lblFilterProj = new Label { Text = "프로젝트:", Location = new Point(10, 33), AutoSize = true, Font = new Font("맑은 고딕", 8.5F) };
            cboProjectFilter = new ComboBox
            {
                Location = new Point(72, 30),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboProjectFilter.Items.Add("전체");
            cboProjectFilter.SelectedIndex = 0;
            cboProjectFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            var lblFilterSev = new Label { Text = "심각도:", Location = new Point(232, 33), AutoSize = true, Font = new Font("맑은 고딕", 8.5F) };
            cboSeverityFilter = new ComboBox
            {
                Location = new Point(282, 30),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Items = { "전체", "CRITICAL", "HIGH", "MEDIUM", "LOW" }
            };
            cboSeverityFilter.SelectedIndex = 0;
            cboSeverityFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            var lblSearch = new Label { Text = "검색:", Location = new Point(392, 33), AutoSize = true, Font = new Font("맑은 고딕", 8.5F) };
            txtSearchFilter = new TextBox { Location = new Point(430, 30), Width = 140 };
            txtSearchFilter.TextChanged += (s, e) => ApplyFilters();

            btnExportCsv = new Button { Text = "CSV 저장", Location = new Point(580, 28), Width = 75, Height = 25 };
            btnExportCsv.Click += BtnExportCsv_Click;

            btnClear = new Button { Text = "초기화", Location = new Point(660, 28), Width = 60, Height = 25 };
            btnClear.Click += (s, e) => {
                _findings.Clear(); _filteredFindings.Clear(); dgvResults.Rows.Clear();
                UpdateSummary(); UpdateProjectFilter();
            };

            lblLogDir = new Label
            {
                Text = "",
                Location = new Point(730, 33),
                AutoSize = true,
                ForeColor = Color.Blue,
                Cursor = Cursors.Hand,
                Font = new Font("맑은 고딕", 8F, FontStyle.Underline)
            };
            lblLogDir.Click += (s, e) =>
            {
                if (Directory.Exists(_logDir))
                    System.Diagnostics.Process.Start("explorer.exe", _logDir);
            };

            pnlSummary.Controls.AddRange(new Control[] {
                lblTotal, lblCritical, lblHigh, lblMedium, lblLow,
                lblFilterProj, cboProjectFilter, lblFilterSev, cboSeverityFilter,
                lblSearch, txtSearchFilter, btnExportCsv, btnClear, lblLogDir
            });
            this.Controls.Add(pnlSummary);

            // 결과 그리드
            dgvResults = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 249, 250) },
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("맑은 고딕", 8.5F) },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(52, 58, 64),
                    ForeColor = Color.White,
                    Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                EnableHeadersVisualStyles = false
            };

            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "No", HeaderText = "No", Width = 40 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Project", HeaderText = "프로젝트", Width = 100 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Severity", HeaderText = "심각도", Width = 72 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleId", HeaderText = "규칙ID", Width = 90 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = "검출파일", Width = 250 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Line", HeaderText = "라인", Width = 45 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "CWE분류", Width = 170 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reason", HeaderText = "검출사유", Width = 300 });
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "검출코드", Width = 220 });

            dgvResults.CellFormatting += DgvResults_CellFormatting;
            this.Controls.Add(dgvResults);

            // 하단 상태바
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            lblStatus = new Label
            {
                Text = "준비됨 - 프로젝트를 추가하고 분석을 시작하세요",
                Dock = DockStyle.Left, AutoSize = false, Width = 800,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };
            progressBar = new ProgressBar { Dock = DockStyle.Right, Width = 300, Style = ProgressBarStyle.Continuous };
            pnlBottom.Controls.AddRange(new Control[] { lblStatus, progressBar });
            this.Controls.Add(pnlBottom);

            // Dock 순서 (아래→위로 쌓임)
            pnlBottom.BringToFront();
            dgvResults.BringToFront();
            pnlSummary.BringToFront();
            pnlLeft.BringToFront();
            pnlTitle.BringToFront();
        }

        private Label CreateSummaryLabel(string text, Color color, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                ForeColor = color
            };
        }

        // ════════════════════════════════════════
        //  프로젝트 관리
        // ════════════════════════════════════════

        private void BtnAddProject_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "분석할 프로젝트 소스코드 폴더를 선택하세요";
                fbd.ShowNewFolderButton = false;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string path = fbd.SelectedPath;
                    // 중복 확인
                    for (int i = 0; i < lstProjects.Items.Count; i++)
                    {
                        if (lstProjects.Items[i].ToString().Equals(path, StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("이미 등록된 프로젝트입니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                    }
                    lstProjects.Items.Add(path);
                    UpdateProjectCount();
                    SaveProjects();
                }
            }
        }

        private void BtnRemoveProject_Click(object sender, EventArgs e)
        {
            if (lstProjects.SelectedIndices.Count == 0)
            {
                MessageBox.Show("제거할 프로젝트를 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // 뒤에서부터 제거 (인덱스 유지)
            var indices = new List<int>();
            foreach (int idx in lstProjects.SelectedIndices) indices.Add(idx);
            indices.Sort();
            indices.Reverse();
            foreach (int idx in indices) lstProjects.Items.RemoveAt(idx);
            UpdateProjectCount();
            SaveProjects();
        }

        private void LstProjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            int count = lstProjects.SelectedIndices.Count;
            btnAnalyzeSelected.Text = count > 0
                ? string.Format("▶ 선택 프로젝트 분석 ({0}개)", count)
                : "▶ 선택 프로젝트 분석";
        }

        private void UpdateProjectCount()
        {
            lblProjectCount.Text = string.Format("({0}개)", lstProjects.Items.Count);
        }

        // 프로젝트 목록 저장/불러오기
        private void SaveProjects()
        {
            try
            {
                var lines = new List<string>();
                foreach (var item in lstProjects.Items) lines.Add(item.ToString());
                File.WriteAllLines(_configPath, lines.ToArray(), System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "MainForm.SaveProjects");
            }
        }

        private void LoadProjects()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string[] lines = File.ReadAllLines(_configPath, System.Text.Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && Directory.Exists(trimmed))
                            lstProjects.Items.Add(trimmed);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "MainForm.LoadProjects");
            }
            UpdateProjectCount();
        }

        // ════════════════════════════════════════
        //  분석 실행
        // ════════════════════════════════════════

        private void BtnAnalyzeSelected_Click(object sender, EventArgs e)
        {
            if (lstProjects.SelectedIndices.Count == 0)
            {
                MessageBox.Show("분석할 프로젝트를 선택해주세요.\n(Ctrl+클릭으로 여러 개 선택 가능)", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var paths = new List<string>();
            foreach (int idx in lstProjects.SelectedIndices)
                paths.Add(lstProjects.Items[idx].ToString());
            RunAnalysis(paths);
        }

        private void BtnAnalyzeAll_Click(object sender, EventArgs e)
        {
            if (lstProjects.Items.Count == 0)
            {
                MessageBox.Show("등록된 프로젝트가 없습니다.\n먼저 프로젝트를 추가해주세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var paths = new List<string>();
            foreach (var item in lstProjects.Items)
                paths.Add(item.ToString());
            RunAnalysis(paths);
        }

        private void RunAnalysis(List<string> projectPaths)
        {
            btnAnalyzeSelected.Enabled = false;
            btnAnalyzeAll.Enabled = false;
            _findings.Clear();
            dgvResults.Rows.Clear();
            progressBar.Value = 0;

            // 최신 룰셋 반영 (파일 기반이므로 편집 후 즉시 적용됨)
            _analyzer = new CodeAnalyzer();

            int totalProjects = projectPaths.Count;
            lblStatus.Text = string.Format("분석 중... (0/{0} 프로젝트)", totalProjects);

            var worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;

            worker.DoWork += (ws, we) =>
            {
                var allFindings = new List<Finding>();
                for (int p = 0; p < projectPaths.Count; p++)
                {
                    string projPath = projectPaths[p];
                    string projName = Path.GetFileName(projPath);

                    var findings = _analyzer.AnalyzeDirectory(projPath, (current, total, file, count) =>
                    {
                        int overallPct = (int)(((double)p / totalProjects + (double)current / total / totalProjects) * 100);
                        string msg = string.Format("[{0}/{1}] {2} - {3}",
                            p + 1, totalProjects, projName, Path.GetFileName(file));
                        worker.ReportProgress(Math.Min(overallPct, 100), msg);
                    });

                    // 각 Finding에 프로젝트명 태깅 (FilePath에서 유추)
                    foreach (var f in findings) f.MatchedCode = projName + "|" + f.MatchedCode;

                    allFindings.AddRange(findings);

                    // 프로젝트별 로그 저장
                    string projLogDir = Path.Combine(_logDir, projName);
                    LogWriter.WriteCommitLog(findings, projLogDir, projPath);
                    LogWriter.WriteCsvLog(findings, projLogDir, projPath);
                }

                // 통합 로그도 저장
                if (allFindings.Count > 0)
                {
                    LogWriter.WriteCommitLog(allFindings, _logDir, null);
                    LogWriter.WriteCsvLog(allFindings, _logDir, null);
                }

                we.Result = allFindings;
            };

            worker.ProgressChanged += (ws, we) =>
            {
                progressBar.Value = Math.Min(we.ProgressPercentage, 100);
                lblStatus.Text = (string)we.UserState;
            };

            worker.RunWorkerCompleted += (ws, we) =>
            {
                btnAnalyzeSelected.Enabled = true;
                btnAnalyzeAll.Enabled = true;

                if (we.Error != null)
                {
                    ErrorLogger.Log(we.Error, "MainForm.RunAnalysis (BackgroundWorker)");
                    MessageBox.Show("분석 중 오류 발생: " + we.Error.Message, "오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "오류 발생";
                    return;
                }

                _findings = (List<Finding>)we.Result;
                progressBar.Value = 100;

                lblLogDir.Text = "로그 폴더 열기";
                UpdateProjectFilter();
                ApplyFilters();
                UpdateSummary();

                lblStatus.Text = string.Format("분석 완료 - {0}개 프로젝트, {1}건 검출",
                    projectPaths.Count, _findings.Count);
            };

            worker.RunWorkerAsync();
        }

        // ════════════════════════════════════════
        //  Git Hook 설치
        // ════════════════════════════════════════

        private void BtnInstallHookSelected_Click(object sender, EventArgs e)
        {
            if (lstProjects.SelectedIndices.Count == 0)
            {
                MessageBox.Show("Hook을 설치할 프로젝트를 선택해주세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int installed = 0;
            int failed = 0;
            var messages = new List<string>();

            foreach (int idx in lstProjects.SelectedIndices)
            {
                string path = lstProjects.Items[idx].ToString();
                string projName = Path.GetFileName(path);
                string result = InstallHookForProject(path);

                if (result == null)
                {
                    installed++;
                    messages.Add("[OK] " + projName);
                }
                else
                {
                    failed++;
                    messages.Add("[FAIL] " + projName + " - " + result);
                }
            }

            string summary = string.Format("Hook 설치 결과\n\n성공: {0}개  /  실패: {1}개\n\n{2}",
                installed, failed, string.Join("\n", messages.ToArray()));

            MessageBox.Show(summary,
                "Git Hook 설치", MessageBoxButtons.OK,
                failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private string InstallHookForProject(string projectPath)
        {
            string gitDir = FindGitDir(projectPath);
            if (gitDir == null)
                return "Git 저장소가 아닙니다 (.git 없음)";

            try
            {
                string hooksDir = Path.Combine(gitDir, "hooks");
                Directory.CreateDirectory(hooksDir);

                string hookPath = Path.Combine(hooksDir, "pre-commit");
                string exePath = Path.Combine(Application.StartupPath, "CodeInspect.exe");

                // 기존 hook 백업
                if (File.Exists(hookPath))
                {
                    string backup = hookPath + ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    File.Copy(hookPath, backup, true);
                }

                string hookContent = string.Format(@"#!/bin/sh
# CodeInspect Pre-Commit Hook
# 커밋 시 취약점 분석 후 로그 기록 (커밋은 항상 진행)

CODEINSPECT_EXE=""{0}""

if [ -f ""$CODEINSPECT_EXE"" ]; then
    STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep -E '\.(c|h|cpp|cxx|cc|hpp|hxx|hh|java|cs)$')

    if [ -n ""$STAGED_FILES"" ]; then
        echo ""[CodeInspect] 커밋 파일 취약점 분석 중...""

        TMPFILE=$(mktemp)
        echo ""$STAGED_FILES"" > ""$TMPFILE""

        ""$CODEINSPECT_EXE"" --hook ""$TMPFILE"" ""$(git rev-parse --show-toplevel)""
        RESULT=$?

        rm -f ""$TMPFILE""

        if [ $RESULT -ne 0 ]; then
            echo ""[CodeInspect] 취약점이 검출되었습니다. 로그를 확인해주세요.""
            echo ""[CodeInspect] (커밋은 정상 진행됩니다)""
        else
            echo ""[CodeInspect] 취약점이 검출되지 않았습니다.""
        fi
    fi
fi

exit 0
", exePath.Replace("\\", "/"));

                File.WriteAllText(hookPath, hookContent, new System.Text.UTF8Encoding(false));

                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "update-index --chmod=+x \"" + hookPath + "\"",
                        WorkingDirectory = Path.GetDirectoryName(gitDir),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception chmodEx)
                {
                    ErrorLogger.Log(chmodEx, "MainForm.InstallHookForProject (chmod+x)");
                }

                return null; // 성공
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "MainForm.InstallHookForProject");
                return ex.Message;
            }
        }

        // ════════════════════════════════════════
        //  Git Hook 제거
        // ════════════════════════════════════════

        private void BtnRemoveHookSelected_Click(object sender, EventArgs e)
        {
            if (lstProjects.SelectedIndices.Count == 0)
            {
                MessageBox.Show("Hook을 제거할 프로젝트를 선택해주세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int removed = 0;
            int notFound = 0;
            int failed = 0;
            var messages = new List<string>();

            foreach (int idx in lstProjects.SelectedIndices)
            {
                string path = lstProjects.Items[idx].ToString();
                string projName = Path.GetFileName(path);
                string result = RemoveHookForProject(path);

                if (result == null)
                {
                    removed++;
                    messages.Add("[OK] " + projName + " - hook 제거됨");
                }
                else if (result == "NOT_FOUND")
                {
                    notFound++;
                    messages.Add("[SKIP] " + projName + " - hook이 없거나 Git 저장소 아님");
                }
                else
                {
                    failed++;
                    messages.Add("[FAIL] " + projName + " - " + result);
                }
            }

            string summary = string.Format(
                "Hook 제거 결과\n\n제거: {0}개  /  해당없음: {1}개  /  실패: {2}개\n\n{3}",
                removed, notFound, failed, string.Join("\n", messages.ToArray()));

            MessageBox.Show(summary, "Git Hook 제거", MessageBoxButtons.OK,
                failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        /// <summary>프로젝트의 pre-commit hook 제거. null=성공, "NOT_FOUND"=없음, 그 외=오류</summary>
        private string RemoveHookForProject(string projectPath)
        {
            string gitDir = FindGitDir(projectPath);
            if (gitDir == null) return "NOT_FOUND";

            string hookPath = Path.Combine(gitDir, "hooks", "pre-commit");
            if (!File.Exists(hookPath)) return "NOT_FOUND";

            try
            {
                // CodeInspect hook인지 확인
                string content = File.ReadAllText(hookPath);
                if (!content.Contains("CodeInspect"))
                {
                    // CodeInspect가 아닌 다른 hook → 함부로 삭제하지 않음
                    var answer = MessageBox.Show(
                        Path.GetFileName(projectPath) + " 의 pre-commit hook은 CodeInspect가 설치한 것이 아닙니다.\n그래도 제거하시겠습니까?",
                        "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer != DialogResult.Yes) return "NOT_FOUND";
                }

                // 백업 후 삭제
                string backup = hookPath + ".removed_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(hookPath, backup, true);
                File.Delete(hookPath);
                return null; // 성공
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "MainForm.RemoveHookForProject");
                return ex.Message;
            }
        }

        // ════════════════════════════════════════
        //  분석 룰셋 관리
        // ════════════════════════════════════════

        private void BtnManageRules_Click(object sender, EventArgs e)
        {
            using (var dlg = new RulesEditorForm())
            {
                dlg.ShowDialog(this);
                UpdateRuleCount();
            }
        }

        private void BtnViewRules_Click(object sender, EventArgs e)
        {
            try
            {
                // 파일 없으면 기본 룰로 시드해서 생성
                if (!File.Exists(RuleStore.RulesFile))
                {
                    RuleStore.Load();
                }
                else
                {
                    // 편집 전 현재 파일을 백업하여 "이전 버전" 보존
                    RuleStore.Backup();
                }

                System.Diagnostics.Process.Start("notepad.exe", "\"" + RuleStore.RulesFile + "\"");

                MessageBox.Show(
                    "notepad에서 룰셋 파일을 편집 후 저장하세요.\n"
                    + "다음 분석 실행 시 변경사항이 자동 반영됩니다.\n\n"
                    + "이전 버전은 backup 폴더에 저장되었습니다:\n" + RuleStore.BackupDir,
                    "룰셋 편집", MessageBoxButtons.OK, MessageBoxIcon.Information);

                UpdateRuleCount();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "MainForm.BtnViewRules_Click");
                MessageBox.Show("파일 열기 실패: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnUpdateRules_Click(object sender, EventArgs e)
        {
            using (var dlg = new RulesUpdateDialog())
            {
                dlg.ShowDialog(this);
                UpdateRuleCount();
            }
        }

        // ════════════════════════════════════════
        //  CSV 내보내기
        // ════════════════════════════════════════

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            if (_findings.Count == 0)
            {
                MessageBox.Show("저장할 분석 결과가 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV 파일 (*.csv)|*.csv";
                sfd.FileName = "vuln_report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var target = _filteredFindings.Count > 0 ? _filteredFindings : _findings;
                    string logFile = LogWriter.WriteCsvLog(target, Path.GetDirectoryName(sfd.FileName), null);

                    if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
                    File.Move(logFile, sfd.FileName);

                    MessageBox.Show("저장 완료: " + sfd.FileName, "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // ════════════════════════════════════════
        //  필터링
        // ════════════════════════════════════════

        private void UpdateProjectFilter()
        {
            cboProjectFilter.Items.Clear();
            cboProjectFilter.Items.Add("전체");

            var projectNames = new Dictionary<string, bool>();
            foreach (var f in _findings)
            {
                string projName = ExtractProjectName(f);
                if (!projectNames.ContainsKey(projName))
                    projectNames[projName] = true;
            }
            foreach (var name in projectNames.Keys)
                cboProjectFilter.Items.Add(name);

            cboProjectFilter.SelectedIndex = 0;
        }

        private string ExtractProjectName(Finding f)
        {
            // MatchedCode에 "프로젝트명|코드" 형태로 태깅됨
            if (f.MatchedCode != null && f.MatchedCode.Contains("|"))
            {
                int sep = f.MatchedCode.IndexOf('|');
                return f.MatchedCode.Substring(0, sep);
            }
            return "-";
        }

        private string ExtractCode(Finding f)
        {
            if (f.MatchedCode != null && f.MatchedCode.Contains("|"))
            {
                int sep = f.MatchedCode.IndexOf('|');
                return f.MatchedCode.Substring(sep + 1);
            }
            return f.MatchedCode ?? "";
        }

        private void ApplyFilters()
        {
            object selProj = cboProjectFilter.SelectedItem;
            string projFilter = (selProj != null) ? selProj.ToString() : "전체";

            object selSev = cboSeverityFilter.SelectedItem;
            string sevFilter = (selSev != null) ? selSev.ToString() : "전체";

            string searchText = txtSearchFilter.Text.Trim().ToLower();

            _filteredFindings = new List<Finding>();

            foreach (var f in _findings)
            {
                if (projFilter != "전체" && ExtractProjectName(f) != projFilter) continue;
                if (sevFilter != "전체" && f.Severity != sevFilter) continue;
                if (!string.IsNullOrEmpty(searchText))
                {
                    string code = ExtractCode(f);
                    if (!f.FilePath.ToLower().Contains(searchText) &&
                        !f.Reason.ToLower().Contains(searchText) &&
                        !f.Category.ToLower().Contains(searchText) &&
                        !code.ToLower().Contains(searchText) &&
                        !f.RuleId.ToLower().Contains(searchText))
                        continue;
                }
                _filteredFindings.Add(f);
            }

            PopulateGrid(_filteredFindings);
        }

        private void PopulateGrid(List<Finding> findings)
        {
            dgvResults.Rows.Clear();

            for (int i = 0; i < findings.Count; i++)
            {
                var f = findings[i];
                string projName = ExtractProjectName(f);
                string code = ExtractCode(f);
                string fileDisplay = f.FilePath;

                // 파일 경로를 프로젝트 상대 경로로 표시
                foreach (var item in lstProjects.Items)
                {
                    string projPath = item.ToString();
                    if (f.FilePath.StartsWith(projPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string basePath = projPath;
                            if (!basePath.EndsWith("\\")) basePath += "\\";
                            Uri baseUri = new Uri(basePath);
                            Uri fullUri = new Uri(f.FilePath);
                            fileDisplay = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', '\\'));
                        }
                        catch { }
                        break;
                    }
                }

                dgvResults.Rows.Add(i + 1, projName, f.Severity, f.RuleId, fileDisplay,
                    f.LineNumber, f.Category, f.Reason, code);
            }
        }

        private void UpdateSummary()
        {
            var summary = AnalysisSummary.Create(_findings);
            lblTotal.Text = "전체: " + summary.Total;
            lblCritical.Text = "CRITICAL: " + summary.Critical;
            lblHigh.Text = "HIGH: " + summary.High;
            lblMedium.Text = "MEDIUM: " + summary.Medium;
            lblLow.Text = "LOW: " + summary.Low;
        }

        // ════════════════════════════════════════
        //  셀 포맷팅
        // ════════════════════════════════════════

        private void DgvResults_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || dgvResults.Columns[e.ColumnIndex].Name != "Severity") return;
            string val = (e.Value != null) ? e.Value.ToString() : "";
            switch (val)
            {
                case "CRITICAL":
                    e.CellStyle.BackColor = Color.FromArgb(220, 53, 69);
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.Font = new Font(dgvResults.Font, FontStyle.Bold);
                    break;
                case "HIGH":
                    e.CellStyle.BackColor = Color.FromArgb(255, 152, 0);
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.Font = new Font(dgvResults.Font, FontStyle.Bold);
                    break;
                case "MEDIUM":
                    e.CellStyle.BackColor = Color.FromArgb(255, 235, 59);
                    e.CellStyle.ForeColor = Color.Black;
                    break;
                case "LOW":
                    e.CellStyle.BackColor = Color.FromArgb(224, 224, 224);
                    e.CellStyle.ForeColor = Color.Black;
                    break;
            }
        }

        // ════════════════════════════════════════
        //  Git 유틸
        // ════════════════════════════════════════

        private string FindGitDir(string startPath)
        {
            string current = startPath;
            while (!string.IsNullOrEmpty(current))
            {
                string gitDir = Path.Combine(current, ".git");
                if (Directory.Exists(gitDir)) return gitDir;
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            return null;
        }
    }
}

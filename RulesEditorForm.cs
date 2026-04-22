using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CodeInspect
{
    /// <summary>취약점 분석 룰셋 관리 폼 (리스트 기반 추가/수정/삭제).</summary>
    public class RulesEditorForm : Form
    {
        private ListView lv;
        private Button btnAdd, btnEdit, btnDelete, btnSave, btnCancel;
        private Button btnRestore, btnOpenFile, btnOpenFolder;
        private Label lblStatus;
        private TextBox txtFilter;
        private ComboBox cboLangFilter;
        private List<VulnerabilityRule> _rules;
        private bool _dirty;

        public RulesEditorForm()
        {
            _rules = RuleStore.Load();
            _dirty = false;
            InitializeUI();
            RefreshList();
        }

        private void InitializeUI()
        {
            this.Text = "분석 룰셋 관리";
            this.Size = new Size(1050, 640);
            this.MinimumSize = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("맑은 고딕", 9F);

            // 상단 툴바
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8), BackColor = Color.FromArgb(248, 249, 250) };

            var lblFilter = new Label { Text = "검색:", Location = new Point(8, 12), AutoSize = true };
            txtFilter = new TextBox { Location = new Point(50, 9), Width = 200 };
            txtFilter.TextChanged += (s, e) => RefreshList();

            var lblLang = new Label { Text = "언어:", Location = new Point(260, 12), AutoSize = true };
            cboLangFilter = new ComboBox
            {
                Location = new Point(300, 9),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboLangFilter.Items.AddRange(new object[] { "전체", "c", "cpp", "java", "csharp" });
            cboLangFilter.SelectedIndex = 0;
            cboLangFilter.SelectedIndexChanged += (s, e) => RefreshList();

            btnOpenFile = new Button { Text = "파일 열기(notepad)", Location = new Point(420, 8), Width = 130, Height = 26 };
            btnOpenFile.Click += BtnOpenFile_Click;

            btnOpenFolder = new Button { Text = "폴더 열기", Location = new Point(555, 8), Width = 80, Height = 26 };
            btnOpenFolder.Click += BtnOpenFolder_Click;

            btnRestore = new Button
            {
                Text = "기본 CWE 룰셋 복원",
                Location = new Point(640, 8),
                Width = 150,
                Height = 26,
                BackColor = Color.FromArgb(255, 193, 7),
                FlatStyle = FlatStyle.Flat
            };
            btnRestore.Click += BtnRestore_Click;

            pnlTop.Controls.AddRange(new Control[] {
                lblFilter, txtFilter, lblLang, cboLangFilter,
                btnOpenFile, btnOpenFolder, btnRestore
            });
            this.Controls.Add(pnlTop);

            // 하단 버튼바
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(8), BackColor = Color.FromArgb(248, 249, 250) };

            btnAdd = new Button
            {
                Text = "+ 룰 추가",
                Location = new Point(8, 10),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAdd.Click += BtnAdd_Click;

            btnEdit = new Button
            {
                Text = "✎ 수정",
                Location = new Point(115, 10),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnEdit.Click += BtnEdit_Click;

            btnDelete = new Button
            {
                Text = "✕ 삭제",
                Location = new Point(222, 10),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDelete.Click += BtnDelete_Click;

            btnSave = new Button
            {
                Text = "💾 저장 (백업 생성)",
                Location = new Point(640, 10),
                Width = 180,
                Height = 32,
                BackColor = Color.FromArgb(23, 162, 184),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold)
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "취소",
                Location = new Point(825, 10),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += BtnCancel_Click;

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(8, 50),
                AutoSize = false,
                Width = 1000,
                Height = 22,
                ForeColor = Color.FromArgb(108, 117, 125)
            };

            pnlBottom.Controls.AddRange(new Control[] {
                btnAdd, btnEdit, btnDelete, btnSave, btnCancel, lblStatus
            });
            this.Controls.Add(pnlBottom);

            // 중앙 리스트뷰
            lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HideSelection = false,
                Font = new Font("맑은 고딕", 9F)
            };
            lv.Columns.Add("규칙ID", 110);
            lv.Columns.Add("언어", 90);
            lv.Columns.Add("심각도", 80);
            lv.Columns.Add("CWE분류", 170);
            lv.Columns.Add("설명", 280);
            lv.Columns.Add("정규식 패턴", 200);
            lv.Columns.Add("출처", 180);
            lv.DoubleClick += (s, e) => BtnEdit_Click(s, e);
            this.Controls.Add(lv);

            pnlTop.BringToFront();
            lv.BringToFront();
            pnlBottom.BringToFront();

            this.FormClosing += RulesEditorForm_FormClosing;
        }

        private void RefreshList()
        {
            lv.BeginUpdate();
            lv.Items.Clear();

            string search = (txtFilter.Text ?? "").Trim().ToLower();
            string lang = cboLangFilter.SelectedItem != null
                ? cboLangFilter.SelectedItem.ToString() : "전체";

            for (int i = 0; i < _rules.Count; i++)
            {
                var r = _rules[i];

                if (lang != "전체")
                {
                    bool match = false;
                    if (r.Languages != null)
                    {
                        foreach (var l in r.Languages)
                            if (string.Equals(l, lang, StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                    }
                    if (!match) continue;
                }

                if (search.Length > 0)
                {
                    string hay = (r.RuleId + " " + r.Category + " " + r.Description + " " + r.Pattern + " " + r.Reference).ToLower();
                    if (!hay.Contains(search)) continue;
                }

                var lvi = new ListViewItem(r.RuleId ?? "");
                lvi.SubItems.Add(r.Languages != null ? string.Join(",", r.Languages) : "");
                lvi.SubItems.Add(r.Severity ?? "");
                lvi.SubItems.Add(r.Category ?? "");
                lvi.SubItems.Add(r.Description ?? "");
                lvi.SubItems.Add(r.Pattern ?? "");
                lvi.SubItems.Add(r.Reference ?? "");
                lvi.Tag = i; // 원본 인덱스 보관

                // 심각도 색상
                switch ((r.Severity ?? "").ToUpper())
                {
                    case "CRITICAL": lvi.BackColor = Color.FromArgb(255, 230, 230); break;
                    case "HIGH": lvi.BackColor = Color.FromArgb(255, 245, 225); break;
                    case "MEDIUM": lvi.BackColor = Color.FromArgb(255, 253, 225); break;
                }

                lv.Items.Add(lvi);
            }
            lv.EndUpdate();

            lblStatus.Text = string.Format("표시 {0} / 전체 {1}건{2}",
                lv.Items.Count, _rules.Count, _dirty ? "   *변경됨 (저장 필요)" : "");
        }

        private int GetSelectedOriginalIndex()
        {
            if (lv.SelectedItems.Count == 0) return -1;
            return (int)lv.SelectedItems[0].Tag;
        }

        // ───────────────────────────────────────────
        //  버튼 핸들러
        // ───────────────────────────────────────────

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new RuleEditDialog(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Rule != null)
                {
                    // 중복 ID 체크
                    foreach (var r in _rules)
                    {
                        if (string.Equals(r.RuleId, dlg.Rule.RuleId, StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("동일한 규칙ID가 이미 존재합니다: " + dlg.Rule.RuleId,
                                "중복", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    _rules.Add(dlg.Rule);
                    _dirty = true;
                    RefreshList();
                }
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            int idx = GetSelectedOriginalIndex();
            if (idx < 0)
            {
                MessageBox.Show("수정할 룰을 선택하세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new RuleEditDialog(_rules[idx]))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Rule != null)
                {
                    // 다른 룰과의 ID 중복 체크
                    for (int i = 0; i < _rules.Count; i++)
                    {
                        if (i == idx) continue;
                        if (string.Equals(_rules[i].RuleId, dlg.Rule.RuleId, StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("다른 룰과 ID가 중복됩니다: " + dlg.Rule.RuleId,
                                "중복", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    _rules[idx] = dlg.Rule;
                    _dirty = true;
                    RefreshList();
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            int idx = GetSelectedOriginalIndex();
            if (idx < 0)
            {
                MessageBox.Show("삭제할 룰을 선택하세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var r = _rules[idx];
            var result = MessageBox.Show(
                string.Format("다음 규칙을 삭제하시겠습니까?\n\n{0}\n{1}",
                    r.RuleId, r.Description),
                "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _rules.RemoveAt(idx);
                _dirty = true;
                RefreshList();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                RuleStore.Save(_rules);
                _dirty = false;
                MessageBox.Show(
                    "룰셋 저장 완료.\n\n이전 버전은 다음 폴더에 백업됨:\n" + RuleStore.BackupDir,
                    "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 실패: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void BtnOpenFile_Click(object sender, EventArgs e)
        {
            try
            {
                // 파일이 없으면 먼저 현재 상태로 저장 후 연다
                if (!File.Exists(RuleStore.RulesFile))
                {
                    RuleStore.Save(_rules);
                    _dirty = false;
                }
                else if (_dirty)
                {
                    var r = MessageBox.Show(
                        "현재 변경사항이 있습니다. 파일로 먼저 저장한 후 열까요?",
                        "저장 후 열기", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel) return;
                    if (r == DialogResult.Yes)
                    {
                        RuleStore.Save(_rules);
                        _dirty = false;
                    }
                }
                else
                {
                    // 편집 전 안전을 위해 미리 백업
                    RuleStore.Backup();
                }

                System.Diagnostics.Process.Start("notepad.exe", "\"" + RuleStore.RulesFile + "\"");

                MessageBox.Show(
                    "notepad에서 편집 후 저장하세요.\n편집을 완료한 뒤 이 창을 닫으면 다음 분석부터 반영됩니다.\n\n현재 파일을 이미 백업했습니다:\n" + RuleStore.BackupDir,
                    "파일 편집", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일 열기 실패: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            try
            {
                RuleStore.EnsureDirectories();
                System.Diagnostics.Process.Start("explorer.exe", "\"" + RuleStore.RulesDir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show(
                "현재 룰셋을 내장 기본 CWE 룰셋으로 복원합니다.\n현재 파일은 백업됩니다.\n계속하시겠습니까?",
                "기본 룰셋 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            try
            {
                RuleStore.RestoreDefaults();
                _rules = RuleStore.Load();
                _dirty = false;
                RefreshList();
                MessageBox.Show("기본 룰셋으로 복원되었습니다.", "완료",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("복원 실패: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RulesEditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_dirty && this.DialogResult != DialogResult.OK)
            {
                var r = MessageBox.Show(
                    "저장되지 않은 변경사항이 있습니다.\n저장하시겠습니까?",
                    "확인", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.Yes)
                {
                    try
                    {
                        RuleStore.Save(_rules);
                        _dirty = false;
                        this.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("저장 실패: " + ex.Message, "오류",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        e.Cancel = true;
                    }
                }
            }
        }
    }

    /// <summary>개별 규칙 편집/추가 다이얼로그.</summary>
    public class RuleEditDialog : Form
    {
        private TextBox txtId, txtCategory, txtDescription, txtPattern, txtReference;
        private CheckBox chkC, chkCpp, chkJava, chkCSharp;
        private CheckBox chkIgnoreCase, chkMultiline, chkSingleline;
        private ComboBox cboSeverity;
        private Button btnOk, btnCancel, btnTestPattern;
        private TextBox txtTestInput;
        private Label lblTestResult;

        public VulnerabilityRule Rule { get; private set; }

        public RuleEditDialog(VulnerabilityRule original)
        {
            InitializeUI();
            if (original != null) LoadFromRule(original);
            else ApplyDefaults();
        }

        private void InitializeUI()
        {
            this.Text = "룰 편집";
            this.Size = new Size(720, 680);
            this.MinimumSize = new Size(600, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("맑은 고딕", 9F);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;

            int y = 12;
            int labW = 90;
            int inpX = 110;
            int inpW = 570;

            AddLabel("규칙ID:", 12, y + 4, labW);
            txtId = new TextBox { Location = new Point(inpX, y), Width = inpW };
            this.Controls.Add(txtId);
            y += 32;

            AddLabel("언어:", 12, y + 4, labW);
            chkC = new CheckBox { Text = "c", Location = new Point(inpX, y + 2), AutoSize = true };
            chkCpp = new CheckBox { Text = "cpp", Location = new Point(inpX + 60, y + 2), AutoSize = true };
            chkJava = new CheckBox { Text = "java", Location = new Point(inpX + 130, y + 2), AutoSize = true };
            chkCSharp = new CheckBox { Text = "csharp", Location = new Point(inpX + 210, y + 2), AutoSize = true };
            this.Controls.Add(chkC); this.Controls.Add(chkCpp);
            this.Controls.Add(chkJava); this.Controls.Add(chkCSharp);
            y += 32;

            AddLabel("심각도:", 12, y + 4, labW);
            cboSeverity = new ComboBox
            {
                Location = new Point(inpX, y),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboSeverity.Items.AddRange(new object[] { "CRITICAL", "HIGH", "MEDIUM", "LOW" });
            this.Controls.Add(cboSeverity);
            y += 32;

            AddLabel("CWE분류:", 12, y + 4, labW);
            txtCategory = new TextBox { Location = new Point(inpX, y), Width = inpW };
            this.Controls.Add(txtCategory);
            y += 32;

            AddLabel("설명:", 12, y + 4, labW);
            txtDescription = new TextBox
            {
                Location = new Point(inpX, y),
                Width = inpW,
                Multiline = true,
                Height = 55,
                ScrollBars = ScrollBars.Vertical
            };
            this.Controls.Add(txtDescription);
            y += 62;

            AddLabel("정규식 패턴:", 12, y + 4, labW);
            txtPattern = new TextBox
            {
                Location = new Point(inpX, y),
                Width = inpW,
                Font = new Font("Consolas", 9F),
                Multiline = true,
                Height = 60,
                ScrollBars = ScrollBars.Vertical
            };
            this.Controls.Add(txtPattern);
            y += 66;

            AddLabel("옵션:", 12, y + 4, labW);
            chkIgnoreCase = new CheckBox { Text = "IgnoreCase", Location = new Point(inpX, y + 2), AutoSize = true };
            chkMultiline = new CheckBox { Text = "Multiline", Location = new Point(inpX + 110, y + 2), AutoSize = true, Checked = true };
            chkSingleline = new CheckBox { Text = "Singleline", Location = new Point(inpX + 210, y + 2), AutoSize = true };
            this.Controls.Add(chkIgnoreCase);
            this.Controls.Add(chkMultiline);
            this.Controls.Add(chkSingleline);
            y += 32;

            AddLabel("출처:", 12, y + 4, labW);
            txtReference = new TextBox
            {
                Location = new Point(inpX, y),
                Width = inpW,
                Font = new Font("Consolas", 9F)
            };
            var lblRefHint = new Label
            {
                Text = "(선택) 참조 출처. 예: semgrep:java.lang.security..., OWASP ASVS v4.0.3 V6.2.5",
                Location = new Point(inpX, y + 26),
                Width = inpW,
                ForeColor = Color.FromArgb(108, 117, 125),
                Font = new Font("맑은 고딕", 8.5F)
            };
            this.Controls.Add(txtReference);
            this.Controls.Add(lblRefHint);
            y += 48;

            // 패턴 테스트 영역
            var grpTest = new GroupBox
            {
                Text = "패턴 테스트 (샘플 코드 입력 후 검증)",
                Location = new Point(12, y),
                Size = new Size(680, 130)
            };
            txtTestInput = new TextBox
            {
                Location = new Point(10, 22),
                Width = 540,
                Height = 80,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F)
            };
            btnTestPattern = new Button
            {
                Text = "테스트",
                Location = new Point(560, 22),
                Width = 110,
                Height = 30
            };
            btnTestPattern.Click += BtnTestPattern_Click;
            lblTestResult = new Label
            {
                Location = new Point(560, 60),
                Width = 110,
                Height = 60,
                AutoSize = false,
                Font = new Font("맑은 고딕", 8.5F)
            };
            grpTest.Controls.Add(txtTestInput);
            grpTest.Controls.Add(btnTestPattern);
            grpTest.Controls.Add(lblTestResult);
            this.Controls.Add(grpTest);
            y += 140;

            btnOk = new Button
            {
                Text = "확인",
                Location = new Point(480, y),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.None
            };
            btnOk.Click += BtnOk_Click;
            btnCancel = new Button
            {
                Text = "취소",
                Location = new Point(585, y),
                Width = 100,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void AddLabel(string text, int x, int y, int w)
        {
            var l = new Label { Text = text, Location = new Point(x, y), Width = w, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            this.Controls.Add(l);
        }

        private void ApplyDefaults()
        {
            txtId.Text = "VUL-CUSTOM-001";
            chkC.Checked = true; chkCpp.Checked = true;
            cboSeverity.SelectedIndex = 1; // HIGH
            txtCategory.Text = "CWE-??? ";
            chkMultiline.Checked = true;
        }

        private void LoadFromRule(VulnerabilityRule r)
        {
            txtId.Text = r.RuleId ?? "";
            txtCategory.Text = r.Category ?? "";
            txtDescription.Text = r.Description ?? "";
            txtPattern.Text = r.Pattern ?? "";
            txtReference.Text = r.Reference ?? "";

            if (r.Languages != null)
            {
                foreach (var l in r.Languages)
                {
                    string ll = (l ?? "").ToLower();
                    if (ll == "c") chkC.Checked = true;
                    else if (ll == "cpp") chkCpp.Checked = true;
                    else if (ll == "java") chkJava.Checked = true;
                    else if (ll == "csharp") chkCSharp.Checked = true;
                }
            }

            int sevIdx = cboSeverity.FindStringExact(r.Severity ?? "");
            cboSeverity.SelectedIndex = sevIdx >= 0 ? sevIdx : 1;

            chkIgnoreCase.Checked = (r.Options & RegexOptions.IgnoreCase) != 0;
            chkMultiline.Checked = (r.Options & RegexOptions.Multiline) != 0;
            chkSingleline.Checked = (r.Options & RegexOptions.Singleline) != 0;
            if (!chkIgnoreCase.Checked && !chkMultiline.Checked && !chkSingleline.Checked)
                chkMultiline.Checked = true;
        }

        private void BtnTestPattern_Click(object sender, EventArgs e)
        {
            try
            {
                var opts = BuildOptions();
                var re = new Regex(txtPattern.Text, opts);
                var matches = re.Matches(txtTestInput.Text ?? "");
                if (matches.Count == 0)
                {
                    lblTestResult.ForeColor = Color.FromArgb(108, 117, 125);
                    lblTestResult.Text = "매치 없음";
                }
                else
                {
                    lblTestResult.ForeColor = Color.FromArgb(40, 167, 69);
                    lblTestResult.Text = matches.Count + "건 매치";
                }
            }
            catch (Exception ex)
            {
                lblTestResult.ForeColor = Color.FromArgb(220, 53, 69);
                lblTestResult.Text = "오류: " + ex.Message;
            }
        }

        private RegexOptions BuildOptions()
        {
            RegexOptions opts = RegexOptions.None;
            if (chkIgnoreCase.Checked) opts |= RegexOptions.IgnoreCase;
            if (chkMultiline.Checked) opts |= RegexOptions.Multiline;
            if (chkSingleline.Checked) opts |= RegexOptions.Singleline;
            if (opts == RegexOptions.None) opts = RegexOptions.Multiline;
            return opts;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            // 검증
            string id = (txtId.Text ?? "").Trim();
            if (id.Length == 0)
            {
                MessageBox.Show("규칙ID를 입력하세요.", "입력 필요",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtId.Focus(); return;
            }

            var langs = new List<string>();
            if (chkC.Checked) langs.Add("c");
            if (chkCpp.Checked) langs.Add("cpp");
            if (chkJava.Checked) langs.Add("java");
            if (chkCSharp.Checked) langs.Add("csharp");
            if (langs.Count == 0)
            {
                MessageBox.Show("대상 언어를 하나 이상 선택하세요.", "입력 필요",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboSeverity.SelectedIndex < 0)
            {
                MessageBox.Show("심각도를 선택하세요.", "입력 필요",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pattern = (txtPattern.Text ?? "").Trim();
            if (pattern.Length == 0)
            {
                MessageBox.Show("정규식 패턴을 입력하세요.", "입력 필요",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPattern.Focus(); return;
            }

            RegexOptions opts = BuildOptions();
            try { new Regex(pattern, opts); }
            catch (Exception ex)
            {
                MessageBox.Show("정규식이 올바르지 않습니다:\n" + ex.Message,
                    "정규식 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPattern.Focus(); return;
            }

            Rule = new VulnerabilityRule
            {
                RuleId = id,
                Languages = langs.ToArray(),
                Severity = cboSeverity.SelectedItem.ToString(),
                Category = (txtCategory.Text ?? "").Trim(),
                Description = (txtDescription.Text ?? "").Trim(),
                Pattern = pattern,
                Options = opts,
                Reference = (txtReference.Text ?? "").Trim()
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    /// <summary>룰셋 URL 다운로드 / 내장 룰팩 적용 다이얼로그.</summary>
    public class RulesUpdateDialog : Form
    {
        private TextBox txtUrl;
        private RadioButton radDownload, radRestore;
        private RadioButton radPackSemgrep, radPackOwasp, radPackCombined;
        private RadioButton radMerge, radReplace;
        private Button btnRun, btnCancel;
        private Label lblStatus;

        public RulesUpdateDialog()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "룰셋 업데이트";
            this.Size = new Size(640, 555);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("맑은 고딕", 9F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblInfo = new Label
            {
                Text = "룰셋을 외부 URL 또는 내장 큐레이션 룰팩(폐쇄망 대응)에서 적용합니다.\n"
                     + "URL은 사내 서버/저장소의 rules.config 파일을 가리켜야 합니다 ([RULE] 블록 포맷).\n"
                     + "내장 룰팩 출처: Semgrep 공개 룰셋(OSS), OWASP ASVS v4.0.3.\n"
                     + "기존 룰셋은 자동으로 backup 폴더에 백업됩니다.",
                Location = new Point(12, 12),
                Size = new Size(600, 72),
                ForeColor = Color.FromArgb(73, 80, 87)
            };
            this.Controls.Add(lblInfo);

            // 업데이트 소스
            var grpSrc = new GroupBox
            {
                Text = "업데이트 소스",
                Location = new Point(12, 90),
                Size = new Size(600, 220)
            };
            radDownload = new RadioButton
            {
                Text = "URL에서 다운로드",
                Location = new Point(12, 22),
                AutoSize = true,
                Checked = true
            };
            var lblUrl = new Label { Text = "URL:", Location = new Point(30, 52), AutoSize = true };
            txtUrl = new TextBox
            {
                Location = new Point(70, 49),
                Width = 515,
                Text = RuleStore.GetUpdateUrl()
            };
            radRestore = new RadioButton
            {
                Text = "내장 기본 CWE 룰셋으로 복원 (오프라인)",
                Location = new Point(12, 82),
                AutoSize = true
            };
            radPackSemgrep = new RadioButton
            {
                Text = "Semgrep 공개 룰팩 (내장, returntocorp/semgrep-rules 기반)",
                Location = new Point(12, 110),
                AutoSize = true
            };
            radPackOwasp = new RadioButton
            {
                Text = "OWASP ASVS v4.0.3 룰팩 (내장)",
                Location = new Point(12, 138),
                AutoSize = true
            };
            radPackCombined = new RadioButton
            {
                Text = "Semgrep + OWASP ASVS 통합 룰팩 (내장, RuleId 중복 제거)",
                Location = new Point(12, 166),
                AutoSize = true
            };
            var lblPackNote = new Label
            {
                Text = "내장 룰팩은 각 규칙에 출처(semgrep:..., OWASP ASVS Vx.y.z)를 기록합니다.",
                Location = new Point(30, 190),
                AutoSize = true,
                ForeColor = Color.FromArgb(108, 117, 125),
                Font = new Font("맑은 고딕", 8.5F)
            };
            grpSrc.Controls.Add(radDownload);
            grpSrc.Controls.Add(lblUrl);
            grpSrc.Controls.Add(txtUrl);
            grpSrc.Controls.Add(radRestore);
            grpSrc.Controls.Add(radPackSemgrep);
            grpSrc.Controls.Add(radPackOwasp);
            grpSrc.Controls.Add(radPackCombined);
            grpSrc.Controls.Add(lblPackNote);
            this.Controls.Add(grpSrc);

            // 적용 방식
            var grpApply = new GroupBox
            {
                Text = "적용 방식 (URL 다운로드 및 내장 룰팩에 공통 적용. 기본 복원은 항상 전체 교체)",
                Location = new Point(12, 320),
                Size = new Size(600, 80)
            };
            radMerge = new RadioButton
            {
                Text = "기존 룰과 병합 (동일 규칙ID는 새 버전으로 덮어쓰기)",
                Location = new Point(12, 24),
                AutoSize = true,
                Checked = true
            };
            radReplace = new RadioButton
            {
                Text = "기존 룰 전체 교체",
                Location = new Point(12, 50),
                AutoSize = true
            };
            grpApply.Controls.Add(radMerge);
            grpApply.Controls.Add(radReplace);
            this.Controls.Add(grpApply);

            lblStatus = new Label
            {
                Location = new Point(12, 410),
                Size = new Size(600, 40),
                ForeColor = Color.FromArgb(108, 117, 125)
            };
            this.Controls.Add(lblStatus);

            btnRun = new Button
            {
                Text = "업데이트",
                Location = new Point(402, 465),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRun.Click += BtnRun_Click;

            btnCancel = new Button
            {
                Text = "취소",
                Location = new Point(510, 465),
                Width = 100,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(btnRun);
            this.Controls.Add(btnCancel);
            this.CancelButton = btnCancel;

            // URL이 비어있으면 통합 룰팩을 기본 진입점으로 (폐쇄망에서 가장 유용)
            if (string.IsNullOrEmpty((txtUrl.Text ?? "").Trim()))
            {
                radDownload.Checked = false;
                radPackCombined.Checked = true;
            }
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            try
            {
                btnRun.Enabled = false;
                lblStatus.ForeColor = Color.FromArgb(108, 117, 125);

                if (radRestore.Checked)
                {
                    lblStatus.Text = "기본 CWE 룰셋으로 복원 중...";
                    Application.DoEvents();
                    RuleStore.RestoreDefaults();
                    lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
                    lblStatus.Text = "기본 CWE 룰셋으로 복원 완료. 이전 파일은 backup 폴더에 저장됨.";
                    this.DialogResult = DialogResult.OK;
                    MessageBox.Show("기본 CWE 룰셋으로 복원되었습니다.", "완료",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Close();
                    return;
                }

                // 내장 룰팩 적용 (Semgrep / OWASP ASVS / 통합)
                if (radPackSemgrep.Checked || radPackOwasp.Checked || radPackCombined.Checked)
                {
                    string packName;
                    List<VulnerabilityRule> pack;
                    if (radPackSemgrep.Checked)
                    {
                        packName = "Semgrep 공개 룰팩";
                        pack = RulePacks.GetSemgrepPack();
                    }
                    else if (radPackOwasp.Checked)
                    {
                        packName = "OWASP ASVS v4.0.3 룰팩";
                        pack = RulePacks.GetOwaspAsvsPack();
                    }
                    else
                    {
                        packName = "Semgrep + OWASP ASVS 통합 룰팩";
                        pack = RulePacks.GetCombinedPack();
                    }

                    lblStatus.Text = packName + " 적용 중...";
                    Application.DoEvents();

                    int applied = RuleStore.ApplyPack(pack, radMerge.Checked);
                    lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
                    lblStatus.Text = string.Format("{0} 적용 완료 ({1}건). 이전 파일은 backup 폴더에 저장됨.",
                        packName, applied);
                    this.DialogResult = DialogResult.OK;
                    MessageBox.Show(
                        string.Format("{0} 적용 완료.\n\n적용된 룰 수: {1}\n적용 방식: {2}\n\n각 룰에는 출처(Reference)가 기록됩니다.",
                            packName, applied, radMerge.Checked ? "병합" : "전체 교체"),
                        "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Close();
                    return;
                }

                string url = (txtUrl.Text ?? "").Trim();
                if (url.Length == 0)
                {
                    MessageBox.Show("URL을 입력하세요.", "입력 필요",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                lblStatus.Text = "다운로드 중: " + url;
                Application.DoEvents();

                int downloaded;
                string err = RuleStore.DownloadAndApply(url, radMerge.Checked, out downloaded);
                if (err != null)
                {
                    lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                    lblStatus.Text = "실패: " + err;
                    MessageBox.Show(
                        "다운로드 실패:\n" + err + "\n\n"
                        + "URL이 접근 가능한지, 룰셋 파일 포맷이 올바른지 확인하세요.\n"
                        + "(파일은 INI 스타일: [RULE] 블록과 key=value 형식)",
                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
                lblStatus.Text = string.Format("완료. {0}건 다운로드됨, 이전 파일은 backup 폴더에 저장됨.", downloaded);
                this.DialogResult = DialogResult.OK;
                MessageBox.Show(
                    string.Format("룰셋 업데이트 완료.\n\n다운로드: {0}건\n적용 방식: {1}",
                        downloaded, radMerge.Checked ? "병합" : "전체 교체"),
                    "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                lblStatus.Text = "오류: " + ex.Message;
            }
            finally
            {
                btnRun.Enabled = true;
            }
        }
    }
}

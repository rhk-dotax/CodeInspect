using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CodeInspect
{
    /// <summary>LLM 분석 설정 다이얼로그 (Provider/Endpoint/Model 선택)</summary>
    public class LLMConfigForm : Form
    {
        private RadioButton rbOllama;
        private RadioButton rbLMStudio;
        private TextBox txtEndpoint;
        private ComboBox cboModel;
        private Button btnFetchModels;
        private NumericUpDown numTimeout;
        private NumericUpDown numTemperature;
        private NumericUpDown numMaxFileKB;
        private Button btnTestConnection;
        private Button btnOK;
        private Button btnCancel;
        private TextBox txtLog;

        private bool _suppressEndpointAuto;

        public LLMConfigForm()
        {
            InitializeUI();
            LoadInitialValues();
        }

        private void InitializeUI()
        {
            this.Text = "LLM 분석 설정";
            this.Size = new Size(500, 470);
            this.MinimumSize = new Size(500, 470);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("맑은 고딕", 9F);

            // ── 프로바이더 ──
            var lblProvider = new Label { Text = "프로바이더", Location = new Point(15, 18), AutoSize = true };
            rbOllama = new RadioButton { Text = "Ollama", Location = new Point(110, 16), Width = 90, Checked = true };
            rbLMStudio = new RadioButton { Text = "LM Studio", Location = new Point(210, 16), Width = 110 };
            rbOllama.CheckedChanged += Provider_CheckedChanged;
            rbLMStudio.CheckedChanged += Provider_CheckedChanged;

            // ── 엔드포인트 ──
            var lblEndpoint = new Label { Text = "엔드포인트", Location = new Point(15, 53), AutoSize = true };
            txtEndpoint = new TextBox { Location = new Point(110, 50), Width = 350 };

            // ── 모델 ──
            var lblModel = new Label { Text = "모델", Location = new Point(15, 88), AutoSize = true };
            cboModel = new ComboBox { Location = new Point(110, 85), Width = 230, DropDownStyle = ComboBoxStyle.DropDown };
            btnFetchModels = new Button { Text = "모델 목록", Location = new Point(345, 84), Width = 115, Height = 25 };
            btnFetchModels.Click += BtnFetchModels_Click;

            // ── 타임아웃 / 온도 ──
            var lblTimeout = new Label { Text = "Timeout(초)", Location = new Point(15, 123), AutoSize = true };
            numTimeout = new NumericUpDown
            {
                Location = new Point(110, 120), Width = 70,
                Minimum = 5, Maximum = 600, Value = 120
            };

            var lblTemp = new Label { Text = "Temperature", Location = new Point(200, 123), AutoSize = true };
            numTemperature = new NumericUpDown
            {
                Location = new Point(290, 120), Width = 70,
                Minimum = 0, Maximum = 2, DecimalPlaces = 2, Increment = 0.1M, Value = 0.1M
            };

            // ── 최대 파일 크기 ──
            var lblMaxFile = new Label { Text = "최대 파일(KB)", Location = new Point(15, 158), AutoSize = true };
            numMaxFileKB = new NumericUpDown
            {
                Location = new Point(110, 155), Width = 70,
                Minimum = 1, Maximum = 1024, Value = 50
            };
            var lblMaxFileHint = new Label
            {
                Text = "초과 파일은 LLM-SKIP Finding으로 표시됨",
                Location = new Point(195, 158), AutoSize = true, ForeColor = Color.Gray
            };

            // ── 연결 테스트 ──
            btnTestConnection = new Button
            {
                Text = "연결 테스트",
                Location = new Point(15, 195), Width = 120, Height = 28,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            btnTestConnection.Click += BtnTestConnection_Click;

            // ── 로그 영역 ──
            var lblLog = new Label { Text = "상태", Location = new Point(15, 235), AutoSize = true };
            txtLog = new TextBox
            {
                Location = new Point(15, 256), Width = 445, Height = 110,
                Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true,
                BackColor = Color.FromArgb(248, 249, 250),
                Font = new Font("Consolas", 8.5F)
            };

            // ── OK / Cancel ──
            btnOK = new Button
            {
                Text = "확인", Location = new Point(285, 380), Width = 85, Height = 30,
                BackColor = Color.FromArgb(0, 123, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;
            btnCancel = new Button
            {
                Text = "취소", Location = new Point(375, 380), Width = 85, Height = 30,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] {
                lblProvider, rbOllama, rbLMStudio,
                lblEndpoint, txtEndpoint,
                lblModel, cboModel, btnFetchModels,
                lblTimeout, numTimeout, lblTemp, numTemperature,
                lblMaxFile, numMaxFileKB, lblMaxFileHint,
                btnTestConnection,
                lblLog, txtLog,
                btnOK, btnCancel
            });

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void LoadInitialValues()
        {
            var cfg = LLMConfig.Load();

            _suppressEndpointAuto = true;
            try
            {
                if (string.Equals(cfg.Provider, "lmstudio", StringComparison.OrdinalIgnoreCase))
                    rbLMStudio.Checked = true;
                else
                    rbOllama.Checked = true;

                txtEndpoint.Text = string.IsNullOrEmpty(cfg.Endpoint)
                    ? LLMConfig.DefaultEndpointFor(cfg.Provider)
                    : cfg.Endpoint;

                if (!string.IsNullOrEmpty(cfg.Model))
                {
                    cboModel.Items.Add(cfg.Model);
                    cboModel.SelectedItem = cfg.Model;
                    cboModel.Text = cfg.Model;
                }

                numTimeout.Value = ClampDecimal(cfg.TimeoutSeconds, numTimeout.Minimum, numTimeout.Maximum);
                numTemperature.Value = ClampDecimal((decimal)cfg.Temperature, numTemperature.Minimum, numTemperature.Maximum);
                numMaxFileKB.Value = ClampDecimal(cfg.MaxFileSizeKB, numMaxFileKB.Minimum, numMaxFileKB.Maximum);
            }
            finally
            {
                _suppressEndpointAuto = false;
            }
        }

        private static decimal ClampDecimal(decimal v, decimal lo, decimal hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private void Provider_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressEndpointAuto) return;
            if (sender == rbOllama && rbOllama.Checked)
                txtEndpoint.Text = LLMConfig.DefaultEndpointFor("ollama");
            else if (sender == rbLMStudio && rbLMStudio.Checked)
                txtEndpoint.Text = LLMConfig.DefaultEndpointFor("lmstudio");
            cboModel.Items.Clear();
            cboModel.Text = "";
        }

        private string CurrentProvider()
        {
            return rbLMStudio.Checked ? "lmstudio" : "ollama";
        }

        private LLMConfig BuildConfigFromUI()
        {
            return new LLMConfig
            {
                Provider = CurrentProvider(),
                Endpoint = (txtEndpoint.Text ?? "").Trim(),
                Model = (cboModel.Text ?? "").Trim(),
                TimeoutSeconds = (int)numTimeout.Value,
                Temperature = (double)numTemperature.Value,
                MaxFileSizeKB = (int)numMaxFileKB.Value
            };
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + Environment.NewLine);
        }

        private void BtnFetchModels_Click(object sender, EventArgs e)
        {
            string provider = CurrentProvider();
            string endpoint = (txtEndpoint.Text ?? "").Trim();
            if (string.IsNullOrEmpty(endpoint))
            {
                MessageBox.Show("엔드포인트를 입력하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnFetchModels.Enabled = false;
            this.Cursor = Cursors.WaitCursor;
            AppendLog("모델 목록 조회 중... (" + provider + ", " + endpoint + ")");

            string error = null;
            List<string> models = null;
            try
            {
                models = LLMAnalyzer.ListModels(provider, endpoint, 15, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                btnFetchModels.Enabled = true;
                this.Cursor = Cursors.Default;
            }

            if (error != null)
            {
                AppendLog("[실패] " + error);
                MessageBox.Show("모델 목록 조회 실패:\n" + error, "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string preserved = (cboModel.Text ?? "").Trim();
            cboModel.Items.Clear();
            if (models != null)
            {
                foreach (var m in models) cboModel.Items.Add(m);
            }

            if (cboModel.Items.Count == 0)
            {
                AppendLog("[알림] 등록된 모델이 없습니다.");
            }
            else
            {
                AppendLog(string.Format("[성공] {0}개 모델 조회됨", cboModel.Items.Count));
                if (!string.IsNullOrEmpty(preserved) && cboModel.Items.Contains(preserved))
                    cboModel.SelectedItem = preserved;
                else
                    cboModel.SelectedIndex = 0;
            }
        }

        private void BtnTestConnection_Click(object sender, EventArgs e)
        {
            var cfg = BuildConfigFromUI();
            if (!cfg.IsConfigured())
            {
                MessageBox.Show("Provider/Endpoint/Model을 모두 입력하거나 선택하세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnTestConnection.Enabled = false;
            this.Cursor = Cursors.WaitCursor;
            AppendLog(string.Format("연결 테스트 중... ({0} / {1})", cfg.Provider, cfg.Model));

            string err = null;
            try
            {
                var analyzer = new LLMAnalyzer(cfg);
                err = analyzer.TestConnection();
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
            finally
            {
                btnTestConnection.Enabled = true;
                this.Cursor = Cursors.Default;
            }

            if (err == null)
            {
                AppendLog("[성공] 연결 OK");
                MessageBox.Show("연결 성공", "확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AppendLog("[실패] " + err);
                MessageBox.Show("연결 실패:\n" + err, "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            var cfg = BuildConfigFromUI();
            if (string.IsNullOrEmpty(cfg.Endpoint))
            {
                MessageBox.Show("엔드포인트를 입력하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.None;
                return;
            }
            if (string.IsNullOrEmpty(cfg.Model))
            {
                MessageBox.Show("모델을 선택하세요.\n('모델 목록' 버튼으로 자동 조회 가능)", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.None;
                return;
            }

            try
            {
                cfg.Save();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "LLMConfigForm.BtnOK_Click - Save");
                MessageBox.Show("설정 저장 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
                return;
            }
        }
    }
}

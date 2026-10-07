using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace AutoDeleteApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private TextBox txtPath;
        private TextBox txtPattern;
        private TextBox txtLog;
        private Button btnBrowse;
        private Button btnRun;
        private Button btnStop;
        private Label lblStatus;
        private Label lblCount;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private Thread workThread;
        private bool isRunning = false;
        private int deletedCount = 0;

        public MainForm()
        {
            this.Text = "Auto Delete File Utility (Older than 1 Hour)";
            this.Size = new Size(520, 420);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblPath = new Label() { Text = "Folder Path:", Location = new Point(15, 18), AutoSize = true };
            txtPath = new TextBox() { Location = new Point(120, 15), Width = 270, ReadOnly = true };
            btnBrowse = new Button() { Text = "Browse...", Location = new Point(400, 13), Width = 85 };
            btnBrowse.Click += BtnBrowse_Click;

            Label lblPattern = new Label() { Text = "FileName / Ext:", Location = new Point(15, 53), AutoSize = true };
            txtPattern = new TextBox() { Text = ".bxmd2", Location = new Point(120, 50), Width = 150 };

            lblStatus = new Label() { Text = "Status: Stopped", ForeColor = Color.Red, Location = new Point(15, 85), AutoSize = true };
            lblCount = new Label() { Text = "Deleted Count: 0 files", ForeColor = Color.Blue, Location = new Point(330, 85), AutoSize = true };

            btnRun = new Button() { Text = "Run", Location = new Point(120, 115), Width = 110, Height = 30, BackColor = Color.LightGreen };
            btnRun.Click += BtnRun_Click;

            btnStop = new Button() { Text = "Stop", Location = new Point(250, 115), Width = 110, Height = 30, Enabled = false, BackColor = Color.LightCoral };
            btnStop.Click += BtnStop_Click;

            Label lblLogHeader = new Label() { Text = "Deleted Log History (Age > 1 Hr):", Location = new Point(15, 155), AutoSize = true };
            txtLog = new TextBox() { Location = new Point(15, 175), Width = 470, Height = 180, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime };

            this.Controls.Add(lblPath);
            this.Controls.Add(txtPath);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(lblPattern);
            this.Controls.Add(txtPattern);
            this.Controls.Add(lblStatus);
            this.Controls.Add(lblCount);
            this.Controls.Add(btnRun);
            this.Controls.Add(btnStop);
            this.Controls.Add(lblLogHeader);
            this.Controls.Add(txtLog);

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open GUI", null, ShowWindow);
            trayMenu.Items.Add("Exit", null, ExitAppHandler);

            trayIcon = new NotifyIcon()
            {
                Text = "Auto Delete Utility",
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = false
            };
            trayIcon.DoubleClick += ShowWindow;

            this.Resize += MainForm_Resize;
            this.FormClosing += MainForm_FormClosing;
        }

        private void ShowWindow(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
        }

        private void ExitAppHandler(object sender, EventArgs e)
        {
            ExitApp();
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK) txtPath.Text = fbd.SelectedPath;
            }
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPath.Text) || !Directory.Exists(txtPath.Text))
            {
                MessageBox.Show("Please select a valid folder!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPattern.Text))
            {
                MessageBox.Show("Please enter keyword or extension!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            isRunning = true;
            btnRun.Enabled = false;
            btnStop.Enabled = true;
            btnBrowse.Enabled = false;
            txtPattern.Enabled = false;
            lblStatus.Text = "Status: Running...";
            lblStatus.ForeColor = Color.Green;

            string folder = txtPath.Text;
            string pattern = txtPattern.Text.Trim();

            workThread = new Thread(() => DeleteLoop(folder, pattern));
            workThread.IsBackground = true;
            workThread.Start();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            isRunning = false;
            btnRun.Enabled = true;
            btnStop.Enabled = false;
            btnBrowse.Enabled = true;
            txtPattern.Enabled = true;
            lblStatus.Text = "Status: Stopped";
            lblStatus.ForeColor = Color.Red;
        }

        private void DeleteLoop(string rootFolder, string pattern)
        {
            while (isRunning)
            {
                try
                {
                    SearchAndDelete(rootFolder, pattern);
                }
                catch { }
                Thread.Sleep(5000); // เช็คทุกๆ 5 วินาที
            }
        }

        private void SearchAndDelete(string currentFolder, string pattern)
        {
            try
            {
                string[] files = Directory.GetFiles(currentFolder);
                DateTime thresholdTime = DateTime.Now.AddHours(-1); // กำหนดเวลาอ้างอิง: ย้อนหลังไป 1 ชั่วโมง

                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            // ดึงเวลาเขียนไฟล์ล่าสุด
                            FileInfo fi = new FileInfo(filePath);
                            
                            // เช็คว่าไฟล์ถูกเขียน/แก้ไขล่าสุด ก่อนเวลา 1 ชั่วโมงที่แล้วหรือไม่
                            if (fi.LastWriteTime <= thresholdTime)
                            {
                                File.Delete(filePath);
                                deletedCount++;
                                string logEntry = string.Format("[{0}] Deleted (Age > 1h): {1}", DateTime.Now.ToString("HH:mm:ss"), filePath);
                                AppendLog(logEntry, deletedCount);
                            }
                        }
                        catch { }
                    }
                }

                string[] subDirs = Directory.GetDirectories(currentFolder);
                foreach (string dir in subDirs)
                {
                    SearchAndDelete(dir, pattern);
                }
            }
            catch { }
        }

        private void AppendLog(string text, int count)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(text, count)));
            }
            else
            {
                txtLog.AppendText(text + Environment.NewLine);
                lblCount.Text = string.Format("Deleted Count: {0} files", count);
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                trayIcon.Visible = true;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
                trayIcon.Visible = true;
            }
        }

        private void ExitApp()
        {
            isRunning = false;
            trayIcon.Visible = false;
            Application.ExitThread();
            Environment.Exit(0);
        }
    }
}
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using Timer = System.Threading.Timer;


namespace CursorSelector
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        const int SPI_SETCURSORS = 0x57;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDCHANGE = 0x02;

        private bool autoStart = false;
        private string appDataFolder = @"C:\CursorSelector";
        private string cursorFolder = @"C:\Cursors";
        private string[] availableThemes;
        private string[] selectedThemes;
        private int currentIndex = 0;
        private int interval = 1;
        private Timer timer;

        public Form1()
        {
            InitializeComponent();
            LoadAvailableThemes();

            // 트레이 아이콘 오른쪽 클릭 메뉴 설정
            var trayMenu = new ContextMenuStrip();
            var openItem = new ToolStripMenuItem("Open");
            var exitItem = new ToolStripMenuItem("Exit");
            openItem.Click += OpenToolStripMenuItem_Click;
            exitItem.Click += ExitToolStripMenuItem_Click;
            trayMenu.Items.Add(openItem);
            trayMenu.Items.Add(exitItem);

            notifyIcon1.ContextMenuStrip = trayMenu;
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopTimer();
            this.FormClosing -= Form1_FormClosing; // 강제 종료 시 FormClosing 이벤트 무시
            this.Close();
        }

        // 폴더 선택 및 하위 테마 로드
        private void buttonSelectFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    cursorFolder = fbd.SelectedPath;
                    LoadAvailableThemes();
                    tbxFolder.Text = cursorFolder;
                }
            }

            SaveConfig();
        }

        private void LoadAvailableThemes()
        {
            if (!Directory.Exists(cursorFolder)) return;

            availableThemes = Directory.GetDirectories(cursorFolder);
            listBoxAvailable.Items.Clear();
            foreach (var t in availableThemes)
                listBoxAvailable.Items.Add(Path.GetFileName(t));
        }

        // 선택/삭제 → 순서 지정
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            foreach (var item in listBoxAvailable.SelectedItems)
                listBoxSelected.Items.Add(item);

            SaveConfig();
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            // 선택된 인덱스를 복사
            int[] indices = new int[listBoxSelected.SelectedIndices.Count];
            listBoxSelected.SelectedIndices.CopyTo(indices, 0);

            // 내림차순으로 제거
            Array.Sort(indices);
            Array.Reverse(indices);

            foreach (int idx in indices)
                listBoxSelected.Items.RemoveAt(idx);

            SaveConfig();
        }


        // Apply Now
        private void buttonApplyNow_Click(object sender, EventArgs e)
        {
            // 정확히 하나만 선택된 경우만 실행
            if (listBoxAvailable.SelectedItems.Count == 1)
            {
                string themeName = listBoxAvailable.SelectedItem.ToString();
                string themePath = Path.Combine(cursorFolder, themeName);
                ApplyCursor(themePath);
            }
            else
            {
                MessageBox.Show("적용할 테마를 하나만 선택하세요.",
                                "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Start 버튼 → 타이머 반복
        private void buttonStart_Click(object sender, EventArgs e)
        {
            StopTimer();

            selectedThemes = new string[listBoxSelected.Items.Count];
            listBoxSelected.Items.CopyTo(selectedThemes, 0);
            if (selectedThemes.Length == 0) return;
            if(listBoxSelected.SelectedIndex >= 0 && listBoxSelected.SelectedIndex < listBoxSelected.Items.Count)
            {
                currentIndex = listBoxSelected.SelectedIndex;
            }

            listBoxAvailable.SelectedItems.Clear();
            listBoxSelected.SelectedItems.Clear();
            buttonSelectFolder.Enabled = false;
            listBoxAvailable.Enabled = false;
            listBoxSelected.Enabled = false;
            buttonApplyNow.Enabled = false; 
            buttonAdd.Enabled = false;
            buttonRemove.Enabled = false;
            numericUpDownInterval.Enabled = false;
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;
            buttonUp.Enabled = false;
            buttonDown.Enabled = false;

            int interval = (int)numericUpDownInterval.Value * 1000;

            timer = new Timer(_ =>
            {
                try
                {
                    if (selectedThemes.Length == 0) return;

                    string themeName = selectedThemes[currentIndex];
                    string themePath = Path.Combine(cursorFolder, themeName);
                    ApplyCursor(themePath);

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        SaveConfig();

                        listBoxSelected.ClearSelected();
                        int idx = listBoxSelected.Items.IndexOf(themeName);
                        if (idx >= 0)
                            listBoxSelected.SelectedIndex = idx;
                    });

                    currentIndex = (currentIndex + 1) % selectedThemes.Length;
                }
                catch (Exception ex)
                {
                    // 로그 처리
                }
            }, null, 0, interval);

            StartWithWindows();

            autoStart = true;
            SaveConfig();
        }

        private void SaveConfig()
        {
            string configPath = Path.Combine(appDataFolder, "config.txt");
            var lines = new List<string>
            {
                $"CursorFolder={cursorFolder}",
                $"SelectedThemes={string.Join(",", listBoxSelected.Items.Cast<string>())}",
                $"IntervalSec={interval}",
                $"currentIndex={currentIndex}",
                $"AutoStart={autoStart}"
            };
            File.WriteAllLines(configPath, lines);
        }

        // Stop 버튼 → 반복 종료
        private void buttonStop_Click(object sender, EventArgs e)
        {
            StopTimer();

            autoStart = false;
            SaveConfig();

            buttonSelectFolder.Enabled = true;
            listBoxAvailable.Enabled = true;
            listBoxSelected.Enabled = true;
            buttonApplyNow.Enabled = true;
            buttonAdd.Enabled = true;
            buttonRemove.Enabled = true;
            numericUpDownInterval.Enabled = true;
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
            buttonUp.Enabled = true;
            buttonDown.Enabled = true;
        }

        private void StopTimer()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }

        // 커서 적용
        private void ApplyCursor(string themeFolder)
        {
            var cursorMap = new (string key, string file)[]
            {
                ( "Arrow", "Normal Select.ani" ),
                ( "Hand", "Link Select.ani" ),
                ( "IBeam", "Text Select.ani" ),
                ( "Crosshair", "Precision Select.ani" ),
                ( "Help", "Help Select.ani" ),
                ( "No", "Unavailable.ani" ),
                ( "AppStarting", "Working In Background.ani" ),
                ( "UpArrow", "Alternate Select.ani" ),
                ( "SizeAll", "Move.ani" ),
                ( "SizeNESW", "Diagonal Resize 2.ani" ),
                ( "SizeNS", "Vertical Resize.ani" ),
                ( "SizeNWSE", "Diagonal Resize 1.ani" ),
                ( "SizeWE", "Horizontal Resize.ani" ),
                ( "Pin", "Location Select.ani" ),
                ( "Person", "Person Select.ani" ),
                ( "NWPen", "Handwriting.ani" ),
                ( "Wait", "Busy.ani" )
            };

            // 레지스트리에 적용
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", true))
            {
                foreach (var (k, f) in cursorMap)
                {
                    string path = Path.Combine(themeFolder, f);
                    if (File.Exists(path))
                        key.SetValue(k, path);
                }
            }

            // 시스템에 적용
            SystemParametersInfo(SPI_SETCURSORS, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }

        // 폼 닫기 → 트레이로 최소화
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        // Windows 시작 시 자동 실행 등록
        private void StartWithWindows()
        {
            string targetExePath = Path.Combine(appDataFolder, "CursorSelector.exe");
            string currentExe = Application.ExecutablePath;

            File.Copy(currentExe, targetExePath, true);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);

            key.SetValue("CursorSelector", targetExePath);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            string configPath = Path.Combine(appDataFolder, "config.txt");
            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                var config = lines
                    .Select(l => l.Split(new[] { '=' }, 2))
                    .ToDictionary(s => s[0], s => s[1]);

                // 값 적용
                cursorFolder = config.ContainsKey("CursorFolder") ? config["CursorFolder"] : "";
                tbxFolder.Text = cursorFolder;
                LoadAvailableThemes();

                if (config.ContainsKey("SelectedThemes"))
                {
                    // SelectedThemes는 ','로 구분
                    var themes = config["SelectedThemes"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    listBoxSelected.Items.AddRange(themes);
                }

                if (config.ContainsKey("IntervalSec") && int.TryParse(config["IntervalSec"], out int interval))
                {
                    numericUpDownInterval.Value = interval;
                }

                if (config.ContainsKey("currentIndex") && int.TryParse(config["currentIndex"], out int currentIdx))
                {
                    if (currentIdx >= 0 && currentIdx < listBoxSelected.Items.Count)
                    {
                        currentIndex = currentIdx;
                    }
                }

                if (config.ContainsKey("AutoStart") && bool.TryParse(config["AutoStart"], out bool autoStart) && autoStart)
                {
                    // 바로 타이머 시작 & 트레이로
                    buttonStart_Click(this, new EventArgs());
                    this.Hide();
                }
            }
        }

        private void numericUpDownInterval_ValueChanged(object sender, EventArgs e)
        {
            interval = (int)numericUpDownInterval.Value;
            SaveConfig();
        }

        // 위로 이동
        private void buttonUp_Click(object sender, EventArgs e)
        {
            if (listBoxSelected.SelectedItems.Count != 1)
            {
                MessageBox.Show("이동할 테마를 하나만 선택하세요.",
                                "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int idx = listBoxSelected.SelectedIndex;
            if (idx > 0)
            {
                var item = listBoxSelected.Items[idx];
                listBoxSelected.Items.RemoveAt(idx);
                listBoxSelected.Items.Insert(idx - 1, item);
                listBoxSelected.SelectedIndex = idx - 1;
                SaveConfig();
            }
        }

        // 아래로 이동
        private void buttonDown_Click(object sender, EventArgs e)
        {
            if (listBoxSelected.SelectedItems.Count != 1)
            {
                MessageBox.Show("이동할 테마를 하나만 선택하세요.",
                                "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int idx = listBoxSelected.SelectedIndex;
            if (idx < listBoxSelected.Items.Count - 1 && idx >= 0)
            {
                var item = listBoxSelected.Items[idx];
                listBoxSelected.Items.RemoveAt(idx);
                listBoxSelected.Items.Insert(idx + 1, item);
                listBoxSelected.SelectedIndex = idx + 1;
                SaveConfig();
            }
        }

        public class AppConfig
        {
            public string CursorFolder { get; set; }
            public string[] SelectedThemes { get; set; }
            public int IntervalSec { get; set; }
            public int LastIndex { get; set; }
            public bool AutoStart { get; set; }
        }

        private void listBoxSelected_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode) 
            {
                case Keys.Delete:
                    buttonRemove_Click(this, new EventArgs());
                    break;
            }
        }

        private void listBoxAvailable_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    buttonAdd_Click(this, new EventArgs());
                    break;
            }
         }
    }
}

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

        private string appDataFolder = @"C:\CursorSelector";
        private string cursorFolder = @"C:\Cursors";
        private string[] availableThemes;
        private string[] selectedThemes;
        private int currentIndex = 0;
        private Timer timer;

        public Form1()
        {
            InitializeComponent();
            LoadAvailableThemes();

            // 트레이 아이콘 오른쪽 클릭 메뉴 설정
            var trayMenu = new ContextMenuStrip();
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += ExitToolStripMenuItem_Click;
            trayMenu.Items.Add(exitItem);

            notifyIcon1.ContextMenuStrip = trayMenu;
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopTimer();
            notifyIcon1.Visible = false;
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
                    lbFolder.Text = cursorFolder;
                }
            }
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
            currentIndex = 0;

            buttonSelectFolder.Enabled = false;
            listBoxAvailable.Enabled = false;
            listBoxSelected.Enabled = false;
            buttonApplyNow.Enabled = false; 
            buttonAdd.Enabled = false;
            buttonRemove.Enabled = false;
            numericUpDownInterval.Enabled = false;
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;

            int interval = (int)numericUpDownInterval.Value * 1000;

            timer = new Timer(_ =>
            {
                if (selectedThemes.Length == 0) return;

                string themeName = selectedThemes[currentIndex];
                string themePath = Path.Combine(cursorFolder, selectedThemes[currentIndex]);
                ApplyCursor(themePath);

                // ★ 현재 적용 테마를 리스트에서 선택 표시
                this.Invoke((MethodInvoker)delegate
                {
                    listBoxSelected.SelectedIndices.Clear();
                    int idx = listBoxSelected.Items.IndexOf(themeName);
                    if (idx >= 0)
                        listBoxSelected.SelectedIndex = idx;
                });

                currentIndex++;
                if (currentIndex >= selectedThemes.Length)
                    currentIndex = 0;

            }, null, 0, interval);
            
            StartWithWindows();

            string configPath = Path.Combine(appDataFolder, "config.txt");
            var lines = new List<string>
            {
                $"CursorFolder={cursorFolder}",
                $"SelectedThemes={string.Join(",", listBoxSelected.Items.Cast<string>())}",
                $"IntervalSec={numericUpDownInterval.Value}",
                $"AutoStart={true}" 
            };
            File.WriteAllLines(configPath, lines);

            // 트레이 최소화
            notifyIcon1.Visible = true;
            this.Hide();
        }

        // Stop 버튼 → 반복 종료
        private void buttonStop_Click(object sender, EventArgs e)
        {
            StopTimer();

            string configPath = Path.Combine(appDataFolder, "config.txt");
            var lines = new List<string>
            {
                $"CursorFolder={cursorFolder}",
                $"SelectedThemes={string.Join(",", listBoxSelected.Items.Cast<string>())}",
                $"IntervalSec={numericUpDownInterval.Value}",
                $"AutoStart={false}"
            };
            File.WriteAllLines(configPath, lines);

            buttonSelectFolder.Enabled = true;
            listBoxAvailable.Enabled = true;
            listBoxSelected.Enabled = true;
            buttonApplyNow.Enabled = true;
            buttonAdd.Enabled = true;
            buttonRemove.Enabled = true;
            numericUpDownInterval.Enabled = true;
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
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
            notifyIcon1.Visible = true;
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
            notifyIcon1.Visible = false;
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

                if (config.ContainsKey("AutoStart") && bool.TryParse(config["AutoStart"], out bool autoStart) && autoStart)
                {
                    // 바로 타이머 시작 & 트레이로
                    buttonStart_Click(this, new EventArgs());
                }
            }
        }

        public class AppConfig
        {
            public string CursorFolder { get; set; }
            public string[] SelectedThemes { get; set; }
            public int IntervalSec { get; set; }
            public bool AutoStart { get; set; }
        }
    }
}

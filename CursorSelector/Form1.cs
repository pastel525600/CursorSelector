using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
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
        private int currentIndex = 0;
        private int interval = 1;
        private string appDataFolder = @"C:\CursorSelector";
        private string cursorFolder = @"C:\Cursors";
        private string[] availableThemes;
        private string[] selectedThemes;
        private Timer timer;

        /// <summary>
        /// 생성자
        /// </summary>
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

        /// <summary>
        /// 폴더 내의 하위 폴더(테마) 로드
        /// </summary>
        private void LoadAvailableThemes()
        {
            // 폴더가 없으면 종료
            if (!Directory.Exists(cursorFolder)) return;

            // 하위 폴더 로드
            availableThemes = Directory.GetDirectories(cursorFolder);
            listBoxAvailable.Items.Clear();
            foreach (var t in availableThemes)
                listBoxAvailable.Items.Add(Path.GetFileName(t));
        }

        /// <summary>
        /// 트레이 아이콘 메뉴 - Open
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        /// <summary>
        /// 트레이 아이콘 메뉴 - Exit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopTimer();
            this.FormClosing -= Form1_FormClosing; // 강제 종료 시 FormClosing 이벤트 무시
            this.Close();
        }

        /// <summary>
        /// 폴더 선택 버튼으로 커서 테마 폴더 선택
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// config.txt 저장
        /// </summary>
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

        /// <summary>
        /// 선택된 테마 즉시 적용
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// 선택된 테마 추가
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            foreach (var item in listBoxAvailable.SelectedItems)
                listBoxSelected.Items.Add(item);

            SaveConfig();
        }

        /// <summary>
        /// 선택된 테마 제거
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// 테마 변경 주기 설정
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numericUpDownInterval_ValueChanged(object sender, EventArgs e)
        {
            interval = (int)numericUpDownInterval.Value;
            SaveConfig();
        }

        /// <summary>
        /// 테마 순서 위로 이동
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonUp_Click(object sender, EventArgs e)
        {
            // 정확히 하나만 선택된 경우만 실행
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

        /// <summary>
        /// 테마 순서 아래로 이동
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDown_Click(object sender, EventArgs e)
        {
            // 정확히 하나만 선택된 경우만 실행
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

        /// <summary>
        /// 시작 버튼으로 반복 시작
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStart_Click(object sender, EventArgs e)
        {
            StopTimer();

            // 선택된 테마 목록 복사
            selectedThemes = new string[listBoxSelected.Items.Count];
            listBoxSelected.Items.CopyTo(selectedThemes, 0);

            // 선택된 테마가 없으면 종료
            if (selectedThemes.Length == 0) return;

            // 최신 인덱스가 있으면 유지
            if (listBoxSelected.SelectedIndex >= 0 && listBoxSelected.SelectedIndex < listBoxSelected.Items.Count)
            {
                currentIndex = listBoxSelected.SelectedIndex;
            }

            // UI 비활성화
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

            // 타이머 시작
            timer = new Timer(_ =>
            {
                try
                {
                    // 현재 인덱스의 테마 적용
                    string themeName = selectedThemes[currentIndex];
                    string themePath = Path.Combine(cursorFolder, themeName);
                    ApplyCursor(themePath);

                    // UI 스레드에서 선택된 항목 표시
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        SaveConfig();

                        listBoxSelected.ClearSelected();
                        int idx = listBoxSelected.Items.IndexOf(themeName);
                        if (idx >= 0)
                            listBoxSelected.SelectedIndex = idx;
                    });

                    // 다음 인덱스로 이동 (순환)
                    currentIndex = (currentIndex + 1) % selectedThemes.Length;
                }
                catch (Exception ex)
                {
                    // 오류 시 타이머 중지 및 UI 복원
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        StopTimer();
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
                        MessageBox.Show("오류가 발생하여 중지합니다.\n" + ex.Message,
                                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }, null, 0, interval * 1000);

            // Windows 시작 시 자동 실행 등록
            StartWithWindows();

            // 자동 시작 설정 저장
            autoStart = true;
            SaveConfig();
        }

        /// <summary>
        /// 테마 폴더의 커서 파일을 레지스트리에 적용하고 시스템에 반영
        /// </summary>
        /// <param name="themeFolder"></param>
        private void ApplyCursor(string themeFolder)
        {
            // 파일 매핑
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

        /// <summary>
        /// Windows 시작 시 자동 실행 등록
        /// </summary>
        private void StartWithWindows()
        {
            // 현재 실행 중인 경로와 복사 대상이 다를 때만 복사
            string targetExePath = Path.Combine(appDataFolder, "CursorSelector.exe");
            string currentExe = Application.ExecutablePath;

            if (!string.Equals(
                    Path.GetFullPath(currentExe),
                    Path.GetFullPath(targetExePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, targetExePath, true);
            }

            try
            {
                // 자동 실행 레지스트리 설정
                RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);

                key.SetValue("CursorSelector", targetExePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("권한 문제로 자동 실행 설정에 실패했습니다.\n" + ex.Message,
                                "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 정지 버튼으로 반복 중지
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStop_Click(object sender, EventArgs e)
        {
            // 반복 타이머 중지
            StopTimer();

            // Windows 시작 시 자동 실행 해제
            try
            {
                // Windows 시작 시 자동 실행 레지스트리 해제
                string targetExePath = Path.Combine(appDataFolder, "CursorSelector.exe");

                RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);

                key.SetValue("CursorSelector", targetExePath);

                // 자동 시작 해제 설정 저장
                autoStart = false;
                SaveConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show("권한 문제로 자동 실행 해제에 실패했습니다.\n" + ex.Message,
                                "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // UI 복원
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

        /// <summary>
        /// 반복 타이머 중지
        /// </summary>
        private void StopTimer()
        {
            if (timer != null)
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);

                timer.Dispose();
                timer = null;
            }
        }

        /// <summary>
        /// 폼 닫기 시 트레이로 최소화
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        /// <summary>
        /// 아이콘 더블 클릭 시 폼 복원
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        /// <summary>
        /// 프로그램 시작 시 config.txt 로드 후 자동 시작
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Shown(object sender, EventArgs e)
        {
            // 프로그램용 폴더 생성
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            // config.txt 로드
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
                    var themes = config["SelectedThemes"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    listBoxSelected.Items.AddRange(themes);
                }
                if (config.ContainsKey("IntervalSec") && int.TryParse(config["IntervalSec"], out int intval))
                {
                    interval = intval;
                    numericUpDownInterval.Value = intval;
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

        /// <summary>
        /// Delete 키로 선택된 테마 제거
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBoxSelected_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    buttonRemove_Click(this, new EventArgs());
                    break;
            }
        }

        /// <summary>
        /// Enter 키로 선택된 테마 추가
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBoxAvailable_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    buttonAdd_Click(this, new EventArgs());
                    break;
            }
        }

        /// <summary>
        /// config.txt 구조체
        /// </summary>
        public class AppConfig
        {
            public string CursorFolder { get; set; }
            public string[] SelectedThemes { get; set; }
            public int IntervalSec { get; set; }
            public int LastIndex { get; set; }
            public bool AutoStart { get; set; }
        }
    }
}
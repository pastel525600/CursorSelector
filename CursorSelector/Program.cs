using System;
using System.Windows.Forms;

namespace CursorSelector
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (IsAlreadyRunning())
            {
                MessageBox.Show("이미 프로그램이 실행 중입니다.",
                                "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.Run(new Form1());
        }
        private static bool IsAlreadyRunning()
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(
                System.IO.Path.GetFileNameWithoutExtension(
                    System.Reflection.Assembly.GetEntryAssembly().Location));
            return processes.Length > 1;
        }
    }

}

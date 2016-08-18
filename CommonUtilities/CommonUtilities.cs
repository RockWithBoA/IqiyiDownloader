using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace RockWithBoA
{
    public class CommonUtilities
    {
        static public FileInfo GetLatestM3u8File(string searchFolder)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(searchFolder);
            if (!dirInfo.Exists)
            {
                return null;
            }

            FileInfo[] m3u8Files = dirInfo.GetFiles("*.m3u8");
            if (m3u8Files.Length < 1)
            {
                MessageBox.Show("Cannot find m3u8 files.");
                return null;
            }

            FileInfo latestM3u8File = (from f in m3u8Files
                                       orderby f.LastWriteTime descending
                                       select f).First();
            return latestM3u8File;
        }

        /// <summary>
        /// 执行cmd命令
        /// 多命令请使用批处理命令连接符：
        /// &:同时执行两个命令
        /// |:将上一个命令的输出,作为下一个命令的输入
        /// &&：当&&前的命令成功时,才执行&&后的命令
        /// ||：当||前的命令失败时,才执行||后的命令]]>
        /// 其他请百度
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="output"></param>
        public static void RunCmd(string cmd, out string output)
        {
            cmd = cmd.Trim().TrimEnd('&') + "&exit";//说明：不管命令是否成功均执行exit命令，否则当调用ReadToEnd()方法时，会处于假死状态

            using (Process p = new Process())
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
                p.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
                p.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
                p.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
                p.StartInfo.CreateNoWindow = true;          //不显示程序窗口
                p.Start();//启动程序

                //向cmd窗口写入命令
                p.StandardInput.WriteLine(cmd);
                p.StandardInput.AutoFlush = true;

                //获取cmd窗口的输出信息
                output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();//等待程序执行完退出进程
                p.Close();
            }
        }

        static public string ReplaceNonNumberEnglishChineseCharsByUnderscore(string oldString)
        {
            string newString = oldString;

            //Replace non number, non English charactor, non Chinese charactor with underscore.
            foreach (var ch in newString)
            {

                char[] chars = { ch };
                string charStr = new string(chars);
                if (!Regex.IsMatch(charStr, "[a-zA-Z0-9\u4e00-\u9fa5]"))
                {
                    newString = newString.Replace(ch, '_');
                }
            }

            return newString;
        }
    }
}

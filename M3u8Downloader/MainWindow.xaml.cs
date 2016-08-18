using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace RockWithBoA
{
    namespace M3u8Downloader
    {
        /// <summary>
        /// Interaction logic for MainWindow.xaml
        /// </summary>
        public partial class MainWindow : Window
        {
            private Dictionary<int, string> missingFileUrls = null;
            private int expectedFileCount = 0;

            public MainWindow()
            {
                InitializeComponent();

                try
                {
                    M3u8FileDir.Text = (string)Application.Current.Properties["M3u8FileDir"];
                }
                catch (KeyNotFoundException)
                {
                    M3u8FileDir.Text = "";
                }
            }

            private Dictionary<int, string> GetUrlsFromM3u8File(string m3u8FilePath)
            {
                Dictionary<int, string> urls = new Dictionary<int, string>();
                FileStream fs = null;
                StreamReader sr = null;
                try
                {
                    fs = new FileStream(m3u8FilePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    if (fs != null)
                    {
                        sr = new StreamReader(fs);
                        if (sr != null)
                        {
                            string nextLine;
                            int count = 1;
                            while ((nextLine = sr.ReadLine()) != null)
                            {
                                if (nextLine.StartsWith("http://"))
                                {
                                    urls.Add(count++, nextLine);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    InfoTexts.Text = "Parsing m3u8 file fails with error: " + ex.Message + ".";
                    return null;
                }
                finally
                {
                    if (sr != null)
                    {
                        sr.Close();
                        sr = null;
                    }

                    if (fs != null)
                    {
                        fs.Close();
                        fs = null;
                    }
                }

                return urls;
            }

            private void DownloadByThunder(Dictionary<int, string> urls, string downloadDir)
            {
                DirectoryInfo downloadDirInfo = new DirectoryInfo(downloadDir);
                if (!downloadDirInfo.Exists)
                {
                    InfoTexts.Text = "Download directory " + downloadDir + " doesn't exist.";
                    return;
                }

                //Call Thunder to download sequentially.
                ThunderAgentLib.AgentClass thunderAgent = new ThunderAgentLib.AgentClass();
                if (null == thunderAgent)
                {
                    InfoTexts.Text = "Cannot connect to Thunder. Check if Thunder is properly installed.";
                    return;
                }

                int interval = 3000;
                try
                {
                    interval = int.Parse(((ComboBoxItem)AddTaskInterval.SelectedItem).Content.ToString()) * 1000;
                }
                catch
                {
                    InfoTexts.Text = "Cannot parse Add Task Interval value.";
                    return;
                }

                string videoName = downloadDir.Substring(downloadDir.LastIndexOf('\\') + 1);
                foreach (KeyValuePair<int, string> pair in urls)
                {
                    string saveAsName = String.Format("{0:D4}_{1}", pair.Key, videoName);

                    //参数意义：
                    //bstrUrl：目标URL，必需参数
                    //bstrFileName：另存名称，默认为空，表示由迅雷处理，可选参数
                    //bstrPath：存储目录，默认为空，表示由迅雷处理，可选参数
                    //bstrComments：下载注释，默认为空，可选参数
                    //bstrReferUrl：引用页URL，默认为空，可选参数
                    //nStartMode：开始模式，0手工开始，1立即开始，默认为 -1，表示由迅雷处理，可选参数
                    //nOnlyFromOrigin：是否只从原始URL下载，1只从原始URL下载，0多资源下载，默认为0，可选参数
                    //nOriginThreadCount：原始地址下载线程数，范围1 - 10，默认为 - 1，表示由迅雷处理，可选参数
                    thunderAgent.AddTask(pair.Value, saveAsName, downloadDir, "", "", 1, 0, -1);
                    thunderAgent.CommitTasks();

                    Thread.Sleep(interval); //Waiting for specified seconds so that Thunder will have enough time to add task.
                }

                //Wait for a period to let Thunder update all file names to the expected format.
                Thread.Sleep(interval);
            }

            //Check if any video slice is missing. If so, store the missing file URLs and enable "下载缺失文件" button.
            private void CheckMissingFiles(Dictionary<int, string> urls, string downloadDir)
            {
                DirectoryInfo downloadDirInfo = new DirectoryInfo(downloadDir);
                if (!downloadDirInfo.Exists)
                {
                    InfoTexts.Text = "Download directory " + downloadDir + " doesn't exist.";
                    return;
                }

                FileInfo[] tsFiles = downloadDirInfo.GetFiles("*.*ts");
                if (tsFiles.Length != expectedFileCount)
                {
                    //Get file sequence numbers of all existing files.
                    int fileSeqNumber = 0;
                    HashSet<int> fileSeqNumbers = new HashSet<int>();
                    foreach (FileInfo f in tsFiles)
                    {
                        try
                        {
                            fileSeqNumber = int.Parse(f.Name.Substring(0, 4));
                            fileSeqNumbers.Add(fileSeqNumber);
                        }
                        catch
                        {
                            InfoTexts.Text = "Cannot parse file sequential number.";
                            return;
                        }
                    }

                    DownloadMissingFilesBtn.IsEnabled = true;

                    //Get missing files.
                    if (null == missingFileUrls)
                    {
                        missingFileUrls = new Dictionary<int, string>();
                    }
                    missingFileUrls.Clear();

                    string videoName = downloadDir.Substring(downloadDir.LastIndexOf('\\') + 1);
                    string missingFiles = " Missing files sequential numbers:";
                    foreach (KeyValuePair<int, string> pair in urls)
                    {
                        if (!fileSeqNumbers.Contains(pair.Key))
                        {
                            missingFiles += String.Format("\n{0:D4}_{1}", pair.Key, videoName);
                            missingFileUrls.Add(pair.Key, pair.Value);
                        }
                    }
                    missingFiles += ".";

                    InfoTexts.Text = "Downloaded files number is not the same as URL number." + missingFiles;
                }
                else
                {
                    InfoTexts.Text = "No missing file is found.";
                }
            }

            private string GetDownloadDirectory()
            {
                FileInfo latestM3u8File = CommonUtilities.GetLatestM3u8File(M3u8FileDir.Text);
                if (null == latestM3u8File)
                {
                    InfoTexts.Text = "Cannot find m3u8 file.";
                    return null;
                }

                //Set download directory to a folder with the same name of m3u8 file in m3u8 directory.
                string videoName = latestM3u8File.Name.Substring(0, latestM3u8File.Name.LastIndexOf('.')).Trim();
                videoName = CommonUtilities.ReplaceNonNumberEnglishChineseCharsByUnderscore(videoName);

                return (M3u8FileDir.Text.TrimEnd('\\') + "\\" + videoName);
            }

            private void StartDownloadBtn_Click(object sender, RoutedEventArgs e)
            {
                FileInfo latestM3u8File = CommonUtilities.GetLatestM3u8File(M3u8FileDir.Text);
                if (null == latestM3u8File)
                {
                    InfoTexts.Text = "Cannot find m3u8 file.";
                    return;
                }

                string downloadDir = GetDownloadDirectory();
                DirectoryInfo downloadDirInfo = new DirectoryInfo(downloadDir);
                if (!downloadDirInfo.Exists)
                {
                    downloadDirInfo.Create();
                }

                Dictionary<int, string> urls = GetUrlsFromM3u8File(latestM3u8File.FullName);
                expectedFileCount = urls.Count;

#if DEBUG
                string urlFilePath = downloadDir + "\\URLs.txt";
                FileStream fs = null;
                StreamWriter sw = null;
                try
                {
                    fs = new FileStream(urlFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    sw = new StreamWriter(fs);
                    foreach (KeyValuePair<int, string> pair in urls)
                    {
                        sw.WriteLine(pair.Value);
                    }
                }
                catch (Exception ex)
                {
                    InfoTexts.Text = "Writing URLs.txt fails with error: " + ex.Message + ".";
                    return;
                }
                finally
                {
                    if (sw != null)
                    {
                        sw.Close();
                        sw = null;
                    }

                    if (fs != null)
                    {
                        fs.Close();
                        fs = null;
                    }
                }
#endif

                DownloadByThunder(urls, downloadDirInfo.FullName);

                DownloadMissingFilesBtn.IsEnabled = false;
                CheckMissingFiles(urls, downloadDir);
            }

            private void WriteCommandStringLog(string logStr)
            {
                FileStream fs = null;
                StreamWriter sw = null;
                try
                {
                    fs = new FileStream(GetDownloadDirectory() + "\\CommandString.txt", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    sw = new StreamWriter(fs);

                    sw.WriteLine(logStr);
                }
                catch (Exception ex)
                {
                    InfoTexts.Text = "Outputing merge command string fails with error: " + ex.Message + ".";
                    return;
                }
                finally
                {
                    if (sw != null)
                    {
                        sw.Close();
                        sw = null;
                    }

                    if (fs != null)
                    {
                        fs.Close();
                        fs = null;
                    }
                }
            }

            private string GetVideoId(string url)
            {
                int videoIdLength = 32;
                int index = url.IndexOf(".265ts");
                if (-1 == index)
                {
                    index = url.IndexOf(".ts");
                }

                string videoId = null;
                try
                {
                    videoId = url.Substring(index - videoIdLength, videoIdLength);
                }
                catch (ArgumentOutOfRangeException)
                {
                    InfoTexts.Text = "Videos to be merged have unsupported extension.";
                    return null;
                }

                return videoId;
            }

            /// <summary>
            /// 执行合并命令。
            /// 首先根据 URL 中的特征码合并出各个分段视频，最后再将分段视频合并为最终的结果。
            /// </summary>
            /// <param name="cmd"></param>
            /// <param name="output"></param>
            private void StartMergeBtn_Click(object sender, RoutedEventArgs e)
            {
                FileInfo latestM3u8File = CommonUtilities.GetLatestM3u8File(M3u8FileDir.Text);
                if (null == latestM3u8File)
                {
                    InfoTexts.Text = "Cannot find m3u8 file.";
                    return;
                }

                string downloadDir = GetDownloadDirectory();
                DirectoryInfo dirInfo = new DirectoryInfo(downloadDir);
                if (!dirInfo.Exists)
                {
                    InfoTexts.Text = "Download directory " + downloadDir + " doesn't exist.";
                    return;
                }

                FileInfo[] tsFiles = dirInfo.GetFiles("*.*ts");
                if (tsFiles.Length < 1)
                {
                    InfoTexts.Text = "Cannot find ts files.";
                    return;
                }

                var tsFilesByAscendingName = from f in tsFiles
                                             orderby f.Name ascending
                                             select f;

                string cmd = "copy /b ", output = "";
                string filePath = "";
                bool isFirstInputFile = true;
                int urlKey = 1, subCmdCount = 1;
                List<string> subCmds = new List<string>();
                Dictionary<int, string> urls = GetUrlsFromM3u8File(latestM3u8File.FullName);
                foreach (var f in tsFilesByAscendingName)
                {
                    filePath = String.Format("\"{0}\\{1}\"", downloadDir, f.Name);
                    if (isFirstInputFile)   //If the file is the first file of a video slice.
                    {
                        cmd += filePath;
                        isFirstInputFile = false;
                    }
                    else
                    {
                        cmd += "+" + filePath;
                    }

                    

                    //If the current URL is not the first one and next video slice is found.
                    if (urlKey > 1)
                    {
                        string preVideoId = GetVideoId(urls[urlKey - 1]);
                        string curVideoId = GetVideoId(urls[urlKey]);
                        if (null == preVideoId || null == curVideoId)
                        {
                            return;
                        }

                        if (!curVideoId.Equals(preVideoId))
                        {
                            cmd += String.Format(" \"{0}\\{1:D2}_VideoSlice.vslice\"", downloadDir, subCmdCount++);
                            subCmds.Add(cmd);
                            cmd = "copy /b ";
                            isFirstInputFile = true;
                        }
                    }

                    //If next URL key doesn't exist.
                    if (!urls.ContainsKey(++urlKey))
                    {
                        cmd += String.Format(" \"{0}\\{1:D2}_VideoSlice.vslice\"", downloadDir, subCmdCount++);
                        subCmds.Add(cmd);
                    }
                }

#if DEBUG
                WriteCommandStringLog("Sub merge commands:\n");
#else
                if (EnableDebug.IsChecked.HasValue && (bool) EnableDebug.IsChecked)
	            {
                    WriteCommandStringLog("Sub merge commands:\n");
	            }         
#endif
                //Create video slices.
                foreach (string subCmd in subCmds)
                {
#if DEBUG
                    WriteCommandStringLog(subCmd);
#else
                    if (EnableDebug.IsChecked.HasValue && (bool) EnableDebug.IsChecked)
	                {
                        WriteCommandStringLog(cmd);
	                }         
#endif
                    CommonUtilities.RunCmd(subCmd, out output);
                }
#if DEBUG
                WriteCommandStringLog("\n");
#else
                if (EnableDebug.IsChecked.HasValue && (bool) EnableDebug.IsChecked)
	            {
                    WriteCommandStringLog("\n");
	            }         
#endif

                //Create final video file.
                //Not used for now.
//                FileInfo[] videoSlices = dirInfo.GetFiles("*.vslice");
//                if (videoSlices.Length < 1)
//                {
//                    InfoTexts.Text = "Cannot find vslice files.";
//                    return;
//                }

//                var videoSlicesByAscendingName = from f in videoSlices
//                                                 orderby f.Name ascending
//                                                 select f;

//                cmd = "copy /b ";
//                isFirstInputFile = true;
//                foreach (var f in videoSlicesByAscendingName)
//                {
//                    filePath = String.Format("\"{0}\\{1}\"", downloadDir, f.Name);
//                    if (isFirstInputFile)
//                    {
//                        cmd += filePath;
//                        isFirstInputFile = false;
//                    }
//                    else
//                    {
//                        cmd += "+" + filePath;
//                    }
//                }

//                //Merged file has the same name of m3u8 file.
//                string mergedFileName = latestM3u8File.Name.Substring(0, latestM3u8File.Name.LastIndexOf('.')).Trim();
//                mergedFileName = CommonUtilities.ReplaceNonNumberEnglishChineseCharsByUnderscore(mergedFileName) + ".ts";
//                cmd += String.Format(" \"{0}\\{1}\"", downloadDir, mergedFileName);
//#if DEBUG
//                WriteCommandStringLog("Final merge command:\n");
//                WriteCommandStringLog(cmd);
//#else
//                if (EnableDebug.IsChecked.HasValue && (bool) EnableDebug.IsChecked)
//	            {
//                    WriteCommandStringLog("Final merge command:\n");
//                    WriteCommandStringLog(cmd);
//	            }         
//#endif
//                CommonUtilities.RunCmd(cmd, out output);

                if (OutputMergeCmdResult.IsChecked.HasValue && (bool) OutputMergeCmdResult.IsChecked)
                {
                    InfoTexts.Text = output + "\nMerge opreation is done. Check the above information for the cause if expected files are not generated.";
                    return;
                }

                InfoTexts.Text = "Merge opreation is done. Check \"显示合并命令输出结果\" and click \"检查缺失文件\" for the cause if expected files are not generated.";
            }

            private void Window_Loaded(object sender, RoutedEventArgs e)
            {
                FrameworkElement element = sender as FrameworkElement;
                element.MinHeight = element.ActualHeight;
            }

            private void DownloadMissingFilesBtn_Click(object sender, RoutedEventArgs e)
            {
                //Set download directory to a folder with the same name of m3u8 file in m3u8 directory.
                FileInfo latestM3u8File = CommonUtilities.GetLatestM3u8File(M3u8FileDir.Text);
                if (null == latestM3u8File)
                {
                    InfoTexts.Text = "Cannot find m3u8 file.";
                    return;
                }

                string videoName = latestM3u8File.Name.Substring(0, latestM3u8File.Name.LastIndexOf('.')).Trim();
                videoName = CommonUtilities.ReplaceNonNumberEnglishChineseCharsByUnderscore(videoName);
                string downloadDir = M3u8FileDir.Text.TrimEnd('\\') + "\\" + videoName;

                DownloadByThunder(missingFileUrls, downloadDir);

                CheckMissingFiles(missingFileUrls, downloadDir);
            }

            private void CheckMissingFilesBtn_Click(object sender, RoutedEventArgs e)
            {
                FileInfo latestM3u8File = CommonUtilities.GetLatestM3u8File(M3u8FileDir.Text);
                if (null == latestM3u8File)
                {
                    InfoTexts.Text = "Cannot find m3u8 file.";
                    return;
                }

                Dictionary<int, string> urls = GetUrlsFromM3u8File(latestM3u8File.FullName);
                expectedFileCount = urls.Count;

                string downloadDir = GetDownloadDirectory();

                DownloadMissingFilesBtn.IsEnabled = false;
                CheckMissingFiles(urls, downloadDir);
            }
        }
    }
}

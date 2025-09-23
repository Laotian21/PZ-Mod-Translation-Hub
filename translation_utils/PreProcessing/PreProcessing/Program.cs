using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PreProcessing
{
    class Program
    {
        //翻译条目
        struct TranslationEntry
        {
            public string OriginalText; // 原文
            public string SChiinese; // 简体中文
            public bool IsSChineseTranslated; // 是否已翻译
            public List<string> Comment; // 注释
        }

        //存储ModInfo的map，key为ModId value为ModName
        static Dictionary<string, string> modInfos = new Dictionary<string, string>();
        //存储翻译条目
        static Dictionary<string, Dictionary<string, TranslationEntry>> ModTranslations = new Dictionary<string, Dictionary<string, TranslationEntry>>();

        static void Main(string[] args)
        {
            int errorCount = 0;  // 添加错误计数器

            // 获取可执行文件的完整路径
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            Console.WriteLine($"Exe path: {exePath}");

            // 获取可执行文件所在目录
            string? currentDir = Path.GetDirectoryName(exePath);

            // 向上查找 translation_utils 目录
            string? foundParent = null;
            var repoDir = currentDir;
            while (!string.IsNullOrEmpty(repoDir))
            {
                string candidate = Path.Combine(repoDir, "translation_utils");
                if (Directory.Exists(candidate))
                {
                    foundParent = repoDir;
                    break;
                }
                repoDir = Path.GetDirectoryName(repoDir);
            }

            if (foundParent == null)
            {
                throw new DirectoryNotFoundException("Error: directory not found <repo_dir>\translation_utils");
            }

            // 拼接 data\output_files 路径
            string outputFilesPath = Path.Combine(foundParent, "data", "output_files");

            // 检查目录是否存在
            if (!Directory.Exists(outputFilesPath))
            {
                throw new DirectoryNotFoundException($"Error: directory not found {outputFilesPath}");
            }

            // 读取所有子目录
            string[] subDirs = Directory.GetDirectories(outputFilesPath);
            foreach (var subDir in subDirs)
            {
                //获取subDir的名称
                string dirName = Path.GetFileName(subDir);
                //分割名称，获取ModName和ModId
                //从右查找第一个下划线,分割成两部分
                int lastUnderscoreIndex = dirName.LastIndexOf('_');
                if (lastUnderscoreIndex == -1)
                {
                    Console.WriteLine($"::error file={dirName}::Wrong directory format");
                    errorCount++;
                    continue;
                }
                string modName = dirName.Substring(0, lastUnderscoreIndex);
                string modId = dirName.Substring(lastUnderscoreIndex + 1);
                modInfos[modId] = modName;

                //读取En_output.txt文件
                string enFilePath = Path.Combine(subDir, "EN_output.txt");
                if (!File.Exists(enFilePath))
                {
                    Console.WriteLine($"::error file={enFilePath}::Missing file");
                    errorCount++;
                    continue;
                }
                errorCount += ExtractENText(enFilePath, modId);

                //读取CN_output.txt文件
                string cnOldFilePath = Path.Combine(subDir, "CN_output.txt");
                if (!File.Exists(cnOldFilePath))
                {
                    Console.WriteLine($"::error file={cnOldFilePath}::Missing file");
                    errorCount++;
                    continue;
                }
                errorCount += ExtractOldCNText(cnOldFilePath, modId);

                //读取repoDir\data\completed_files\<modId>\en_completed.txt文件
                string cnNewFilePath = Path.Combine(foundParent, "data", "completed_files", modId, "en_completed.txt");
                if (!File.Exists(cnNewFilePath))
                {
                    Console.WriteLine($"::error file={cnNewFilePath}::Missing file");
                    errorCount++;
                    continue;
                }
                errorCount += ExtractNewCNText(cnNewFilePath, modId);
            }

            //检查repoDir\data\translations_CN.txt是否存在，不存在则创建一个空文件
            string outputTranslationFilePath = Path.Combine(foundParent, "data", "translations_CN.txt");
            if (!File.Exists(outputTranslationFilePath))
            {
                File.Create(outputTranslationFilePath).Close();
            }
            //打开repoDir\data\translations_CN.txt，读取内容
            var linesInFile = File.ReadAllLines(outputTranslationFilePath);
            //复制一份ModTranslations
            var modTranslationsCopy = new Dictionary<string, Dictionary<string, TranslationEntry>>(ModTranslations);
            List<string> tempComments = new List<string>();
            foreach (var line in linesInFile)
            {
                //忽略空行和------开头的行
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("------"))
                {
                    continue;
                }
                //是否是注释行
                if (IsNullOrCommentLine(line))
                {
                    tempComments.Add(line);
                    continue;
                }
                //是否是原文行，格式为 <modId>::EN::<key> = "<originalText>",
                var originalMatch1 = Regex.Match(line, @"^\t\t(?<modId>[^:]+)::EN::(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*");
                if (originalMatch1.Success)
                {
                    string currentModId = originalMatch1.Groups["modId"].Value.Trim();
                    string key = originalMatch1.Groups["key"].Value.Trim();
                    string originalText = originalMatch1.Groups["text"].Value;

                    //存储comments到对应的条目中
                    if (currentModId != null && modTranslationsCopy.ContainsKey(currentModId) && modTranslationsCopy[currentModId].ContainsKey(key))
                    {
                        var entry = modTranslationsCopy[currentModId][key];
                        entry.Comment.AddRange(tempComments);
                        tempComments.Clear();
                        modTranslationsCopy[currentModId][key] = entry;
                    }
                }
                //是否是翻译文本行，格式为 <modId>::CN::<key> = "<originalText>",
                var translationMatch1 = Regex.Match(line, @"^\t\t(?<modId>[^:]+)::CN::(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*");
                if (translationMatch1.Success)
                {
                    string currentModId = originalMatch1.Groups["modId"].Value.Trim();
                    //存储comments到对应的条目中
                    if (currentModId != null && modTranslationsCopy.ContainsKey(currentModId))
                    {
                        string key = originalMatch1.Groups["key"].Value.Trim();
                        if (modTranslationsCopy[currentModId].ContainsKey(key))
                        {
                            var entry = modTranslationsCopy[currentModId][key];
                            entry.Comment.AddRange(tempComments);
                            tempComments.Clear();
                            modTranslationsCopy[currentModId][key] = entry;
                        }
                    }
                }

                //是否是已翻译的原文行，格式为 <modId>::EN::<key> = "<originalText>",
                var originalMatch2 = Regex.Match(line, @"^(?<modId>[^:]+)::EN::(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*");
                if (originalMatch2.Success)
                {
                    string currentModId = originalMatch2.Groups["modId"].Value.Trim();
                    string key = originalMatch2.Groups["key"].Value.Trim();
                    string originalText = originalMatch2.Groups["text"].Value;

                    //存储到对应的条目中，并检查原文是否已经改变
                    if (currentModId != null && modTranslationsCopy.ContainsKey(currentModId) && modTranslationsCopy[currentModId].ContainsKey(key))
                    {
                        var entry = modTranslationsCopy[currentModId][key];
                        entry.Comment.AddRange(tempComments);
                        tempComments.Clear();

                        if (entry.OriginalText.Equals(originalText))//原文没有改变
                        {
                            entry.IsSChineseTranslated = true;
                        }
                        else//原文改变
                        {
                            //如果翻译文本为空
                            if (entry.OriginalText.Equals(""))
                            {
                                entry.OriginalText = originalText;
                            }

                            //标记为未翻译
                            entry.IsSChineseTranslated = false;
                        }
                        modTranslationsCopy[currentModId][key] = entry;
                    }
                }
                //是否是已翻翻译文本行，格式为 <modId>::CN::<key> = "<originalText>",
                var translationMatch2 = Regex.Match(line, @"^(?<modId>[^:]+)::CN::(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*");
                if (translationMatch2.Success)
                {
                    string currentModId = translationMatch2.Groups["modId"].Value.Trim();
                    string key = translationMatch2.Groups["key"].Value.Trim();
                    string originalText = translationMatch2.Groups["text"].Value;

                    //存储到对应的条目中
                    if (currentModId != null && modTranslationsCopy.ContainsKey(currentModId) && modTranslationsCopy[currentModId].ContainsKey(key))
                    {
                        var entry = modTranslationsCopy[currentModId][key];
                        entry.Comment.AddRange(tempComments);
                        tempComments.Clear();
                        entry.SChiinese = originalText;

                        modTranslationsCopy[currentModId][key] = entry;
                    }
                }
            }

            //打开repoDir\data\translations_CN.txt，清空文件，写入新内容
            using (var writer = new StreamWriter(outputTranslationFilePath, false))
            {
                foreach (var modId in modTranslationsCopy.Keys)
                {
                    if (!modInfos.ContainsKey(modId))
                    {
                        continue;
                    }
                    string modName = modInfos[modId];
                    writer.WriteLine();
                    writer.WriteLine($"------ {modId} :: {modName} ------");
                    writer.WriteLine();
                    foreach (var key in modTranslationsCopy[modId].Keys)
                    {
                        var entry = modTranslationsCopy[modId][key];
                        string prefix = entry.IsSChineseTranslated ? "" : "\t\t";
                        //写入注释
                        foreach (var comment in entry.Comment)
                        {
                            writer.WriteLine(prefix + comment);
                        }
                        //写入原文行
                        writer.WriteLine($"{prefix}{modId}::EN::{key} = \"{entry.OriginalText}\",");
                        //写入翻译文本行
                        writer.WriteLine($"{prefix}{modId}::CN::{key} = \"{entry.SChiinese}\",");
                    }
                    writer.WriteLine();
                }
            }

            // 3. 在 Main 方法结尾检查错误并退出
            if (errorCount > 0)
            {
                Console.WriteLine($"::error::总共发现 {errorCount} 个错误，程序退出");
                Environment.Exit(1);  // 非零退出码表示失败
            }
        }

        static int ExtractENText(string outputFilePath, string modId)
        {
            int errorCount = 0;
            var translationEntries = new Dictionary<string, TranslationEntry>();
            //读取En_output.txt全文，到一个字符串
            string outputContent = File.ReadAllText(outputFilePath);
            //匹配并移除 "..\n"
            outputContent = Regex.Replace(outputContent, @"""\s*\.\.\s*\n?\s*""", "");

            //拆分行
            var lines = outputContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 用正则表达式解析每一行，并存储到 OriginalText 和 SChiinese 中，标记为未翻译
            foreach (var line in lines)
            {
                var match = Regex.Match(
                    line,
                    @"^(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*"
                );
                if (match.Success)
                {
                    string key = match.Groups["key"].Value.Trim();
                    string originalText = match.Groups["text"].Value;
                    translationEntries[key] = new TranslationEntry
                    {
                        OriginalText = originalText,
                        SChiinese = "",
                        IsSChineseTranslated = false,
                        Comment = new List<string>()
                    };
                }
                else
                {
                    //判断是否是空行或注释行
                    if (IsNullOrCommentLine(line))
                    {
                        continue;
                    }
                    //输出错误的文件名(不包含)和行号码以及内容
                    Console.WriteLine($"::error file={outputFilePath},line={Array.IndexOf(lines, line) + 1}::Format Error: {line}");
                    errorCount++;
                }
            }
            ModTranslations[modId] = translationEntries;
            return errorCount;
        }

        static int ExtractOldCNText(string outputFilePath, string modId)
        {
            int errorCount = 0;
            //读取CN_output.txt全文，到一个字符串
            string outputContent = File.ReadAllText(outputFilePath);
            //匹配并移除 "..\n"
            outputContent = Regex.Replace(outputContent, @"""\s*\.\.\s*\n?\s*""", "");

            //拆分行
            var lines = outputContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 用正则表达式解析每一行，并存储到 OriginalText 和 SChiinese 中，标记为未翻译
            foreach (var line in lines)
            {
                var match = Regex.Match(
                    line,
                    @"^(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*"
                );
                if (match.Success)
                {
                    string key = match.Groups["key"].Value.Trim();
                    string originalText = match.Groups["text"].Value;

                    //检测ModTranslations[modId]是否包含key，如果包含则更新SChiinese，否则新增一条记录，保持IsSChineseTranslated为false，此时还没有进行核对
                    if (!ModTranslations.ContainsKey(modId))
                    {
                        ModTranslations[modId] = new Dictionary<string, TranslationEntry>();
                    }
                    if (!ModTranslations[modId].ContainsKey(key))
                    {
                        var entry = new TranslationEntry
                        {
                            OriginalText = "======Original Text Missing====",
                            SChiinese = originalText,
                            IsSChineseTranslated = false,
                            Comment = new List<string>()
                        };
                        ModTranslations[modId][key] = entry;
                        continue;
                    }
                    else 
                    {
                        var entry = ModTranslations[modId][key];
                        entry.SChiinese = originalText;
                        entry.IsSChineseTranslated = false;
                        ModTranslations[modId][key] = entry;
                        continue;
                    }
                }
                else
                {
                    //判断是否是空行或注释行
                    if (IsNullOrCommentLine(line))
                    {
                        continue;
                    }
                    //输出错误的文件名(不包含)和行号码以及内容
                    Console.WriteLine($"::error file={outputFilePath},line={Array.IndexOf(lines, line) + 1}::Format Error: {line}");
                    errorCount++;
                }
            }
            return errorCount;
        }
        static int ExtractNewCNText(string outputFilePath, string modId)
        {
            int errorCount = 0;
            //读取CN_output.txt全文，到一个字符串
            string outputContent = File.ReadAllText(outputFilePath);
            //匹配并移除 "..\n"
            outputContent = Regex.Replace(outputContent, @"""\s*\.\.\s*\n?\s*""", "");

            //拆分行
            var lines = outputContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 用正则表达式解析每一行，并存储到 OriginalText 和 SChiinese 中，标记为未翻译
            foreach (var line in lines)
            {
                var match = Regex.Match(
                    line,
                    @"^(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*"
                );
                if (match.Success)
                {
                    string key = match.Groups["key"].Value.Trim();
                    string originalText = match.Groups["text"].Value;

                    //检测ModTranslations[modId]是否包含key，如果包含则更新SChiinese，否则新增一条记录，保持IsSChineseTranslated为false，此时还没有进行核对
                    if (!ModTranslations.ContainsKey(modId))
                    {
                        ModTranslations[modId] = new Dictionary<string, TranslationEntry>();
                    }
                    if (!ModTranslations[modId].ContainsKey(key))
                    {
                        var entry = new TranslationEntry
                        {
                            OriginalText = "======Original Text Missing====",
                            SChiinese = originalText,
                            IsSChineseTranslated = false,
                            Comment = new List<string>()
                        };
                        ModTranslations[modId][key] = entry;
                        continue;
                    }
                    else
                    {
                        var entry = ModTranslations[modId][key];
                        entry.SChiinese = originalText;
                        entry.IsSChineseTranslated = true;
                        ModTranslations[modId][key] = entry;
                        continue;
                    }
                }
                else
                {
                    //判断是否是空行或注释行
                    if (IsNullOrCommentLine(line))
                    {
                        continue;
                    }
                    //输出错误的文件名(不包含)和行号码以及内容
                    Console.WriteLine($"::error file={outputFilePath},line={Array.IndexOf(lines, line) + 1}::Format Error: {line}");
                    errorCount++;
                }
            }
            return errorCount;
        }
        static bool IsNullOrCommentLine(string line)
        {
            return string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("#") || line.TrimStart().StartsWith("/*") || line.TrimStart().StartsWith("*") || line.TrimStart().StartsWith("*/") || line.TrimStart().StartsWith("--");
        }
    }
}
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PostProcessing
{
    class Program
    {
        //翻译条目
        struct TranslationEntry
        {
            public string OriginalText; // 原文
            public string SChiinese; // 简体中文
        }
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
            string? repoDIr = null;
            var searchDir = currentDir;
            while (!string.IsNullOrEmpty(searchDir))
            {
                string candidate = Path.Combine(searchDir, "translation_utils");
                if (Directory.Exists(candidate))
                {
                    repoDIr = searchDir;
                    break;
                }
                searchDir = Path.GetDirectoryName(searchDir);
            }

            //如果无法通过exe路径获取repo目录，则尝试通过工作目录获取repo目录
            //如果无法通过exe路径获取repo目录，则尝试通过工作目录获取repo目录
            if (repoDIr == null)
            {
                // 获取当前工作目录
                string workingDir = Directory.GetCurrentDirectory();
                Console.WriteLine($"Working directory: {workingDir}");

                // 从工作目录开始向上查找 translation_utils 目录
                searchDir = workingDir;
                while (!string.IsNullOrEmpty(searchDir))
                {
                    string candidate = Path.Combine(searchDir, "translation_utils");
                    if (Directory.Exists(candidate))
                    {
                        repoDIr = searchDir;
                        break;
                    }
                    searchDir = Path.GetDirectoryName(searchDir);
                }
            }

            if (repoDIr == null)
            {
                throw new DirectoryNotFoundException($"Error: repo not found");
            }

            // 拼接  translation 文件路径
            string translationFilePath = Path.Combine(repoDIr, "data", "translations_CN.txt");
            //检查repoDir\data\translations_CN.txt是否存在，不存在则爬出异常并退出
            if (!File.Exists(translationFilePath))
            {
                throw new FileNotFoundException($"Error: file not found: {translationFilePath}");
            }
            //打开repoDir\data\translations_CN.txt，读取内容
            var linesInFile = File.ReadAllLines(translationFilePath);
            foreach (var line in linesInFile)
            {
                //忽略空行和注释行
                if (IsNullOrCommentLine(line))
                {
                    continue;
                }
                //是否是原文行，格式为 <modId>::EN::<key> = "<originalText>",
                var originalMatch = Regex.Match(line, @"^(?<modId>[^:]+)::EN::(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*");
                if (originalMatch.Success)
                {
                    string currentModId = originalMatch.Groups["modId"].Value.Trim();
                    string key = originalMatch.Groups["key"].Value.Trim();
                    string originalText = originalMatch.Groups["text"].Value;

                    if (!ModTranslations.ContainsKey(currentModId))
                    {
                        ModTranslations[currentModId] = new Dictionary<string, TranslationEntry>();
                    }
                    ModTranslations[currentModId][key] = new TranslationEntry
                    {
                        OriginalText = originalText,
                        SChiinese = originalText,
                    };
                    continue;
                }
                //是否是翻译文本行，格式为 <modId>::CN::<key> = "<originalText>",
                var translationMatch = Regex.Match(line, @"^(?<modId>[^:]+)::CN::(?<key>[^=]+)=\s*""(?<text>.*)""\s*,?\S*");
                if (translationMatch.Success)
                {
                    string currentModId = translationMatch.Groups["modId"].Value.Trim();
                    string key = translationMatch.Groups["key"].Value.Trim();
                    string originalText = translationMatch.Groups["text"].Value;

                    //存储到对应的条目中
                    var entry = ModTranslations[currentModId][key];
                    if (!originalText.Equals(""))
                    {
                        entry.SChiinese = originalText;
                    }
                    ModTranslations[currentModId][key] = entry;
                }

            }

            // 拼接 output_file 路径
            string outputFilePath = Path.Combine(repoDIr, "data", "PZ-Mod-Translation.txt");
            //打开repoDir\data\.txt，清空文件，写入新内容
            using (var writer = new StreamWriter(outputFilePath, false))
            {
                foreach (var modId in ModTranslations.Keys)
                {
                    writer.WriteLine();
                    writer.WriteLine($"------ {modId} ------");
                    writer.WriteLine();
                    foreach (var key in ModTranslations[modId].Keys)
                    {
                        var entry = ModTranslations[modId][key];
                        //写入翻译文本行
                        writer.WriteLine($"{key} = \"{entry.SChiinese}\",");
                    }
                    writer.WriteLine();
                }
            }
        }

        static bool IsNullOrCommentLine(string line)
        {
            return string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("#") || line.TrimStart().StartsWith("/*") || line.TrimStart().StartsWith("*") || line.TrimStart().StartsWith("*/") || line.TrimStart().StartsWith("--");
        }
    }
}
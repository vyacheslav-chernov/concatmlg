using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static string _filterArg = null;
    static List<string> _excludePatterns = new List<string>();
    static readonly StringComparison _comparison = StringComparison.OrdinalIgnoreCase;

    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Обработка аргумента командной строки
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            _filterArg = args[0].Trim();
            Console.WriteLine($"Filter: '{_filterArg}' (case insensitive)");
        }

        var timer = Stopwatch.StartNew();
        var config = ReadIniFile("config.ini");
        var result = new ConcurrentQueue<string>();

        Console.WriteLine($"Starting processing (using {Environment.ProcessorCount} cores)");

        Parallel.ForEach(config, entry => {
            ProcessFile(entry.Value, entry.Key, result); // Ключ - метка, значение - путь
        });

        Console.WriteLine($"\nFiles processed in {timer.Elapsed.TotalSeconds:N3} sec");
        Console.WriteLine("Starting sorting...");
        var sortTimer = Stopwatch.StartNew();

        var finalLines = result.AsParallel();

        // Дополнительная фильтрация
        if (_filterArg != null)
        {
            finalLines = finalLines.Where(line =>
                line.Contains(_filterArg, _comparison));
        }

        var sortedLines = finalLines
            .OrderBy(line => {
                var parts = line.Split(';');
                return (parts[0], parts[1]); // Сортировка по дате и времени
            })
            .ToList();

        Console.WriteLine($"Sorting completed in {sortTimer.Elapsed.TotalSeconds:N3} sec");
        Console.WriteLine($"Total lines: {sortedLines.Count:N0}");

        File.WriteAllLines("res.txt", sortedLines, Encoding.UTF8);
        Console.WriteLine($"\nTotal execution time: {timer.Elapsed.TotalSeconds:N3} sec");
    }

    static void ProcessFile(string filePath, string label, ConcurrentQueue<string> result)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
        using var reader = new StreamReader(stream, Encoding.GetEncoding(1251), true, 65536);

        string line;
        var batch = new List<string>(100000);

        while ((line = reader.ReadLine()) != null)
        {
            // Проверка формата строки
            if (line.Length < 9 || line[8] != ';' || !IsAllDigits(line.AsSpan(0, 8)))
                continue;

            // Очистка от управляющих символов
            var cleanLine = RemoveControlChars(line);
            var fullLine = $"{cleanLine};{label}";

            // Проверка на исключаемые подстроки
            if (_excludePatterns.Any(pattern => ContainsIgnoreCase(cleanLine, pattern)))
                continue;

            // Фильтрация при чтении (если задан фильтр)
            if (_filterArg == null || ContainsIgnoreCase(cleanLine, _filterArg))
            {
                batch.Add(fullLine);
            }

            if (batch.Count >= 100000)
            {
                foreach (var item in batch) result.Enqueue(item);
                batch.Clear();
            }
        }

        foreach (var item in batch) result.Enqueue(item);
    }

    // Оптимизированная очистка строки (только управляющие символы)
    static string RemoveControlChars(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (!char.IsControl(c)) sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    // Надежная проверка подстроки для кириллицы
    static bool ContainsIgnoreCase(string source, string value)
    {
        return source.IndexOf(value, _comparison) >= 0;
    }

    static bool IsAllDigits(ReadOnlySpan<char> span)
    {
        foreach (char c in span)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }

    static Dictionary<string, string> ReadIniFile(string iniPath)
    {
        var config = new Dictionary<string, string>();
        if (!File.Exists(iniPath))
        {
            Console.WriteLine($"Configuration file not found: {iniPath}");
            return config;
        }

        foreach (var line in File.ReadLines(iniPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(";"))
                continue;

            if (line.Trim().StartsWith("[") && line.Trim().EndsWith("]"))
            {
                // Секции обрабатываются отдельно
                continue;
            }

            var sepIndex = line.IndexOf('=');
            if (sepIndex <= 0) continue;

            var key = line.Substring(0, sepIndex).Trim();
            var value = line.Substring(sepIndex + 1).Trim();

            if (key.Equals("exclude", StringComparison.OrdinalIgnoreCase))
            {
                // Добавляем подстроки для исключения
                _excludePatterns.AddRange(
                    value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(p => p.Trim())
                         .Where(p => !string.IsNullOrEmpty(p)));
            }
            else
            {
                // Добавляем путь к файлу с меткой (ключ=метка, значение=путь)
                config[key] = value;
            }
        }

        return config;
    }
}
using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BaoToolsGui.AI.Knowledge.Models;

namespace BaoToolsGui.AI.Discovery;

public static class KnowledgeGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static bool Generate(string outputDirectory)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("   BaoTools Knowledge Generator");
        Console.WriteLine("========================================");
        Console.WriteLine("Scanning project codebase...");

        var manifest = CodebaseScanner.ScanSystem();

        Console.WriteLine($"Discovered Screens:     {manifest.Screens.Count}");
        Console.WriteLine($"Discovered Settings:    {manifest.Settings.Count}");
        Console.WriteLine($"Discovered Tools:       {manifest.Tools.Count}");
        Console.WriteLine($"Discovered Commands:    {manifest.Commands.Count}");
        Console.WriteLine($"Discovered Services:    {manifest.Services.Count}");
        Console.WriteLine($"Discovered Features:    {manifest.Features.Count}");
        Console.WriteLine($"Discovered Workflows:   {manifest.Workflows.Count}");
        Console.WriteLine($"Discovered Limitations: {manifest.Limitations.Count}");

        // Validate integrity
        Console.WriteLine("\nValidating knowledge consistency...");
        var (isValid, errors) = Validate(manifest);
        if (!isValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Validation FAILED with errors:");
            foreach (var err in errors)
            {
                Console.WriteLine($" - {err}");
            }
            Console.ResetColor();
            return false;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Validation passed cleanly (0 broken references, 0 duplicates).");
        Console.ResetColor();

        // Write files
        Directory.CreateDirectory(outputDirectory);

        WriteJson(Path.Combine(outputDirectory, "SystemManifest.json"), manifest);
        WriteJson(Path.Combine(outputDirectory, "Screens.json"), manifest.Screens);
        WriteJson(Path.Combine(outputDirectory, "Settings.json"), manifest.Settings);
        WriteJson(Path.Combine(outputDirectory, "Tools.json"), manifest.Tools);
        WriteJson(Path.Combine(outputDirectory, "Commands.json"), manifest.Commands);
        WriteJson(Path.Combine(outputDirectory, "Services.json"), manifest.Services);
        WriteJson(Path.Combine(outputDirectory, "Features.json"), manifest.Features);
        WriteJson(Path.Combine(outputDirectory, "Workflows.json"), manifest.Workflows);
        WriteJson(Path.Combine(outputDirectory, "Limitations.json"), manifest.Limitations);

        Console.WriteLine($"\n✓ All 9 Knowledge JSON files generated successfully at: {outputDirectory}\n");
        return true;
    }

    public static (bool isValid, List<string> errors) Validate(SystemManifest manifest)
    {
        var errors = new List<string>();

        // 1. Check duplicate IDs
        CheckDuplicates(manifest.Screens.Select(x => x.Id), "Screens", errors);
        CheckDuplicates(manifest.Settings.Select(x => x.Id), "Settings", errors);
        CheckDuplicates(manifest.Tools.Select(x => x.Name), "Tools", errors);
        CheckDuplicates(manifest.Features.Select(x => x.Id), "Features", errors);
        CheckDuplicates(manifest.Workflows.Select(x => x.Id), "Workflows", errors);
        CheckDuplicates(manifest.Limitations.Select(x => x.Id), "Limitations", errors);

        // 2. Check feature references
        var screenIds = manifest.Screens.Select(s => s.Id).ToHashSet();
        var toolNames = manifest.Tools.Select(t => t.Name).ToHashSet();

        foreach (var feat in manifest.Features)
        {
            foreach (var scr in feat.RelatedScreens)
            {
                if (!screenIds.Contains(scr))
                    errors.Add($"Feature '{feat.Id}' references missing screen '{scr}'");
            }
            foreach (var tool in feat.RelatedTools)
            {
                if (!toolNames.Contains(tool))
                    errors.Add($"Feature '{feat.Id}' references missing tool '{tool}'");
            }
        }

        return (errors.Count == 0, errors);
    }

    private static void CheckDuplicates(IEnumerable<string> items, string category, List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                errors.Add($"Found empty or null identifier in {category}.");
                continue;
            }
            if (!seen.Add(item))
            {
                errors.Add($"Duplicate identifier '{item}' detected in {category}.");
            }
        }
    }

    private static void WriteJson<T>(string filePath, T data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(filePath, json);
    }
}

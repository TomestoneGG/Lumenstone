namespace Lumenstone;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Excel.GeneratedSheets;

class Program
{
    static void Main(string[] args)
     {
        if (args.Length < 2)
        {
            Console.WriteLine("Please provide the path to the sqpack directory as a command-line argument as well as the patch name.");
            return;
        }

        JsonSerializerOptions options = new() {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true,
            Converters = { new SeStringConverter(), new LazyRowConverterFactory() }
        };

        // Initialize Lumina with the sqpack path
        
        string sqpackPath = args[0]; // Assuming the first argument is the sqpack path
        
        var luminaEN = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.English });
        var luminaDE = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.German });
        var luminaFR = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.French });
        var luminaJA = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.Japanese });
        
        var patch = args[1];

        ExtractSheetForAllLanguages<ClassJob>(patch, luminaEN, luminaDE, luminaFR, luminaJA, options);
    }

    static void ExtractSheetForAllLanguages<T>(string patch, Lumina.GameData luminaEN, Lumina.GameData luminaDE, Lumina.GameData luminaFR, Lumina.GameData luminaJA, 
                                            JsonSerializerOptions options) where T : Lumina.Excel.ExcelRow
    {
        ExtractSheet<T>(luminaEN, patch, "en", options);
        ExtractSheet<T>(luminaDE, patch, "de", options);
        ExtractSheet<T>(luminaFR, patch, "fr", options);
        ExtractSheet<T>(luminaJA, patch, "ja", options);
    }

    static void ExtractSheet<T>(Lumina.GameData lumina, string patch, string lang, JsonSerializerOptions options) where T : Lumina.Excel.ExcelRow
    {
        // Get the ClassJobs Excel sheet
        var sheet = lumina.GetExcelSheet<T>();

        if (sheet == null)
            return;

        // Extract data and save as JSON
        var sheetData = new List<T>();
        foreach (var row in sheet)
        {
            sheetData.Add(row);
        }

        // Serialize the object
        string json = JsonSerializer.Serialize(sheetData, options);

        string fileName = $"{typeof(T).Name}.json";
        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"patches/{patch}/{lang}/");

        // Ensure the directory exists
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string filePath = Path.Combine(directoryPath, fileName);

        File.WriteAllText(filePath, json);

        Console.WriteLine($"{typeof(T).Name} data extracted and saved to {fileName}");

    }
}

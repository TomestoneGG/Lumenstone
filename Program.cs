namespace Lumenstone;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        // Get all types in the Lumina.Excel.GeneratedSheets namespace
        var types = Assembly.GetAssembly(typeof(Lumina.Excel.GeneratedSheets2.Item)).GetTypes()
            .Where(t => t.Namespace == "Lumina.Excel.GeneratedSheets2" && t.IsSubclassOf(typeof(Lumina.Excel.ExcelRow)));

        MethodInfo generic = typeof(Program).GetMethod(nameof(ExtractSheetForAllLanguages), BindingFlags.Static | BindingFlags.NonPublic);
        if (generic == null)
            return;

        // Call ExtractSheetForAllLanguages for each type
        foreach (var type in types)
        {
            Console.WriteLine(type.Name);
            
            MethodInfo constructed = generic.MakeGenericMethod(type);
            constructed.Invoke(null, new object[] { patch, luminaEN, luminaDE, luminaFR, luminaJA, options });
        }
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

        const int cPageSize = 1000;
        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"patches/{patch}/{lang}/{typeof(T).Name}");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Extract data and save as JSON
        var sheetData = new List<T>();
        int currentPage = 1;
        foreach (var row in sheet)
        {
            sheetData.Add(row);
            if (sheetData.Count() >= cPageSize) {
                string jsonForPage = JsonSerializer.Serialize(sheetData, options);
                string fileNameForPage = $"{currentPage}.json";
                string filePathForPage = Path.Combine(directoryPath, fileNameForPage);
                File.WriteAllText(filePathForPage, jsonForPage);
                sheetData.Clear();
                Console.WriteLine($"{typeof(T).Name} data extracted and saved to {fileNameForPage}");
                currentPage++;
            }
        }

        if (sheetData.Count() == 0)
            return;

        // Serialize the object
        string json = JsonSerializer.Serialize(sheetData, options);
        string fileName = $"{currentPage}.json";
                
        string filePath = Path.Combine(directoryPath, fileName);

        File.WriteAllText(filePath, json);

        Console.WriteLine($"{typeof(T).Name} data extracted and saved to {fileName}");

    }
}

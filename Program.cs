using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Lumenstone;
class Program
{
    private const int DefaultIconStart = 1;
    private const int DefaultIconEnd = 250000;

    static IJsonTypeInfoResolver CreateLuminaIgnoreExcelPageResolver()
    {
        var resolver = new DefaultJsonTypeInfoResolver();

        resolver.Modifiers.Add(static typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                return;

            // Only touch Lumina Excel row/subrow structs (Action, Item, etc.)
            var t = typeInfo.Type;
            bool isLuminaRow =
                t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(IExcelRow<>) ||
                    i.GetGenericTypeDefinition() == typeof(IExcelSubrow<>)));

            if (!isLuminaRow)
                return;

            // Remove ExcelPage and RowOffset if present
            for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                if (typeInfo.Properties[i].Name == "ExcelPage" || typeInfo.Properties[i].Name == "RowOffset")
                    typeInfo.Properties.RemoveAt(i);
            }
        });

        return resolver;
    }

    static void Main(string[] args)
     {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run <sqpackPath> <patch> [full] [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --full                    Force re-export for icons/maps/loading images.");
            Console.WriteLine("  --icons-only              Skip JSON/maps/loading and export icons only.");
            Console.WriteLine($"  --icon-start <id>         First icon id to export (default: {DefaultIconStart}).");
            Console.WriteLine($"  --icon-end <id>           Last icon id to export (default: {DefaultIconEnd}).");
            Console.WriteLine("  --icon-range <start-end>  Export a contiguous icon range.");
            Console.WriteLine("  --icon-chunk-size <n>     Chunk size for icon range generation.");
            Console.WriteLine("  --icon-chunk-index <n>    Zero-based chunk index when using --icon-chunk-size.");
            Console.WriteLine("  --icons-full              Force overwrite icons in selected range/chunk.");
            return;
        }

         // Initialize Lumina with the sqpack path
        
        string sqpackPath = args[0]; // Assuming the first argument is the sqpack path
        var patch = args[1];

        var optionArgs = args.Skip(2).ToArray();
        bool full = HasFlag(optionArgs, "full") || HasFlag(optionArgs, "--full");
        bool iconsOnly = HasFlag(optionArgs, "--icons-only");
        bool hasIconRangeOption = HasFlag(optionArgs, "--icon-start")
            || HasFlag(optionArgs, "--icon-end")
            || HasFlag(optionArgs, "--icon-range")
            || HasFlag(optionArgs, "--icon-chunk-size")
            || HasFlag(optionArgs, "--icon-chunk-index");
        bool iconsFull = full || HasFlag(optionArgs, "--icons-full");

        int iconStart = DefaultIconStart;
        int iconEnd = DefaultIconEnd;

        if (TryGetOptionValue(optionArgs, "--icon-range", out var iconRange))
        {
            if (!TryParseRange(iconRange, out iconStart, out iconEnd))
            {
                Console.WriteLine($"Invalid --icon-range value '{iconRange}'. Expected format: <start-end>.");
                return;
            }
        }

        if (TryGetIntOption(optionArgs, "--icon-start", out var iconStartOverride))
            iconStart = iconStartOverride;

        if (TryGetIntOption(optionArgs, "--icon-end", out var iconEndOverride))
            iconEnd = iconEndOverride;

        if (TryGetIntOption(optionArgs, "--icon-chunk-size", out var chunkSize))
        {
            if (!TryGetIntOption(optionArgs, "--icon-chunk-index", out var chunkIndex))
            {
                Console.WriteLine("--icon-chunk-index is required when --icon-chunk-size is provided.");
                return;
            }

            if (chunkSize <= 0 || chunkIndex < 0)
            {
                Console.WriteLine("--icon-chunk-size must be > 0 and --icon-chunk-index must be >= 0.");
                return;
            }

            var chunkStart = iconStart + (chunkSize * chunkIndex);
            var chunkEnd = Math.Min(iconEnd, chunkStart + chunkSize - 1);
            if (chunkStart > iconEnd)
            {
                Console.WriteLine($"Chunk index {chunkIndex} is outside the requested icon range {iconStart}-{iconEnd}.");
                return;
            }

            iconStart = chunkStart;
            iconEnd = chunkEnd;
        }

        if (hasIconRangeOption)
        {
            // Explicit icon selection means "regenerate this icon slice only".
            iconsOnly = true;
            iconsFull = true;
        }

        if (iconStart < 1 || iconEnd < iconStart)
        {
            Console.WriteLine($"Invalid icon range {iconStart}-{iconEnd}. Ensure start >= 1 and end >= start.");
            return;
        }

        var luminaEN = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.English });
        var luminaDE = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.German });
        var luminaFR = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.French });
        var luminaJA = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.Japanese });
        
        var uiColors = luminaEN.GetExcelSheet<UIColor>();
        if (uiColors == null)
            throw new InvalidOperationException("Could not load UIColor sheet.");

        JsonSerializerOptions options = new() {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new SeStringConverter(uiColors), new LazyRowConverterFactory(), new LazySubrowConverterFactory() },
            TypeInfoResolver = CreateLuminaIgnoreExcelPageResolver(),
        };

        if (!iconsOnly)
        {
            // Get all types in the Lumina.Excel.GeneratedSheets namespace
           var types = Assembly.GetAssembly(typeof(Lumina.Excel.Sheets.Action))!.GetTypes()
             .Where(t => t.Namespace == "Lumina.Excel.Sheets"
                     && !t.IsAbstract
                     && t.GetInterfaces()
                         .Any(i => i.IsGenericType
                                   && i.GetGenericTypeDefinition() == typeof(Lumina.Excel.IExcelRow<>)
                                   && i.GenericTypeArguments[0] == t));

            MethodInfo? generic = typeof(Program).GetMethod(nameof(ExtractSheetForAllLanguages), BindingFlags.Static | BindingFlags.NonPublic);
            if (generic == null)
                return;

            // Call ExtractSheetForAllLanguages for each type
            foreach (var type in types)
            {
                Console.WriteLine(type.Name);

                MethodInfo constructed = generic.MakeGenericMethod(type);
                constructed.Invoke(null, new object[] { patch, luminaEN, luminaDE, luminaFR, luminaJA, options });
            }

            var subrowTypes = Assembly.GetAssembly(typeof(Lumina.Excel.Sheets.GilShopItem))!.GetTypes()
             .Where(t => t.Namespace == "Lumina.Excel.Sheets"
                     && !t.IsAbstract
                     && t.GetInterfaces()
                         .Any(i => i.IsGenericType
                                   && i.GetGenericTypeDefinition() == typeof(Lumina.Excel.IExcelSubrow<>)
                                   && i.GenericTypeArguments[0] == t));

            generic = typeof(Program).GetMethod(nameof(ExtractSubrowSheetForAllLanguages), BindingFlags.Static | BindingFlags.NonPublic);
            if (generic == null)
                return;

            // Call ExtractSheetForAllLanguages for each type
            foreach (var type in subrowTypes)
            {
                Console.WriteLine(type.Name);

                MethodInfo constructed = generic.MakeGenericMethod(type);
                constructed.Invoke(null, new object[] { patch, luminaEN, luminaDE, luminaFR, luminaJA, options });
            }

            ExtractMaps(luminaEN, full);
            ExtractLoadingImages(luminaEN, full);
        }

        Console.WriteLine($"Extracting icons from {iconStart} to {iconEnd} (full={iconsFull}).");
        ExtractIcons(iconStart, iconEnd, luminaEN, iconsFull);
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetOptionValue(string[] args, string optionName, out string value)
    {
        value = string.Empty;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
                continue;

            value = args[i + 1];
            return true;
        }

        return false;
    }

    private static bool TryGetIntOption(string[] args, string optionName, out int value)
    {
        value = 0;
        if (!TryGetOptionValue(args, optionName, out var rawValue))
            return false;

        if (!int.TryParse(rawValue, out value))
        {
            Console.WriteLine($"Invalid integer for {optionName}: '{rawValue}'.");
            return false;
        }

        return true;
    }

    private static bool TryParseRange(string range, out int start, out int end)
    {
        start = 0;
        end = 0;

        var parts = range.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        return int.TryParse(parts[0], out start) && int.TryParse(parts[1], out end);
    }

    static void ExtractSheetForAllLanguages<T>(string patch, Lumina.GameData luminaEN, Lumina.GameData luminaDE, Lumina.GameData luminaFR, Lumina.GameData luminaJA, 
                                            JsonSerializerOptions options) where T : struct, IExcelRow<T>
    {
        ExtractSheet<T>(luminaEN, patch, "en", options);
        ExtractSheet<T>(luminaDE, patch, "de", options);
        ExtractSheet<T>(luminaFR, patch, "fr", options);
        ExtractSheet<T>(luminaJA, patch, "ja", options);
    }

    static void ExtractSubrowSheetForAllLanguages<T>(string patch, Lumina.GameData luminaEN, Lumina.GameData luminaDE, Lumina.GameData luminaFR, Lumina.GameData luminaJA, 
                                            JsonSerializerOptions options) where T : struct, IExcelSubrow<T>
    {
        ExtractSubrowSheet<T>(luminaEN, patch, "en", options);
        ExtractSubrowSheet<T>(luminaDE, patch, "de", options);
        ExtractSubrowSheet<T>(luminaFR, patch, "fr", options);
        ExtractSubrowSheet<T>(luminaJA, patch, "ja", options);
    }

    static void ExtractSheet<T>(Lumina.GameData lumina, string patch, string lang, JsonSerializerOptions options) where T: struct, Lumina.Excel.IExcelRow<T>
    {
        // Get the ClassJobs Excel sheet
        var sheet = lumina.GetExcelSheet<T>();

        if (sheet == null) {
            return;
        }

        const int cPageSize = 500;
        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"json/{patch}/{lang}/{typeof(T).Name}");

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
            if (sheetData.Count >= cPageSize) {
                string jsonForPage = JsonSerializer.Serialize(sheetData, options);
                string fileNameForPage = $"{currentPage}.json";
                string filePathForPage = Path.Combine(directoryPath, fileNameForPage);
                File.WriteAllText(filePathForPage, jsonForPage);
                sheetData.Clear();
                Console.WriteLine($"{typeof(T).Name} data extracted and saved to {fileNameForPage}");
                currentPage++;
            }
        }

        if (sheetData.Count == 0)
            return;

        // Serialize the object
        string json = JsonSerializer.Serialize(sheetData, options);
        string fileName = $"{currentPage}.json";
                
        string filePath = Path.Combine(directoryPath, fileName);

        File.WriteAllText(filePath, json);

        Console.WriteLine($"{typeof(T).Name} data extracted and saved to {fileName}");
    }

    static void ExtractSubrowSheet<T>(Lumina.GameData lumina, string patch, string lang, JsonSerializerOptions options) where T: struct, Lumina.Excel.IExcelSubrow<T>
    {
        // Get the ClassJobs Excel sheet
        var sheet = lumina.GetSubrowExcelSheet<T>();

        if (sheet == null) {
            return;
        }

        const int cPageSize = 500;
        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"json/{patch}/{lang}/{typeof(T).Name}");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Extract data and save as JSON
        var sheetData = new List<T>();
        int currentPage = 1;
        foreach (var rows in sheet)
        {
            foreach (var row in rows) {
                sheetData.Add(row);
            }
            if (sheetData.Count >= cPageSize) {
                string jsonForPage = JsonSerializer.Serialize(sheetData, options);
                string fileNameForPage = $"{currentPage}.json";
                string filePathForPage = Path.Combine(directoryPath, fileNameForPage);
                File.WriteAllText(filePathForPage, jsonForPage);
                sheetData.Clear();
                Console.WriteLine($"{typeof(T).Name} data extracted and saved to {fileNameForPage}");
                currentPage++;
            }
        }

        if (sheetData.Count == 0)
            return;

        // Serialize the object
        string json = JsonSerializer.Serialize(sheetData, options);
        string fileName = $"{currentPage}.json";
                
        string filePath = Path.Combine(directoryPath, fileName);

        File.WriteAllText(filePath, json);

        Console.WriteLine($"{typeof(T).Name} data extracted and saved to {fileName}");
    }

    private const string IconFileFormat = "ui/icon/{0:D3}000/{1}{2:D6}.tex";
    private const string IconHDFileFormat = "ui/icon/{0:D3}000/{1}{2:D6}_hr1.tex";

    private static TexFile? GetIcon(Lumina.GameData lumina, string type, int iconId, bool hd)
    {
        type ??= string.Empty;
        if (type.Length > 0 && !type.EndsWith("/"))
            type += "/";

        var filePath = string.Format(hd ? IconHDFileFormat :IconFileFormat, iconId / 1000, type, iconId);
        try {
            var file = lumina.GetFile<TexFile>(filePath);

            if (file != default(TexFile) || type.Length <= 0) return file;

            // Couldn't get specific type, try for generic version.
            filePath = string.Format(hd ? IconHDFileFormat : IconFileFormat, iconId / 1000, string.Empty, iconId);
            file = lumina.GetFile<TexFile>(filePath);
            return file;
        } catch (FileNotFoundException) {
            return null;
        }
    }

    static void ExtractIcons(int first, int last, Lumina.GameData lumina, bool fullImport)
    {
        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "icons");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        for (int i = first; i <= last; ++i) {
            var iconFilePath = Path.Combine(directoryPath, $"{i / 1000:D3}000", $"{i:D6}.png");
            if (fullImport || !File.Exists(iconFilePath)) {
                var icon = GetIcon(lumina, "en/", i, false);
                if (icon != null && icon != default(TexFile)) {
                    Console.WriteLine($"-> {i:D6}");
                    var folderPath = Path.Combine(directoryPath, $"{i / 1000:D3}000");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    var image = GetImage(icon);
                    if (image != null)
                        image.Save(iconFilePath);
                }
            }

            var iconHDFilePath = Path.Combine(directoryPath, $"{i / 1000:D3}000", $"{i:D6}_hr1.png");
            if (fullImport || !File.Exists(iconHDFilePath)) {
                var iconHD = GetIcon(lumina, "en/", i,  true);
                if (iconHD != null && iconHD != default(TexFile)) {
                    Console.WriteLine($"-> HD {i:D6}");
                    var folderPath = Path.Combine(directoryPath, $"{i / 1000:D3}000");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    var image = GetImage(iconHD);
                    if (image != null)
                        image.Save(iconHDFilePath);
                }
            }
        }
    }

    private static Image<Bgra32>? GetImage(TexFile tex)
    {
        // Create a new image from the raw pixel data
        try
        {
            var image = Image.LoadPixelData<Bgra32>(tex.ImageData, tex.Header.Width, tex.Header.Height);
            return image;
        }
        catch (System.NotSupportedException)
        {
            Console.WriteLine("Failed to extract image!");
            return null;
        }    }

    private static void ExtractMaps(Lumina.GameData lumina, bool fullImport)
    {
        fullImport = true;
        var sheet = lumina.GetExcelSheet<Map>();
        if (sheet == null)
            return;

        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "maps");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        foreach (var map in sheet) {
            var idProperty = typeof(Map).GetProperty("Id");
            if (idProperty == null || idProperty.PropertyType != typeof(ReadOnlySeString))
                continue;
            var idValue = idProperty.GetValue(map);
            if (idValue == null)
                continue;
            var idString = ((ReadOnlySeString)idValue).AsSpan().ToString();

            // Assuming idString is in the format "xxx/yyy"
            string[] parts = idString.Split('/');
            if (parts.Length != 2)
                continue; // Ensure idString is in the expected format

            var outputFilePath = Path.Combine(directoryPath, parts[0] + "/" + parts[0] + "." + parts[1] + ".jpg");
        
            if (fullImport || !File.Exists(outputFilePath)) {
                 // Directly concatenate "ui" and "maps" with the rest of the path
                string filePath = "ui/map/" + idString + "/" + parts[0] + parts[1] + "_m.tex";
                
                if (!lumina.FileExists(filePath))
                    filePath = "ui/map/" + idString + "/" + parts[0] + parts[1] + "m_m.tex";
                if (!lumina.FileExists(filePath)) {
                    Console.WriteLine("Failed to extract map for " + idString);
                    continue;
                }

                Console.WriteLine("Extracting data for map: " + idString);
                
                // Access the lumina data
                var file = lumina.GetFile<TexFile>(filePath);

                var folderPath = Path.Combine(directoryPath, parts[0]);
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                if (file == null)
                    continue;

                var image = GetImage(file);
                if (image != null)
                    image.SaveAsJpeg(outputFilePath);
            }
        }
    }

    private static void ExtractLoadingImages(Lumina.GameData lumina, bool fullImport)
    {
        var sheet = lumina.GetExcelSheet<LoadingImage>();
        if (sheet == null)
            return;

        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "loadingimages");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        foreach (var loadingImage in sheet) {
            var fileName = typeof(LoadingImage).GetProperty("Unknown0");
            if (fileName == null || fileName.PropertyType != typeof(ReadOnlySeString))
                continue;
            var fileValue = fileName.GetValue(loadingImage);
            if (fileValue == null)
                continue;
            var fileString = ((ReadOnlySeString)fileValue).AsSpan().ToString();

            var outputFilePath = Path.Combine(directoryPath, fileString + ".jpg");
        
            if (fullImport || !File.Exists(outputFilePath)) {

                string filePath = "ui/loadingimage/" + fileString + ".tex";
            
                if (!lumina.FileExists(filePath))
                    continue;

                Console.WriteLine("Extracting data for loading image: " + fileString);
                
                // Access the lumina data
                var file = lumina.GetFile<TexFile>(filePath);

                if (file == null)
                    continue;

                var image = GetImage(file);
                if (image != null)
                    image.SaveAsJpeg(outputFilePath);
            }
        }
    }
}

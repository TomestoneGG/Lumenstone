namespace Lumenstone;

using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data.Files;
using Lumina.Excel.GeneratedSheets2;
using Lumina.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Normalization;

class Program
{
    static int lowestIconID = 0;
    static int highestIconID = 0;

    static void Main(string[] args)
     {
        if (args.Length < 2)
        {
            Console.WriteLine("Please provide the path to the sqpack directory as a command-line argument as well as the patch name.");
            return;
        }

         // Initialize Lumina with the sqpack path
        
        string sqpackPath = args[0]; // Assuming the first argument is the sqpack path
        bool full = args.Length >= 3 ? args[2] == "full" : false;

        var luminaEN = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.English });
        var luminaDE = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.German });
        var luminaFR = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.French });
        var luminaJA = new Lumina.GameData(sqpackPath, new() { DefaultExcelLanguage = Lumina.Data.Language.Japanese });
        
        JsonSerializerOptions options = new() {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true,
            Converters = { new SeStringConverter(luminaEN.GetExcelSheet<UIColor>()), new LazyRowConverterFactory() }
        };
       
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
            if (type.Name == "Achievement")
                continue;

            Console.WriteLine(type.Name);
            
            MethodInfo constructed = generic.MakeGenericMethod(type);
            constructed.Invoke(null, new object[] { patch, luminaEN, luminaDE, luminaFR, luminaJA, options });
        }

        // Use our own copy of Achievement to work around the bug with Data being null.
        ExtractSheetForAllLanguages<Lumenstone.Achievement>(patch, luminaEN, luminaDE, luminaFR, luminaJA, options);

        ExtractIcons(lowestIconID, highestIconID, luminaEN, full);
        ExtractMaps(luminaEN, full);
        ExtractLoadingImages(luminaEN, full);
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
            var iconProperty = typeof(T).GetProperty("Icon");
            if (iconProperty != null && iconProperty.PropertyType == typeof(int)) {
                // If Icon property exists, get its value
                int iconValue = (int)iconProperty.GetValue(row);
                if (iconValue > 0) {
                    if (lowestIconID == 0 || iconValue < lowestIconID)
                        lowestIconID = iconValue;
                    if (iconValue > highestIconID)
                        highestIconID = iconValue;
                }
            }

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

    private const string IconFileFormat = "ui/icon/{0:D3}000/{1}{2:D6}.tex";
    private const string IconHDFileFormat = "ui/icon/{0:D3}000/{1}{2:D6}_hr1.tex";

    private static TexFile GetIcon(Lumina.GameData lumina, string type, int iconId, bool hd)
    {
        type ??= string.Empty;
        if (type.Length > 0 && !type.EndsWith("/"))
            type += "/";

        var filePath = string.Format(hd ? IconHDFileFormat :IconFileFormat, iconId / 1000, type, iconId);
        var file = lumina.GetFile<TexFile>(filePath);

        if (file != default(TexFile) || type.Length <= 0) return file;

        // Couldn't get specific type, try for generic version.
        filePath = string.Format(hd ? IconHDFileFormat : IconFileFormat, iconId / 1000, string.Empty, iconId);
        file = lumina.GetFile<TexFile>(filePath);
        return file;
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
                
                    image.Save(iconHDFilePath);
                }
            }
        }
    }

    private static Image<Rgba32> GetImage(TexFile tex)
    {
        // Create a new image from the raw pixel data
        var image = Image.LoadPixelData<Rgba32>(tex.ImageData, tex.Header.Width, tex.Header.Height);

        // Manually rearrange the color channels
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                var newPixel = new Rgba32(pixel.B, pixel.G, pixel.R, pixel.A);
                image[x, y] = newPixel;
            }
        }

        return image;
    }

    private static void ExtractMaps(Lumina.GameData lumina, bool fullImport)
    {
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
            if (idProperty == null || idProperty.PropertyType != typeof(SeString))
                continue;
            var idValue = idProperty.GetValue(map);
            if (idValue == null)
                continue;
            var idString = ((SeString)idValue).RawString;

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
                if (!lumina.FileExists(filePath))
                    continue;

                Console.WriteLine("Extracting data for map: " + idString);
                
                // Access the lumina data
                var file = lumina.GetFile<TexFile>(filePath);

                var folderPath = Path.Combine(directoryPath, parts[0]);
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var image = GetImage(file);
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
            if (fileName == null || fileName.PropertyType != typeof(SeString))
                continue;
            var fileValue = fileName.GetValue(loadingImage);
            if (fileValue == null)
                continue;
            var fileString = ((SeString)fileValue).RawString;

            var outputFilePath = Path.Combine(directoryPath, fileString + ".jpg");
        
            if (fullImport || !File.Exists(outputFilePath)) {

                string filePath = "ui/loadingimage/" + fileString + ".tex";
            
                if (!lumina.FileExists(filePath))
                    continue;

                Console.WriteLine("Extracting data for loading image: " + fileString);
                
                // Access the lumina data
                var file = lumina.GetFile<TexFile>(filePath);

                var image = GetImage(file);
                image.SaveAsJpeg(outputFilePath);
            }
        }
    }
}


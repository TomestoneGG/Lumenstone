namespace Lumenstone;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Excel.GeneratedSheets;

class Program
{
    static void Main(string[] args)
     {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide the path to the sqpack directory as a command-line argument.");
            return;
        }

        string sqpackPath = args[0]; // Assuming the first argument is the sqpack path
        ExtractClassJobs(sqpackPath);
    }

    static void ExtractClassJobs(string sqpackPath)
    {
        // Initialize Lumina with the sqpack path
        var lumina = new Lumina.GameData(sqpackPath);

        // Get the ClassJobs Excel sheet
        var classJobsSheet = lumina.GetExcelSheet<ClassJob>();

        if (classJobsSheet == null)
            return;

        // Extract data and save as JSON
        var classJobsData = new List<ClassJob>();
        foreach (var row in classJobsSheet)
        {
            classJobsData.Add(row);
        }

        JsonSerializerOptions options = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true,
            Converters = { new SeStringConverter(), new LazyRowConverterFactory() }
        };

        // Serialize the object
        string json = JsonSerializer.Serialize(classJobsData, options);

        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "patches/6.58/en/ClassJob.json");

        File.WriteAllText(filePath, json);

        Console.WriteLine("ClassJob data extracted and saved to ClassJob.json");
    }
}

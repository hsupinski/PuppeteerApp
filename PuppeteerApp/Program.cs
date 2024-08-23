using CsvHelper;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using PuppeteerApp;
using PuppeteerSharp;
using System.Globalization;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Read from appsettings.json
        var config = LoadConfiguration();
        var todoFolderRelative = config["Folders:TODO"];
        var standingsFolderRelative = config["Folders:STANDINGS"];
        var doneFolderRelative = config["Folders:DONE"];

        string workingDirectory = Environment.CurrentDirectory;
        string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;

        var todoFolder = Path.Combine(projectDirectory, todoFolderRelative);
        var standingsFolder = Path.Combine(projectDirectory, standingsFolderRelative);
        var doneFolder = Path.Combine(projectDirectory, doneFolderRelative);

        ErrorMessageService errorMessageService = new ErrorMessageService();
        var program = new Program();
        var puppeteerService = new PuppeteerService(errorMessageService);

        var downloadPath = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
        var browserFetcherOptions = new BrowserFetcherOptions
        {
            Path = downloadPath
        };

        var browserFetcher = new BrowserFetcher(browserFetcherOptions);
        var installedBrowser = await browserFetcher.DownloadAsync();

        var options = new LaunchOptions
        {
            Headless = true,
            ExecutablePath = installedBrowser.GetExecutablePath(),
        };

        await using var browser = await Puppeteer.LaunchAsync(options);
        await using var page = await browser.NewPageAsync();

        // Disable timeout during debugging
        page.DefaultNavigationTimeout = 0;

        var inputFiles = Directory.GetFiles(todoFolder, "*.xlsx");

        foreach(var file in inputFiles)
        {
            using var package = new ExcelPackage(new FileInfo(file));

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var worksheet = package.Workbook.Worksheets[0];

            for (int i = 1; i <= worksheet.Dimension.Rows; i++)
            {
                if(worksheet.Cells[i, 2].Value == null)
                {
                    continue;
                }

                var competitionName = worksheet.Cells[i, 2].Value.ToString();

                await puppeteerService.searchCompetition(page, program, competitionName);

                await puppeteerService.goToCompetitionTablePage(page, program, competitionName);

                var table = await puppeteerService.getTableData(page, program, competitionName);

                if(table == null || table.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"No data found for competition: {competitionName}");
                    Console.ResetColor();
                    errorMessageService.AddError($"No data found for competition: {competitionName}");
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                var fileName = $"{i}_{competitionName}_{timestamp}.csv";
                var outputFilePath = Path.Combine(standingsFolder, fileName);

                using (var writer = new StreamWriter(outputFilePath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(table);
                }

                worksheet.Cells[i, 3].Value = outputFilePath;

                if(errorMessageService.HasErrors())
                {
                    worksheet.Cells[i, 4].Value = errorMessageService.getErrorsAsString();
                }
            }

            package.Save();

            var donePath = Path.Combine(doneFolder, Path.GetFileName(file));
            File.Move(file, donePath);

        }

        await browser.CloseAsync();
    }

    private static IConfiguration LoadConfiguration()
    {
        string workingDirectory = Environment.CurrentDirectory;
        string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;

        var builder = new ConfigurationBuilder()
            .SetBasePath(projectDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        return builder.Build();
    }

    public async Task Sleep(int milliseconds)
    {
        await Task.Delay(milliseconds);
    }
}

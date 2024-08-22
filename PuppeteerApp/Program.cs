using PuppeteerSharp;

var downloadPath = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
var browserFetcherOptions = new BrowserFetcherOptions
{
    Path = downloadPath
};

var browserFetcher = new BrowserFetcher(browserFetcherOptions);
var installedBrowser = await browserFetcher.DownloadAsync();

var options = new LaunchOptions
{
    Headless = false,
    ExecutablePath = installedBrowser.GetExecutablePath()
};

await using var browser = await Puppeteer.LaunchAsync(options);
await using var page = await browser.NewPageAsync();

// Open the page and close popups
await page.GoToAsync("https://www.flashscore.pl/");

var privacyPopupAccept = "#onetrust-accept-btn-handler";

await page.WaitForSelectorAsync(privacyPopupAccept);
await page.ClickAsync(privacyPopupAccept);

// Open search window and type in the competition
var searchWindowSelector = "#search-window";

await page.WaitForSelectorAsync(searchWindowSelector);
await page.ClickAsync(searchWindowSelector);

var competitionName = "Premier League";

await page.Keyboard.TypeAsync(competitionName);

// Wait for search results
await page.WaitForSelectorAsync(".searchResults__section");

var results = await page.QuerySelectorAllAsync(".searchResult");

foreach (var result in results)
{
    var nameElement = await result.QuerySelectorAsync(".searchResult__participantName");
    var nameText = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", nameElement);

    Console.WriteLine(nameText);

    if (nameText == competitionName)
    {
        await result.ClickAsync();
        break;
    }
}

Thread.Sleep(2000);
await browser.CloseAsync();
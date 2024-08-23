using Polly;
using PuppeteerSharp;

namespace PuppeteerApp
{
    internal class PuppeteerService(ErrorMessageService errorMessageService)
    {
        int delay = 0; // Delay in milliseconds
        ErrorMessageService _errorMessageService = errorMessageService; 
        public async Task searchCompetition(IPage page, Program program, string competitionName)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nRestarting scraper for competition: " + competitionName + '\n');
            Console.ResetColor();

            await page.GoToAsync("https://www.flashscore.pl/");

            var searchWindowSelector = "#search-window";

            await page.WaitForSelectorAsync(searchWindowSelector);
            await page.ClickAsync(searchWindowSelector);

            // Delays to ensure everything loads properly
            Console.WriteLine("Waiting for search window to load.");
            await program.Sleep(200);
            
            await page.Keyboard.TypeAsync(competitionName);
            Console.WriteLine("Typing competition name: " + competitionName);

            await program.Sleep(400);
            Console.WriteLine("Waiting for search results to load.");
            await page.WaitForSelectorAsync(".searchResults__section");
            var results = await page.QuerySelectorAllAsync(".searchResult");


            foreach (var result in results)
            {
                var nameElement = await result.QuerySelectorAsync(".searchResult__participantName");
                var nameText = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", nameElement);

                if (nameText == competitionName)
                {
                    var href = await page.EvaluateFunctionAsync<string>("el => el.getAttribute('href')", result);

                    if (href != null)
                    {
                        var fullUrl = $"https://www.flashscore.pl{href}";
                        await page.GoToAsync(fullUrl);
                    }
                    break;
                }
            }
        }

        public async Task goToCompetitionTablePage(IPage page, Program program, string competitionName)
        {
            // Find the "Tabela" tab and go to the table page

            Console.WriteLine("Going to table page for competition: " + competitionName);

            await program.Sleep(delay);

            await page.WaitForSelectorAsync(".tabs__group");
            var tabs = await page.QuerySelectorAllAsync(".tabs__tab");

            foreach (var tab in tabs)
            {
                var tabText = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", tab);

                if (tabText == "Tabela")
                {
                    var href = await page.EvaluateFunctionAsync<string>("el => el.getAttribute('href')", tab);

                    if (href != null)
                    {
                        var fullUrl = $"https://www.flashscore.pl{href}";
                        await page.GoToAsync(fullUrl);
                    }
                    break;
                }
            }

            await program.Sleep(delay);
        }

        public async Task<List<TableRow>> getTableData(IPage page, Program program, string competitionName)
        {

            var retryPolicy = Policy
                .HandleResult<List<TableRow>>(rows => rows == null || rows.Count == 0)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(2), (result, timeSpan, retryCount, context) =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"No data found for competition: {competitionName}. Retrying...");
                    Console.ResetColor();

                    // Avoid printing multiple error messages
                    _errorMessageService.ClearErrors();
                });

            return await retryPolicy.ExecuteAsync(async () =>
            {
                // Get relevant data from the table
                await program.Sleep(400);
                var rows = await page.QuerySelectorAllAsync(".ui-table__row");
                var table = new List<TableRow>();

                Console.WriteLine("Results for competition: " + competitionName);
                Console.WriteLine("Found " + rows.Length + " rows.");
                Console.WriteLine("====================================");

                foreach (var row in rows)
                {
                    var rankElement = await row.QuerySelectorAsync(".tableCellRank");
                    var rankText = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", rankElement);
                    Console.WriteLine("Position: " + rankText);

                    var logoElement = await row.QuerySelectorAsync(".tableCellParticipant__image img");
                    var logoUrl = await page.EvaluateFunctionAsync<string>("el => el.src", logoElement);
                    Console.WriteLine("Logo: " + logoUrl);

                    var nameElement = await row.QuerySelectorAsync(".tableCellParticipant__name");
                    var nameText = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", nameElement);
                    Console.WriteLine("Name: " + nameText);

                    var values = await row.QuerySelectorAllAsync(".table__cell.table__cell--value");

                    if (values.Length < 7)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Not enough values in row for team: " + nameText);
                        Console.ResetColor();
                        _errorMessageService.AddError("Not enough values in row for team: " + nameText);
                        continue;
                    }

                    var matchesPlayed = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", values[0]);
                    Console.WriteLine("Matches Played: " + matchesPlayed);
                    var wins = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", values[1]);
                    Console.WriteLine("Wins: " + wins);
                    var draws = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", values[2]);
                    Console.WriteLine("Draws: " + draws);
                    var losses = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", values[3]);
                    Console.WriteLine("Losses: " + losses);
                    var goalBalance = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", values[4]);
                    Console.WriteLine("Goal Balance: " + goalBalance);
                    var goalDifference = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", values[5]);
                    Console.WriteLine("Goal Difference: " + goalDifference);
                    var points = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", values[6]);
                    Console.WriteLine("Points: " + points);

                    table.Add(new TableRow
                    {
                        Rank = rankText,
                        LogoUrl = logoUrl,
                        Name = nameText,
                        MatchesPlayed = matchesPlayed,
                        Wins = wins,
                        Draws = draws,
                        Losses = losses,
                        GoalBalance = goalBalance,
                        GoalDifference = goalDifference,
                        Points = points
                    }); 
                }

                return table;
            }); 
        }
    }
}

using System.Globalization;
using System.Resources;
using System.Text.Json;

namespace Words2
{
    // I/O interface
    public interface IInputOutput
    {
        void WriteLine(string message);
        string ReadLine();
        void SetCulture(string culture);
    }

    // Implementation of console I/O
    public class ConsoleInputOutput : IInputOutput
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public string ReadLine() => Console.ReadLine() ?? string.Empty;
        public void SetCulture(string culture)
        {
            var ci = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
        }
    }

    // Class for managing localization via .resx
    public static class Localization
    {
        private static readonly ResourceManager _resourceManager =
            new("Words2.Resources.Resources", typeof(Localization).Assembly);

        public static string GetString(string name)
        {
            return _resourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? string.Empty;
        }
    }

    // Class for working with files (asynchronous)
    public class GameDataManager
    {
        private const string FilePath = "gamedata.json";

        public async Task<GameData> LoadGameDataAsync()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using var stream = File.OpenRead(FilePath);
                    return await JsonSerializer.DeserializeAsync<GameData>(stream) ?? new GameData();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading game data: {ex.Message}");
            }
            return new GameData();
        }

        public async Task SaveGameDataAsync(GameData gameData)
        {
            try
            {
                using var stream = File.Create(FilePath);
                await JsonSerializer.SerializeAsync(stream, gameData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving game data: {ex.Message}");
            }
        }
    }

    // Main class of the game
    public class Game
    {
        // Game configuration constants
        private static class GameSettings
        {
            public const int DefaultTimeLeft = 15;
            public const int TimerInterval = 1000;
            public const int MinWordLength = 8;
            public const int MaxWordLength = 30;
        }

        // Game state variables
        private string originalWord = null!;
        private readonly List<string> usedWords = [];
        private Timer timer = null!;
        private bool timeIsUp;
        private int timeLeft = GameSettings.DefaultTimeLeft;
        private int currentPlayer = 1;
        private string language = null!;
        private string player1Name = null!;
        private string player2Name = null!;
        private bool isGameInProgress;
        private readonly IInputOutput io;
        private readonly GameDataManager dataManager;
        private GameData gameData;

        // Initializes game instance with dependencies
        public Game(IInputOutput io, GameDataManager dataManager, GameData gameData)
        {
            this.io = io ?? throw new ArgumentNullException(nameof(io));
            this.dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
            this.gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        }

        // Starts and manages the game
        public async Task RunGame()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            SelectLanguage();
            GetPlayerNames();
            await LoadGameData();
            isGameInProgress = true;
            await StartGame();
        }

        // Loads game statistics from JSON file or initializes new data structure
        private async Task LoadGameData() => gameData = await dataManager.LoadGameDataAsync();

        // Handles language selection process
        private void SelectLanguage()
        {
            do
            {
                io.WriteLine(Localization.GetString("LanguageSelectionPrompt"));
                language = io.ReadLine().ToLower();
            } while (language != "ru" && language != "en");

            io.SetCulture(language);
        }

        // Gets player names from user input with basic validation
        private void GetPlayerNames()
        {
            player1Name = GetValidName(Localization.GetString("EnterPlayer1Name"), null);
            player2Name = GetValidName(Localization.GetString("EnterPlayer2Name"), player1Name);
        }

        // Gets validated player name with duplicate check
        private string GetValidName(string prompt, string? existingName)
        {
            string name;
            do
            {
                io.WriteLine(prompt);
                name = io.ReadLine().Trim();
            } while (!IsNameValid(name, existingName));

            return name;
        }

        // Validates player name input
        private bool IsNameValid(string name, string? existingName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                io.WriteLine(Localization.GetString("InvalidName"));
                return false;
            }

            if (existingName != null && name.Equals(existingName, StringComparison.OrdinalIgnoreCase))
            {
                io.WriteLine(Localization.GetString("DuplicatePlayerName"));
                return false;
            }

            return true;
        }

        // Initializes game state and starts main game loop
        private async Task StartGame()
        {
            isGameInProgress = true;
            InitializeGame();
            SetupTimer();
            await GameLoop();
            await HandleGameEnd();
        }

        // Sets up initial game state and prompts for original word
        private void InitializeGame()
        {
            originalWord = GetValidOriginalWord();
            ResetGameState();
            ShowAvailableCommands();
        }

        // Prompts user for valid original word until correct input is received
        private string GetValidOriginalWord()
        {
            string word;
            do
            {
                io.WriteLine(Localization.GetString("EnterOriginalWord"));
                word = io.ReadLine().ToLower();
            } while (!IsOriginalWordValid(word));

            return word;
        }

        // Validates original word against length and character requirements
        private bool IsOriginalWordValid(string? word) =>
            !string.IsNullOrEmpty(word) &&
            word.Length is >= GameSettings.MinWordLength and <= GameSettings.MaxWordLength &&
            word.All(char.IsLetter);

        // Resets game state variables to initial values
        private void ResetGameState()
        {
            usedWords.Clear();
            usedWords.Add(originalWord);
            timeIsUp = false;
            timeLeft = GameSettings.DefaultTimeLeft;
            currentPlayer = 1;
        }

        // Configures and starts the game timer
        private void SetupTimer()
        {
            timer = new Timer(_ =>
            {
                if (--timeLeft <= 0)
                {
                    timeIsUp = true;
                    timer?.Dispose();
                }
            }, null, GameSettings.TimerInterval, GameSettings.TimerInterval);
        }

        // Main game loop handling player input and commands
        private async Task GameLoop()
        {
            while (!timeIsUp)
            {
                PromptCurrentPlayer();
                var input = io.ReadLine().ToLower();

                if (IsCommand(input))
                {
                    await Task.Run(() => ProcessCommand(input));
                    continue;
                }

                if (timeIsUp) break;

                if (IsInputValid(input))
                {
                    ProcessValidInput(input);
                }
                else
                {
                    io.WriteLine(Localization.GetString("InvalidWord"));
                }
            }
        }

        // Processes valid player input and updates game state
        private void ProcessValidInput(string input)
        {
            usedWords.Add(input);
            currentPlayer = 3 - currentPlayer;
            timeLeft = GameSettings.DefaultTimeLeft;
        }

        // Handles game end sequence and restart logic
        private async Task HandleGameEnd()
        {
            isGameInProgress = false;
            timer?.Dispose();
            DisplayGameResult();
            RecordGameResult();
            await dataManager.SaveGameDataAsync(gameData);
            await HandleRestartPrompt();
        }

        // Records game result in the statistics dictionary

        private void RecordGameResult()
        {
            var winner = currentPlayer == 1 ? player2Name : player1Name;
            gameData.PlayerWins[winner] = gameData.PlayerWins.TryGetValue(winner, out var wins) ? wins + 1 : 1;
        }

        // Handles restart prompt and either restarts game or exits
        private async Task HandleRestartPrompt()
        {
            string input;
            do
            {
                io.WriteLine(Localization.GetString("PlayAgain"));
                input = io.ReadLine().ToLower();

                if (IsCommand(input)) ProcessCommand(input);

            } while (!IsValidRestartResponse(input));

            if (input.Equals(Localization.GetString("Yes"), StringComparison.CurrentCultureIgnoreCase))
            {
                await StartGame();
            }
        }

        // Method showing available commands
        private void ShowAvailableCommands()
        {
            io.WriteLine(Localization.GetString("AvailableCommands"));
            io.WriteLine(Localization.GetString("AvailableCommandsList"));
            io.WriteLine(Localization.GetString("ContinuePrompt"));
        }

        // Event handler for process exit. Saves game state if game was interrupted
        private void OnProcessExit(object? sender, EventArgs e)
        {
            if (isGameInProgress)
            {
                RecordGameResult();
                dataManager.SaveGameDataAsync(gameData)
                    .GetAwaiter()
                    .GetResult();
            }
        }

        // Checks if input is a command (starts with '/')
        private static bool IsCommand(string? input) =>
            !string.IsNullOrEmpty(input) && input.StartsWith("/");

        // Executes game commands based on user input
        private void ProcessCommand(string command)
        {
            switch (command)
            {
                case "/show-words":
                    ShowUsedWords();
                    break;
                case "/score":
                    ShowCurrentPlayersScore();
                    break;
                case "/total-score":
                    ShowTotalScore();
                    break;
                default:
                    io.WriteLine(Localization.GetString("UnknownCommand"));
                    io.WriteLine(Localization.GetString("AvailableCommands"));
                    break;
            }
        }

        // Displays all words used in current game session
        private void ShowUsedWords()
        {
            io.WriteLine(Localization.GetString("CommandShowWords"));
            foreach (var word in usedWords)
                io.WriteLine($"- {word}");
        }

        // Displays current players' score from game statistics
        private void ShowCurrentPlayersScore()
        {
            var wins1 = gameData.PlayerWins.GetValueOrDefault(player1Name, 0);
            var wins2 = gameData.PlayerWins.GetValueOrDefault(player2Name, 0);

            io.WriteLine(string.Format(
                Localization.GetString("CommandScore"),
                player1Name, wins1,
                player2Name, wins2
            ));
        }

        // Displays total score statistics for all recorded players
        private void ShowTotalScore()
        {
            io.WriteLine(Localization.GetString("CommandTotalScore"));
            foreach (var (name, wins) in gameData.PlayerWins)
                io.WriteLine($"{name}: {wins}");
        }

        // Displays current player prompt with time remaining
        private void PromptCurrentPlayer()
        {
            io.WriteLine(string.Format(
                Localization.GetString("PlayerPrompt"),
                currentPlayer,
                timeLeft
            ));
        }

        // Validates player input against game rules
        private bool IsInputValid(string? input) =>
            !string.IsNullOrEmpty(input) &&
            IsWordValid(input) &&
            !usedWords.Contains(input);

        // Validates restart prompt response
        private static bool IsValidRestartResponse(string response)
        {
            var yes = Localization.GetString("Yes").ToLower();
            var no = Localization.GetString("No").ToLower();
            return response == yes || response == no;
        }

        // Validates if player's word can be formed from original word
        private bool IsWordValid(string? word) =>
            !string.IsNullOrEmpty(word) &&
            word.GroupBy(c => c).All(g =>
                originalWord.Count(c => c == g.Key) >= g.Count());

        // Displays game over message with losing player
        private void DisplayGameResult()
        {
            io.WriteLine(string.Format(
                Localization.GetString("TimeUp"),
                currentPlayer
            ));
        }
    }

    // Contains player statistics data structure
    public class GameData
    {
        public Dictionary<string, int> PlayerWins { get; set; } = [];
    }

    // Main program entry point
    class Program
    {
        static async Task Main()
        {
            var io = new ConsoleInputOutput();
            var dataManager = new GameDataManager();
            var gameData = await dataManager.LoadGameDataAsync();

            var game = new Game(io, dataManager, gameData);
            await game.RunGame();
        }
    }
}
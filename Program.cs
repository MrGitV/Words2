using System.Text.Json;

class Program
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
    private static string originalWord = null!;
    private static readonly List<string> usedWords = [];
    private static Timer timer = null!;
    private static bool timeIsUp;
    private static int timeLeft = GameSettings.DefaultTimeLeft;
    private static int currentPlayer = 1;
    private static string language = null!;
    private static string playAgain = null!;
    internal static readonly string[] sourceArray = ["да", "нет", "yes", "no"];
    private static string player1Name = null!;
    private static string player2Name = null!;
    private static GameData gameData = null!;
    private static bool isGameInProgress;

    static void Main()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        SelectLanguage();
        GetPlayerNames();
        LoadGameData();
        isGameInProgress = true;
        StartGame();
    }

    // Event handler for process exit. Saves game state if game was interrupted
    private static void OnProcessExit(object? sender, EventArgs e)
    {
        if (isGameInProgress)
        {
            string winnerName = currentPlayer == 1 ? player2Name : player1Name;
            RecordGameResult(winnerName);
            SaveGameData();
        }
    }

    // Gets player names from user input with basic validation
    private static void GetPlayerNames()
    {
        Console.WriteLine(LocalizationManager.GetMessage("EnterPlayer1Name", language));
        player1Name = Console.ReadLine()?.Trim() ?? "Player1";

        Console.WriteLine(LocalizationManager.GetMessage("EnterPlayer2Name", language));
        player2Name = Console.ReadLine()?.Trim() ?? "Player2";
    }

    // Loads game statistics from JSON file or initializes new data structure
    private static void LoadGameData()
    {
        string filePath = "gamedata.json";
        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                gameData = JsonSerializer.Deserialize<GameData>(json) ?? new GameData();
            }
            else
            {
                gameData = new GameData();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading game data: {ex.Message}");
            gameData = new GameData();
        }
    }

    // Saves game statistics to JSON file with error handling
    private static void SaveGameData()
    {
        string filePath = "gamedata.json";
        try
        {
            string json = JsonSerializer.Serialize(gameData);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving game data: {ex.Message}");
        }
    }

    // Records game result in the statistics dictionary
    private static void RecordGameResult(string winnerName)
    {
        if (gameData.PlayerWins.ContainsKey(winnerName))
            gameData.PlayerWins[winnerName]++;
        else
            gameData.PlayerWins[winnerName] = 1;
    }

    // Handles language selection process
    private static void SelectLanguage()
    {
        do
        {
            Console.WriteLine("Выберите язык / Choose language (ru/en):");
            language = Console.ReadLine()?.ToLower() ?? "";
        } while (language != "ru" && language != "en");
    }

    // Initializes game state and starts main game loop
    private static void StartGame()
    {
        isGameInProgress = true;
        InitializeGame();
        SetupTimer();
        GameLoop();
        HandleGameEnd();
    }

    // Sets up initial game state and prompts for original word
    private static void InitializeGame()
    {
        originalWord = GetValidOriginalWord(language);
        ResetGameState();
    }

    // Prompts user for valid original word until correct input is received
    private static string GetValidOriginalWord(string language)
    {
        string? word;
        do
        {
            Console.WriteLine(LocalizationManager.GetMessage("EnterOriginalWord", language));
            word = Console.ReadLine()?.ToLower();
        } while (!IsOriginalWordValid(word));

        return word!;
    }

    // Validates original word against length and character requirements
    private static bool IsOriginalWordValid(string? word) =>
        !string.IsNullOrEmpty(word) &&
        word.Length >= GameSettings.MinWordLength &&
        word.Length <= GameSettings.MaxWordLength &&
        word.All(char.IsLetter);

    // Resets game state variables to initial values
    private static void ResetGameState()
    {
        usedWords.Clear();
        usedWords.Add(originalWord);
        timeIsUp = false;
        timeLeft = GameSettings.DefaultTimeLeft;
        currentPlayer = 1;
    }

    // Configures and starts the game timer
    private static void SetupTimer()
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
    private static void GameLoop()
    {
        while (!timeIsUp)
        {
            PromptCurrentPlayer();
            var input = Console.ReadLine()?.ToLower();

            if (timeIsUp) break;

            if (IsCommand(input))
            {
                ProcessCommand(input!);
                continue;
            }

            if (IsInputValid(input))
            {
                ProcessValidInput(input!);
            }
            else
            {
                Console.WriteLine(LocalizationManager.GetMessage("InvalidWord", language));
            }
        }

    }

    // Checks if input is a command (starts with '/')
    private static bool IsCommand(string? input) => input?.StartsWith("/") ?? false;

    // Executes game commands based on user input
    private static void ProcessCommand(string command)
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
                Console.WriteLine(LocalizationManager.GetMessage("UnknownCommand", language));
                break;
        }
    }

    // Displays all words used in current game session
    private static void ShowUsedWords()
    {
        Console.WriteLine(LocalizationManager.GetMessage("CommandShowWords", language));
        foreach (var word in usedWords)
            Console.WriteLine(word);
    }

    // Displays current players' score from game statistics
    private static void ShowCurrentPlayersScore()
    {
        int wins1 = gameData.PlayerWins.TryGetValue(player1Name, out int w1) ? w1 : 0;
        int wins2 = gameData.PlayerWins.TryGetValue(player2Name, out int w2) ? w2 : 0;
        Console.WriteLine(LocalizationManager.GetMessage("CommandScore", language)
            .Replace("{player1}", player1Name)
            .Replace("{wins1}", wins1.ToString())
            .Replace("{player2}", player2Name)
            .Replace("{wins2}", wins2.ToString()));
    }

    // Displays total score statistics for all recorded players
    private static void ShowTotalScore()
    {
        Console.WriteLine(LocalizationManager.GetMessage("CommandTotalScore", language));
        foreach (var entry in gameData.PlayerWins)
            Console.WriteLine($"{entry.Key}: {entry.Value} wins");
    }

    // Displays current player prompt with time remaining
    private static void PromptCurrentPlayer()
    {
        Console.WriteLine(LocalizationManager.GetMessage("PlayerPrompt", language)
            .Replace("{player}", currentPlayer.ToString())
            .Replace("{time}", timeLeft.ToString()));
    }

    // Validates player input against game rules
    private static bool IsInputValid(string? input) =>
        !string.IsNullOrEmpty(input) &&
        IsWordValid(input) &&
        !usedWords.Contains(input);

    // Processes valid player input and updates game state
    private static void ProcessValidInput(string input)
    {
        usedWords.Add(input);
        currentPlayer = 3 - currentPlayer;
        timeLeft = GameSettings.DefaultTimeLeft;
    }

    // Handles game end sequence and restart logic
    private static void HandleGameEnd()
    {
        isGameInProgress = false;
        timer?.Dispose();
        DisplayGameResult();
        string winnerName = currentPlayer == 1 ? player2Name : player1Name;
        RecordGameResult(winnerName);
        SaveGameData();
        HandleRestartPrompt();
    }

    // Displays game over message with losing player
    private static void DisplayGameResult()
    {
        Console.WriteLine(LocalizationManager.GetMessage("TimeUp", language)
            .Replace("{player}", currentPlayer.ToString()));
    }

    // Handles restart prompt and either restarts game or exits
    private static void HandleRestartPrompt()
    {
        do
        {
            Console.WriteLine(LocalizationManager.GetMessage("PlayAgain", language));
            playAgain = Console.ReadLine()?.ToLower() ?? "";
        } while (!IsValidRestartResponse(playAgain));

        if (playAgain == "да" || playAgain == "yes")
        {
            StartGame();
        }
    }

    // Validates restart prompt response
    private static bool IsValidRestartResponse(string response) => sourceArray.Contains(response);

    // Validates if player's word can be formed from original word
    private static bool IsWordValid(string? word) =>
        !string.IsNullOrEmpty(word) &&
        word.GroupBy(c => c).All(g =>
            originalWord.Count(c => c == g.Key) >= g.Count());
}

// Provides localized messages for different game components
static class LocalizationManager
{
    private static readonly Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        {
            "ru", new Dictionary<string, string>
            {
                {"EnterOriginalWord", "Введите исходное слово (8-30 символов):"},
                {"PlayerPrompt", "Игрок {player}, введите слово (осталось {time} секунд):"},
                {"InvalidWord", "Неверное слово. Повторите попытку."},
                {"TimeUp", "Время вышло! Игрок {player} проиграл."},
                {"PlayAgain", "Хотите сыграть еще раз? (да/нет)"},
                {"EnterPlayer1Name", "Введите имя игрока 1:"},
                {"EnterPlayer2Name", "Введите имя игрока 2:"},
                {"CommandShowWords", "Использованные слова в текущей игре:"},
                {"CommandScore", "Счет текущих игроков: {player1}: {wins1}, {player2}: {wins2}"},
                {"CommandTotalScore", "Общий счет для всех игроков:"},
                {"UnknownCommand", "Неизвестная команда."}
            }
        },
        {
            "en", new Dictionary<string, string>
            {
                {"EnterOriginalWord", "Enter the original word (8-30 characters):"},
                {"PlayerPrompt", "Player {player}, enter a word ({time} seconds left):"},
                {"InvalidWord", "Invalid word. Try again."},
                {"TimeUp", "Time's up! Player {player} loses."},
                {"PlayAgain", "Do you want to play again? (yes/no)"},
                {"EnterPlayer1Name", "Enter Player 1's name:"},
                {"EnterPlayer2Name", "Enter Player 2's name:"},
                {"CommandShowWords", "Used words in current game:"},
                {"CommandScore", "Current players' scores: {player1}: {wins1}, {player2}: {wins2}"},
                {"CommandTotalScore", "Total scores for all players:"},
                {"UnknownCommand", "Unknown command."}
            }
        }
    };

    // Retrieves localized message for specified key and language
    public static string GetMessage(string key, string language)
    {
        if (_translations.TryGetValue(language, out var languageDict) &&
            languageDict.TryGetValue(key, out var message))
        {
            return message;
        }
        return string.Empty;
    }
}

// Data structure for storing game statistics and player wins
public class GameData
{
    public Dictionary<string, int> PlayerWins { get; set; } = new Dictionary<string, int>();
}
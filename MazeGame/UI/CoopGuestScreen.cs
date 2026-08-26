using MazeGame.Application;
using MazeGame.Domain.Characters;
using MazeGame.Transport.SignalR;

namespace MazeGame.UI;

/// <summary>Az első LAN vertical slice vendégoldali, snapshotból rajzoló konzolképernyője.</summary>
public sealed class CoopGuestScreen
{
    private readonly string _applicationVersion;
    private readonly string _catalogHash;
    private int _redrawRequested = 1;
    private string? _message;

    public CoopGuestScreen(string applicationVersion, string catalogHash)
    {
        _applicationVersion = applicationVersion;
        _catalogHash = catalogHash;
    }

    public async Task RunAsync(string hostUrl, string displayName, CancellationToken cancellationToken = default)
    {
        await using var client = new CoopSignalRClient(hostUrl, _applicationVersion, _catalogHash, displayName);
        client.SnapshotChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ConnectionStateChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ProtocolErrorReceived += error => SetMessage($"Protokollhiba: {error.Message}");
        client.CommandRejected += rejected => SetMessage($"A host elutasította: {rejected.Reason}");

        var hello = await client.ConnectAsync(cancellationToken);
        var options = hello.AvailableCharacters ?? [];
        if (options.Count == 0)
            throw new InvalidOperationException("A host partijában nincs átvehető NPC karakter.");
        var selected = ChooseCharacter(options);
        if (selected is null) return;
        var control = await client.RequestCharacterControlAsync(selected.CharacterId, cancellationToken);
        if (!control.Accepted)
            throw new InvalidOperationException(control.RejectionReason ?? "A karakter átvételét a host elutasította.");

        Console.CursorVisible = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   client.State is not (CoopClientConnectionState.Disconnected or CoopClientConnectionState.Faulted))
            {
                if (Interlocked.Exchange(ref _redrawRequested, 0) != 0)
                    Draw(client, selected);
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape) break;
                    await HandleInputAsync(client, selected.CharacterId, key.Key, cancellationToken);
                }
                await Task.Delay(20, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Console.CursorVisible = true;
            await client.DisconnectAsync(CancellationToken.None);
        }
    }

    private static CoopCharacterOption? ChooseCharacter(IReadOnlyList<CoopCharacterOption> options)
    {
        var selectedIndex = 0;
        while (true)
        {
            ResetConsole();
            WriteLine("=== COOP KARAKTERVÁLASZTÁS ===", ConsoleColor.Yellow);
            WriteLine("Fel/le: választás | Enter: átvétel | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                WriteLine($"{(index == selectedIndex ? ">" : " ")} {option.Name} — " +
                          $"{option.CharacterClassName}, {option.Level}. szint",
                    index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray);
            }
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % options.Count;
                    break;
                case ConsoleKey.Enter:
                    return options[selectedIndex];
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    private async Task HandleInputAsync(CoopSignalRClient client, CharacterId characterId, ConsoleKey key,
        CancellationToken cancellationToken)
    {
        if (client.State != CoopClientConnectionState.Connected) return;
        GameCommand? command = null;
        var snapshot = client.CurrentSnapshot;
        if (snapshot is null || !snapshot.CharacterControls.Any(control =>
                control.CharacterId == characterId && control.AssignedPlayerId == client.PlayerId &&
                control.ConnectionState == PlayerConnectionState.Connected)) return;
        if (snapshot?.Battle is { } battle && battle.ActingCharacterId == characterId)
        {
            var action = key switch
            {
                ConsoleKey.Spacebar => BattleActionKind.PhysicalAttack,
                ConsoleKey.T => BattleActionKind.TurnUndead,
                _ => (BattleActionKind?)null
            };
            if (action is not null)
                command = new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    battle.BattleId, battle.TurnId, action.Value);
        }
        else if (snapshot?.Phase == GameSessionPhase.Exploration && TryGetDirection(key, out var direction))
        {
            command = new MoveCharacterCommand(client.PlayerId!.Value, client.NextCommandId(), characterId, direction);
        }
        if (command is null) return;
        try
        {
            await client.SendCommandAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            SetMessage(exception.Message);
        }
    }

    private void Draw(CoopSignalRClient client, CoopCharacterOption selected)
    {
        ResetConsole();
        WriteLine($"=== COOP VENDÉG — {selected.Name} ===", ConsoleColor.Yellow);
        WriteLine($"Kapcsolat: {client.State} | Esc: kilépés | Nyilak: mozgás | Harc: Space/T",
            client.State == CoopClientConnectionState.Connected ? ConsoleColor.Green : ConsoleColor.DarkYellow);
        var snapshot = client.CurrentSnapshot;
        if (snapshot?.World is not { } world)
        {
            Console.WriteLine();
            WriteLine("Várakozás a host első snapshotjára…", ConsoleColor.DarkCyan);
            return;
        }

        WriteLine($"{snapshot.MazeLevel}. pálya — {snapshot.LevelName} — {snapshot.Phase}", ConsoleColor.Cyan);
        var stillControlled = snapshot.CharacterControls.Any(control =>
            control.CharacterId == selected.CharacterId && control.AssignedPlayerId == client.PlayerId &&
            control.ConnectionState == PlayerConnectionState.Connected);
        if (!stillControlled)
            WriteLine("Megfigyelő mód: a karaktered már nincs a vezérlésed alatt.", ConsoleColor.DarkYellow);
        var grid = new string[world.Width, world.Height];
        for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++) grid[x, y] = "░";
        foreach (var cell in world.RevealedCells)
            if (IsInside(cell.Position, world)) grid[cell.Position.X, cell.Position.Y] = char.ConvertFromUtf32(cell.TileCodePoint);
        foreach (var enemy in world.Enemies) Put(grid, world, enemy.Position, "e");
        foreach (var chest in world.Chests) Put(grid, world, chest.Position, "$");
        foreach (var corpse in world.Corpses) Put(grid, world, corpse.Position, "%");
        foreach (var pile in world.GroundPiles) Put(grid, world, pile.Position, "*");
        foreach (var character in snapshot.Party.Where(character => character.Position is not null))
            Put(grid, world, character.Position!.Value, character.CharacterId == selected.CharacterId ? "@" : "&");

        var maximumWidth = Math.Min(world.Width, Math.Max(1, SafeWindowWidth() - 1));
        var maximumHeight = Math.Min(world.Height, Math.Max(1, SafeWindowHeight() - 8));
        for (var y = 0; y < maximumHeight; y++)
        {
            for (var x = 0; x < maximumWidth; x++) Console.Write(grid[x, y]);
            Console.WriteLine();
        }
        var own = snapshot.Party.FirstOrDefault(character => character.CharacterId == selected.CharacterId);
        if (own is not null)
            WriteLine($"HP {own.CurrentVitality}/{own.MaximumVitality} | Manna {own.CurrentMana}/{own.MaximumMana} | " +
                      $"Étel {own.FoodLevel} | Víz {own.WaterLevel}", ConsoleColor.Gray);
        if (!string.IsNullOrWhiteSpace(_message)) WriteLine(_message, ConsoleColor.DarkYellow);
    }

    private void SetMessage(string message)
    {
        _message = message;
        Interlocked.Exchange(ref _redrawRequested, 1);
    }

    private static bool TryGetDirection(ConsoleKey key, out Direction direction)
    {
        direction = key switch
        {
            ConsoleKey.UpArrow => Direction.Up,
            ConsoleKey.DownArrow => Direction.Down,
            ConsoleKey.LeftArrow => Direction.Left,
            ConsoleKey.RightArrow => Direction.Right,
            _ => default
        };
        return key is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.LeftArrow or ConsoleKey.RightArrow;
    }

    private static bool IsInside(Position position, WorldSnapshot world) =>
        position.X >= 0 && position.X < world.Width && position.Y >= 0 && position.Y < world.Height;

    private static void Put(string[,] grid, WorldSnapshot world, Position position, string value)
    {
        if (IsInside(position, world)) grid[position.X, position.Y] = value;
    }

    private static int SafeWindowWidth()
    {
        try { return Console.WindowWidth; } catch (IOException) { return 80; }
    }

    private static int SafeWindowHeight()
    {
        try { return Console.WindowHeight; } catch (IOException) { return 30; }
    }

    private static void ResetConsole()
    {
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Clear();
    }

    private static void WriteLine(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}

namespace KaoszRubin.Tests.Coop;

internal enum CoopHarnessMode
{
    Simulation,
    HostRole,
    GuestRole
}

internal sealed record CoopHarnessOptions(
    CoopHarnessMode Mode,
    string? Scenario,
    int Port,
    string? Workspace)
{
    public const int DefaultPort = 5127;

    public static bool TryParse(string[] args, out CoopHarnessOptions options)
    {
        options = null!;
        if (args is null || args.Length == 0) return false;

        var runSimulation = false;
        string? role = null;
        string? scenario = null;
        var port = DefaultPort;
        string? workspace = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--coop-sim":
                    runSimulation = true;
                    break;
                case "--coop-role":
                    if (!TryReadNextValue(args, ref i, out role)) return false;
                    break;
                case "--scenario":
                    if (!TryReadNextValue(args, ref i, out scenario)) return false;
                    break;
                case "--port":
                    if (!TryReadNextValue(args, ref i, out var rawPort) ||
                        !int.TryParse(rawPort, out port) ||
                        port is < 1 or > 65535)
                    {
                        return false;
                    }
                    break;
                case "--workspace":
                    if (!TryReadNextValue(args, ref i, out workspace)) return false;
                    break;
            }
        }

        if (!runSimulation && role is null) return false;

        if (role is not null)
        {
            if (role.Equals("host", StringComparison.OrdinalIgnoreCase))
            {
                options = new CoopHarnessOptions(CoopHarnessMode.HostRole, scenario, port, workspace);
                return true;
            }

            if (role.Equals("guest", StringComparison.OrdinalIgnoreCase))
            {
                options = new CoopHarnessOptions(CoopHarnessMode.GuestRole, scenario, port, workspace);
                return true;
            }

            return false;
        }

        options = new CoopHarnessOptions(CoopHarnessMode.Simulation, scenario, port, workspace);
        return true;
    }

    private static bool TryReadNextValue(string[] args, ref int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Length) return false;
        var next = args[index + 1];
        if (next.StartsWith("--", StringComparison.Ordinal)) return false;
        index++;
        value = next;
        return true;
    }
}

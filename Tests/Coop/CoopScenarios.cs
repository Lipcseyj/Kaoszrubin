namespace KaoszRubin.Tests.Coop;

internal sealed record CoopScenario(
    string Name,
    string Description,
    bool StartHost,
    bool StartGuest,
    string[] ControllerInstructions,
    string[] HostHints,
    string[] GuestHints);

internal static class CoopScenarios
{
    private static readonly IReadOnlyDictionary<string, CoopScenario> Scenarios =
        new Dictionary<string, CoopScenario>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = new CoopScenario(
                "host",
                "Csak host ablak inditasa (varakozoszoba es host UI)",
                StartHost: true,
                StartGuest: false,
                [
                    "A host ablak kulon konzolban indul.",
                    "A host varakozik vendegre. Esc-kel kilephetsz."
                ],
                [
                    "Ellenorizd a host varakozoszobat es a csatlakozasi cimet.",
                    "Vendeg nelkul Esc-kel vissza lehet lepni."
                ],
                []),
            ["guest"] = new CoopScenario(
                "guest",
                "Csak vendeg ablak inditasa (egy mar futo hosthoz)",
                StartHost: false,
                StartGuest: true,
                [
                    "A vendeg ablak kulon konzolban indul.",
                    "A hostnak mar futnia kell localhost:{port} cimen."
                ],
                [],
                [
                    "Ellenorizd a csatlakozast es a snapshot frissulest.",
                    "Ha a host nem elerheto, a kliens ujraprobal."
                ]),
            ["join"] = new CoopScenario(
                "join",
                "Host + vendeg belepteteses mozgasszinkron",
                StartHost: true,
                StartGuest: true,
                [
                    "Rendezd a host es vendeg konzolt ket monitorra.",
                    "Lepj mindket oldalon 2-3 mezot, ellenorizd a pozicioszinkront."
                ],
                [
                    "Mozgasd a hostot, majd figyeld a vendeg oldali valtozast.",
                    "Nyomj Esc-et a hoston a kilepeshez."
                ],
                [
                    "A vendeg lepesei jelenjenek meg host oldalon is.",
                    "Ellenorizd, hogy a kepkockak folyamatosan frissulnek."
                ]),
            ["help-banner"] = new CoopScenario(
                "help-banner",
                "Szemelyes ablak + coop status banner demonstracio",
                StartHost: true,
                StartGuest: true,
                [
                    "A vendeg oldalon nyisd meg a Helpet (Shift+F1).",
                    "A host oldalon figyeld a blokkolo ablak statuszjelzest."
                ],
                [
                    "A host felso savjaban jelenjen meg, hogy a vendeg szemelyes ablakban van.",
                    "A jelzes zarodjon, ha a vendeg kilep a Helpbol."
                ],
                [
                    "Help/Settings/Quest Journal alatt latszodjon a figyelmezteto banner.",
                    "Lepj ki az ablakbol es ellenorizd a banner eltuneset."
                ]),
            ["shared-window"] = new CoopScenario(
                "shared-window",
                "Kozos narrativ ablak es ketoldali nyugtazas",
                StartHost: true,
                StartGuest: true,
                [
                    "Host oldalon valts ki egy kozos narrativ/summary ablakot.",
                    "Nyugtazd a hoston es vendegen is a kozos ablakot."
                ],
                [
                    "A host altal megnyitott kozos ablak jelenjen meg a vendegen is.",
                    "A nyugtazas allapota mindket oldalon kovesse egymast."
                ],
                [
                    "A vendeg oldalon a replika ablak jelenjen meg es fogadjon bemenetet.",
                    "Nyugtazas utan terjen vissza normal jatekneztre."
                ])
        };

    public static IReadOnlyList<CoopScenario> List() => Scenarios.Values.OrderBy(value => value.Name).ToList();

    public static CoopScenario GetOrDefault(string? scenarioName)
    {
        if (string.IsNullOrWhiteSpace(scenarioName)) return Scenarios["join"];
        return Scenarios.TryGetValue(scenarioName, out var scenario) ? scenario : Scenarios["join"];
    }

    public static bool TryGet(string? scenarioName, out CoopScenario scenario)
    {
        if (!string.IsNullOrWhiteSpace(scenarioName) && Scenarios.TryGetValue(scenarioName, out var found))
        {
            scenario = found;
            return true;
        }

        scenario = Scenarios["join"];
        return false;
    }
}

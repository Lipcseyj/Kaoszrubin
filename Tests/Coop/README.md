# Coop szimulacios teszt suite (felig manualis)

Ez a mappa egy **felig manualis** teszt suite-ot tartalmaz, amivel egyetlen gepen is
kiprobalhato a coop multiplayer mod. A host es a vendeg **kulon-kulon sajat konzolablakban**
indul, igy ket monitoron egyszerre lathato a `Game` host UI-ja es a `CoopGuestScreen`,
beleertve a `CoopWindowStatusBanner` figyelmezteto savot is.

A suite nem fut le a normal, allitasos tesztek kozott: csak akkor aktivalodik, ha a
`--coop-sim` vagy a `--coop-role` kapcsolo szerepel a parancssorban.

## Futtatas

Interaktiv szcenario-menuvel:

```powershell
dotnet run --project Tests -- --coop-sim
```

Kozvetlenul egy szcenarioval:

```powershell
dotnet run --project Tests -- --coop-sim --scenario join
```

Mas port vagy sajat (nem torlodo) munkakonyvtar:

```powershell
dotnet run --project Tests -- --coop-sim --scenario join --port 5300 --workspace C:\temp\kr-coop
```

Egy szerep kezi inditasa (a vezerlo folyamat ezt hasznalja belul):

```powershell
dotnet run --project Tests -- --coop-role host --port 5127 --workspace C:\temp\kr-coop
dotnet run --project Tests -- --coop-role guest --port 5127 --workspace C:\temp\kr-coop
```

## Ket monitoros munkafolyamat

1. Inditsd a vezerlot a valasztott szcenarioval.
2. A vezerlo kiirja a kezi lepeseket, majd elinditja a szerepablakokat
   (host eloszor, kb. 1,2 masodperc utan a vendeg).
3. Huzd at a host ablakot az egyik, a vendeg ablakot a masik monitorra.
4. Jatszd vegig a kiirt lepeseket, es figyeld mindket oldalt.
5. `Esc` a vezerlo ablakban: mindket szerepablak leall, az ideiglenes munkakonyvtar torlodik.

## Konzolmeret

A jatek es a vendeg kepernyo teljes meretet igenyel (`TerminalViewport`). Ha az ablak tul kicsi,
a kepernyo meretfigyelmeztetest rajzol. Allitsd a szerepablakokat teljes meretre, mielott
ertekelnel egy szcenariot.

## Szcenariok

| Nev | Ablakok | Mit demonstral |
| --- | --- | --- |
| `host` | host | Host varakozoszoba, csatlakozasi cim, host UI |
| `guest` | vendeg | Csatlakozas egy mar futo hosthoz, snapshot frissules |
| `join` | host + vendeg | Belepes es mozgasszinkron mindket oldalon |
| `help-banner` | host + vendeg | Vendeg szemelyes ablaka + `CoopWindowStatusBanner`, host oldali blokkolo ablak jelzes |
| `shared-window` | host + vendeg | Kozos narrativ ablak replikacioja es ketoldali nyugtazas |

## Uj szcenario hozzaadasa

1. Vegy fel egy uj bejegyzest a `CoopScenarios.Scenarios` szotarba: nev, leiras,
   `StartHost`/`StartGuest`, valamint a vezerlonek, hostnak es vendegnek szolo kezi lepesek.
2. A `{port}` helyorzo a szovegekben automatikusan a tenyleges portra cserelodik.
3. Ha a szcenariohoz automatikus billentyubeadas kell, hasznald a `ICoopKeyScript` seamet
   (`WindowsConsoleKeyScript`); alapertelmezesben minden szcenario kezi vezerlesu
   (`NoOpCoopKeyScript`).

## Fajlok

- `CoopHarnessOptions.cs` - parancssori kapcsolok feldolgozasa
- `CoopSimulationHarness.cs` - vezerlo: menu, szerepinditas, leallitas, takaritas
- `CoopHarnessProcess.cs` - gyermekfolyamat sajat konzolablakkal
- `CoopFixtureFactory.cs` - determinisztikus katalogus, hash, karakterek, izolalt mentesek
- `CoopHostRole.cs` - host szerep (`CoopHostRuntime` + `Game.Run`)
- `CoopGuestRole.cs` - vendeg szerep (`CoopGuestScreen.RunAsync`)
- `CoopScenarios.cs` - szcenario-regiszter
- `CoopKeyScript.cs` - opcionalis billentyu-injektalas seam

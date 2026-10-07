# FixFlow.Mobile

[![CI](https://github.com/mpalus-git/FixFlow.Mobile/actions/workflows/ci.yml/badge.svg)](https://github.com/mpalus-git/FixFlow.Mobile/actions/workflows/ci.yml)

Aplikacja mobilna technika systemu FixFlow (.NET MAUI 10, Android i Windows). Technik widzi przypisane mu zlecenia, rozpoczyna pracę, dodaje wpisy serwisowe z czasem pracy, zużytymi częściami, zdjęciami i pozycją GPS, a na końcu zamyka zlecenie. Wszystko działa także bez zasięgu: zmiany zapisują się lokalnie i trafiają do API po odzyskaniu sieci.

## Wypróbowanie

Najnowsze wydanie: [github.com/mpalus-git/FixFlow.Mobile/releases/latest](https://github.com/mpalus-git/FixFlow.Mobile/releases/latest).

| Plik | Platforma | Uruchomienie |
|---|---|---|
| `FixFlow-<wersja>-windows-x64.zip` | Windows 10 1809 lub nowszy, x64 | rozpakować i uruchomić `FixFlow.Mobile.exe`, bez instalacji |
| `FixFlow-<wersja>.apk` | Android 10 lub nowszy | zainstalować spoza sklepu (system poprosi o zgodę na instalację z tego źródła) |

Plik exe nie jest podpisany certyfikatem, więc przy pierwszym uruchomieniu Windows SmartScreen pokaże ostrzeżenie: trzeba wybrać „Więcej informacji”, a potem „Uruchom mimo to”.

Aplikacja łączy się z instancją demo API na Render. Konto technika (Jan Kowalski) ma dane podane w [README FixFlow.Api](https://github.com/mpalus-git/FixFlow.Api#opis-systemu), a w wydaniu można też użyć przycisku „Zaloguj się kontem demo”. Inne role po zalogowaniu dostają link do [panelu dyspozytora](https://fix-flow-web.vercel.app). Warto wiedzieć, że:

- dane demo są wspólne dla wszystkich odwiedzających i wracają do stanu początkowego codziennie o 2:00 UTC;
- serwer działa na darmowym planie, więc po okresie bezczynności startuje do minuty; aplikacja pokazuje wtedy pasek „Serwer się uruchamia” i do tego czasu pracuje na danych lokalnych;
- pracę offline najłatwiej zobaczyć, wyłączając sieć w trakcie pracy i włączając ją z powrotem.

## Zrzuty ekranu

| Moje zlecenia | Szczegóły zlecenia | Wpis serwisowy |
|---|---|---|
| ![Lista zleceń z podziałem na zaległe, dziś, nadchodzące i zakończone](docs/screenshots/work-orders.png) | ![Szczegóły zlecenia z klientem i urządzeniem](docs/screenshots/work-order-details.png) | ![Formularz wpisu serwisowego z czasem pracy i zużytą częścią](docs/screenshots/service-entry.png) |

| Praca offline z kolejką | Zmiana odrzucona przez serwer | Tryb ciemny |
|---|---|---|
| ![Lista bez połączenia z liczbą zmian do wysłania](docs/screenshots/offline-queue.png) | ![Ekran synchronizacji z rozpoczęciem odrzuconym po przekazaniu zlecenia innemu technikowi](docs/screenshots/conflict.png) | ![Szczegóły zlecenia w toku w trybie ciemnym](docs/screenshots/work-order-details-dark.png) |

Praca bez sieci: rozpoczęcie zlecenia i wpis serwisowy czekają w kolejce, a po odzyskaniu połączenia trafiają do API w kolejności, w jakiej powstały.

![Rozpoczęcie zlecenia i wpis serwisowy bez sieci, potem synchronizacja po odzyskaniu połączenia](docs/screenshots/offline-sync.gif)

Wersja Windows:

![Lista zleceń w wersji Windows](docs/screenshots/windows.png)

## Opis systemu

FixFlow to system obsługi zleceń serwisowych w terenie dla firmy naprawiającej klimatyzację i urządzenia biurowe. Składa się z trzech części:

- [FixFlow.Api](https://github.com/mpalus-git/FixFlow.Api) - backend ASP.NET Core z PostgreSQL, źródło prawdy dla reguł biznesowych i kontraktu OpenAPI.
- [FixFlow.Web](https://github.com/mpalus-git/FixFlow.Web) - panel webowy dyspozytora i administratora: zlecenia, przypisywanie techników, klienci, urządzenia i części. Technik ma w nim tylko podgląd swoich zleceń.
- FixFlow.Mobile (to repozytorium) - jedyne miejsce, w którym technik zmienia stan zleceń.

Technik może:

- przeglądać swoje zlecenia w sekcjach zaległe, dziś, nadchodzące i zakończone ostatnio;
- otworzyć szczegóły z klientem, urządzeniem, wpisami serwisowymi i swoimi wcześniejszymi zleceniami na tym urządzeniu, zadzwonić do klienta, otworzyć adres w aplikacji map i pobrać protokół PDF zakończonego zlecenia;
- rozpocząć zlecenie przypisane (Przypisane -> W toku), przy czym w toku może mieć najwyżej jedno; aplikacja zapisuje pozycję GPS z chwili rozpoczęcia, jeśli technik się na to zgodzi;
- dodawać wpisy serwisowe z notatką, czasem pracy, częściami z katalogu (wyszukiwanie działa offline) i zdjęciami z aparatu lub z plików;
- poprawić pomyłkę wpisem korygującym, który zwraca części najwyżej do ilości netto zużytej na zleceniu (wpisów nie da się edytować ani usuwać);
- zakończyć zlecenie (W toku -> Zakończone), jeśli ma co najmniej jeden wpis;
- dostać przypomnienie godzinę przed terminem zlecenia (Android);
- przełączyć język (polski, angielski) i motyw (systemowy, jasny, ciemny).

Reguły biznesowe egzekwuje API. Aplikacja je odzwierciedla, czyli ukrywa niedozwolone akcje i blokuje lokalnie oczywiste błędy, ale zawsze obsługuje odpowiedź błędu z serwera, także z opóźnieniem, gdy zmiana zrobiona offline zostanie odrzucona przy synchronizacji.

## Stack

| Obszar | Technologie |
|---|---|
| Podstawa | .NET 10, .NET MAUI 10 (Android API 29+, Windows unpackaged i self-contained), C# z Nullable i TreatWarningsAsErrors |
| UI | XAML z kompilowanymi wiązaniami, Shell, CommunityToolkit.Mvvm (source generatory, WeakReferenceMessenger) |
| Komunikacja z API | Refit 16 z klientem i kontraktami generowanymi przez Refitter z OpenAPI, Microsoft.Extensions.Http.Resilience (Polly) |
| Dane lokalne | SQLite (sqlite-net-pcl), osobny plik bazy dla każdego technika, własne migracje schematu |
| Funkcje urządzenia | MAUI Essentials (Connectivity, SecureStorage, MediaPicker, Geolocation, Map, Launcher, Clipboard), Plugin.LocalNotification |
| Języki | pliki .resx (polski domyślny, angielski) |
| Testy | xUnit v3, Shouldly, NSubstitute, Microsoft.Extensions.TimeProvider.Testing, Testcontainers |
| Narzędzia | własne narzędzie Roslyn (usuwanie i kontrola komentarzy), Central Package Management, GitHub Actions |

## Uruchomienie lokalne

Wymagane są .NET SDK 10 (wersja w `global.json`) i workloady MAUI w zestawie przypiętym w `global.json`:

```bash
dotnet workload restore src/FixFlow.Mobile/FixFlow.Mobile.csproj
```

Do wersji Android potrzebne są Android SDK z emulatorem (API 29 lub nowsze) i JDK 21, na przykład z instalacji Visual Studio. Wersja Windows buduje się tylko na Windows.

### Z lokalnym API

Wymagany jest Docker. W repozytorium [FixFlow.Api](https://github.com/mpalus-git/FixFlow.Api#uruchomienie-lokalne) całe środowisko startuje przez `docker compose up --build` (z kontami i danymi demo). Build Debug łączy się z nim automatycznie: emulator Androida przez `http://10.0.2.2:8080`, Windows przez `http://localhost:8080`. Ruch HTTP bez TLS jest dozwolony tylko w Debug.

```bash
dotnet build src/FixFlow.Mobile -t:Run -f net10.0-android
dotnet run --project src/FixFlow.Mobile -f net10.0-windows10.0.19041.0
```

### Z API na Render

Adres API jest właściwością MSBuild `FixFlowApiBaseUrl` zamienianą przy buildzie na stałą w kodzie. Build Release używa Render domyślnie, a w Debug wystarczy ją nadpisać:

```bash
dotnet build src/FixFlow.Mobile -t:Run -f net10.0-android -p:FixFlowApiBaseUrl=https://fixflow-api-us2p.onrender.com
```

Przycisk szybkiego logowania kontem demo pojawia się tylko wtedy, gdy przy buildzie podano właściwości `DemoTechnicianEmail` i `DemoTechnicianPassword` (w workflow wydania pochodzą z sekretów repozytorium).

### Skrypty

| Polecenie | Co robi |
|---|---|
| `scripts/sync-api-contract.sh` | pobiera aktualny kontrakt z main repozytorium API do `openapi/fixflow-api.v1.json` |
| `scripts/generate-api-client.sh` | generuje klienta Refit z kontraktu (Refitter) i usuwa z niego komentarze |
| `dotnet test --project tests/FixFlow.Mobile.UnitTests` | testy jednostkowe |
| `dotnet test --project tests/FixFlow.Mobile.IntegrationTests` | testy integracyjne na prawdziwym API (wymagają Dockera) |
| `dotnet run --project tools/FixFlow.Tools -- check-comments .` | kontrola braku komentarzy w repozytorium |

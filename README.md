# 1wireHID - iButton Keyboard Emulator

.NET 8 aplikacija koja čita iButton/1-Wire uređaje i šalje njihov ROM kao keyboard input (simulira tipkovnicu).

## Funkcionalnost

- Čita iButton uređaje preko TMEX API-ja (IBFS64.dll)
- Šalje ROM kao keyboard input - 16 hex znakova bez separatora
- Dodaje Enter na kraju svakog unosa
- Radi kao tray aplikacija ili terminal za testiranje
- Podržava USB (DS9490R) i Serijski (DS9097U) adapter

---

## Instalacija

### Preporučeno: bootstrapper iz linka

Copy-paste u CMD ili PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm 'https://raw.githubusercontent.com/avukelic-aiot/1wireHID/master/setup.ps1' | iex"
```

Za debug, da prozor ostane otvoren i vidiš sav izlaz:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -NoExit -Command "irm 'https://raw.githubusercontent.com/avukelic-aiot/1wireHID/master/setup.ps1' | iex"
```

Bootstrapper će:
- tražiti admin privilegije ako nisu već aktivne
- instalirati 1-Wire driver MSI ako nije prisutan
- instalirati samo .NET 8 runtime ako nije prisutan
- skinuti najnoviji compiled release asset
- instalirati aplikaciju u `C:\Program Files\1wireHID`
- stvoriti startup shortcut i pokrenuti tray app
- zapisati log u `%TEMP%\1wireHID-setup\setup-<run>.log`

### Build iz source koda

#### Preduvjeti

- Windows 10/11 x64
- [Git](https://git-scm.com/download/win)
- .NET 8 SDK (samo za build iz sourcea)

#### Koraci

```batch
# Kloniraj repozitorij
git clone https://github.com/avukelic-aiot/1wireHID.git
cd 1wireHID

# Pokreni lokalni source setup
powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1
```

Local build služi za development. Remote bootstrapper (`setup.ps1`) koristi compiled release.

---

### 1-Wire Driveri (obavezno)

Bootstrapper instalira driver MSI ako ga ne nađe.

Izvor drivera:

`.1Wire/OneWireDrivers_x64.msi`

Kod lokalnog builda MSI se kopira u `bin\Release\net8.0-windows\win-x64\` i `publish\` output.

Ako trebaš .NET 8 runtime ručno, koristi:

https://dotnet.microsoft.com/download/dotnet/8.0/runtime

---

## Korištenje

### Terminal Mode (testiranje)

```batch
# USB adapter (DS9490R) - default
1wireHID.exe

# Verbose mod - prikazuje sve podatke od drivera
1wireHID.exe -v

# COM port adapter (DS9097U)
1wireHID.exe -com 1
```

### Tray app

```batch
# Tray app će se pokrenuti automatski nakon instalacije
# Ručno pokretanje:
"C:\Program Files\1wireHID\1wireHID.exe" -tray
```

### Testiranje bez hardware-a

```batch
# Pošalji testni ROM kao keyboard input
1wireHID.exe -rom 1800000012345678
```

---

## Opcije naredbenog retka

| Opcija | Opis |
|--------|------|
| `-usb [n]` | USB adapter (DS9490R), port broj n (default: 0) |
| `-com n` | COM port adapter (DS9097U), port broj n |
| `-v, -verbose` | Verbose output - prikazuje raw podatke od drivera |
| `-tray` | Pokreni tray način rada |
| `-console` | Pokreni console debug način |
| `-rom <hex>` | Pošalji specificiran ROM i izađi (za testiranje) |
| `-h, -help` | Prikaži pomoć |

---

## Kako radi

1. Aplikacija otvara 1-Wire adapter preko IBFS64.dll (TMEX API)
2. U petlji izvodi reset na 1-Wire mreži (svakih 100ms)
3. Kad se detektira iButton (reset vraća Presence ili Alarm), čita ROM
4. Šalje ROM kao keyboard input:
   - 16 hex znakova (npr. `1800000012345678`)
   - Zatim Enter (VK_RETURN)

### ROM Format

1-Wire ROM je 8 bajtova: `[Family Code][Serial Number x6][CRC]`

Prikazan kao 16 hex znakova u little-endian formatu:

```
Bajtovi:     [01] [23] [45] [67] [89] [AB] [CD] [EF]
Prikaz:     EFCDAB8967452301
            └──┘ └──┘ └──┘ └──┘ └──┘ └──┘ └──┘ └──┘
            CRC  S/N6  S/N5  S/N4  S/N3  S/N2  S/N1  Family
```

### Detekcija iButton dodira

- Reset vraća `Presence (1)` kad je iButton prislonjen
- `NoPresence (0)` kad nema uređaja
- Samo nova (drugačija) ROM adresa aktivira keyboard output
- Ovo sprječava duplicirane slanje ako iButton ostane prislonjen

---

## Verzioniranje i Release

### Pravila verzioniranja

1. Verzija je u formatu `vMAJOR.MINOR.PATCH` (npr. `v0.1.0`)
2. Commit s promjenama se radi normalno
3. Kada je spremno za release:
   ```batch
   git tag -a v0.2.4 -m "Release version 0.2.4 - [opis promjena]"
   git push origin main --tags
   ```
4. Na GitHubu kreiraj Release iz taga

### Provjera verzija

Bootstrapper automatski uzima latest release.

Za ručnu provjeru:
```batch
# Pogledaj zadnji tag
git tag -l "v*" --sort=-version:refname | head -1

# Pogledaj sve tagove
git tag -l "v*"
```

---

## Struktura projekta

```
1wireHID/
├── 1wireHID.csproj      # .NET 8 projekt
├── Program.cs            # Glavni program + tray/console host
├── TMEX64.cs             # P/Invoke wrapper za IBFS64.dll
├── KeyboardSimulator.cs  # SendInput keyboard emulation
├── setup.ps1             # Remote bootstrapper
├── README.md             # Ova datoteka
├── Agents.md             # Development notes
└── .gitignore            # Git ignore pravila
```

---

## Razvoj

### Build locally

```batch
dotnet build 1wireHID.csproj -c Release -r win-x64

dotnet publish 1wireHID.csproj -c Release -r win-x64
```

### Kreiraj novi release

```batch
# Ažuriraj verziju u Program.cs (VERSION konstanta)
# Commitaj promjene
git add . && git commit -m "Update version to 0.2.4"

# Tagiraj
git tag -a v0.2.4 -m "Release version 0.2.4"

# Push
git push origin main --tags
```

Na GitHubu:
1. Idi na Releases page
2. Klikni "Draft a new release"
3. Odaberi tag `v0.2.4`
4. Unesi release notes
5. Klikni "Publish release"

---

## Poznati problemi

- `IBFS64.dll` mora biti u instalacijskom folderu ili u PATH
- Na nekim USB adapterima može trebati par sekundi za inicijalizaciju
- Za COM port adapter, koristi `-com n` where n je 0-15

---

## Verzija

v0.2.4 - Bugfix: bootstrapper path resolution and log handling

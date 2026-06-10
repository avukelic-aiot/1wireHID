# 1wireHID - iButton Keyboard Emulator

.NET 8 aplikacija koja čita iButton/1-Wire uređaje i šalje njihov ROM kao keyboard input (simulira tipkovnicu).

## Funkcionalnost

- Čita iButton uređaje preko TMEX API-ja (IBFS64.dll)
- Šalje ROM kao keyboard input - 16 hex znakova bez separatora
- Dodaje Enter na kraju svakog unosa
- Radi kao terminal aplikacija ili Windows Service
- Podržava USB (DS9490R) i Serijski (DS9097U) adapter

## Preduvjeti

### .NET 8 Runtime

Aplikacija je self-contained i ne zahtijeva .NET instalaciju, ali za build trebate .NET 8 SDK.

Preuzmite sa: https://dotnet.microsoft.com/download/dotnet/8.0

### 1-Wire Driveri

Driver DLL (`IBFS64.dll`) je potreban za rad. Može se ekstrahirati iz:

```
.1Wire/install_1_wire_drivers_x64_v405.zip
```

Driver kopirajte u jednu od lokacija:
- Isti folder kao `1wireHID.exe`
- `C:\Windows\System32\`

## Instalacija

### Korak 1: Ekstrahiraj drivere

1. Otvorite `.1Wire/install_1_wire_drivers_x64_v405.zip`
2. Ekstrahirajte sadržaj (npr. u `C:\1WireDrivers\`)
3. Kopirajte `IBFS64.dll` iz extrashiranog foldera uz exe ili u System32

### Korak 2: Build aplikacije

```powershell
dotnet build 1wireHID.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

Ili pokrenite `build.bat`

### Korak 3: Instaliraj kao Windows Service (opcionalno)

```batch
sc create OneWireHID binPath= "C:\putanja\do\1wireHID.exe -service"
sc start OneWireHID
```

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

### Windows Service

```batch
# Instalacija
sc create OneWireHID binPath= "C:\putanja\do\1wireHID.exe -service"

# Pokretanje
sc start OneWireHID

# Zaustavljanje
sc stop OneWireHID

# Brisanje servisa
sc delete OneWireHID
```

### Testiranje bez hardware-a

```batch
# Pošalji testni ROM kao keyboard input
1wireHID.exe -rom 1800000012345678
```

## Opcije naredbenog retka

| Opcija | Opis |
|--------|------|
| `-usb [n]` | USB adapter (DS9490R), port broj n (default: 0) |
| `-com n` | COM port adapter (DS9097U), port broj n |
| `-v, -verbose` | Verbose output - prikazuje raw podatke od drivera |
| `-service, -svc` | Pokreni kao Windows Service |
| `-rom <hex>` | Pošalji specificiran ROM i izađi (za testiranje) |
| `-h, -help` | Prikaži pomoć |

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

## Poznati problemi

- `IBFS64.dll` mora biti u PATH ili u istom folderu kao exe
- Na nekim USB adapterima može trebati par sekundi za inicijalizaciju
- Za COM port adapter, koristi `-com n` where n je 0-15

## Struktura projekta

```
1wireHID/
├── 1wireHID.csproj      # .NET 8 projekt
├── Program.cs            # Glavni program + Windows Service
├── TMEX64.cs             # P/Invoke wrapper za IBFS64.dll
├── KeyboardSimulator.cs  # SendInput keyboard emulation
├── build.bat             # Build skripta
├── README.md             # Ova datoteka
├── Agents.md             # Development notes
└── .gitignore            # Git ignore pravila
```

## Verzija

1.0.0 - Initial release
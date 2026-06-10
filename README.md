# 1wireHID - iButton Keyboard Emulator

.NET 8 aplikacija koja čita iButton/1-Wire uređaje i šalje njihov ROM kao keyboard input (simulira tipkovnicu).

## Funkcionalnost

- Čita iButton uređaje preko TMEX API-ja (IBFS64.dll)
- Šalje ROM kao keyboard input - 16 hex znakova bez separatora
- Dodaje Enter na kraju svakog unosa
- Radi kao terminal aplikacija ili Windows Service
- Podržava USB (DS9490R) i Serijski (DS9097U) adapter

---

## Instalacija

### Opcija 1: Preuzmi i pokreni setup.bat (preporučeno)

```batch
# Preuzmi setup.bat i pokreni ga
powershell -Command "Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/avukelic-aiot/1wireHID/main/setup.bat' -OutFile '%USERPROFILE%\Desktop\setup.bat'; Start-Process '%USERPROFILE%\Desktop\setup.bat'"
```

Ili ručno:
1. Preuzmi `setup.bat` iz repozitorija
2. Pokreni ga (desni klik → Run as administrator)

Setup će automatski:
- Provjeriti .NET 8 SDK i instalirati ga ako nedostaje
- Buildati aplikaciju
- Kopirati datoteke u `C:\Program Files\1wireHID\`

### Opcija 2: Preuzmi gotovi release

Preuzmi najnoviji release s GitHub stranice:
https://github.com/avukelic-aiot/1wireHID/releases/latest

### Opcija 2: Build iz source koda

#### Preduvjeti

- Windows 10/11 x64
- [Git](https://git-scm.com/download/win)
- .NET 8 SDK (setup.bat će ga automatski instalirati ako nedostaje)

#### Koraci

```batch
# Kloniraj repozitorij
git clone https://github.com/avukelic-aiot/1wireHID.git
cd 1wireHID

# Pokreni setup (automatski builda i instalira)
setup.bat
```

Setup skripta će:
1. Zatražiti administratorske privilegije (ako nije pokrenut kao admin)
2. Provjeriti ima li .NET 8 SDK, i ako nema - automatski ga instalirati
3. Provjeriti ima li novije verzije na GitHubu
4. Pitati za putanju instalacije (default: `C:\Program Files\1wireHID`)
5. Buildati aplikaciju
6. Kopirati datoteke u odabranu putanju
7. Stvoriti `version.ini` file s informacijama o verziji

---

### 1-Wire Driveri (obavezno)

Driver DLL (`IBFS64.dll`) je potreban za rad. Kopirajte ga u instalacijski folder:

1. Ekstrahirajte iz `.1Wire/install_1_wire_drivers_x64_v405.zip`
2. Kopirajte `IBFS64.dll` u `C:\Program Files\1wireHID\`

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

### Windows Service

```batch
# Instalacija servisa (kao administrator)
sc create OneWireHID binPath= "C:\Program Files\1wireHID\1wireHID.exe -service"
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

---

## Opcije naredbenog retka

| Opcija | Opis |
|--------|------|
| `-usb [n]` | USB adapter (DS9490R), port broj n (default: 0) |
| `-com n` | COM port adapter (DS9097U), port broj n |
| `-v, -verbose` | Verbose output - prikazuje raw podatke od drivera |
| `-service, -svc` | Pokreni kao Windows Service |
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
   git tag -a v0.2.0 -m "Release version 0.2.0 - [opis promjena]"
   git push origin main --tags
   ```
4. Na GitHubu kreiraj Release iz taga

### Provjera verzija

Setup skripta automatski provjerava ima li novije verzije na GitHubu prije buildanja.

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
├── Program.cs            # Glavni program + Windows Service
├── TMEX64.cs             # P/Invoke wrapper za IBFS64.dll
├── KeyboardSimulator.cs  # SendInput keyboard emulation
├── setup.bat             # Setup/instalacijska skripta
├── README.md             # Ova datoteka
├── Agents.md             # Development notes
└── .gitignore            # Git ignore pravila
```

---

## Razvoj

### Build locally

```batch
dotnet build 1wireHID.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

### Kreiraj novi release

```batch
# Ažuriraj verziju u Program.cs (VERSION konstanta)
# Commitaj promjene
git add . && git commit -m "Update version to 0.2.0"

# Tagiraj
git tag -a v0.2.0 -m "Release version 0.2.0"

# Push
git push origin main --tags
```

Na GitHubu:
1. Idi na Releases page
2. Klikni "Draft a new release"
3. Odaberi tag `v0.2.0`
4. Unesi release notes
5. Klikni "Publish release"

---

## Poznati problemi

- `IBFS64.dll` mora biti u instalacijskom folderu ili u PATH
- Na nekim USB adapterima može trebati par sekundi za inicijalizaciju
- Za COM port adapter, koristi `-com n` where n je 0-15

---

## Verzija

v0.1.1 - Fix: Corrected installation URL in README
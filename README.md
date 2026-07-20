# 1wireHID

1wireHID je Windows tray aplikacija za iButton / 1-Wire citace. Aplikacija cita iButton ROM i upisuje ga u trenutno aktivno polje kao da je upisan tipkovnicom, zatim salje Enter.

## Najjednostavnija instalacija

Korisnik treba samo otvoriti PowerShell ili CMD i pokrenuti ovu naredbu:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm 'https://raw.githubusercontent.com/avukelic-aiot/1wireHID/master/setup.ps1' | iex"
```

Installer ce automatski:

1. zatraziti administratorska prava ako su potrebna,
2. instalirati 1-Wire drivere ako `IBFS64.dll` nije pronaden,
3. instalirati .NET 8 runtime ako nije prisutan,
4. skinuti zadnji GitHub release paket `1wireHID-win-x64.zip`,
5. instalirati aplikaciju u `C:\Program Files\1wireHID`,
6. dodati startup shortcut za automatsko pokretanje nakon prijave,
7. odmah pokrenuti tray aplikaciju.

Za debug instalacije, da prozor ostane otvoren:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -NoExit -Command "irm 'https://raw.githubusercontent.com/avukelic-aiot/1wireHID/master/setup.ps1' | iex"
```

## Preduvjeti

- Windows 10/11 x64
- DS9490R/B ili kompatibilan 1-Wire USB adapter
- Internet pristup tijekom instalacije
- Administrator prava za instalaciju drivera i pisanje u `Program Files`

Korisnik ne treba rucno instalirati drivere ni .NET runtime. Installer to radi automatski.

## Nakon instalacije

Tray aplikacija se pokrece automatski. Kada je iButton prislonjen na citac, njegov ROM se upisuje u aktivnu aplikaciju kao tekst:

```text
3000001956DFE001
```

Nakon ROM-a se salje Enter.

## Logovi

Aplikacija zapisuje dva dnevna loga u korisnikov Documents folder:

- `1wireHID-iButtons-YYYY-MM-DD.csv` - jednostavan CSV zapis svakog iButton dodira
- `1wireHID-YYYY-MM-DD.log` - runtime dijagnosticki log

CSV format:

```csv
timestamp,rom,source,event,forwarded
"2026-07-20 10:23:58.968","3000001956DFE001","tray","touch","true"
```

`forwarded=true` znaci da je Windows `SendInput` prihvatio slanje tipkovnice.

## Rucno pokretanje

```powershell
"C:\Program Files\1wireHID\1wireHID.exe" -tray -usb 2
```

## Dijagnostika

Console mode s prikazom adaptera, ROM-ova i slanja:

```powershell
"C:\Program Files\1wireHID\1wireHID.exe" -console -v -usb 2
```

Samo detekcija i logiranje, bez slanja tipkovnice:

```powershell
"C:\Program Files\1wireHID\1wireHID.exe" -console -v -nosend -usb 2
```

Test slanja bez hardwarea:

```powershell
"C:\Program Files\1wireHID\1wireHID.exe" -rom 3000001956DFE001
```

## Opcije

| Opcija | Opis |
| --- | --- |
| `-tray` | Pokrece tray aplikaciju |
| `-console` | Pokrece console dijagnostiku |
| `-v`, `-verbose` | Ispisuje detaljan debug output |
| `-usb [n]` | USB 1-Wire adapter port, default je `2` |
| `-com n` | Serijski 1-Wire adapter port |
| `-nosend`, `-detectonly` | Detektira i logira iButton, ali ne salje tipkovnicu |
| `-rom <hex>` | Posalje zadani ROM kao tipkovnicu i izade |

## Kako radi

1. Aplikacija otvara TMEX session preko `IBFS64.dll`.
2. Citac DS9490R/B ima vlastiti ROM; aplikacija ga prepoznaje i ignorira.
3. Svakih 100 ms skenira 1-Wire mrezu.
4. Kada vidi novi iButton ROM, zapisuje ga u CSV i runtime log.
5. ROM salje u aktivni prozor preko Windows `SendInput` API-ja.

## Build iz sourcea

Za development je potreban .NET SDK:

```powershell
dotnet build .\1wireHID.csproj -c Release -r win-x64
dotnet publish .\1wireHID.csproj -c Release -r win-x64 --self-contained false
```

Release asset koji installer ocekuje:

```text
1wireHID-win-x64.zip
```

Driver release asset koji installer ocekuje:

```text
OneWireDrivers_x64.msi
```

## Release

Za verziju `1.0.0`:

```powershell
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin master --tags
```

Na GitHub Releases treba objaviti zip asset `1wireHID-win-x64.zip` i driver asset `OneWireDrivers_x64.msi`, jer ih `setup.ps1` automatski preuzima.

## Verzija

`v1.0.0`

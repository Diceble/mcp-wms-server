# MCP WMS Server — Slice 1 Opzet

2026-10-25 · @Bas

## Doel en definitie van klaar

Een kleine MCP-server in .NET waarmee Claude in gewone taal vragen over voorraad en orders beantwoordt, rechtstreeks uit een eigen database.

**Klaar is:** ik kan Claude vragen stellen over voorraad en orders, en het antwoordt correct uit mijn database via een MCP-server die ik in .NET heb gebouwd, met een README.

## Scope

### Datamodel (6 tabellen)

| Tabel       | Velden                                                        |
|-------------|---------------------------------------------------------------|
| Product     | sku, naam, bestelniveau (reorder level)                       |
| Location    | bincode, zone                                                 |
| StockLevel  | product, locatie, aantal, gereserveerd                        |
| Order       | ordernummer, klant, status, beloofde leverdatum               |
| OrderLine   | order, product, aantal, aantal verzonden                      |
| Reservation | orderregel, locatie, aantal                                   |

De zesde tabel `Reservation` is tijdens het interview toegevoegd (zie Besluiten).

Seed een paar honderd realistische rijen, inclusief bewuste rommel: late orders, lage voorraad, voorraad verspreid over locaties, cryptische statuscodes, deels verzonden regels en voorraad die gereserveerd maar niet gepickt is.

### Vier vragen die correct beantwoord moeten worden

1. Welke producten zitten onder hun bestelniveau?
2. Waar ligt SKU X, en hoeveel per locatie?
3. Welke orders zijn te laat of dreigen te laat te worden, en waarom?
4. Kunnen we order 1042 vandaag volledig verzenden?

Vraag 4 dwingt Claude om tools te combineren; dat is waar MCP echt waarde toevoegt.

### MCP-tools (alleen lezen)

- `search_products`
- `get_stock_for_product`
- `get_low_stock_products`
- `get_order` (inclusief regels)
- `get_late_orders`

Smalle, doelgerichte tools, geen generieke `run_sql`. Veiliger, en precies het patroon dat je later bij een bestaand WMS wilt laten zien.

### Stack

- .NET 10, EF Core met SQLite
- NuGet `ModelContextProtocol` (stdio-transport), gekoppeld aan Claude Desktop
- Querylaag achter een interface (`IInventoryQueries`), zodat een koppeling met een bestaand WMS later alleen een tweede implementatie is

```
src/
  Wms.Domain/          entiteiten, IInventoryQueries en resultaattypes
  Wms.Infrastructure/  EF Core, SQLite, seed, IInventoryQueries-implementatie
  Wms.McpServer/       host + tools
tests/
  Wms.Tests/           unit tests op de querylaag
```

## Besluiten (interview 2026-10-25)

### Voorraadregels

- **Beschikbaar** = som over locaties van `aantal − gereserveerd`, **alleen in verzendbare zones**.
- **Zones:** `PICK`, `BULK`, `RECV`, `QC`, `DMG`. Voorraad in `QC` (kwaliteitscontrole) en `DMG` (beschadigd) telt niet als beschikbaar voor vraag 1 en 4, maar wordt wel getoond bij vraag 2.
- **Onder bestelniveau (vraag 1):** beschikbaar < bestelniveau.

### Reserveringen

- Nieuwe tabel `Reservation(orderregel, locatie, aantal)`: een reservering hoort bij een specifieke orderregel op een specifieke locatie.
- `StockLevel.gereserveerd` blijft een **opgeslagen totaal** (zoals in echte WMS'en). De seed bevat bewust minstens één afwijking tussen dit totaal en de som van de `Reservation`-rijen.
- **Verzendbaarheid (vraag 4):** per regel is het openstaande aantal (`aantal − aantal verzonden`) te dekken uit beschikbare voorraad **plus de eigen reserveringen van die order**. Reserveringen van andere orders zijn niet bruikbaar.

### Orders

- **Statuscodes** staan cryptisch in de database. Tools geven code **én** betekenis terug; onbekende of verouderde codes worden gemarkeerd als `unknown`.

  | Code | Betekenis                       |
  |------|---------------------------------|
  | NW   | Nieuw                           |
  | ALC  | Gealloceerd (voorraad gereserveerd) |
  | PCK  | In pick                         |
  | PSH  | Deels verzonden                 |
  | SHP  | Verzonden                       |
  | HLD  | On hold                         |
  | BO   | Backorder                       |
  | CNX  | Geannuleerd                     |

  Plus een paar legacy/onbekende codes in de seed (bijvoorbeeld `X9`, `pck` in kleine letters).

- **Te laat:** beloofde leverdatum < vandaag en niet volledig verzonden (`SHP`/`CNX` tellen niet).
- **Dreigt te laat (vraag 3):** beloofde leverdatum binnen **N = 2 dagen**, niet volledig verzonden, **én** er is een probleem: onvoldoende beschikbare voorraad voor een regel, of status `HLD`/`BO`. De query geeft per order de reden(en) terug.
- **Order 1042:** **niet** volledig verzendbaar, om een subtiele reden. Het totaal lijkt genoeg, maar voor één regel ligt een deel in `QC` of is het gereserveerd voor een andere order. Claude moet tools combineren om dat te zien.

### Tijd

- Overal `TimeProvider` injecteren; geen `DateTime.Now` in de querylaag.
- Seeddatums zijn **relatief aan vandaag**, zodat de demo altijd klopt. Tests gebruiken `FakeTimeProvider` met een vaste datum.

### Seed

- Domein: **fietsonderdelen** (banden, kettingen, remblokken, zadels, enz., met varianten in maat en kleur).
- Deterministisch (vaste random seed), zodat tests stabiele verwachtingen hebben.

### Conventies

- Code, toolnamen, toolbeschrijvingen en README in het **Engels**.
- Tests: xUnit op SQLite in-memory. Database aanmaken via `EnsureCreated` (geen migrations in slice 1).

## Planning (3–4 weken avonden)

- [x] Week 1: datamodel, seeddata en querylaag met een paar unit tests
- [x] Week 2: MCP-server met de 5 tools, gekoppeld aan Claude Desktop, vragen 1 en 2 werken
- [x] Week 3: vragen 3 en 4 werken; tool-beschrijvingen tunen (hier leer je het meest over hoe modellen tools kiezen en combineren)
- [x] Week 4: README, op GitHub. Klaar. (De demovideo is geschrapt.)

Past het niet in de tijd, schrap dan scope, geen kwaliteit.

## Buiten scope (parkeerlijst)

- Schrijfacties (voorraad reserveren, orders aanmaken)
- Voorraadmutatie-tabel (ledger): append-only mutaties waaruit de stand volgt, zoals bij bankboekhouding. Hoort samen met de schrijfacties in slice 2.
- HTTP-transport en authenticatie
- Web-UI
- Meerdere magazijnen
- Shopify- of EDI-integratie
- Eigen LLM-client

Allemaal goede kandidaten voor slice 2 en verder. Nieuwe ideeën komen hier, niet in de huidige slice.

## Ontwerpkeuzes

- **Generiek schema, herkenbare termen.** Geen kopie van het datamodel van een bestaand WMS: dat houdt het project duidelijk van mij en vrij van legacy-keuzes.
- **De tools zijn het contract, niet de tabellen.** Claude ziet alleen toolnamen, beschrijvingen, parameters en returnvormen. Bij een demo op een bestaand WMS hergebruik ik de tools en schrijf ik alleen een nieuwe `IInventoryQueries`-implementatie.
- **Realistische rommel in de seed.** Schone data verbergt het moeilijke deel; zo leer ik nu al hoe tool-beschrijvingen met rommelige data omgaan.

## Startprompt voor Claude Code

```
Lees docs/slice-1.md. We bouwen alleen week 1:

1. Maak de solution-structuur uit het doc (.NET 10, src/ en tests/).
2. Implementeer de 6 entiteiten in Wms.Domain en een EF Core DbContext met
   SQLite in Wms.Infrastructure.
3. Schrijf een seeder met een paar honderd realistische rijen, inclusief de
   rommel die in het doc staat.
4. Definieer IInventoryQueries met methodes voor de vier vragen en
   implementeer die met EF Core.
5. Schrijf unit tests (xUnit) die bewijzen dat elke methode de verwachte
   resultaten geeft op de seeddata.

Nog geen MCP-server. Stel eerst een plan voor en wacht op mijn akkoord
voordat je code schrijft.
```

# Week 3 — evaluatie in Claude Desktop

Stel elke vraag in een **nieuwe chat**, zodat eerdere antwoorden niet meetellen. Klap de toolaanroepen open en noteer welke tools Claude koos en in welke volgorde. "Vandaag" is de dag waarop de server gestart is; de seed rekent daarvanaf.

## Vraag 3 — Welke orders zijn te laat of dreigen te laat te worden, en waarom?

**Verwacht:** `get_late_orders`, en eventueel `get_order` of `get_stock_for_product` voor details.

| Order | Klant | Wanneer | Waarom |
|---|---|---|---|
| 1104 | Rijwielzaak Bakker | 5 dagen te laat | Voorraad is er; status New, dus nog niet opgepakt |
| 1107 | Cycle Center Groningen | 2 dagen te laat | Voorraad is er; status Picking (in de data staat `pck`) |
| 1042 | Fietsenwinkel De Spaak | vandaag | Remblokken 2 tekort |
| 1106 | Pedaal & Co | morgen | On hold |
| 1110 | Tweewieler Centrum Gouda | morgen | Onbekende statuscode `X9` |
| 1108 | Fietsplus Eindhoven | over 2 dagen | Binnenband SV17: 10 nodig, 3 beschikbaar |
| 1112 | Velo Amsterdam | over 2 dagen | Backorder |

**Mag er niet in staan:**
- 1105, verzonden
- 1111, geannuleerd
- 1113, gezond
- 1109, over 3 dagen en dus buiten het venster, al heeft hij hetzelfde tekort als 1108

**Pluspunten:**
- Claude ziet dat 1108 en 1109 dezelfde 3 binnenbanden nodig hebben. De tekortreden van 1108 noemt 1109 ("also needed by other open orders").
- Claude vraagt bij `X9` om navraag in plaats van een betekenis te gokken.

## Vraag 4 — Kunnen we order 1042 vandaag volledig verzenden?

**Verwacht:** eerst `get_order(1042)`, daarna `get_stock_for_product` voor alle drie de regels.

**Antwoord: nee.**

| Regel | Open | Eigen reservering | Beschikbaar | Gedekt? |
|---|---|---|---|---|
| Ketting KMC X11 | 4 | 4 | 22 | ja |
| Band GP5000 (6 van 10 al verzonden) | 4 | 4 | 16 | ja |
| Remblokken B01S | 12 | 0 | 10 | **nee, 2 tekort** |

**Waarom de remblokken tekortkomen:**
- Er liggen er fysiek 18, maar 4 daarvan staan in QC-01 (kwaliteitscontrole) en 4 op A-01-03 zijn gereserveerd voor **order 1101**.
- Vrij beschikbaar zijn er dus 10: 6 op A-01-03 en 4 op B-01-02.

**Pluspunten:**
- Claude ziet dat order 1101 pas over 10 dagen geleverd moet worden. Dat staat in `reservedFor` bij A-01-03. De reservering omhangen naar 1042 lost het tekort dan vandaag op.
- Claude noemt als alternatieven: de QC-partij laten vrijgeven, of 1042 deels verzenden (de status is al "deels verzonden").

**Fout als:**
- Claude "ja" zegt omdat het totaal van 18 genoeg lijkt.
- Claude de ketting of de band als tekort ziet, omdat de eigen reservering van 1042 niet is meegeteld.

## Controlevragen

| Vraag | Verwacht antwoord |
|---|---|
| Kan order 1101 vandaag weg? | Ja, de eigen reservering van 4 op A-01-03 dekt hem |
| Kan order 1106 vandaag weg? | Voorraad is er, maar de order staat on hold, dus niet zonder vrijgave |
| Kan order 1109 vandaag weg? | Nee: 10 binnenbanden nodig, 3 beschikbaar, en 1108 wil dezelfde |
| Wat is er met order 1110? | Onbekende statuscode `X9`; navragen, niet gokken |
| Kan order 1105 vandaag weg? | Al verzonden, er valt niets te verzenden |

## Observaties

Vul in wat opviel. Als een tool verkeerd gekozen wordt of een veld verkeerd wordt gelezen, pas dan de beschrijving aan in `WmsTools.cs` of `ServerInstructions.cs`, publiceer opnieuw en stel de vraag opnieuw.

### Ronde 1 — 2026-10-25

| Vraag | Klopte het? | Wat viel op |
|---|---|---|
| 3 | Ja: precies de 7 verwachte orders, met de juiste redenen | Bij 1104 en 1107 werd de vertraging terecht bij de status gelegd. Bij 1112 zag Claude zelf dat de backorderstatus niet klopt, want er zijn 100 bidonhouders. **Mis:** "er zijn geen orders die om dezelfde SKU concurreren". 1109 wil dezelfde binnenbanden als 1108, maar valt buiten het venster en was dus niet zichtbaar. |
| 4 | Ja: nee, de remblokken komen 2 tekort; ketting en band zijn gedekt door de eigen reserveringen | Claude noemde alle drie de oplossingen: QC vrijgeven, de reservering van 1101 omhangen, of deels verzenden. **Mis:** bij 1101 gokte Claude op "waarschijnlijk meer speling" in plaats van de leverdatum te noemen, want die stond niet in de data. Gesteld in dezelfde chat als vraag 3. |

Algemeen: "Let op: het is zondag" komt uit de serverinstructies. Claude Desktop geeft die dus door aan het model.

**Aanpassingen na ronde 1:**
- `reservedFor` geeft per reserverende order ook `promisedDate` en `daysUntilPromised` terug.
- De tekortredenen in `get_late_orders` noemen andere open orders voor dezelfde SKU, ook als die buiten het venster vallen.

### Ronde 2 — 2026-10-25

Vraag 4 is gesteld in een **nieuwe chat**, zodat hij niet leunde op de lijst uit vraag 3.

| Vraag | Klopte het? |
|---|---|
| 3 | Ja |
| 4 | Ja |

Beide vragen werden correct beantwoord na de aanpassingen uit ronde 1. Daarmee is het doel van week 3 gehaald. De volgorde van de toolaanroepen is in deze ronde niet genoteerd.

## Wat week 3 leerde over toolbeschrijvingen

- **Geef de data die Claude nodig heeft, niet alleen een instructie.** Ronde 1 vroeg om de leverdatum van reserverende orders te noemen, maar die datum zat niet in de uitvoer, dus ging Claude gokken. Pas toen `reservedFor` de datum bevatte, kwam het concrete antwoord.
- **Wat buiten het zicht van een tool valt, bestaat voor Claude niet.** `get_late_orders` filtert op een venster, en daardoor concludeerde Claude dat er geen concurrentie was. Een tool moet zelf melden wat er buiten zijn filter relevant is.
- **Splits velden die verschillende acties vragen.** `unshippable` gooide QC en beschadigd op één hoop, terwijl de actie verschilt: laten keuren of afschrijven.
- **Serverinstructies komen aan.** Claude Desktop geeft ze door aan het model, zoals bleek uit "het is zondag". Ze zijn een goede plek voor begrippen en het rekenrecept dat voor alle tools geldt.

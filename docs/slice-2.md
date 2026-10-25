# MCP WMS Server — Slice 2: Evaluatieharness

2026-10-25 · @Bas

## Doel en definitie van klaar

Meten in plaats van vertellen: hoe beïnvloeden het tooldesign en de modelkeuze of Claude de vragen over voorraad en orders correct beantwoordt? Slice 1 liet dat zien met twee handmatige rondes in Claude Desktop. Slice 2 maakt er een herhaalbare meting van.

**Klaar is:** één commando draait de evaluatieset tegen beide serverversies en drie modellen. Een HTML-rapport in het repo laat per combinatie zien:
- het slagingspercentage, met de ruismarge;
- de kosten en tokens per vraag;
- per case het volledige antwoord, met alle toolaanroepen.

## Wat we vergelijken

Er zijn twee assen, samen 6 combinaties.

| As | Varianten |
|---|---|
| Serverversie | **week 2**: de eerste vijf tools (commit `06e679c`). **week 3**: de bijgeschaafde uitvoer, beschrijvingen en serverinstructies (commit `c54fbd8`). |
| Model | `claude-opus-5-5`, `claude-sonnet-5-5`, `claude-haiku-5-5` |

- De twee serverversies krijgen een git-tag (`slice1-week2`, `slice1-week3`). De harness bouwt ze vanuit git en start ze via stdio, net als Claude Desktop. Zo meten we de echte server, inclusief de veranderde uitvoer, en niet alleen andere beschrijvingen.
- Alle modellen draaien op effort `medium`. De standaardwaarde verschilt per model, en zo vergelijken we eerlijk.
- Elke case draait **5 keer** per combinatie.

## Evaluatieset

Er komen ongeveer 23 cases, in vijf groepen. Alle vragen zijn in het Nederlands, zoals in Claude Desktop.

| Groep | Voorbeelden | Wat goed is |
|---|---|---|
| Lage voorraad | "Welke producten zitten onder hun bestelniveau?", plus varianten | De scenarioproducten worden genoemd, met de juiste reden (gereserveerd, in QC). De pedalen worden niet genoemd. |
| Locatie | "Waar ligt CAS-SHI-HG50-1132?", "Waar liggen de Brooks-zadels?" | De juiste locaties en aantallen. DMG wordt niet als verzendbaar genoemd. De afwijking in de reserveringen wordt gemeld. |
| Late orders | "Welke orders zijn te laat of dreigen te laat te worden, en waarom?", plus varianten | Precies de 7 orders uit de evaluatieset van week 3, met de juiste reden per order. |
| Verzendbaarheid | Order 1042, 1101, 1106, 1109, 1105, 1110 | Het juiste ja of nee, de juiste blokkade of het juiste tekort, en bij 1042 de reservering van 1101. |
| Grensvragen | "Wat kost een B01S-remblok?", vragen over leveranciers, omzet, toekomstige leveringen | Claude zegt dat het niet in de data staat, en verzint niets. |

**Wie wat doet.** Bas schrijft per groep een paar echte formuleringen, en per case de verwachte feiten. Claude maakt daar varianten van. Bas leest de volledige set en keurt hem goed: **sign-off 1**.

**Verwachte feiten zijn datumonafhankelijk.** Ze gaan over ordernummers, SKU's, aantallen en ja of nee, nooit over absolute datums. De seed rekent vanaf vandaag, en beide serverversies draaien in dezelfde meting, dus de uitkomsten blijven vergelijkbaar.

## Beoordeling

- **Feitenextractie door een jury.** `claude-haiku-5-5` haalt met structured output de feiten uit het Nederlandse antwoord, per groep met een eigen schema. Voorbeelden: het oordeel (ja of nee), de SKU met tekort en de hoeveelheid, de genoemde orders, en of er iets beweerd wordt dat niet in de data staat.
- **Code oordeelt, niet de jury.** De geëxtraheerde feiten worden programmatisch vergeleken met de verwachte feiten.
- **Tooltrace.** Code controleert de toolaanroepen die de harness zelf heeft gelogd. Voorbeeld voor 1042: `get_order`, en daarna `get_stock_for_product` voor alle drie de regels.
- **Metrieken:**

  | Metriek | Wat |
  |---|---|
  | `correct` (hoofdmetriek) | Alle verwachte feiten kloppen |
  | `tools_ok` | De verplichte toolaanroepen zijn gedaan |
  | `no_fabrication` | Er zijn geen feiten verzonnen; telt vooral bij de grensvragen |
  | daarnaast | Input- en output-tokens, kosten, latency, aantal toolaanroepen |

- **Sign-off 2.** Voordat we de jury vertrouwen, beoordeelt Bas ~10 antwoorden met de hand en vergelijkt die met de uitkomst van de jury. Wijkt het af, dan wordt het schema of de prompt van de jury aangepast.

## Opzet harness

- **Project:** `evals/Wms.Evals`, een console-app in .NET 10 met de officiële Anthropic C# SDK.
- **Per run:**
  1. Start de gebouwde serverversie via een MCP-client over stdio.
  2. Haal de tools op en zet ze om naar tooldefinities voor de Claude API.
  3. Draai een tool-use-loop: elke toolaanroep van Claude gaat via de MCP-client naar de echte server.
  4. Sla het eindantwoord op.
- **Systeemprompt:** de serverinstructies, zoals Claude Desktop die doorgeeft. De week-2-versie heeft geen instructies; dat hoort bij het verschil dat we meten.
- **Uitvoer** per variant: `results.jsonl`, `traces/<case>_rep<k>.json` en `errors.jsonl`. Het HTML-rapport wordt met de standaard rapportgenerator gemaakt; daarvoor is Node.js nodig, en dat staat al op de machine.
- **Robuustheid:**
  - een harde tijdslimiet per case;
  - backoff met jitter bij rate limits;
  - hervatten per (case, herhaling);
  - controle dat het model dat antwoordde ook het gevraagde model is;
  - mislukte runs apart vastgelegd, nooit als score 0.
- **API-key:** Bas' eigen `ANTHROPIC_API_KEY` als omgevingsvariabele. Die komt nooit in het repo.
- **Beperking:** de harness is niet Claude Desktop. Hij heeft niet de systeemprompt van de app en geen geheugen. We meten het API-pad met dezelfde server en dezelfde tools.

## Budget

- **Eerst een pilot:** een handvol cases × 1 herhaling × 6 combinaties.
- Uit de gemeten tokens volgt een kostenraming voor de volledige meting, met de berekening erbij.
- **Bas beslist** daarna of de volledige meting doorgaat. Die is ongeveer 23 cases × 5 herhalingen × 6 combinaties ≈ 690 runs, plus de jury.

## Planning (3 weken avonden)

- [ ] **Week 1:**
  - Bas: de cases en de verwachte feiten.
  - Claude: de git-tags, het bouwen van de serverversies, en de harness met de tool-use-loop.
  - Sign-off 1 op de evaluatieset.
- [ ] **Week 2:**
  - De jury en de beoordeling.
  - De pilot en sign-off 2 op de beoordeling.
  - De kostenraming; na akkoord de volledige meting.
- [ ] **Week 3:** het HTML-rapport in het repo, de interpretatie van de resultaten, en opruimen.

Past het niet in de tijd, schrap dan scope, geen kwaliteit. Bijvoorbeeld eerst alleen Haiku en Opus, of 3 herhalingen in plaats van 5.

## Presentatie

- Het HTML-rapport komt in het repo.
- **Open punt:** GitHub toont een `.html`-bestand als broncode, niet als webpagina. Om het rapport leesbaar te maken is GitHub Pages nodig, of een link vanuit de README naar een gerenderde versie. Dat beslissen we in week 3.

## Buiten scope (parkeerlijst)

- **Resultatentabel in de README en een write-up of blogpost.** Nu niet gekozen; goede kandidaten direct na slice 2.
- Automatisch bijschaven tegen de evaluatieset (hillclimbing)
- Draaien in CI, want elke run kost geld
- Effort-sweeps en andere modellen
- Claude Desktop zelf automatiseren
- Schrijfacties en de voorraadmutatie-tabel (kandidaat voor slice 3)

## Ontwerpkeuzes

- **Meet de echte server.** De harness start de gebouwde server en praat er via MCP mee. Hij bouwt niets na.
- **Feiten, geen smaak.** Een antwoord is goed als de feiten kloppen, niet als het mooi klinkt.
- **De jury extraheert, code oordeelt.** Het jurymodel doet alleen het mechanische werk; het eindoordeel is deterministisch en controleerbaar.
- **Kosten uit metingen.** Ramingen komen uit de pilot, niet uit een schatting vooraf.
- **Jouw domeinkennis is de maatstaf.** De verwachte feiten en de grensvragen komen van Bas; daar zit de waarde van de meting.

## Startprompt voor Claude Code

```
Lees docs/slice-2.md. We bouwen alleen week 1, het deel van Claude:

1. Zet de git-tags slice1-week2 en slice1-week3 op de commits uit het doc.
2. Maak een script dat beide versies van de server vanuit git bouwt naar een eigen map.
3. Maak evals/Wms.Evals: start een serverversie via MCP-stdio, zet de tools om naar
   Claude API-tooldefinities en draai de tool-use-loop met de Anthropic C# SDK.
4. Schrijf per run results.jsonl, traces/ en errors.jsonl volgens het doc.
5. Lees de cases uit een bestand dat ik zelf vul; maak alleen een voorbeeldcase.

Nog geen jury en geen volledige meting. Stel eerst een plan voor en wacht op mijn
akkoord voordat je code schrijft.
```

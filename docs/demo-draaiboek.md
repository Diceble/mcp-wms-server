# Draaiboek demovideo (2–3 minuten)

## Voorbereiding

- [ ] Publiceer de laatste versie (`dotnet publish src/Wms.McpServer -c Release -o artifacts/mcp`) en herstart Claude Desktop helemaal.
- [ ] **Sluit of verberg de zijbalk met je chats.** Daarin staan titels van andere gesprekken die niet in een publieke video horen.
- [ ] Doe één proefvraag in een wegwerpchat en kies bij de toestemmingsvraag "Allow always", zodat er in de opname geen pop-ups verschijnen.
- [ ] Zoom het venster in tot ongeveer 125%, zodat tabellen leesbaar zijn op een telefoon.
- [ ] Houd [docs/week-3-evaluatie.md](week-3-evaluatie.md) ernaast om te controleren of de antwoorden kloppen.
- [ ] Opnemen kan met de Xbox Game Bar (`Win`+`Alt`+`R`) of met OBS, in 1080p.

De seeddatums zijn relatief aan vandaag. De ordernummers en tekorten zijn dus altijd hetzelfde, maar de datums in beeld schuiven mee met de opnamedag.

## Scènes

| Tijd | In beeld | Wat je zegt (ongeveer) |
|---|---|---|
| 0:00–0:15 | README op GitHub | "Dit is een kleine MCP-server in .NET. Claude kan daarmee in gewone taal vragen beantwoorden over voorraad en orders, rechtstreeks uit een magazijndatabase." |
| 0:15–0:35 | `WmsTools.cs`, kort scrollen | "Claude krijgt geen SQL-toegang, maar vijf smalle tools die alleen lezen. De toolbeschrijvingen zijn het contract: daarmee kiest Claude wat het aanroept. De data is bewust rommelig: cryptische statuscodes, voorraad in kwaliteitscontrole, reserveringen die niet kloppen." |
| 0:35–1:05 | Nieuwe chat: *"Welke producten zitten onder hun bestelniveau?"* | Wijs op het zadel (fysiek genoeg, maar gereserveerd) en de handvatten (de voorraad in QC telt niet mee). "Claude legt niet alleen uit dát iets laag is, maar ook waaróm." |
| 1:05–2:05 | Nieuwe chat: *"Kunnen we order 1042 vandaag volledig verzenden?"* | Klap de toolaanroepen open: eerst `get_order`, dan per regel `get_stock_for_product`. "Hier is geen tool voor; Claude combineert er twee." Laat het antwoord zien: 2 remblokken tekort, 4 in QC, 4 gereserveerd voor 1101, die pas over 10 dagen geleverd moet worden. "Dat laatste kon Claude pas zeggen toen de tool de leverdatum meegaf. Dat was de belangrijkste les van het bijschaven." |
| 2:05–2:35 | Nieuwe chat: *"Welke orders zijn te laat of dreigen te laat te worden, en waarom?"* | Scroll door de lijst. Wijs op de onbekende statuscode `X9`, die Claude niet probeert te raden, en op 1108 en 1109, die dezelfde binnenbanden nodig hebben. |
| 2:35–2:50 | Architectuurschema in de README | "De tools praten met een interface. Voor een ander WMS schrijf ik alleen een tweede implementatie; de tools blijven gelijk. Volgende stap: schrijfacties, zoals voorraad reserveren." |

## Als het misgaat

- **Er verschijnen geen tools:** de laatste regels van `%APPDATA%\Claude\logs\mcp-server-wms.log` laten zien waarom.
- **Claude kiest een omweg of mist iets:** stel de vraag opnieuw in een nieuwe chat. Bewaar het mislukte antwoord voor de evaluatieset; een eerlijk "zo ging het eerst mis" is ook sterk materiaal.

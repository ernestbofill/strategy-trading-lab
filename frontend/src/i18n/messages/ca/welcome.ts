import type { welcome as welcomeEn } from '../en/welcome'

export const welcome: Record<keyof typeof welcomeEn, string> = {
  'welcome.topBar': 'STL // STRATEGY TRADING LAB',
  'welcome.logIn': 'Inicia la sessió',
  'welcome.hero.eyebrow': 'SISTEMA A PUNT // LABORATORI DE REGLES',
  'welcome.hero.title': 'Opera segons regles, no per emocions.',
  'welcome.hero.lead':
    'Un laboratori d\u2019estrat\u00E8gies transparent. Tria plantilles provades, executa simulacions en temps real sense risc i segueix senyals en directe sense connexi\u00F3 al broker.',
  'welcome.hero.tryGuest': 'PROVA COM A CONVIDAT \u203A',
  'welcome.hero.register': 'Crea un compte',
  'welcome.hero.haveAccount': 'Ja tens un compte? Inicia la sessi\u00F3',

  'welcome.features.eyebrow': 'CAPACITATS PRINCIPALS',
  'welcome.features.title': 'Com funciona Strategy Trading Lab',
  'welcome.features.lead':
    'Tres pilars disciplinats per ajudar-te a construir, provar i executar estrat\u00E8gies sistem\u00E0tiques als mercats moderns.',

  // Pillar 1: Strategy builder
  'welcome.builder.tag': '01 // CONSTRUEIX',
  'welcome.builder.badge': 'PLANTILLES I REGLES',
  'welcome.builder.title': 'Constructor d\u2019estrat\u00E8gies',
  'welcome.builder.tagline': 'Defineix les teves regles abans que obri el mercat',
  'welcome.builder.summary':
    'Tria plantilles matem\u00E0tiques provades o seleccions d\u2019IA. Tria les teves accions, defineix indicadors i configura pressupostos per passos.',
  'welcome.builder.point1': 'Plantilles provades amb l\u00F2gica clara: mitjanes m\u00F2bils, retrocessos de descompte i remuntades.',
  'welcome.builder.point2': 'Tu controles les llistes d\u2019accions, la mida dels passos i els l\u00EDmits de pressupost amb valors per defecte provats.',
  'welcome.builder.point3': 'Regles en llenguatge planer: sense caixes negres ni suposicions amagades.',
  'welcome.builder.cta': 'Prova a construir una estrat\u00E8gia com a convidat \u203A',

  // Mock interactive builder widget
  'welcome.builder.mock.templateLabel': 'Tria plantilla',
  'welcome.builder.mock.t1Name': 'L\u00EDders amb descompte',
  'welcome.builder.mock.t1Tagline': 'Compra la caiguda',
  'welcome.builder.mock.t2Name': 'La remuntada',
  'welcome.builder.mock.t2Tagline': 'Accions a l\u2019al\u00E7a',
  'welcome.builder.mock.t3Name': 'Mercats baixistes',
  'welcome.builder.mock.t3Tagline': 'Quan el mercat est\u00E0 d\u2019oferta',
  'welcome.builder.mock.ruleLabel': 'Regles de disparador',
  'welcome.builder.mock.ruleValue': 'SMA 200 + zona de descompte 5%\u201315%',
  'welcome.builder.mock.budgetLabel': 'Pressupost per pas',
  'welcome.builder.mock.budgetValue': '100 \u20AC / pas \u00B7 m\u00E0x. 3 passos',
  'welcome.builder.mock.stocksLabel': 'Accions en seguiment',

  // Pillar 2: Simulation
  'welcome.sim.tag': '02 // SIMULA',
  'welcome.sim.badge': 'SIMULACI\u00D3',
  'welcome.sim.title': 'Simulaci\u00F3 en temps real',
  'welcome.sim.tagline': 'Test cap endavant amb preus reals sense cap risc financer',
  'welcome.sim.summary':
    'Opera en paper sobre preus reals des d\u2019avui mateix. Posa a prova l\u2019execuci\u00F3 i compara rendiments amb el fons indexat S&P 500 i el pitjor any hist\u00F2ric.',
  'welcome.sim.point1': 'Preus reals de tancament actualitzats di\u00E0riament despr\u00E9s del tancament del mercat dels EUA.',
  'welcome.sim.point2': 'Registre autom\u00E0tic d\u2019operacions simulades segons les regles exactes de la teva estrat\u00E8gia.',
  'welcome.sim.point3': 'M\u00E8triques de rendiment honestes al costat del fons indexat i el pitjor any hist\u00F2ric.',
  'welcome.sim.cta': 'Testa en mode simulaci\u00F3 \u203A',

  // Mock interactive simulation widget
  'welcome.sim.mock.mode': 'SIMULACI\u00D3',
  'welcome.sim.mock.returnLabel': 'Rendiment de l\u2019estrat\u00E8gia',
  'welcome.sim.mock.indexLabel': 'Fons indexat S&P 500',
  'welcome.sim.mock.worstLabel': 'Pitjor any',
  'welcome.sim.mock.tabPerf': 'Comparaci\u00F3 de rendiment',
  'welcome.sim.mock.tabTrades': 'Registre d\u2019operacions en paper',
  'welcome.sim.mock.log1': 'Comprat 1 pas \u00B7 AAPL a 178,40 $',
  'welcome.sim.mock.log2': 'Comprat 1 pas \u00B7 MSFT a 412,10 $',
  'welcome.sim.mock.note': 'El seguiment de refer\u00E8ncia utilitza preus de tancament. Els resultats passats no garanteixen els futurs.',

  // Pillar 3: Trading tracker
  'welcome.tracker.tag': '03 // SEGUIMENT',
  'welcome.tracker.badge': 'EXECUCI\u00D3 EN DIRECTE',
  'welcome.tracker.title': 'Seguiment d\u2019operacions',
  'welcome.tracker.tagline': 'Opera al teu broker. 100% privat i no custodi',
  'welcome.tracker.summary':
    'Rep alertes di\u00E0ries quan es compleixin les teves regles. Confirma, ajorna o omet passos en segons. No et demanem mai contrasenyes ni claus de broker.',
  'welcome.tracker.point1': 'Zero sincronitzaci\u00F3 amb el broker: sense contrasenyes, OAuth, claus d\u2019API ni acc\u00E9s als teus diners.',
  'welcome.tracker.point2': 'Alertes de senyals di\u00E0ries que t\u2019indiquen quina regla s\u2019ha complert i quin pas cal executar.',
  'welcome.tracker.point3': 'Registra les operacions amb els preus reals d\u2019execuci\u00F3 del broker o omet senyals amb un motiu r\u00E0pid.',
  'welcome.tracker.cta': 'Explora el flux de seguiment en directe \u203A',

  // Mock interactive tracker widget
  'welcome.tracker.mock.mode': 'EN DIRECTE',
  'welcome.tracker.mock.signalWaiting': 'Senyal esperant',
  'welcome.tracker.mock.signalStock': 'Zscaler (ZS)',
  'welcome.tracker.mock.signalDetail': 'Ha caigut un 8,2% per sota del m\u00E0xim anual \u00B7 Per sobre de SMA 200 \u00B7 Pas 1 de 3',
  'welcome.tracker.mock.prompt': 'Has actuat sobre el senyal?',
  'welcome.tracker.mock.btnDidIt': 'Ho he fet',
  'welcome.tracker.mock.btnSkipped': 'L\u2019he om\u00E8s',
  'welcome.tracker.mock.btnSnooze': 'Recorda-m\u2019ho dem\u00E0',
  'welcome.tracker.mock.statusDidIt': '\u2713 Operaci\u00F3 registrada: pas de 100 \u20AC anotat. Efectiu i rendiment actualitzats.',
  'welcome.tracker.mock.statusSkipped': 'Pas om\u00E8s: motiu registrat. Les regles continuen monitorant.',
  'welcome.tracker.mock.statusSnooze': 'Ajornat: recordatori programat per a la propera sessi\u00F3.',
  'welcome.tracker.mock.privacyCallout': 'STL mai no toca el teu broker. Mantens el control total de les teves operacions.',

  // Toggle & expansion controls
  'welcome.expand': 'Mostra detalls \u25BE',
  'welcome.collapse': 'Amaga detalls \u25B4',

  // Trust & Architecture section
  'welcome.trust.eyebrow': 'PER QU\u00C8 STRATEGY TRADING LAB',
  'welcome.trust.title': 'Dissenyat per a la transpar\u00E8ncia i el control',
  'welcome.trust.card1Title': 'Zero acc\u00E9s al broker',
  'welcome.trust.card1Text':
    'Mai no et demanem credencials de broker ni permisos d\u2019operativa. Els teus diners continuen exclusivament a les teves mans.',
  'welcome.trust.card2Title': 'Honestedat sense concessions',
  'welcome.trust.card2Text':
    'Cada estrat\u00E8gia es mostra al costat d\u2019un fons indexat S&P 500 i del seu pitjor any hist\u00F2ric. Sense falses promeses.',
  'welcome.trust.card3Title': 'Regles sistem\u00E0tiques disciplinades',
  'welcome.trust.card3Text':
    'Regles matem\u00E0tiques definides pr\u00E8viament que eliminen les compres impulsives i les vendes de p\u00E0nic en moments de volatilitat.',

  // Bottom CTA
  'welcome.bottom.title': 'A punt per provar la teva primera estrat\u00E8gia?',
  'welcome.bottom.lead':
    'Explora totes les plantilles, personalitza par\u00E0metres i executa simulacions en paper sense cap comprom\u00EDs. Sense targeta de cr\u00E8dit.',
  'welcome.bottom.tryGuest': 'PROVA COM A CONVIDAT \u203A',
  'welcome.bottom.register': 'Crea un compte',
}

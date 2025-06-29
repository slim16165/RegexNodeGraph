# RegexNodeGraph

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![NuGet](https://img.shields.io/nuget/v/RegexNodeGraph.svg)
![Build Status](https://img.shields.io/github/actions/workflow/status/YourUsername/RegexNodeGraph/build.yml)

**Versione:** 1.2.0 (2025-04-27)

---

## 🚀 Introduzione

**RegexNodeGraph** è una libreria .NET dedicata alla trasformazione e all'analisi di testi attraverso insiemi di regole basate su espressioni regolari. A partire dalla versione **1.2.0** espone un **motore di valutazione riusabile** che permette di calcolare metriche di copertura, throughput e diagnostica in modo indipendente dall'interfaccia utente, rendendo più semplice costruire applicazioni di analisi (es. gestionali finanziari, strumenti di debugging o UI enterprise).

La libreria si compone di due macro aree:

1. **Motore di valutazione regex** – gestisce campioni, compilazione delle espressioni, cronometra ogni esecuzione, calcola telemetria aggregata e fornisce risultati pronti per la UI.
2. **Transformation Graph** – mantiene la topologia delle trasformazioni, utile quando è necessario modellare dipendenze fra regole e costruire visualizzazioni o esportazioni verso Neo4j.

---

## ✨ Novità principali (1.2.0)

- **`RegexEvaluationEngine`** con API sincrone/asincrone (`EvaluateAll`, `EvaluateAllAsync`, `EvaluateRule`, `EvaluateRuleAsync`) e supporto a `CancellationToken`.
- **Caching opzionale** delle regex compilate e riutilizzo di `RegexTransformationRule.RegexFrom` quando disponibile.
- **Metriche evolute**: throughput per match e per valutazioni, conteggio dei campioni distinti, top delle regole più lente, vettore dei campioni coperti.
- **Parallelismo configurabile**: esecuzione su più thread mantenendo l'ordine deterministico dell'output.
- **DTO immutabili** (`RegexSample`, `RuleCoverageResult`, `SampleMatchResult`, `RegexEvaluationReport`, `RegexEvaluationMetrics`) pronti per il binding nelle applicazioni client.
- **Helper condivisi** come `RegexTransformationRuleExtensions.GetDisplayName` per visualizzare pattern complessi in maniera compatta.

---

## 📦 Installazione da NuGet

```powershell
PM> Install-Package RegexNodeGraph
```

Oppure tramite CLI:

```bash
dotnet add package RegexNodeGraph
```

La distribuzione NuGet include il file `README.nupkg.md` con un estratto della documentazione rapida e i metadati completi (autore, repository, note di rilascio).

---

## 🧱 Architettura del pacchetto

```
RegexNodeGraph/
├── RegexGraph/                   # progetto principale (libreria)
│   ├── Evaluation/               # motore di valutazione regex e relativi DTO
│   ├── Graph/                    # gestione del grafo di trasformazione
│   ├── RegexRules/               # definizione delle regole e factory
│   └── ...                       # altri componenti condivisi
├── RegexGraph.Test/              # test automatici
└── readme.md                     # questo file
```

---

## 🧪 Quickstart – Motore di valutazione

### 1. Preparare le regole

```csharp
using RegexNodeGraph.RegexRules;

var rules = new[]
{
    new RegexTransformationRule(@"(?i)unicredit", "UNICREDIT", "Banca"),
    new RegexTransformationRule(@"(?i)bancomat", "BANCOMAT", "Prelievo")
};
```

### 2. Preparare i campioni

```csharp
using RegexNodeGraph.Evaluation;

var samples = new[]
{
    new RegexSample("Pagamento POS Unicredit Milano", identifier: "txn-001"),
    new RegexSample("Prelievo bancomat Roma", identifier: "txn-002")
};
```

### 3. Creare la richiesta

```csharp
var request = new RegexEvaluationRequest(
    rules,
    samples,
    optionsOverride: RegexOptions.CultureInvariant | RegexOptions.Singleline,
    matchTimeout: TimeSpan.FromMilliseconds(200),
    recompileExpressions: true,
    useRegexCache: true,
    degreeOfParallelism: Environment.ProcessorCount
);
```

### 4. Eseguire il motore

```csharp
IRegexEvaluationEngine engine = new RegexEvaluationEngine();
var report = engine.EvaluateAll(request);
```

### 5. Consumare i risultati

```csharp
foreach (var ruleResult in report.RuleResults)
{
    Console.WriteLine($"[{ruleResult.Coverage.RuleIndex}] {ruleResult.Coverage.Rule.Description}");
    Console.WriteLine($"  Match: {ruleResult.Coverage.MatchCount}/{ruleResult.Coverage.TotalSamples}");
    Console.WriteLine($"  Eval throughput: {ruleResult.Coverage.EvalThroughput:F2} sample/s");

    foreach (var sampleResult in ruleResult.SampleResults)
    {
        Console.WriteLine($"    - {sampleResult.Sample.Identifier}: {(sampleResult.IsMatch ? "MATCH" : "MISS")}");
    }
}

var metrics = RegexEvaluationMetrics.BuildSummary(report);
Console.WriteLine($"Durata totale: {metrics.TotalDuration}");
Console.WriteLine($"Campioni distinti: {metrics.DistinctSamplesEvaluated}");
Console.WriteLine($"Throughput complessivo: {metrics.OverallEvalThroughput:F2} sample/s");
```

Il vettore `report.CoveredSamples` consente di individuare rapidamente gli indici non coperti tramite `report.GetUnmatchedSampleIndices()`.

---

## 🧭 Quickstart – Transformation Graph

L'uso del grafo rimane invariato rispetto alle versioni precedenti. Dopo aver valutato le regole è possibile proiettare i risultati sulla struttura a nodi/archi per visualizzazioni, esportazioni o analisi avanzate.

```csharp
var graph = new TransformationGraph();
// Popola il grafo utilizzando le tue regole e i tuoi nodi dominio...
```

---

## 📊 Telemetria e ranking

- `RegexEvaluationMetrics.BuildSummary(report)` produce un riepilogo aggregato con throughput, errori e top delle regole più lente.
- `metrics.GetSlowestRules(top: 5)` restituisce le regole ordinate per durata decrescente.
- `RuleCoverageResult.MatchThroughput` e `EvalThroughput` aiutano a stimare la densità dei match rispetto ai campioni totali.

Tutte le entità sono `sealed` (o `record`) e immutabili per favorire il binding in UI multi-thread e minimizzare gli errori di concorrenza.

---

## 🧰 Utilità aggiuntive

- `RegexTransformationRuleExtensions.GetDisplayName(maxLength)` abbrevia pattern e descrizioni troppo lunghi mantenendo un'indicazione leggibile.
- `RegexEvaluationEngine.EvaluateAllAsync` delega internamente alla versione sincrona garantendo compatibilità con `CancellationToken`.
- Supporto per `RegexMatchTimeoutException` con telemetria puntuale delle eccezioni.

---

## 🧑‍💻 Sviluppo locale

1. **Clona il repository** e apri `MyRegexTester.sln` con Visual Studio 2022 o successivo.
2. Compila in modalità `Release` per generare l'assembly destinato a NuGet.
3. Esegui i test (`RegexGraph.Test`).
4. Genera il pacchetto NuGet con `dotnet pack RegexGraph/RegexNodeGraph.csproj -c Release`.

> **Nota:** in ambienti CI senza .NET SDK preinstallato, installare precedentemente l'SDK `net472`.

---

## 🤝 Contributi

Contributi, issue e feature request sono benvenuti. Apri una PR descrivendo la modifica e includendo test automatici ove possibile.

---

## 📄 Licenza

Distribuito con licenza [MIT](LICENSE).

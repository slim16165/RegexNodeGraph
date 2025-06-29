# RegexNodeGraph

RegexNodeGraph è una libreria .NET per orchestrare regole di trasformazione testuale basate su espressioni regolari. Include un motore di valutazione riusabile che si occupa di compilare le regex, misurare le prestazioni e produrre metriche di copertura pronte per la UI.

## Caratteristiche

- Motore `RegexEvaluationEngine` con API sincrone e asincrone, supporto a `CancellationToken` e parallelismo configurabile.
- DTO immutabili (`RegexSample`, `SampleMatchResult`, `RuleCoverageResult`, `RegexEvaluationReport`, `RegexEvaluationMetrics`) facili da serializzare e bindare.
- Caching opzionale delle regex compilate e rispetto dei timeout configurati.
- Calcolo della telemetria aggregata (throughput, regole più lente, errori, campioni distinti) centralizzato nella libreria.
- Helper come `RegexTransformationRuleExtensions.GetDisplayName` per pattern lunghi.

## Esempio rapido

```csharp
var rules = new[]
{
    new RegexTransformationRule(@"(?i)unicredit", "UNICREDIT", "Banca")
};

var samples = new[]
{
    new RegexSample("Pagamento POS Unicredit Milano", identifier: "txn-001")
};

var request = new RegexEvaluationRequest(rules, samples, useRegexCache: true);
var engine = new RegexEvaluationEngine();
var report = engine.EvaluateAll(request);

var metrics = RegexEvaluationMetrics.BuildSummary(report);
```

Per esempi più estesi consulta il repository GitHub.

using System.Collections.Generic;
using System.Linq;
using RegexNodeGraph.Graph.Processing;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Engine;

/// <summary>
/// Motore che applica una lista di ITransformationRule ad una serie di Description.
/// NON conosce il grafo; restituisce solo i TransformationRecord.
/// </summary>
public sealed class TransformationEngine : ITransformationEngine
{
    public IEnumerable<TransformationRecord> Run(
        IEnumerable<Description> inputs,
        IEnumerable<TransformationRuleBase> rules)
    {
        var stack = new Stack<(Description desc, int ruleIndex)>();

        // avviamo la cascata: ogni input parte dalla prima regola (index 0)
        foreach (var d in inputs)
            stack.Push((d, 0));

        var ruleArray = rules.ToArray();

        while (stack.Count > 0)
        {
            var (current, idx) = stack.Pop();
            if (idx >= ruleArray.Length)
                continue;                         // nessuna regola restante

            var rule = ruleArray[idx];
            var (dbg, resultDesc) = rule.Apply(current);  // modifica in-place

            // creiamo comunque un record (anche se nessuna modifica)
            yield return new TransformationRecord(current, resultDesc, rule, dbg);

            // se la regola prevede “EsciInCasoDiMatch” ed ha matchato, stop
            if (dbg.IsMatch && rule.EsciInCasoDiMatch)
                continue;

            // se c'è un’altra regola, pushiamo lo stesso Description (modificato) con idx+1
            stack.Push((resultDesc, idx + 1));
        }
    }
}
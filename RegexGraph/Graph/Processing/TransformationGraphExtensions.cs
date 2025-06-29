using System;
using System.Collections.Generic;
using System.Linq;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.Processing;

public static class TransformationGraphExtensions
{
    /// <summary>
    /// Restituisce la sequenza ordinata di <see cref="DebugStep"/>
    /// che descrive tutte le trasformazioni applicate ad
    /// <paramref name="originalDescription"/>.
    /// </summary>
    public static List<DebugStep> GetDebugTrail(
        this TransformationGraph graph,
        string originalDescription)
    {
        var steps = new List<DebugStep>();

        if (graph == null || originalDescription == null)
            return steps;

        // 1) nodo di dettaglio di partenza
        var current = graph.Nodes
            .OfType<DetailedTransactionNode>()
            .FirstOrDefault(n =>
                string.Equals(n.Description.OriginalDescription,
                              originalDescription,
                              StringComparison.OrdinalIgnoreCase));

        if (current == null)
            return steps;   // nessuna traccia

        int idx = 1;
        while (true)
        {
            // 2) primo arco uscente fra i dettagli
            var edge = graph.Edges
                            .OfType<DetailTransformationEdge>()
                            .FirstOrDefault(e => e.SourceNode == current);
            if (edge == null) break;

            // 3) meta-info
            var meta = edge.Rule       as IRegexRuleMetadata;
            var dbg  = edge.DebugData;

            string? category = null;
            if (edge.Rule is RegexTransformationRule regexRule)
                category = regexRule.Categories?.FirstOrDefault();

            steps.Add(new DebugStep(
                $"{idx++}. {(meta?.Description ?? meta?.From ?? "<rule>")}",
                meta?.From ?? string.Empty,
                dbg?.Output ?? current.Description.CurrentDescription,
                category));

            // 4) prosegui al nodo target se esiste
            if (edge.TargetNode is not DetailedTransactionNode next)
                break;

            current = next;
        }

        return steps;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.Processing
{
    /// <summary>
    /// Shim che restituisce la vecchia API
    /// <c>TransformationGraph.UpdateOriginalItemsWithFinalCategory(...)</c>
    /// senza re-introdurre tutta la logica monolitica.
    /// </summary>
    public partial class TransformationGraph   // il file “nuovo” deve essere dichiarato partial
    {
        public static string UpdateOriginalItemsWithFinalCategory(
            string originalDescription,
            TransformationGraph graph)
            => graph.ResolveFinalDescription(originalDescription);

        /*********  — implementation helper —  *********/
        private string ResolveFinalDescription(string original)
        {
            var start = Nodes
                .OfType<DetailedTransactionNode>()
                .FirstOrDefault(n =>
                    n.Description.OriginalDescription.Equals(original,
                        StringComparison.OrdinalIgnoreCase));

            if (start is null)
                return original;          // no match → restituisco l’input

            var visited = new HashSet<int>();
            var current = start;

            while (true)
            {
                if (!visited.Add(current.Id))          // ciclo
                    break;

                // primo arco di trasformazione uscente
                var edge = Edges
                    .OfType<DetailTransformationEdge>()
                    .FirstOrDefault(e => e.SourceNode == current);

                if (edge?.TargetNode is not DetailedTransactionNode next)
                    break;

                current = next;

                // se la regola prevede “EsciInCasoDiMatch”, ci fermiamo qui
                if (edge.Rule is RegexTransformationRule r &&
                    (r.ConfigOptions & ConfigOptions.EsciInCasoDiMatch) ==
                    ConfigOptions.EsciInCasoDiMatch)
                    break;
            }

            return current.Description.CurrentDescription;
        }
    }
}

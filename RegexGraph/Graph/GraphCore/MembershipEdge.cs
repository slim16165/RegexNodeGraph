using System;
namespace RegexNodeGraph.Graph.GraphCore;

public class MembershipEdge : GraphEdge
{
    // Rappresenta l'appartenenza di un nodo di dettaglio a un nodo aggregato
    // Puoi aggiungere proprietà se necessario

    public override bool Equals(object obj)
        => obj is MembershipEdge m
           && m.SourceNode.Id == SourceNode.Id
           && m.TargetNode.Id == TargetNode.Id;

    public override int GetHashCode()
    {
        unchecked
        {
            // 397 è un numero primo che aiuta a distribuire meglio gli hash
            return (SourceNode.Id * 397) ^ TargetNode.Id;
        }
    }
}
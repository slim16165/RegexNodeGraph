namespace RegexNodeGraph.Runtime.Graph.Model;

public class Description
{
    public string OriginalDescription { get; set; }
    public string CurrentDescription { get; set; }
    public string Category { get; set; }
    public bool IsCategorized { get; set; } = false;
    public bool HasChangedNow { get; set; } = false; // Indica se la descrizione è cambiata nell'ultimo step
    public bool WasEverModified { get; set; } = false; // Indica se la descrizione è mai stata modificata

    public Description(string description)
    {
        OriginalDescription = description;
        CurrentDescription = description;
        Category = null;
    }

    // Metodo per aggiornare la descrizione mantenendo traccia dello stato precedente
    public void UpdateDescription(string newDescription)
    {
        if (newDescription != CurrentDescription)
        {
            CurrentDescription = newDescription;
            HasChangedNow = true;
            WasEverModified = true;
        }
        else
        {
            HasChangedNow = false;
        }
    }
}
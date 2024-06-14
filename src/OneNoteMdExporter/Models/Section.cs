namespace alxnbl.OneNoteMdExporter.Models
{
    public class Section(Node parent) : Node(parent)
    {
        public bool IsSectionGroup { get; set; }
    }
}

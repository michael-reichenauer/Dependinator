namespace Dependinator.UI.Modeling.Models;

// How a node is shown, set by the user from the node menu. Ordered by strength: a stronger
// state takes away more of the diagram. Dimmed nodes and their lines are drawn faintly; hidden
// nodes and their lines are left out (View › Reveal Hidden Nodes draws them faintly for a while).
enum NodeVisibility
{
    Visible,
    Dimmed,
    Hidden,
}

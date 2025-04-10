using Dependinator.Models;

namespace Dependinator.Diagrams.Dependencies;


// internal class Tree
// {
//     TreeItem rootItem;

//     public Tree(DependenciesService service, Node root)
//     {
//         Service = service;
//         rootItem = TreeItem.CreateTreeItem(this, null, root);
//         TreeItems.Add(rootItem);
//     }


//     public DependenciesService Service { get; }
//     public List<TreeItem> TreeItems { get; } = [];


//     public void EmptyTo(Node root)
//     {
//         TreeItems.Clear();
//         rootItem = TreeItem.CreateTreeItem(this, null, root);
//         TreeItems.Add(rootItem);
//     }


//     public void SetSelectedItem(TreeItem item)
//     {
//         Log.Debug($"SetSelectedItem: {item.NodeId}");
//         item.Selected = true;
//         Service.SetSelected(item);
//     }

//     public TreeItem AddNode(Node node)
//     {
//         // First add Ancestor items, so the node can be added to its parent item
//         var parentItem = AddAncestors(node);

//         var item = parentItem.AddChildNode(node);
//         item.ShowTreeItem();
//         return item;
//     }


//     TreeItem AddAncestors(Node node)
//     {
//         // Start from root, but skip root, since it is already added by default
//         var ancestors = node.Ancestors().Reverse().Skip(1);

//         var ancestorItem = rootItem;
//         foreach (var ancestor in ancestors)
//         {   // Add ancestor item if not already added
//             var item = ancestorItem.ChildItems.FirstOrDefault(n => n.NodeId == ancestor.Id);
//             if (item == null)
//             {
//                 item = ancestorItem.AddChildNode(ancestor);
//             }
//             ;

//             ancestorItem = item!;
//         }

//         return ancestorItem!;
//     }
// }

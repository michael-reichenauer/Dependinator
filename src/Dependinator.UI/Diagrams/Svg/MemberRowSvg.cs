using System.Globalization;
using System.Web;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using static Dependinator.UI.Diagrams.Svg.SvgText;

// A member row (a field, property, method, …) in an open type's card: icon, name, then its
// signature and comment on the same line, cut to the row's width.
namespace Dependinator.UI.Diagrams.Svg;

static class MemberRowSvg
{
    const double MemberTextGap = 4;
    const double MemberHorizontalPadding = 4;

    const double MemberSignatureGap = 3; // Before the comment
    const double MemberDescriptionScale = 0.85;

    public static string GetMemberNodeSvg(
        Node node,
        Rect nodeCanvasRect,
        double parentZoom,
        bool isEditing = false,
        NodeProblems? problems = null
    )
    {
        var textZoom = TextZoom(parentZoom);
        var fontSize = FontSize * textZoom;
        var member = CalculateMemberNodeLayout(node, nodeCanvasRect, textZoom, fontSize);
        var rowEnd = nodeCanvasRect.X + nodeCanvasRect.Width;
        // A private row is muted, with a lock on its icon.
        var isPrivate = node.IsPrivate ?? false;

        var layout = new NodeSvg.LeafNodeLayout(
            IconRect: member.Icon,
            TextPos: member.Text,
            TextClass: isPrivate ? "memberName memberPrivate" : "memberName",
            TextBaseline: "middle",
            FontSize: fontSize,
            DescriptionClass: "memberDescription",
            DescriptionY: member.Text.Y,
            DescriptionMaxLines: 0, // On the row itself, see MemberDetailsSvg
            // The whole row hovers and selects, not only its name
            Bounds: member.Bounds with
            {
                Width = Math.Max(member.Bounds.Width, rowEnd - member.Bounds.X),
            },
            MarkerPos: new Pos(member.Text.X + EstimateTextWidth(node.DisplayName, fontSize), member.Text.Y),
            TextSuffix: MemberDetailsSvg(node, member.Text, rowEnd, fontSize, textZoom),
            TextAttributes: $"data-name=\"{node.HtmlDisplayName}\"",
            IsPrivateBadge: isPrivate
        );

        return NodeSvg.BuildLeafNodeSvg(node, layout, DescriptionFontSize * textZoom, isEditing)
            + BadgeSvg.Problem(problems, member.Bounds, textZoom);
    }

    // A member row's signature and comment after its name, as tspans of the name's text so they
    // follow it exactly, cut to the row's width: "(string query): Task  Finds the …". Text stops
    // growing at deep zoom while the row keeps growing, so zooming in reveals more of the comment.
    // The name alone is the row's label (data-name; e2e tests and the search match it exactly).
    static string MemberDetailsSvg(Node node, Pos namePos, double rowEnd, double fontSize, double textZoom)
    {
        var signature = node.Signature ?? "";
        var description = GetDescriptionLines(node.Description, int.MaxValue, 1).FirstOrDefault() ?? "";
        if (signature.Length == 0 && description.Length == 0)
            return "";

        // The signature follows the name directly, UML style ("Url: string", "Start(int a)").
        var gap = MemberSignatureGap * textZoom;
        var available = rowEnd - (namePos.X + EstimateTextWidth(node.DisplayName, fontSize));
        var charWidth = fontSize * AverageCharWidthFactor;
        var svg = new System.Text.StringBuilder();
        var signatureText = ClipRow(signature, (int)(available / charWidth));
        if (signatureText.Length > 0)
        {
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<tspan class="memberSignature">{HttpUtility.HtmlEncode(signatureText)}</tspan>"""
                )
            );
            available -= EstimateTextWidth(signatureText, fontSize) + 2 * gap;
        }
        if (signatureText.Length < signature.Length)
            return svg.ToString();

        var descriptionFontSize = fontSize * MemberDescriptionScale;
        var descriptionText = ClipRow(description, (int)(available / (descriptionFontSize * AverageCharWidthFactor)));
        if (descriptionText.Length > 0)
        {
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<tspan class="memberDescription" dx="{2 * gap:0.##}" font-size="{descriptionFontSize:0.##}px">{HttpUtility.HtmlEncode(descriptionText)}</tspan>"""
                )
            );
        }
        return svg.ToString();
    }

    static MemberNodeLayout CalculateMemberNodeLayout(Node node, Rect nodeCanvasRect, double textZoom, double fontSize)
    {
        var iconSize = fontSize;
        var centerY = nodeCanvasRect.Y + nodeCanvasRect.Height / 2;

        var iconX = nodeCanvasRect.X;
        var iconY = centerY - iconSize / 2;

        var gap = MemberTextGap * textZoom;
        var padding = MemberHorizontalPadding * textZoom;
        var textWidth = Math.Max(fontSize, EstimateTextWidth(node.DisplayName, fontSize));

        var layoutWidth = iconSize + gap + textWidth + padding;
        var layoutHeight = Math.Max(iconSize, fontSize);
        var layoutX = iconX;
        var layoutY = centerY - layoutHeight / 2;

        var textX = iconX + iconSize + gap;
        var textY = centerY;

        var layoutRect = new Rect(layoutX, layoutY, layoutWidth, layoutHeight);
        var iconRect = new Rect(iconX, iconY, iconSize, iconSize);
        var textPos = new Pos(textX, textY);

        return new MemberNodeLayout(layoutRect, iconRect, textPos);
    }

    readonly record struct MemberNodeLayout(Rect Bounds, Rect Icon, Pos Text);
}

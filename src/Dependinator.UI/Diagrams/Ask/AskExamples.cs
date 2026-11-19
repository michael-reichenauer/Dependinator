namespace Dependinator.UI.Diagrams.Ask;

// Example questions under one heading of the Ask panel: the first ones are shown at once, the
// rest behind "More examples".
record AskExampleGroup(string Title, IReadOnlyList<string> Top, IReadOnlyList<string> More);

// The example questions the Ask panel offers, which depend on what the question is about: the
// whole model, one selected node, or several. A click fills in the question, to send or edit.
static class AskExamples
{
    public static IReadOnlyList<AskExampleGroup> For(int nodeCount) =>
        nodeCount switch
        {
            0 => Model,
            1 => OneNode,
            _ => SeveralNodes,
        };

    static readonly IReadOnlyList<AskExampleGroup> Model =
    [
        new(
            "Understand",
            [
                "What are the main parts of this codebase, and how do they depend on each other?",
                "Which parts are the most central, used by the most others?",
            ],
            ["Where does the program start, and what happens first?", "Where should I start reading to understand it?"]
        ),
        new(
            "Change",
            ["What would be affected if I changed the most used type?"],
            ["Which parts could change without affecting much else?"]
        ),
        new(
            "Improve",
            ["Which dependencies look wrong for this architecture?"],
            [
                "Which types have grown too large and should be split?",
                "Which cycle would be the most valuable to break, and how?",
                "What architecture rules would protect this structure?",
            ]
        ),
    ];

    static readonly IReadOnlyList<AskExampleGroup> OneNode =
    [
        new(
            "Understand",
            ["What is this and why is it here?", "Who uses it, and for what?"],
            ["What does it depend on, and why?", "How does it work, step by step?"]
        ),
        new(
            "Change",
            ["What would be affected if I changed it?"],
            ["What should I know before I change it?", "Could it be removed, and what would that take?"]
        ),
        new(
            "Improve",
            ["Is it doing too much? Should it be split, and how?", "Does it depend on things it should not?"],
            [
                "Is it in the right place, or does it belong somewhere else?",
                "Is its name right for what it does?",
                "How could what uses it be made to depend on it less?",
            ]
        ),
    ];

    static readonly IReadOnlyList<AskExampleGroup> SeveralNodes =
    [
        new(
            "Understand",
            ["How do these work together?", "Why do these depend on each other?"],
            ["What do these have in common, and how do they differ?"]
        ),
        new("Change", ["What would be affected if I changed these?"], ["Which other parts use all of these?"]),
        new(
            "Improve",
            [
                "Is the responsibility split between these clear?",
                "Could the dependencies between these be removed, and how?",
            ],
            ["Should these be merged, or moved together?", "Is something missing between these, such as an interface?"]
        ),
    ];
}

import * as ts from "typescript";

// A member row's signature as it is shown after the member's name, the way the other parsers
// give it: "(question: string): Promise<Answer>" for a function or method, ": number" for a
// variable or property. Types are the declared ones, or else what the compiler infers.

const MaxTypeLength = 50;

export function functionSignature(declaration: ts.SignatureDeclaration, checker: ts.TypeChecker): string {
    const parameters = declaration.parameters.map(p => parameterText(p, checker)).join(", ");
    if (ts.isConstructorDeclaration(declaration)) {
        return `(${parameters})`;
    }
    const returnType = declaration.type ? typeNodeText(declaration.type) : inferredReturnType(declaration, checker);
    return returnType ? `(${parameters}): ${returnType}` : `(${parameters})`;
}

export function variableSignature(
    declaration: ts.VariableDeclaration | ts.PropertyDeclaration | ts.PropertySignature | ts.ParameterDeclaration,
    checker: ts.TypeChecker
): string {
    const optional = "questionToken" in declaration && declaration.questionToken ? "?" : "";
    const type = declaration.type ? typeNodeText(declaration.type) : inferredType(declaration.name, checker);
    return type ? `${optional}: ${type}` : optional;
}

export function accessorSignature(declaration: ts.AccessorDeclaration, checker: ts.TypeChecker): string {
    const typeNode = ts.isGetAccessor(declaration) ? declaration.type : declaration.parameters[0]?.type;
    const type = typeNode ? typeNodeText(typeNode) : inferredType(declaration.name, checker);
    return type ? `: ${type}` : "";
}

function parameterText(parameter: ts.ParameterDeclaration, checker: ts.TypeChecker): string {
    const rest = parameter.dotDotDotToken ? "..." : "";
    // A destructured parameter is shown by its pattern's braces only: "{…}"
    const name = ts.isIdentifier(parameter.name) ? parameter.name.text : ts.isObjectBindingPattern(parameter.name) ? "{…}" : "[…]";
    const optional = parameter.questionToken || parameter.initializer ? "?" : "";
    const type = parameter.type ? typeNodeText(parameter.type) : inferredType(parameter.name, checker);
    return type ? `${rest}${name}${optional}: ${type}` : `${rest}${name}${optional}`;
}

function inferredReturnType(declaration: ts.SignatureDeclaration, checker: ts.TypeChecker): string {
    try {
        const signature = checker.getSignatureFromDeclaration(declaration);
        const text = signature ? checker.typeToString(checker.getReturnTypeOfSignature(signature)) : "";
        return text === "any" ? "" : shorten(text);
    } catch {
        return "";
    }
}

// The inferred type, unless the compiler knows nothing ("any", as for untyped JavaScript).
function inferredType(name: ts.Node, checker: ts.TypeChecker): string {
    try {
        // The type of a literal is its kind: string for "idle", not "idle"
        const text = checker.typeToString(checker.getBaseTypeOfLiteralType(checker.getTypeAtLocation(name)));
        return text === "any" ? "" : shorten(text);
    } catch {
        return "";
    }
}

function typeNodeText(type: ts.TypeNode): string {
    return shorten(type.getText());
}

function shorten(text: string): string {
    const oneLine = text.replace(/\s+/g, " ").trim();
    return oneLine.length > MaxTypeLength ? `${oneLine.slice(0, MaxTypeLength - 1)}…` : oneLine;
}

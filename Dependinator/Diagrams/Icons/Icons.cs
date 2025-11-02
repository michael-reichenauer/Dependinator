namespace Dependinator.Diagrams.Icons;

static class Icon
{
    public const string DependenciesIcon = MudBlazor.Icons.Material.Outlined.Polyline;
    public const string ReferencesIcon =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g transform=\"rotate(180,12,12)\"><path d=\"M15,16v1.26l-6-3v-3.17L11.7,8H16V2h-6v4.9L7.3,10H3v6h5l7,3.5V22h6v-6H15z M12,4h2v2h-2V4z M7,14H5v-2h2V14z M19,20h-2v-2 h2V20z\"/></g>";
    public const string DirectConnection =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g><rect x=\"3.5\" y=\"9.5\" width=\"5\" height=\"5\" rx=\"1\" fill=\"currentColor\"/><rect x=\"15.5\" y=\"9.5\" width=\"5\" height=\"5\" rx=\"1\" fill=\"currentColor\"/><rect x=\"8\" y=\"11.25\" width=\"8\" height=\"1.5\" rx=\"0.75\" fill=\"currentColor\"/></g>";
    public const string LineSourceIcon =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g><circle cx=\"6\" cy=\"12\" r=\"2.4\" fill=\"currentColor\"/><rect x=\"8\" y=\"11\" width=\"12\" height=\"2\" rx=\"1\" fill=\"currentColor\"/></g>";
    public const string LineTargetIcon =
        "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g><rect x=\"4\" y=\"11\" width=\"12\" height=\"2\" rx=\"1\" fill=\"currentColor\"/><polygon points=\"16,8.5 22,12 16,15.5\" fill=\"currentColor\"/></g>";

    static readonly string ExternalsIcon = """
        <svg id="ExternalsIcon" version="1.1" baseProfile="tiny" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
            x="0px" y="0px" width="24px" height="24px" viewBox="0 0 24 24" overflow="visible" xml:space="preserve">
            <g >
            <rect y="0" fill="none" width="24" height="24"/>
            <g transform="translate(2.000000, 2.000000)">
                <polygon id="Fill-1" fill-rule="evenodd" fill="#3367D6" points="3.4,10.7 7.4,10.7 7.4,9.3 3.4,9.3 		"/>
                <polygon id="Fill-2" fill-rule="evenodd" fill="#3367D6" points="2.6,18.4 9.2,11.8 8.3,10.9 1.7,17.5 		"/>
                <polygon id="Fill-3" fill-rule="evenodd" fill="#3367D6" points="2,2.9 8.6,9.5 9.5,8.6 2.9,2 		"/>
                <polygon id="Fill-4" fill-rule="evenodd" fill="#3367D6" points="12.6,10.7 16.6,10.7 16.6,9.3 12.6,9.3 		"/>
                <polygon id="Fill-5" fill-rule="evenodd" fill="#3367D6" points="10.8,11.8 17.1,18 18,17.1 11.8,10.8 		"/>
                <polygon id="Fill-6" fill-rule="evenodd" fill="#3367D6" points="12.5,8.5 18.3,2.6 17.4,1.7 11.5,7.5 		"/>
                <polygon id="Fill-7" fill-rule="evenodd" fill="#5C85DE" points="-0.5,20.5 4,20.5 4,16 -0.5,16 		"/>
                <polygon id="Fill-8" fill-rule="evenodd" fill="#5C85DE" points="16,20.5 20.5,20.5 20.5,16 16,16 		"/>
                <polygon id="Fill-9" fill-rule="evenodd" fill="#5C85DE" points="-0.5,4 4,4 4,-0.5 -0.5,-0.5 		"/>
                <polygon id="Fill-10" fill-rule="evenodd" fill="#5C85DE" points="16,4 20.5,4 20.5,-0.5 16,-0.5 		"/>
                <polygon id="Fill-11" fill-rule="evenodd" fill="#85A4E6" points="6.2,13.8 13.8,13.8 13.8,6.2 6.2,6.2 		"/>
                <polygon id="Fill-12" fill-rule="evenodd" fill="#5C85DE" points="-0.5,12.2 4,12.2 4,7.8 -0.5,7.8 		"/>
                <polygon id="Fill-13" fill-rule="evenodd" fill="#5C85DE" points="16,12.2 20.5,12.2 20.5,7.8 16,7.8 		"/>
            </g>
        </g>
        </svg>
        """;

    static readonly string PrivateIcon = """
        <svg  id="PrivateIcon" version="1.1" baseProfile="tiny" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
            x="0px" y="0px" width="24px" height="24px" viewBox="0 0 24 24" overflow="visible" xml:space="preserve">
        <g >
            <rect y="0" fill="none" width="24" height="24"/>
            <g transform="translate(3.000000, 4.000000)">
                <path fill="#3367D6" d="M9.9,13.6c0.3,1.2,1.4,2.1,2.7,2.1c1.6,0,2.8-1.3,2.8-2.8s-1.3-2.8-2.8-2.8
                    c-1.3,0-2.4,0.9-2.7,2.1H2.6v1.5H9.9z M18.8,17.8H-0.8V7.2h5.2L6,8.8h3h3l1.5-1.5h5.2V17.8z M4.9,13.6H3.4v1.5h1.5V13.6z
                    M7.1,13.6H5.6v1.5h1.5V13.6z M11.2,12.9c0,0.8,0.6,1.4,1.4,1.4s1.4-0.6,1.4-1.4s-0.6-1.4-1.4-1.4S11.2,12.1,11.2,12.9z"/>
                <polygon fill="#5C85DE" points="8.2,8 6.4,8 5.1,6.5 0.8,6.5 0.8,3.5 8.2,3.5 		"/>
                <polygon id="Shape-Copy-2" fill="#85A4E6" points="12.8,6.8 11.5,8 9,8 9,2.8 3,2.8 3,1.2 12.8,1.2 		"/>
                <polygon id="Shape-Copy-3" fill="#85A4E6" points="17.2,6.5 13.5,6.5 13.5,0.5 6.8,0.5 6.8,-1 17.2,-1 		"/>
            </g>
        </g>
        </svg>
        """;

    static readonly string FilesIcon = """
        <svg id="FilesIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><linearGradient id="ee9e6206-3cd1-473c-8654-89b04e62a5fd" x1="11.246" y1="18" x2="11.246" y2="3.281" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#0078d4" /><stop offset="0.817" stop-color="#5ea0ef" /></linearGradient></defs><title>MsPortalFx.base.images-23</title><g id="ff5c5c80-dfbf-4584-91f1-56ebc112ac71"><g><rect x="1.078" y="0.334" width="10.11" height="11.685" fill="#5ea0ef" /><path d="M11.188,12.353H1.078a.334.334,0,0,1-.334-.334V.334A.333.333,0,0,1,1.078,0h10.11a.333.333,0,0,1,.333.334V12.019A.334.334,0,0,1,11.188,12.353Zm-9.777-.668h9.443V.668H1.411Z" fill="#0078d4" /><rect x="3.01" y="1.918" width="10.11" height="11.685" fill="#83b9f9" /><path d="M13.12,13.937H3.01a.335.335,0,0,1-.334-.334V1.918a.335.335,0,0,1,.334-.334H13.12a.334.334,0,0,1,.334.334V13.6A.334.334,0,0,1,13.12,13.937Zm-9.776-.668h9.443V2.252H3.344Z" fill="#0078d4" /><g><path d="M12.082,3.39H5.818a.5.5,0,0,0-.495.5V17.4a.5.5,0,0,0,.495.5H16.674a.5.5,0,0,0,.494-.5V8.454a.5.5,0,0,0-.494-.5h-3.6a.5.5,0,0,1-.495-.495V3.886A.5.5,0,0,0,12.082,3.39Z" fill="#fff" /><path d="M11.853,4.023V7.416A1.246,1.246,0,0,0,13.1,8.661h3.418v8.6H5.977V4.023h5.876m.24-.742H5.737a.5.5,0,0,0-.5.5V17.5a.5.5,0,0,0,.5.5H16.754a.5.5,0,0,0,.5-.5V8.421a.5.5,0,0,0-.5-.5H13.1a.5.5,0,0,1-.5-.5V3.783a.5.5,0,0,0-.5-.5Z" fill="url(#ee9e6206-3cd1-473c-8654-89b04e62a5fd)" /><path d="M17.064,8.019,12.422,3.39V7.162a.852.852,0,0,0,.846.857Z" fill="#0078d4" /></g><rect x="7.157" y="9.982" width="8.177" height="1.097" rx="0.493" fill="#5ea0ef" /><rect x="7.157" y="11.948" width="8.177" height="1.097" rx="0.493" fill="#5ea0ef" /><rect x="7.157" y="13.913" width="5.154" height="1.097" rx="0.493" fill="#5ea0ef" /></g></g></svg>
        """;

    static readonly string TypeIcon = """
        <svg  id="TypeIcon" xmlns="http://www.w3.org/2000/svg" width="24px" height="24px" viewBox="0 0 24 24"><defs><style>.cls-1{fill:#669df6;}.cls-2{fill:#4285f4;}.cls-3{fill:#aecbfa;}</style></defs><title>Icon_24px_NaturalLanguage_Color</title><g data-name="Product Icons"><polygon class="cls-1" points="20 5 17 5 17 7 20 7 20 19 17 19 17 21 20 21 22 21 22 19 22 7 22 5 20 5"/><g ><polygon class="cls-2" points="20 8 22 7 20 7 20 8"/><polygon class="cls-2" points="22 18 20 19 22 19 22 18"/></g><polygon class="cls-1" points="4 21 7 21 7 19 4 19 4 7 7 7 7 5 4 5 2 5 2 7 2 19 2 21 4 21"/><g data-name="Shape"><polygon class="cls-2" points="2 18 4 19 2 19 2 18"/><polygon class="cls-2" points="4 8 2 7 4 7 4 8"/></g><rect id="Rectangle-7-Copy" class="cls-3" x="7" y="12" width="10" height="2"/><rect id="Rectangle-7-Copy-2" data-name="Rectangle-7-Copy" class="cls-3" x="7" y="15" width="10" height="2"/><rect id="Rectangle-7-Copy-3" data-name="Rectangle-7-Copy" class="cls-3" x="7" y="9" width="10" height="2"/></g></svg>
        """;

    static readonly string SolutionIcon = """
        <svg id="SolutionIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><linearGradient id="fd591b35-0e01-4610-a025-8e017443b6ca" x1="3.96" y1="2.52" x2="3.96" y2="7.82" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#32d4f5" /><stop offset="1" stop-color="#198ab3" /></linearGradient><linearGradient id="bded69e0-afb1-4c25-aa81-a799bd12553b" x1="14.04" y1="5.58" x2="14.04" y2="10.89" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#32d4f5" /><stop offset="1" stop-color="#198ab3" /></linearGradient><linearGradient id="f25b6c72-365d-4ac2-a15c-1cace994f1e6" x1="5.29" y1="10.18" x2="5.29" y2="15.48" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#32d4f5" /><stop offset="1" stop-color="#198ab3" /></linearGradient></defs><title>Icon-analytics-148</title><rect x="0.5" y="2.52" width="6.92" height="5.31" rx="0.28" fill="url(#fd591b35-0e01-4610-a025-8e017443b6ca)" /><rect x="10.58" y="5.58" width="6.92" height="5.31" rx="0.28" fill="url(#bded69e0-afb1-4c25-aa81-a799bd12553b)" /><rect x="1.82" y="10.18" width="6.92" height="5.31" rx="0.42" fill="url(#f25b6c72-365d-4ac2-a15c-1cace994f1e6)" /><path d="M13.15,8.92l.23-.64L4.82,5.13l-.23.63Zm.53-.09-.32-.6L5.29,12.48l.31.6ZM5,13.37l.67-.12L4.31,5.54l-.67.12Z" fill="#773adc" /><circle cx="14.04" cy="8.23" r="1.08" transform="translate(-2.23 8.81) rotate(-32.41)" fill="#fff" /><circle cx="3.96" cy="5.17" r="1.08" transform="translate(-2.15 2.93) rotate(-32.41)" fill="#fff" /><circle cx="5.29" cy="12.83" r="1.08" transform="translate(-6.05 4.83) rotate(-32.41)" fill="#fff" /></svg>
        """;

    static readonly string MemberIcon = """
        <svg id="MemberIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><radialGradient id="b915730c-dc69-4cd4-8cf3-619882b8e8ab" cx="55.71" cy="71.92" r="9" gradientTransform="translate(-43.61 -58.92) scale(0.94 0.94)" gradientUnits="userSpaceOnUse"><stop offset="0.67" stop-color="#6bb9f2" /><stop offset="0.74" stop-color="#61b4f1" /><stop offset="0.85" stop-color="#47a8ef" /><stop offset="0.99" stop-color="#1d94eb" /><stop offset="1" stop-color="#1b93eb" /></radialGradient></defs><title>Icon-machinelearning-165</title><path id="f6a29e1b-194b-4d8d-8529-49edea7bbba0" d="M9,.5A8.5,8.5,0,1,0,17.5,9,8.5,8.5,0,0,0,9,.5Z" fill="url(#b915730c-dc69-4cd4-8cf3-619882b8e8ab)" /><circle cx="9" cy="9" r="7.03" fill="#fff" /><circle cx="7.45" cy="9" r="0.77" fill="#32bedd" /><path d="M5.26,6.8H4.88a.29.29,0,0,0-.29.29v5.72a.59.59,0,0,0,.59.59h5.57a.29.29,0,0,0,.29-.3v-.38a.29.29,0,0,0-.29-.29h-5a.14.14,0,0,1-.14-.15V7.09A.29.29,0,0,0,5.26,6.8Z" fill="#32bedd" /><circle cx="10.55" cy="9" r="0.77" fill="#32bedd" /><path d="M12.42,4.6H7.23a.29.29,0,0,0-.29.3v.38a.29.29,0,0,0,.29.29h5a.15.15,0,0,1,.15.15v5.19a.29.29,0,0,0,.29.29h.38a.29.29,0,0,0,.29-.29V5.19a.59.59,0,0,0-.58-.59Z" fill="#32bedd" /></svg>
        """;

    static readonly string FieldIcon = """
        <svg id="FieldIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><radialGradient id="fieldGradient" cx="55.71" cy="71.92" r="9" gradientTransform="translate(-43.61 -58.92) scale(0.94 0.94)" gradientUnits="userSpaceOnUse"><stop offset="0.67" stop-color="#6bb9f2" /><stop offset="0.74" stop-color="#61b4f1" /><stop offset="0.85" stop-color="#47a8ef" /><stop offset="0.99" stop-color="#1d94eb" /><stop offset="1" stop-color="#1b93eb" /></radialGradient></defs><path d="M9,.5A8.5,8.5,0,1,0,17.5,9,8.5,8.5,0,0,0,9,.5Z" fill="url(#fieldGradient)" /><circle cx="9" cy="9" r="7.03" fill="#fff" /><g fill="#32bedd"><circle cx="6.3" cy="9" r="0.65" /><rect x="7.7" y="8.4" width="5.6" height="1.2" rx="0.6" /></g></svg>
        """;

    static readonly string EventIcon = """
        <svg id="EventIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><radialGradient id="eventGradient" cx="55.71" cy="71.92" r="9" gradientTransform="translate(-43.61 -58.92) scale(0.94 0.94)" gradientUnits="userSpaceOnUse"><stop offset="0.67" stop-color="#6bb9f2" /><stop offset="0.74" stop-color="#61b4f1" /><stop offset="0.85" stop-color="#47a8ef" /><stop offset="0.99" stop-color="#1d94eb" /><stop offset="1" stop-color="#1b93eb" /></radialGradient></defs><path d="M9,.5A8.5,8.5,0,1,0,17.5,9,8.5,8.5,0,0,0,9,.5Z" fill="url(#eventGradient)" /><circle cx="9" cy="9" r="7.03" fill="#fff" /><circle cx="9" cy="9" r="1.25" fill="#32bedd" /><path d="M6.4,6.4A3.6,3.6,0,0,1,6.4,11.6" fill="none" stroke="#32bedd" stroke-width="1" stroke-linecap="round" /><path d="M11.6,6.4A3.6,3.6,0,0,0,11.6,11.6" fill="none" stroke="#32bedd" stroke-width="1" stroke-linecap="round" /><path d="M5,5A5.2,5.2,0,0,1,5,13" fill="none" stroke="#32bedd" stroke-width="0.8" stroke-linecap="round" opacity="0.6" /><path d="M13,5A5.2,5.2,0,0,0,13,13" fill="none" stroke="#32bedd" stroke-width="0.8" stroke-linecap="round" opacity="0.6" /></svg>
        """;

    static readonly string PropertyIcon = """
        <svg id="PropertyIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><radialGradient id="propertyGradient" cx="55.71" cy="71.92" r="9" gradientTransform="translate(-43.61 -58.92) scale(0.94 0.94)" gradientUnits="userSpaceOnUse"><stop offset="0.67" stop-color="#6bb9f2" /><stop offset="0.74" stop-color="#61b4f1" /><stop offset="0.85" stop-color="#47a8ef" /><stop offset="0.99" stop-color="#1d94eb" /><stop offset="1" stop-color="#1b93eb" /></radialGradient></defs><path d="M9,.5A8.5,8.5,0,1,0,17.5,9,8.5,8.5,0,0,0,9,.5Z" fill="url(#propertyGradient)" /><circle cx="9" cy="9" r="7.03" fill="#fff" /><circle cx="6.8" cy="9" r="1.8" fill="none" stroke="#32bedd" stroke-width="1.1" /><rect x="8.7" y="8.3" width="4.6" height="1.4" rx="0.7" fill="#32bedd" /></svg>
        """;

    static readonly string FunctionIcon = """
        <svg id="FunctionIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><radialGradient id="functionGradient" cx="55.71" cy="71.92" r="9" gradientTransform="translate(-43.61 -58.92) scale(0.94 0.94)" gradientUnits="userSpaceOnUse"><stop offset="0.67" stop-color="#6bb9f2" /><stop offset="0.74" stop-color="#61b4f1" /><stop offset="0.85" stop-color="#47a8ef" /><stop offset="0.99" stop-color="#1d94eb" /><stop offset="1" stop-color="#1b93eb" /></radialGradient></defs><path d="M9,.5A8.5,8.5,0,1,0,17.5,9,8.5,8.5,0,0,0,9,.5Z" fill="url(#functionGradient)" /><circle cx="9" cy="9" r="7.03" fill="#fff" /><path d="M6.5,6.2h1.35l1.08,3.94,1.51-2.59h1.18l-2.24,3.66v1.9H8.22V11.5Z" fill="#32bedd" /></svg>
        """;

    static readonly string MethodIcon = """
        <svg id="MethodIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><radialGradient id="methodGradient" cx="55.71" cy="71.92" r="9" gradientTransform="translate(-43.61 -58.92) scale(0.94 0.94)" gradientUnits="userSpaceOnUse"><stop offset="0.67" stop-color="#6bb9f2" /><stop offset="0.74" stop-color="#61b4f1" /><stop offset="0.85" stop-color="#47a8ef" /><stop offset="0.99" stop-color="#1d94eb" /><stop offset="1" stop-color="#1b93eb" /></radialGradient></defs><path d="M9,.5A8.5,8.5,0,1,0,17.5,9,8.5,8.5,0,0,0,9,.5Z" fill="url(#methodGradient)" /><circle cx="9" cy="9" r="7.03" fill="#fff" /><path d="M7.2,5.5c-.9.35-1.5,1-1.5,1.7s.6,1.3,1.5,1.6v0c-.9.35-1.5,1-1.5,1.7s.6,1.35,1.5,1.7" fill="none" stroke="#32bedd" stroke-width="1.1" stroke-linecap="round" stroke-linejoin="round" /><path d="M10.8,5.5c.9.35,1.5,1,1.5,1.7s-.6,1.3-1.5,1.6v0c.9.35,1.5,1,1.5,1.7s-.6,1.35-1.5,1.7" fill="none" stroke="#32bedd" stroke-width="1.1" stroke-linecap="round" stroke-linejoin="round" /></svg>
        """;

    static readonly string ConstructorIcon = """
        <svg id="ConstructorIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><defs><radialGradient id="methodGradient" cx="55.71" cy="71.92" r="9" gradientTransform="translate(-43.61 -58.92) scale(0.94 0.94)" gradientUnits="userSpaceOnUse"><stop offset="0.67" stop-color="#6bb9f2" /><stop offset="0.74" stop-color="#61b4f1" /><stop offset="0.85" stop-color="#47a8ef" /><stop offset="0.99" stop-color="#1d94eb" /><stop offset="1" stop-color="#1b93eb" /></radialGradient></defs><path d="M9,.5A8.5,8.5,0,1,0,17.5,9,8.5,8.5,0,0,0,9,.5Z" fill="url(#methodGradient)" /><circle cx="9" cy="9" r="7.03" fill="#fff" /><circle cx="9" cy="9" r="2.4" fill="none" stroke="#32bedd" stroke-width="1" /><circle cx="9" cy="9" r="0.95" fill="#32bedd" /><rect x="8.7" y="5.3" width="0.6" height="1.6" rx="0.3" fill="#32bedd" /><rect x="8.7" y="11.1" width="0.6" height="1.6" rx="0.3" fill="#32bedd" /><rect x="5.3" y="8.7" width="1.6" height="0.6" rx="0.3" fill="#32bedd" /><rect x="11.1" y="8.7" width="1.6" height="0.6" rx="0.3" fill="#32bedd" /><rect x="6" y="6" width="1.2" height="1.2" transform="rotate(-45 6.6 6.6)" fill="#32bedd" opacity="0.7" /><rect x="10.8" y="10.8" width="1.2" height="1.2" transform="rotate(-45 11.4 11.4)" fill="#32bedd" opacity="0.7" /><rect x="10.8" y="6" width="1.2" height="1.2" transform="rotate(45 11.4 6.6)" fill="#32bedd" opacity="0.7" /><rect x="6" y="10.8" width="1.2" height="1.2" transform="rotate(45 6.6 11.4)" fill="#32bedd" opacity="0.7" /></svg>
        """;
    internal static readonly string ModuleIcon =
        """<svg id="ModuleIcon" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 18 18"><rect x="1" y="1" width="16" height="16" rx="2" fill="#2f73d6" /><rect x="1" y="1" width="16" height="8.5" rx="2" fill="#6aa7f4" /><g fill="#ffffff"><rect x="2.1" y="2.1" width="3.6" height="3.2" rx="0.6" /><rect x="7.2" y="2.1" width="3.6" height="3.2" rx="0.6" /><rect x="12.3" y="2.1" width="3.6" height="3.2" rx="0.6" /></g><rect x="2.1" y="7.5" width="8.5" height="3.2" rx="0.6" fill="#d6e6ff" /><rect x="11.5" y="7.5" width="3.6" height="3.2" rx="0.6" fill="#d6e6ff" /><rect x="2.1" y="12.1" width="12.4" height="3.2" rx="0.6" fill="#c4dcff" /></svg>""";

    static Dictionary<Parsing.NodeType, string> IconMap = new()
    {
        { Parsing.NodeType.Solution, SolutionIcon },
        { Parsing.NodeType.Externals, ExternalsIcon },
        { Parsing.NodeType.Type, TypeIcon },
        { Parsing.NodeType.Member, MemberIcon },
    };

    public static string GetIcon(Parsing.NodeType iconName)
    {
        if (!IconMap.TryGetValue(iconName, out string? icon))
            return ModuleIcon;

        return icon;
    }

    public static readonly string IconDefs = $"""
            {SolutionIcon}
            {ExternalsIcon}
            {PrivateIcon}
            {FilesIcon}
            {TypeIcon}
            {MemberIcon}
            {FieldIcon}
            {EventIcon}
            {PropertyIcon}
            {FunctionIcon}
            {MethodIcon}
            {ConstructorIcon}
            {ModuleIcon}
        """;
}

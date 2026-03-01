using System.Runtime.InteropServices;

namespace Dependinator.Core.Shared;

enum HostType
{
    Wasm,
    Web,
    VscExtWasm,
    VscExtLsp,
}

interface IHost
{
    HostType Type { get; }
    bool IsVscExtWasm { get; }
    void SetIsVsCodeExt();
}

[Singleton]
class Host : IHost
{
    readonly bool isWasm = RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
    HostType hostType;

    public Host()
    {
        hostType = isWasm ? HostType.Wasm : HostType.Web;
        Log.Info("Base Host Type", hostType.Name());
    }

    public HostType Type => hostType;

    public bool IsVscExtWasm => hostType == HostType.VscExtWasm;

    public void SetIsVsCodeExt()
    {
        hostType = isWasm ? hostType = HostType.VscExtWasm : HostType.VscExtLsp;
        Log.Info("Set Host Type", hostType.Name());
    }
}

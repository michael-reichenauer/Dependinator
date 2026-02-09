namespace Dependinator.Shared;

interface IProgressScope : IDisposable
{
    void SetText(string text);
}

interface IProgressService
{
    bool IsDiscreetActive { get; }
    bool IsProminentActive { get; }
    string? ProminentText { get; }
    IProgressScope StartDiscreet();
    IProgressScope Start(string? text = null);
}

[Scoped]
class ProgressService(IApplicationEvents applicationEvents) : IProgressService
{
    readonly object syncRoot = new();
    readonly IApplicationEvents applicationEvents = applicationEvents;
    readonly Dictionary<Guid, ProgressEntry> prominentEntries = new();

    long updateStamp;
    string? prominentText;

    public bool IsDiscreetActive
    {
        get
        {
            lock (syncRoot)
            {
                return FindLatestKind() is ProgressKind.Discreet;
            }
        }
    }

    public bool IsProminentActive
    {
        get
        {
            lock (syncRoot)
            {
                return FindLatestKind() is ProgressKind.Prominent;
            }
        }
    }

    public string? ProminentText
    {
        get
        {
            lock (syncRoot)
            {
                return prominentText;
            }
        }
    }

    public IProgressScope StartDiscreet() => Start(ProgressKind.Discreet, text: null);

    public IProgressScope Start(string? text = null) => Start(ProgressKind.Prominent, text);

    IProgressScope Start(ProgressKind kind, string? text)
    {
        var id = Guid.NewGuid();
        lock (syncRoot)
        {
            var entry = new ProgressEntry(kind, ++updateStamp, NormalizeText(text));
            prominentEntries[id] = entry;
            prominentText = FindLatestText();
        }

        applicationEvents.TriggerUIStateChanged();
        return new ProgressScope(this, kind, id);
    }

    void Stop(ProgressKind _, Guid id)
    {
        var changed = false;
        lock (syncRoot)
        {
            if (prominentEntries.Remove(id))
            {
                prominentText = FindLatestText();
                changed = true;
            }
        }

        if (changed)
            applicationEvents.TriggerUIStateChanged();
    }

    void SetText(ProgressKind kind, Guid id, string text)
    {
        var changed = false;
        lock (syncRoot)
        {
            if (!prominentEntries.TryGetValue(id, out var entry))
                return;
            entry.Kind = kind;
            entry.Text = NormalizeText(text);
            entry.UpdateStamp = ++updateStamp;
            prominentText = FindLatestText();
            changed = true;
        }

        if (changed)
            applicationEvents.TriggerUIStateChanged();
    }

    string? NormalizeText(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    string? FindLatestText() => FindLatest()?.Text;

    ProgressKind? FindLatestKind() => FindLatest()?.Kind;

    ProgressEntry? FindLatest()
    {
        ProgressEntry? selected = null;
        foreach (var entry in prominentEntries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.Text))
                continue;

            if (selected == null || entry.UpdateStamp > selected.UpdateStamp)
                selected = entry;
        }

        return selected;
    }

    sealed class ProgressScope : IProgressScope
    {
        readonly ProgressService owner;
        readonly ProgressKind kind;
        readonly Guid id;
        bool disposed;

        public ProgressScope(ProgressService owner, ProgressKind kind, Guid id)
        {
            this.owner = owner;
            this.kind = kind;
            this.id = id;
        }

        public void SetText(string text) => owner.SetText(kind, id, text);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            owner.Stop(kind, id);
        }
    }

    sealed class ProgressEntry(ProgressService.ProgressKind Kind, long updateStamp, string? text)
    {
        public ProgressKind Kind { get; set; } = Kind;
        public long UpdateStamp { get; set; } = updateStamp;
        public string? Text { get; set; } = text;
    }

    enum ProgressKind
    {
        Discreet,
        Prominent,
    }
}

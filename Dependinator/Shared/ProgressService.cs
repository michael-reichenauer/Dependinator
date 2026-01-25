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
class ProgressService : IProgressService
{
    readonly object syncRoot = new();
    readonly IApplicationEvents applicationEvents;
    readonly Dictionary<Guid, ProgressEntry> prominentEntries = new();
    int discreetCount;
    long updateStamp;
    string? prominentText;

    public ProgressService(IApplicationEvents applicationEvents)
    {
        this.applicationEvents = applicationEvents;
    }

    public bool IsDiscreetActive
    {
        get
        {
            lock (syncRoot)
            {
                return discreetCount > 0;
            }
        }
    }

    public bool IsProminentActive
    {
        get
        {
            lock (syncRoot)
            {
                return prominentEntries.Count > 0;
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
            if (kind == ProgressKind.Discreet)
            {
                discreetCount++;
            }
            else
            {
                var entry = new ProgressEntry(++updateStamp, NormalizeText(text));
                prominentEntries[id] = entry;
                prominentText = FindLatestText();
            }
        }

        applicationEvents.TriggerUIStateChanged();
        return new ProgressScope(this, kind, id);
    }

    void Stop(ProgressKind kind, Guid id)
    {
        var changed = false;
        lock (syncRoot)
        {
            if (kind == ProgressKind.Discreet)
            {
                if (discreetCount > 0)
                {
                    discreetCount--;
                    changed = true;
                }
            }
            else if (prominentEntries.Remove(id))
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
        if (kind != ProgressKind.Prominent)
            return;

        var changed = false;
        lock (syncRoot)
        {
            if (!prominentEntries.TryGetValue(id, out var entry))
                return;

            entry.Text = NormalizeText(text);
            entry.UpdateStamp = ++updateStamp;
            prominentText = FindLatestText();
            changed = true;
        }

        if (changed)
            applicationEvents.TriggerUIStateChanged();
    }

    string? NormalizeText(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    string? FindLatestText()
    {
        ProgressEntry? selected = null;
        foreach (var entry in prominentEntries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.Text))
                continue;

            if (selected == null || entry.UpdateStamp > selected.UpdateStamp)
                selected = entry;
        }

        return selected?.Text;
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

    sealed class ProgressEntry
    {
        public ProgressEntry(long updateStamp, string? text)
        {
            UpdateStamp = updateStamp;
            Text = text;
        }

        public long UpdateStamp { get; set; }
        public string? Text { get; set; }
    }

    enum ProgressKind
    {
        Discreet,
        Prominent,
    }
}

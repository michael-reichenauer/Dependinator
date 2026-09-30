namespace Dependinator.UI.Shared;

interface IProgressScope : IDisposable
{
    void SetText(string text);
}

interface IProgressService
{
    bool IsDiscreetActive { get; }
    bool IsProminentActive { get; }
    string? ProminentText { get; }

    // When the prominent (blocking) progress started, for an elapsed-time display.
    DateTime? ProminentStartedUtc { get; }

    // Turns the current prominent progress into the discreet corner spinner: the work goes on,
    // the user can keep using the app, and the result lands when it is done.
    void ContinueInBackground();

    IProgressScope StartDiscreet();
    IProgressScope Start(string? text = null);
}

[Scoped]
class ProgressService(IApplicationEvents applicationEvents) : IProgressService
{
    readonly object syncRoot = new();
    readonly IApplicationEvents applicationEvents = applicationEvents;
    readonly Dictionary<Guid, ProgressEntry> entries = new();

    long updateStamp;
    string? prominentText;
    bool isProminentDismissed;
    DateTime? prominentStartedUtc;

    public bool IsDiscreetActive
    {
        get
        {
            lock (syncRoot)
            {
                var kind = FindLatestKind();
                return kind is ProgressKind.Discreet || (kind is ProgressKind.Prominent && isProminentDismissed);
            }
        }
    }

    public bool IsProminentActive
    {
        get
        {
            lock (syncRoot)
            {
                return FindLatestKind() is ProgressKind.Prominent && !isProminentDismissed;
            }
        }
    }

    public DateTime? ProminentStartedUtc
    {
        get
        {
            lock (syncRoot)
            {
                return prominentStartedUtc;
            }
        }
    }

    public void ContinueInBackground()
    {
        lock (syncRoot)
        {
            if (FindLatestKind() is not ProgressKind.Prominent)
                return;
            isProminentDismissed = true;
        }

        applicationEvents.TriggerUIStateChanged();
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

    public IProgressScope StartDiscreet() => Start(ProgressKind.Discreet, text: ".");

    public IProgressScope Start(string? text = null) => Start(ProgressKind.Prominent, text);

    IProgressScope Start(ProgressKind kind, string? text)
    {
        var id = Guid.NewGuid();
        lock (syncRoot)
        {
            var entry = new ProgressEntry(kind, ++updateStamp, NormalizeText(text));
            entries[id] = entry;
            prominentText = FindLatestText();
            if (kind is ProgressKind.Prominent)
            {
                prominentStartedUtc ??= DateTime.UtcNow;
                isProminentDismissed = false;
            }
        }

        applicationEvents.TriggerUIStateChanged();
        return new ProgressScope(this, kind, id);
    }

    void Stop(Guid id)
    {
        var changed = false;
        lock (syncRoot)
        {
            if (entries.Remove(id))
            {
                prominentText = FindLatestText();
                changed = true;
                if (!entries.Values.Any(e => e.Kind is ProgressKind.Prominent))
                {
                    prominentStartedUtc = null;
                    isProminentDismissed = false;
                }
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
            if (!entries.TryGetValue(id, out var entry))
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

    static string? NormalizeText(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    string? FindLatestText() => FindLatest()?.Text;

    ProgressKind? FindLatestKind() => FindLatest()?.Kind;

    ProgressEntry? FindLatest()
    {
        ProgressEntry? selected = null;
        foreach (var entry in entries.Values)
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
            owner.Stop(id);
        }
    }

    sealed class ProgressEntry(ProgressService.ProgressKind kind, long updateStamp, string? text)
    {
        public ProgressKind Kind { get; set; } = kind;
        public long UpdateStamp { get; set; } = updateStamp;
        public string? Text { get; set; } = text;
    }

    enum ProgressKind
    {
        Discreet,
        Prominent,
    }
}

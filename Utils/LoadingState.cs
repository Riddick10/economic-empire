namespace GrandStrategyGame;

/// <summary>
/// Thread-sicherer Ladezustand fuer die asynchrone Spielinitialisierung.
/// </summary>
class LoadingState
{
    private readonly object _lock = new();
    private string _status = "Initialisiere...";
    private float _progress = 0f;
    private bool _complete = false;

    public bool Started { get; set; } = false;
    public Task? LoadingTask { get; set; }

    public string Status
    {
        get { lock (_lock) { return _status; } }
        set { lock (_lock) { _status = value; } }
    }

    public float Progress
    {
        get { lock (_lock) { return _progress; } }
        set { lock (_lock) { _progress = value; } }
    }

    public bool Complete
    {
        get { lock (_lock) { return _complete; } }
        set { lock (_lock) { _complete = value; } }
    }
}

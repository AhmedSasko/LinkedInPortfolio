using System.Collections.Concurrent;
using LinkedInAI.API.DTOs;

namespace LinkedInAI.API.Services;

public class ScrapeProgressStore
{
    public static readonly IReadOnlyList<(string Key, string Label)> AllSteps =
    [
        ("launching_browser", "Launching browser"),
        ("loading_profile",   "Loading LinkedIn profile"),
        ("basic_info",        "Extracting name, headline & photo"),
        ("experience",        "Extracting work experience"),
        ("education",         "Extracting education"),
        ("skills",            "Extracting skills"),
        ("certifications",    "Extracting certifications & projects"),
        ("saving",            "Saving to your profile"),
    ];

    private readonly ConcurrentDictionary<int, ScrapeState> _jobs = new();

    public bool IsRunning(int userId) =>
        _jobs.TryGetValue(userId, out var s) && !s.IsComplete;

    public void Start(int userId) =>
        _jobs[userId] = new ScrapeState();

    public void SetStep(int userId, string key)
    {
        if (!_jobs.TryGetValue(userId, out var state)) return;
        if (state.CurrentStep is { } prev)
            state.DoneSteps.Add(prev);
        state.CurrentStep = key;
    }

    public void Complete(int userId, ProfileDto result)
    {
        if (!_jobs.TryGetValue(userId, out var state)) return;
        if (state.CurrentStep is { } prev)
            state.DoneSteps.Add(prev);
        state.CurrentStep = null;
        state.Result = result;
        state.IsComplete = true;
    }

    public void Fail(int userId, string error)
    {
        if (!_jobs.TryGetValue(userId, out var state)) return;
        state.FailedStep = state.CurrentStep;
        state.Error = error;
        state.IsComplete = true;
    }

    public ScrapeProgressDto? Get(int userId)
    {
        if (!_jobs.TryGetValue(userId, out var state)) return null;

        var steps = AllSteps.Select(s =>
        {
            var status = state.FailedStep == s.Key ? "error"
                       : state.DoneSteps.Contains(s.Key) ? "done"
                       : state.CurrentStep == s.Key ? "running"
                       : "pending";
            return new ScrapeStepDto(s.Key, s.Label, status);
        }).ToList();

        return new ScrapeProgressDto(!state.IsComplete, steps, state.Error, state.Result);
    }

    public void Remove(int userId) => _jobs.TryRemove(userId, out _);
}

public class ScrapeState
{
    public string? CurrentStep { get; set; }
    public HashSet<string> DoneSteps { get; set; } = new(StringComparer.Ordinal);
    public string? FailedStep { get; set; }
    public bool IsComplete { get; set; }
    public string? Error { get; set; }
    public ProfileDto? Result { get; set; }
}

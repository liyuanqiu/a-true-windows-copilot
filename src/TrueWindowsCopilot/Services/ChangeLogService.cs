using TrueWindowsCopilot.Models;

namespace TrueWindowsCopilot.Services;

/// <summary>
/// Stores the history of all system changes made by the AI, enabling revert.
/// </summary>
public class ChangeLogService
{
    private readonly List<SystemChange> _changes = [];
    private int _nextId = 1;

    /// <summary>
    /// Records a new system change.
    /// </summary>
    public SystemChange Record(string description, string script, string revertScript, string? output)
    {
        var change = new SystemChange
        {
            Id = _nextId++,
            Description = description,
            Script = script,
            RevertScript = revertScript,
            Output = output
        };
        _changes.Add(change);
        return change;
    }

    /// <summary>
    /// Gets all changes (oldest first).
    /// </summary>
    public IReadOnlyList<SystemChange> GetAll() => _changes.AsReadOnly();

    /// <summary>
    /// Gets only changes that haven't been reverted yet (newest first).
    /// </summary>
    public IReadOnlyList<SystemChange> GetPending() =>
        _changes.Where(c => !c.IsReverted).Reverse().ToList().AsReadOnly();

    /// <summary>
    /// Gets a change by ID.
    /// </summary>
    public SystemChange? GetById(int id) => _changes.FirstOrDefault(c => c.Id == id);

    /// <summary>
    /// Marks a change as reverted.
    /// </summary>
    public void MarkReverted(int id, string? revertOutput)
    {
        var change = GetById(id);
        if (change != null)
        {
            change.IsReverted = true;
            change.RevertOutput = revertOutput;
        }
    }
}

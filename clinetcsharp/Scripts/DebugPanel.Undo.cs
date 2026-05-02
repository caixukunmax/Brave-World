using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — Undo system (disabled).
    /// Previous implementation was incomplete (only covered partial state),
    /// giving users false expectations. Replaced with no-op stubs.
    /// Can be re-implemented later using SaveConfig/LoadConfig snapshots.
    /// </summary>
    public partial class DebugPanel
    {
        /// <summary>No-op: Undo system removed.</summary>
        internal void PushCurrentStateToHistory() { }
    }
}

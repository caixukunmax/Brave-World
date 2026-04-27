using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 撤销系统
    /// Captures/applies state by delegating to tab classes.
    /// </summary>
    public partial class DebugPanel
    {
        #region Undo System
        internal void PushCurrentStateToHistory()
        {
            if (_isRestoring) return;

            var state = CaptureCurrentState();

            // If we are not at the end of history, trim future entries
            if (_historyIndex >= 0 && _historyIndex < _configHistory.Count - 1)
            {
                _configHistory.RemoveRange(_historyIndex + 1, _configHistory.Count - _historyIndex - 1);
            }

            _configHistory.Add(state);
            _historyIndex = _configHistory.Count - 1;

            // Trim old entries
            while (_configHistory.Count > MAX_HISTORY_STEPS)
            {
                _configHistory.RemoveAt(0);
                _historyIndex--;
            }
        }

        private Godot.Collections.Dictionary CaptureCurrentState()
        {
            var state = new Godot.Collections.Dictionary();

            // Delegate to each tab for tab-specific state
            foreach (var tab in _tabs)
            {
                var tabState = tab.CaptureUndoState();
                if (tabState != null)
                    state[tab.TabKey] = tabState;
            }

            return state;
        }

        private void ApplyState(Godot.Collections.Dictionary state)
        {
            _isRestoring = true;

            // Delegate to each tab for tab-specific state application
            foreach (var tab in _tabs)
            {
                if (state.ContainsKey(tab.TabKey))
                {
                    var tabState = state[tab.TabKey].AsGodotDictionary();
                    tab.ApplyUndoState(tabState);
                }
            }

            _isRestoring = false;
        }

        private bool StatesEqual(Godot.Collections.Dictionary a, Godot.Collections.Dictionary b)
        {
            if (a.Count != b.Count) return false;
            foreach (var key in a.Keys)
            {
                if (!b.ContainsKey(key)) return false;
                var aVal = a[key];
                var bVal = b[key];
                if (aVal.VariantType != bVal.VariantType) return false;
                if (aVal.VariantType == Variant.Type.Dictionary)
                {
                    if (!StatesEqual(aVal.AsGodotDictionary(), bVal.AsGodotDictionary()))
                        return false;
                }
                else if (aVal.VariantType == Variant.Type.Array)
                {
                    var aArr = aVal.AsGodotArray();
                    var bArr = bVal.AsGodotArray();
                    if (aArr.Count != bArr.Count) return false;
                    for (int i = 0; i < aArr.Count; i++)
                    {
                        if (!aArr[i].Equals(bArr[i])) return false;
                    }
                }
                else
                {
                    if (!aVal.Equals(bVal)) return false;
                }
            }
            return true;
        }

        private void UndoLastChange()
        {
            if (_historyIndex <= 0)
            {
                GD.Print("[DebugPanel] No more undo steps available");
                return;
            }

            _historyIndex--;
            var state = _configHistory[_historyIndex];
            ApplyState(state);
            ShowUndoNotification();
        }

        private void ShowUndoNotification()
        {
            GD.Print("[DebugPanel] Undo applied");
        }
        #endregion
    }
}
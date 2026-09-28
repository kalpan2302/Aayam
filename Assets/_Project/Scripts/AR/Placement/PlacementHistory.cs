using System.Collections.Generic;
using UnityEngine;

namespace Aayam.AR.Placement
{
    // ─── Command Interface ────────────────────────────────────

    /// <summary>Base interface for undoable/redoable placement commands.</summary>
    public interface IPlacementCommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    // ─── Concrete Commands ────────────────────────────────────

    /// <summary>Command representing a single object placement.</summary>
    public class PlaceCommand : IPlacementCommand
    {
        private readonly PlacedProduct   _product;
        private readonly AnchorController _anchors;

        public string Description => $"Place {_product.ProductId}";

        public PlaceCommand(PlacedProduct product, AnchorController anchors)
        {
            _product = product;
            _anchors  = anchors;
        }

        public void Execute()
        {
            // Object was already placed before command was recorded — re-show if hidden
            if (_product.Instance != null)
                _product.Instance.SetActive(true);
        }

        public void Undo()
        {
            if (_product.Instance != null)
            {
                _anchors?.RemoveAnchorForObject(_product.Instance);
                _product.Instance.SetActive(false);  // Hide rather than destroy for redo
            }
        }
    }

    /// <summary>Command representing removal of a placed object.</summary>
    public class RemoveCommand : IPlacementCommand
    {
        private readonly PlacedProduct   _product;
        private readonly AnchorController _anchors;
        private readonly Pose            _originalPose;

        public string Description => $"Remove {_product.ProductId}";

        public RemoveCommand(PlacedProduct product, AnchorController anchors)
        {
            _product      = product;
            _anchors       = anchors;
            _originalPose  = product.PlacedPose;
        }

        public void Execute()
        {
            if (_product.Instance != null)
            {
                _anchors?.RemoveAnchorForObject(_product.Instance);
                _product.Instance.SetActive(false);
            }
        }

        public void Undo()
        {
            // Re-anchor at original position
            if (_product.Instance != null && _anchors != null)
            {
                var anchor = _anchors.CreateAnchor(_originalPose);
                if (anchor != null)
                {
                    _anchors.AttachObjectToAnchor(_product.Instance, anchor);
                    _product.Instance.SetActive(true);
                }
            }
        }
    }

    // ─── History Manager ──────────────────────────────────────

    /// <summary>
    /// Undo/redo manager for placement operations.
    /// Uses a Command Pattern with two stacks (undo / redo).
    /// Max history size configurable to prevent memory bloat.
    /// </summary>
    public class PlacementHistory : MonoBehaviour
    {
        [SerializeField] private int _maxHistorySize = 20;

        private readonly Stack<IPlacementCommand> _undoStack = new();
        private readonly Stack<IPlacementCommand> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        // ─── API ──────────────────────────────────────────────────

        /// <summary>Record and execute a command.</summary>
        public void ExecuteCommand(IPlacementCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();   // Any new action kills redo history

            // Trim to max size
            while (_undoStack.Count > _maxHistorySize)
            {
                var arr = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = arr.Length - 2; i >= 0; i--)
                    _undoStack.Push(arr[i]);
            }

            Debug.Log($"[History] Executed: {command.Description} | Undo stack: {_undoStack.Count}");
        }

        /// <summary>Shortcut: push a place action (already executed).</summary>
        public void PushPlaceAction(PlacedProduct product, AnchorController anchors)
        {
            var cmd = new PlaceCommand(product, anchors);
            _undoStack.Push(cmd);
            _redoStack.Clear();
        }

        /// <summary>Undo the last command.</summary>
        public void Undo()
        {
            if (!CanUndo)
            {
                Debug.Log("[History] Nothing to undo.");
                return;
            }

            var cmd = _undoStack.Pop();
            cmd.Undo();
            _redoStack.Push(cmd);
            Debug.Log($"[History] Undo: {cmd.Description} | Redo stack: {_redoStack.Count}");
        }

        /// <summary>Redo the last undone command.</summary>
        public void Redo()
        {
            if (!CanRedo)
            {
                Debug.Log("[History] Nothing to redo.");
                return;
            }

            var cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Push(cmd);
            Debug.Log($"[History] Redo: {cmd.Description}");
        }

        /// <summary>Clear all history.</summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}

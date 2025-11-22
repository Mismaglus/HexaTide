// Script/Game/Battle/Actions/MoveAction.cs
using System.Collections;
using UnityEngine;
using Core.Hex;
using Game.Units;

namespace Game.Battle.Actions
{
    /// <summary>
    /// Single-hex step action that defers to UnitMover.TryStepTo().
    /// </summary>
    public class MoveAction : IAction
    {
        private readonly UnitMover _mover;
        private readonly HexCoords _to;

        public MoveAction(UnitMover mover, HexCoords to)
        {
            _mover = mover;
            _to = to;
        }

        // Mirror UnitMover preconditions as much as we can from outside:
        // 1) mover exists and not already moving
        // 2) destination is adjacent (distance == 1)
        // 3) ⭐ at least 1 stride left (Checked via Attributes)
        public bool IsValid
        {
            get
            {
                if (_mover == null || _mover.IsMoving) return false;
                if (_mover._mCoords.DistanceTo(_to) != 1) return false;

                // 获取属性组件检查步数
                var attrs = _mover.GetComponent<UnitAttributes>();
                if (attrs == null || attrs.Core.CurrentStride < 1) return false;

                return true;
            }
        }

        public IEnumerator Execute()
        {
            if (_mover == null)
                yield break;

            // Try to start the step. UnitMover will internally check:
            // - has grid
            // - distance == 1
            // - strideLeft >= movement cost (>=1)
            bool finished = false;
            bool started = _mover.TryStepTo(_to, () => finished = true);

            if (!started)
                yield break; // nothing to do if preconditions failed

            // Wait until UnitMover calls our onDone callback
            while (!finished)
                yield return null;
        }
    }
}
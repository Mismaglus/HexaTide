using System.Collections;
using UnityEngine; // 需要引用这个来使用 GetComponent
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

        // 校验逻辑：
        // 1. Mover 存在且空闲
        // 2. 目标是邻居
        // 3. ⭐ 修复：从 Attributes 检查剩余步数 >= 1
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

            bool finished = false;
            // UnitMover 内部也会检查 Attributes，这里调用是安全的
            bool started = _mover.TryStepTo(_to, () => finished = true);

            if (!started)
                yield break;

            while (!finished)
                yield return null;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Units;
using Core.Hex;

namespace Game.Battle.Actions
{
    /// <summary>
    /// 一个简单的 Action，用于驱动 UnitMover 沿着整条路径移动 (FollowPath)
    /// </summary>
    public class PathMoveAction : IAction
    {
        private readonly UnitMover _mover;
        private readonly List<HexCoords> _path;

        public PathMoveAction(UnitMover mover, List<HexCoords> path)
        {
            _mover = mover;
            _path = path;
        }

        // 有效性检查：Mover存在、不在移动中、路径有效
        public bool IsValid =>
            _mover != null &&
            !_mover.IsMoving &&
            _path != null &&
            _path.Count > 0;

        public IEnumerator Execute()
        {
            bool finished = false;

            // 调用我们之前在 UnitMover 里新增的 FollowPath 方法
            _mover.FollowPath(_path, () => finished = true);

            // 等待回调完成
            while (!finished)
                yield return null;
        }
    }
}
// Script/Game/Battle/Actions/ActionQueue.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.Actions
{
    public class ActionQueue : MonoBehaviour
    {
        private readonly Queue<IAction> _queue = new();

        public void Enqueue(IAction action)
        {
            if (action != null) _queue.Enqueue(action);
        }

        public IEnumerator RunAll()
        {
            while (_queue.Count > 0)
            {
                var a = _queue.Dequeue();
                if (a.IsValid)
                    yield return a.Execute();
            }
        }

        public void Clear() => _queue.Clear();
        public bool IsEmpty => _queue.Count == 0;
    }
}

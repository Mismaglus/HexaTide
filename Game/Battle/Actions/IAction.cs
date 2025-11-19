// Script/Game/Battle/Actions/IAction.cs
using System.Collections;

namespace Game.Battle.Actions
{
    public interface IAction
    {
        IEnumerator Execute();
        bool IsValid { get; }
    }
}

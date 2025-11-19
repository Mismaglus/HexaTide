// Script/Game/Battle/Abilities/AbilityContext.cs
using System.Collections.Generic;
using Core.Hex;
using Game.Battle;

namespace Game.Battle.Abilities
{
    public class AbilityContext
    {
        public BattleUnit Caster;
        public HexCoords Origin;
        public List<BattleUnit> TargetUnits = new();
        public List<HexCoords> TargetTiles = new();

        public bool HasAnyTarget => TargetUnits.Count > 0 || TargetTiles.Count > 0;
    }
}

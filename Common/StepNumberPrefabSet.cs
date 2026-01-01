using UnityEngine;

namespace Game.Common
{
    [CreateAssetMenu(menuName = "HexaTide/UI/Step Number Prefab Set", fileName = "StepNumberPrefabSet")]
    public class StepNumberPrefabSet : ScriptableObject
    {
        [Tooltip("Index 0-9 maps to the prefab for that digit.")]
        public GameObject[] digits = new GameObject[10];

        public bool TryGetDigitPrefab(int digit, out GameObject prefab)
        {
            prefab = null;
            if (digits == null || digit < 0 || digit >= digits.Length) return false;
            prefab = digits[digit];
            return prefab != null;
        }
    }
}

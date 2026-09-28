using UnityEngine;

namespace TrickalFanGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyFloorLevel : MonoBehaviour
    {
        [SerializeField, Min(1)] private int floorNumber = 1;

        public int FloorNumber => floorNumber;

        public void SetFloor(int configuredFloorNumber)
        {
            floorNumber = Mathf.Max(1, configuredFloorNumber);
        }

        public static void Apply(GameObject enemy, int floorNumber)
        {
            if (enemy == null)
            {
                return;
            }

            EnemyFloorLevel level = enemy.GetComponent<EnemyFloorLevel>();
            if (level == null)
            {
                level = enemy.AddComponent<EnemyFloorLevel>();
            }

            level.SetFloor(floorNumber);
        }

        public static int Resolve(GameObject source)
        {
            EnemyFloorLevel level = source != null ? source.GetComponent<EnemyFloorLevel>() : null;
            return level != null ? level.FloorNumber : 1;
        }
    }
}

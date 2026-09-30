using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [CreateAssetMenu(fileName = "ObstacleVariant", menuName = "Trickal Fan Game/Obstacle Variant")]
    public sealed class ObstacleVariantDefinition : ScriptableObject
    {
        [SerializeField] private string variantId;
        [SerializeField, Min(1)] private int requiredHits = DestructibleObstacle.DefaultRequiredHits;
        [SerializeField] private ResourceDropTable dropTable;
        [SerializeField] private Color intactColor = new(0.62f, 0.45f, 0.3f);
        [SerializeField] private Color crackedColor = new(0.3f, 0.2f, 0.14f);

        public string VariantId => variantId;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public ResourceDropTable DropTable => dropTable;
        public Color IntactColor => intactColor;
        public Color CrackedColor => crackedColor;

        public void Configure(string configuredVariantId, int configuredRequiredHits,
            ResourceDropTable configuredDropTable, Color configuredIntactColor, Color configuredCrackedColor)
        {
            variantId = configuredVariantId;
            requiredHits = Mathf.Max(1, configuredRequiredHits);
            dropTable = configuredDropTable;
            intactColor = configuredIntactColor;
            crackedColor = configuredCrackedColor;
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(variantId, "Obstacle variant", out error)) return false;
            if (requiredHits < 1)
            {
                error = $"Obstacle variant '{variantId}' needs at least one hit.";
                return false;
            }

            if (dropTable == null || !dropTable.TryValidate(out error))
            {
                error = $"Obstacle variant '{variantId}' needs a valid drop table. {error}";
                return false;
            }

            error = null;
            return true;
        }
    }
}

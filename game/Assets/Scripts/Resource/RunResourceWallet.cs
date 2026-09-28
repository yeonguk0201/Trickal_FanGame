using System;

namespace TrickalFanGame.Resource
{
    // Run-scoped exploration resources. Counts never exceed MaxCount; the part of a grant that does not fit is dropped.
    public sealed class RunResourceWallet
    {
        public const int MaxCount = 99;

        private static readonly RunResourceType[] AllTypes =
            (RunResourceType[])Enum.GetValues(typeof(RunResourceType));

        private readonly int[] counts = new int[AllTypes.Length];

        public event Action<RunResourceType, int> Changed;

        public static bool IsDefined(RunResourceType type)
        {
            return Enum.IsDefined(typeof(RunResourceType), type);
        }

        public int GetCount(RunResourceType type)
        {
            return IsDefined(type) ? counts[(int)type] : 0;
        }

        public bool CanAccept(RunResourceType type)
        {
            return IsDefined(type) && counts[(int)type] < MaxCount;
        }

        // Returns the amount actually granted: 0 when the resource is full, less than amount when it fills up.
        public int Add(RunResourceType type, int amount)
        {
            if (amount <= 0 || !CanAccept(type))
            {
                return 0;
            }

            int index = (int)type;
            int granted = Math.Min(amount, MaxCount - counts[index]);
            counts[index] += granted;
            Changed?.Invoke(type, counts[index]);
            return granted;
        }

        public void Clear()
        {
            foreach (RunResourceType type in AllTypes)
            {
                int index = (int)type;
                if (counts[index] == 0)
                {
                    continue;
                }

                counts[index] = 0;
                Changed?.Invoke(type, 0);
            }
        }
    }
}

using System;

namespace CapstoneDesign.Runtime.LocalState
{
    // Approved product amounts for future commands. Completed sessions and
    // purchase receipts retain their historical values and command identities.
    public static class GardenEconomyPolicy
    {
        public const int BasicMissionCoins=50;
        public const int AdditionalMissionCoins=30;
        public const int EmotionRecordCoins=100;
        public const int PhotoBonusCoins=10;
        public const int MiniFlowerBedCoins=400;
        public const int RestCornerCoins=1000;

        public static int PracticeReward(ActivitySession session)
        {
            if(session==null)throw new ArgumentNullException(nameof(session));
            // Use the practice role, including a P-course alternative that
            // consumes the additional slot. Legacy sessions use their typed ID.
            return FocusMissionPolicy.RoleFor(session)==FocusMissionCatalog.AdditionalRole
                ?AdditionalMissionCoins:BasicMissionCoins;
        }

        public static int EnvironmentPrice(string environmentId,int legacyPrice)
        {
            switch(environmentId)
            {
                case "environment:E01":return MiniFlowerBedCoins;
                case "environment:E02":return RestCornerCoins;
                // Unlisted legacy catalog entries retain their previous policy.
                default:return legacyPrice;
            }
        }
    }
}

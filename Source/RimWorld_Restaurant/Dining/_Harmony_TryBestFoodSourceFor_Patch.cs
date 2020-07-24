using HarmonyLib;
using RimWorld;
using Verse;

namespace Restaurant.Dining
{
    /// <summary>
    /// For allowing pawns to find DiningSpots when hungry. This should later be replaced with BestFoodSourceOnMap, so alternatives are considered
    /// </summary>
    internal static class _Harmony_TryBestFoodSourceFor_Patch
    {
        [HarmonyPatch(typeof(FoodUtility), "TryFindBestFoodSourceFor")]
        public class TryFindBestFoodSourceFor
        {
            [HarmonyPrefix]
            internal static bool Prefix(Pawn getter, Pawn eater, ref bool __result, ref Thing foodSource, ref ThingDef foodDef, ref bool desperate)
            {
                if (desperate) return true; // Run original code

                if (getter != eater)
                {
                    //Log.Message($"{getter?.NameShortColored} != {eater?.NameShortColored}.");
                    return true; // Run original code
                }

                // Only if time assignment allows
                if (!eater.GetTimeAssignment().allowJoy) return true;

                bool canManipulate = getter.RaceProps.ToolUser && getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
                if (!canManipulate)
                {
                    return true; // Run original code
                }

                // Can't talk? Can't order.
                if (!getter.health.capacities.CapableOf(PawnCapacityDefOf.Talking)) return true;

                bool allowDrug = !eater.IsTeetotaler();
                var diningSpot = DiningUtility.FindDiningSpotFor(getter, out foodDef, allowDrug);

                if (diningSpot != null)
                {
                    foodSource = diningSpot;
                    //Log.Message($"{getter.NameShortColored} found diningSpot at {diningSpot.Position} with {foodDef?.label}.");
                    __result = true;
                    return false; // Don't run original code
                }

                return true; // Run original code
            }
        }
    }
}
using HarmonyLib;
using Restaurant.Dining;
using Verse;

namespace Restaurant.Harmony {
    /// <summary>
    /// So DiningSpot is not checked by Nutrient Paste Dispenser alerts
    /// </summary>
    internal static class ThingDef_Patch
    {
        [HarmonyPatch(typeof(ThingDef), "get_IsFoodDispenser")]
        public class IsFoodDispenser
        {
            [HarmonyPostfix]
            internal static void Postfix(ThingDef __instance, ref bool __result)
            {
                if (!__result) return;
                if (typeof(DiningSpot).IsAssignableFrom(__instance.thingClass)) __result = false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CashRegister;
using Gastronomy.Dining;
using Gastronomy.Waiting;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Gastronomy.Restaurant
{
    public static class RestaurantUtility
    {
        private static readonly Dictionary<Pair<Pawn, Region>, bool> dangerousRegionsCache = new Dictionary<Pair<Pawn, Region>, bool>();

        public static void OnTick()
        {
            if(GenTicks.TicksGame > lastTick)
                if (GenTicks.TicksGame % GenTicks.TickRareInterval == 0)
                {
                    // RARE TICK
                    dangerousRegionsCache.Clear();
                    lastTick = GenTicks.TicksGame;
                }
        }

        private static int lastTick;

        public static bool HasDiningQueued(this Pawn patron)
        {
            if (patron?.CurJobDef == DiningDefOf.Gastronomy_Dine) return true;
            return patron?.jobs.jobQueue?.Any(j => j.job.def == DiningDefOf.Gastronomy_Dine) == true;
        }

        public static RestaurantsManager GetRestaurantsManager(this Thing thing)
        {
            return thing.Map.GetComponent<RestaurantsManager>();
        }

        public static List<RestaurantController> GetAllRestaurants(this Thing thing)
        {
            return thing.Map.GetComponent<RestaurantsManager>().restaurants;
        }

        public static IEnumerable<RestaurantController> GetAllRestaurantsEmployed(this Pawn pawn)
        {
            return pawn.Map.GetComponent<RestaurantsManager>().restaurants.Where(r=>r.HasToWork(pawn));
        }

        public static RestaurantsManager GetRestaurantsManager(this RestaurantController restaurant)
        {
            return restaurant.Map.GetComponent<RestaurantsManager>();
        }

        public static RestaurantController GetRestaurant([NotNull]this Pawn pawn)
        {
            // This depends on a lot of cases... could be a waiter or a patron, etc. oof.
            var driver = pawn.jobs.curDriver;
            if(driver == null) Log.Error($"{pawn.Name.ToStringShort} doesn't have a driver but tries to GetRestaurant.");
            else
                switch (driver)
                {
                    case JobDriver_EmptyRegister jobDriverEmptyRegister:
                        break;
                    case JobDriver_Dine jobDriverDine:
                        var diningSpot = jobDriverDine.DiningSpot;
                        var order = diningSpot?.GetRestaurants().Select(r => r.Orders.GetOrderFor(pawn))
                            .FirstOrDefault(o => o != null);
                        break;
                    case JobDriver_MakeTable jobDriverMakeTable:
                        break;
                    case JobDriver_Serve jobDriverServe:
                        break;
                    case JobDriver_StandBy jobDriverStandBy:
                        throw new Exception("Pawn should get all restaurants");
                        return pawn.GetAllRestaurantsEmployed().FirstOrDefault();
                    case JobDriver_TakeOrder jobDriverTakeOrder:
                        break;
                }
            return null;
        }

        public static RestaurantController GetRestaurant([NotNull]this Building_CashRegister register)
        {
            return register.GetRestaurantsManager().GetLinkedRestaurant(register);
        }

        public static void GetRequestGroup(Thing thing)
        {
            foreach (ThingRequestGroup group in Enum.GetValues(typeof(ThingRequestGroup)))
            {
                if (@group == ThingRequestGroup.Undefined) continue;
                if (thing.Map.listerThings.ThingsInGroup(@group).Contains(thing))
                    Log.Message($"DiningSpot group: {@group}");
            }
        }

        public static bool IsRegionDangerous(Pawn pawn, Danger maxDanger, Region region = null)
        {
            region ??= pawn.GetRegion();
            var key = new Pair<Pawn, Region>(pawn, region);
            if (dangerousRegionsCache.TryGetValue(key, out bool result)) return result;

            var isRegionDangerous = region.DangerFor(pawn) > maxDanger;
            dangerousRegionsCache.Add(key, isRegionDangerous);

            return isRegionDangerous;
        }

        public static bool IsGuest(this Pawn pawn)
        {
            var faction = pawn.GetLord()?.faction;
            if (pawn.IsPrisoner) return false;
            //Log.Message($"{pawn.NameShortColored}: Faction = {faction?.GetCallLabel()} Is player = {faction?.IsPlayer} Hostile = {faction?.HostileTo(Faction.OfPlayer)}");
            return faction is {IsPlayer: false} && !faction.HostileTo(Faction.OfPlayer);
            //var isGuest = AccessTools.Method("Hospitality.GuestUtility:IsGuest");
            //Log.Message($"isGuest == null? {isGuest == null}");
            //if(isGuest != null)
            //{
            //    return (bool) isGuest.Invoke(null, new object[] {pawn, false});
            //}
            //return false;
        }

        public static int GetSilver(this Pawn pawn)
        {
            if (pawn?.inventory?.innerContainer == null) return 0;
            return pawn.inventory.innerContainer.Where(s => s.def == ThingDefOf.Silver).Sum(s => s.stackCount);
        }

        public static float GetPrice(this ThingDef mealDef, RestaurantController restaurant)
        {
            if (mealDef == null) return 0;
            return mealDef.BaseMarketValue * 0.6f * restaurant.guestPricePercentage * (1 - Find.Storyteller.difficulty.tradePriceFactorLoss);
        }

        public static T FailOnRestaurantsClosed<T>(this T f, TargetIndex spotInd) where T : IJobEndable
        {
            JobCondition OnRestaurantClosed()
            {
                var target = f.GetActor().CurJob.GetTarget(spotInd);
                var spot = target.IsValid ? target.Thing as DiningSpot : null;
                if (spot == null) return JobCondition.Errored;
                return spot.GetRestaurants().Any(r=>r.IsOpenedRightNow)
                    ? JobCondition.Ongoing
                    : JobCondition.Incompletable;
            }

            f.AddEndCondition(OnRestaurantClosed);
            return f;
        }

        public static T FailOnHasShift<T>(this T f) where T : IJobEndable
        {
            JobCondition HasShift()
            {
                var pawn = f.GetActor();
                return pawn.GetAllRestaurantsEmployed().Any(r=>r.ActiveStaff.Contains(pawn)) ? JobCondition.Incompletable : JobCondition.Ongoing;
            }

            f.AddEndCondition(HasShift);
            return f;
        }

        public static T FailOnNotDining<T>(this T f, TargetIndex patronInd) where T : IJobEndable
        {
            JobCondition PatronIsNotDining()
            {
                var patron = f.GetActor().jobs.curJob.GetTarget(patronInd).Thing as Pawn;
                if (patron?.jobs.curDriver is JobDriver_Dine) return JobCondition.Ongoing;
                Log.Message($"Checked {patron?.NameShortColored}. Not dining >> failing {f.GetActor().NameShortColored}'s job {f.GetActor().CurJobDef?.label}.");
                return JobCondition.Incompletable;
            }

            f.AddEndCondition(PatronIsNotDining);
            return f;
        }

        public static T FailOnNotDiningQueued<T>(this T f, TargetIndex patronInd) where T : IJobEndable
        {
            JobCondition PatronHasNoDiningInQueue()
            {
                var patron = f.GetActor().jobs.curJob.GetTarget(patronInd).Thing as Pawn;
                if (patron.HasDiningQueued()) return JobCondition.Ongoing;
                Log.Message($"Checked {patron?.NameShortColored}. Not planning to dine >> failing {f.GetActor().NameShortColored}'s job {f.GetActor().CurJobDef?.label}.");
                return JobCondition.Incompletable;
            }

            f.AddEndCondition(PatronHasNoDiningInQueue);
            return f;
        }

        /// <summary>
        /// Find a valid order at any restaurant
        /// </summary>
        [CanBeNull]
        public static Order FindValidOrder(this Pawn patron)
        {
            if(patron == null)
            {
                Log.Warning("Patron not set.");
                return null;
            }

            return patron.GetAllRestaurants().Select(r => r.Orders.GetOrderFor(patron)).FirstOrDefault(o => o != null); }
    }
}

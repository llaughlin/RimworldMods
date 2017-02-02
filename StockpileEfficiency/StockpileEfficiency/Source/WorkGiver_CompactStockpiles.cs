using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace StockpileEfficiency
{
    public class WorkGiver_CompactStockpiles : WorkGiver_Haul
    {
        private readonly Dictionary<Pawn, Job> _HaulingJobs = new Dictionary<Pawn, Job>();
        private int _LastTick;


        public override int LocalRegionsToScanFirst
            => LogProperty("LocalRegionsToScanFirst", base.LocalRegionsToScanFirst);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override ThingRequest PotentialWorkThingRequest
            => LogProperty("PotentialWorkThingRequest", ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));

        public override bool Prioritized
            => LogProperty("Prioritized", base.Prioritized);

        //public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        //{
        //    return base.PotentialWorkCellsGlobal(pawn);
        //    //Debug.Log($"PotentialWorkCellsGlobal {new { pawn }}");
        //    //var stockpiles = pawn.Map.zoneManager.AllZones.OfType<Zone_Stockpile>();
        //    //foreach (var stockpile in stockpiles)
        //    //{
        //    //    //Debug.Log($"Checking stockpike {stockpile}");
        //    //    var stockpileThings = stockpile.AllContainedThings.Where(t => t.def.EverHaulable);

        //    //    var grouped = stockpileThings.GroupBy(t => t.def);
        //    //    foreach (var group in grouped)
        //    //    {
        //    //        foreach (var thing in group)
        //    //        {
        //    //            var thingDef = thing.def;
        //    //            if (IsThingCompactible(thing)) continue;
        //    //            //Debug.Log($"Potential compacting at {thing.Position} of type {thingDef}: only has {thing.stackCount}/{thingDef.stackLimit}");
        //    //            yield return thing.Position;
        //    //        }
        //    //    }
        //    //}
        //}

        private static bool LogTick => Find.TickManager.TicksGame % 10 == 0;
        private TickManager TickManager => Find.TickManager;

        public override float GetPriority(Pawn pawn, TargetInfo targetInfo)
        {
            var result = base.GetPriority(pawn, targetInfo);
            //Debug.Log($"GetPriority {new { pawn, targetInfo, result }}");
            return result;
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 cell)
        {
            var result = base.HasJobOnCell(pawn, cell);
            //Debug.Log($"HasJobOnCell {new { pawn, cell, result }}");
            return result;
        }


        public override bool HasJobOnThingForced(Pawn pawn, Thing thing)
        {
            var result = base.HasJobOnThingForced(pawn, thing);
            //Debug.Log($"HasJobOnThingForced {new { pawn, thing, result }}");
            return result;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell)
        {
            var result = base.JobOnCell(pawn, cell);
            //Debug.Log($"JobOnCell {new { pawn, cell, result }}");
            return result;
        }

        public override Job JobOnThingForced(Pawn pawn, Thing thing)
        {
            var result = base.JobOnThingForced(pawn, thing);
            //Debug.Log($"JobOnThingForced {new { pawn, thing, result }}");
            return result;
        }

        public override Job NonScanJob(Pawn pawn)
        {
            var result = base.NonScanJob(pawn);
            //Debug.Log($"NonScanJob {new { pawn, result }}");
            return result;
        }

        //public override bool HasJobOnThing(Pawn pawn, Thing thing)
        //{
        //    //Debug.Log($"HasJobOnThing {new { pawn, thing }}");

        //    //Debug.Log($"Pawn {pawn} intervalTick(10)? {pawn.IsHashIntervalTick(10)}");
        //    //if (!pawn.IsHashIntervalTick(10)) return false;

        //    return JobOnThing(pawn, thing) != null;

        //    if (!CanHaul(pawn, thing)) return false;

        //    if (!IsThingCompactible(thing)) return false;

        //    var otherThing = FindThingToStackOnto(thing);

        //    if (otherThing == null) return false;

        //    return CanHaul(pawn, thing, otherThing, true);
        //}
        //public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        //{
        //    return pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling();
        //}

        public override bool ShouldSkip(Pawn pawn)
        {
            return pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling().Count == 0;
        }

        public bool CanHaul(Pawn pawn, Thing target, Thing destination = null, bool logValues = false)
        {
            var canHaulTarget = HaulAIUtility.PawnCanAutomaticallyHaul(pawn, target);
            if (!canHaulTarget) return false;

            var canHaulToDestination = true;

            if (destination != null)
                canHaulToDestination = HaulablePlaceValidator(target, pawn, destination);

            //if (logValues || LogTick) Debug.Log($"Pawn {pawn} can haul {target}={canHaulTarget} to {destination}={canHaulDestination}: {canHaulTarget && canHaulDestination}");

            return canHaulTarget && canHaulToDestination;
        }

        public static Thing HaulPlaceBlockerIn(Thing haulThing, IntVec3 c, Map map, bool checkBlueprints)
        {
            var thingList = map.thingGrid.ThingsListAt(c);
            foreach (var thing in thingList)
            {
                if (checkBlueprints && thing.def.IsBlueprint ||
                    (thing.def.category != ThingCategory.Plant || thing.def.passability != Traversability.Standable) &&
                    thing.def.category != ThingCategory.Filth &&
                    (haulThing == null || thing.def.category != ThingCategory.Item || !thing.CanStackWith(haulThing)) &&
                    (thing.def.EverHaulable || haulThing != null && GenSpawn.SpawningWipes(haulThing.def, thing.def) ||
                     thing.def.passability != Traversability.Standable && thing.def.surfaceType != SurfaceType.Item))
                    return thing;
            }
            return null;
        }

        private static bool HaulablePlaceValidator(Thing target, Pawn pawn, Thing destination)
        {
            return StoreUtility.IsGoodStoreCell(destination.Position, pawn.Map, target, pawn, pawn.Faction);
            var destinationCell = destination.Position;
            var canReserveDestination = pawn.CanReserveAndReach(destinationCell, PathEndMode.OnCell, pawn.NormalMaxDanger());
            var cantReserveDestination = !pawn.CanReserveAndReach(destinationCell, PathEndMode.OnCell, pawn.NormalMaxDanger());
            var haulBlocked = HaulPlaceBlockerIn(target, destinationCell, pawn.Map, true) != null;
            var notStandable = !destinationCell.Standable(pawn.Map);
            var samePosition = destinationCell == target.Position && target.Spawned;
            var plantingBlocked = target != null && target.def.BlockPlanting && pawn.Map.zoneManager.ZoneAt(destinationCell) is Zone_Growing;
            var edifice = destinationCell.GetEdifice(pawn.Map);
            var isTrap = edifice is Building_Trap;

            if (cantReserveDestination || haulBlocked || notStandable || samePosition || plantingBlocked || isTrap)
                if (canReserveDestination)
                {
                    Log.Warning(
                        $"{pawn} can reserve destination, but failed hauling validator: {new { canReserveDestination, cantReserveDestination, haulBlocked, notStandable, samePosition, plantingBlocked, isTrap }}");
                    return false;
                }
            if (target.def.passability != Traversability.Standable)
                for (var index = 0; index < 8; ++index)
                {
                    var c1 = destinationCell + GenAdj.AdjacentCells[index];
                    if (pawn.Map.designationManager.DesignationAt(c1, DesignationDefOf.Mine) != null)
                        return false;
                }
            return true;
        }

        private bool IsThingCompactible(Thing thing)
        {
            var thingDef = thing.def;
            //Debug.Log($"Checking thing type {thingDef}");
            if (!thingDef.EverHaulable)
            {
                if (LogTick) Debug.Log($"Thingdef {thingDef} not haulable, skipping");
                return false;
            }
            if (thing.stackCount >= thingDef.stackLimit)
                return false;
            var stockpile = thing.Map.zoneManager.ZoneAt(thing.Position) as Zone_Stockpile;
            if (stockpile == null)
                return false;

            //if (LogTick) Debug.Log($"Potential compacting at {thing.Position} in {stockpile} of type {thingDef}: only has {thing.stackCount}/{thingDef.stackLimit}");
            return true;
        }

        public Thing FindThingToStackOnto(Pawn pawn, Thing thisThing)
        {
            var stockpile = thisThing.Map.zoneManager.ZoneAt(thisThing.Position) as Zone_Stockpile;
            if (stockpile == null)
                return null;
            //Debug.Log($"Thing {target} is in valid stockpile");
            var otherThings = stockpile.AllContainedThings
                    .Where(other => other.thingIDNumber != thisThing.thingIDNumber)
                    .Where(thisThing.CanStackWith)
                    .Where(other => other.stackCount < other.def.stackLimit)
                    .Where(other => other.stackCount >= thisThing.stackCount)
                    .Where(other => CanHaul(pawn, thisThing, other))
                //.OrderByDescending(other => other.stackCount)
                ;

            return otherThings.Any() ? otherThings.RandomElement() : null;

            //Debug.Log($"Found thing to stack onto: {otherThing} has {otherThing.stackCount}/{otherThing.def.stackLimit}");
        }

        public override bool HasJobOnThing(Pawn pawn, Thing target)
        {
            var job = JobOnThing(pawn, target);
            return job != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing target)
        {
            if (_LastTick != TickManager.TicksGame)
            {
                Debug.Log("New tick, clearing pawn list");
                _HaulingJobs.Clear();
                _LastTick = TickManager.TicksGame;
            }

            if (_HaulingJobs.TryGetValue(pawn, out Job cachedJob))
            {
                var jobTarget = cachedJob.targetA;
                if (jobTarget == target)
                {
                    Debug.Log($"Found cached job for {pawn} hauling {target.stackCount} {target.def.defName}");
                    return cachedJob;
                }
                return null;
            }
            //Debug.Log($"JobOnThing  {new { pawn, target }}");
            if (!CanHaul(pawn, target))
                return null;

            if (!IsThingCompactible(target))
                return null;
            var destination = FindThingToStackOnto(pawn, target);
            if (destination == null)
                return null;


            var stockpile = target.Map.zoneManager.ZoneAt(target.Position) as Zone_Stockpile;
            //Log.Message($"Found cell to haul {target.stackCount} of {target} to : {foundStackableThing.Position} in zone {stockpile}, having {foundStackableThing.stackCount}/{foundStackableThing.def.stackLimit}");


            if (!CanHaul(pawn, target, destination))
                return null;

            // reserve target and destination
            if (!CanHaul(pawn, target, destination) || !pawn.Reserve(target))
            {
                Debug.LogWarning($"{pawn} failed to reserve target {target.stackCount} {target}");
                pawn.ClearReservations();
                return null;
            }
            if (!CanHaul(pawn, target, destination) || !pawn.Reserve(destination))
            {
                Debug.LogWarning($"{pawn} failed to reserve destination {destination.stackCount} {destination}");
                pawn.ClearReservations();
                return null;
            }
            if (!CanHaul(pawn, target, destination))
            {
                Debug.LogWarning($"{pawn} can't haul {target.stackCount} {target.def.defName} to {destination.stackCount} {destination} after reserving");
                pawn.ClearReservations();
                return null;
            }
            var job = HaulAIUtility.HaulMaxNumToCellJob(pawn, target, destination.Position, false);

            _HaulingJobs.Add(pawn, job);

            Log.Message(
                $"{pawn} hauling {target.stackCount} of {target} to {destination.Position} in zone {stockpile}, having {destination.stackCount}/{destination.def.stackLimit}");
            return job;
        }

        private string _getJobKey(Pawn pawn, Thing target)
        {
            return $"{pawn.ThingID}-{target.ThingID}";
        }

        private static T LogProperty<T>(string propertyName, T property)
        {
            //Debug.Log($"{propertyName}={property}");
            return property;
        }
    }
}
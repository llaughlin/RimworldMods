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
        private int _LastTick = 0;
        private readonly Dictionary<Pawn, Job> _HaulingJobs = new Dictionary<Pawn, Job>();
        private TickManager TickManager => Find.TickManager;

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
            {
                canHaulToDestination = HaulablePlaceValidator(target, pawn, destination);
            }

            //if (logValues || LogTick) Debug.Log($"Pawn {pawn} can haul {target}={canHaulTarget} to {destination}={canHaulDestination}: {canHaulTarget && canHaulDestination}");

            return canHaulTarget && canHaulToDestination;
        }

        private static bool HaulablePlaceValidator(Thing haulable, Pawn worker, Thing destination)
        {
            var destinationCell = destination.Position;
            var canReserveDestination = worker.CanReserveAndReach(destinationCell, PathEndMode.OnCell, worker.NormalMaxDanger());
            var cantReserveDestination = !worker.CanReserveAndReach(destinationCell, PathEndMode.OnCell, worker.NormalMaxDanger());
            var haulBlocked = GenPlace.HaulPlaceBlockerIn(haulable, destinationCell, worker.Map, true) != null;
            if (haulBlocked && destination.def.stackLimit - destination.stackCount >= haulable.stackCount)
            {
                haulBlocked = false;
            }
            var notStandable = !destinationCell.Standable(worker.Map);
            var samePosition = destinationCell == haulable.Position && haulable.Spawned;
            var plantingBlocked = haulable != null && haulable.def.BlockPlanting && worker.Map.zoneManager.ZoneAt(destinationCell) is Zone_Growing;
            Building edifice = destinationCell.GetEdifice(worker.Map);
            var isTrap = edifice is Building_Trap;

            if (cantReserveDestination || haulBlocked || notStandable || samePosition || plantingBlocked || isTrap)
            {

                if (canReserveDestination)
                {
                    Log.Warning($"{worker} can reserve destination, but failed hauling validator: {new { canReserveDestination, cantReserveDestination, haulBlocked, notStandable, samePosition, plantingBlocked, isTrap }}");
                    return false;
                }
            }
            if (haulable.def.passability != Traversability.Standable)
            {
                for (int index = 0; index < 8; ++index)
                {
                    IntVec3 c1 = destinationCell + GenAdj.AdjacentCells[index];
                    if (worker.Map.designationManager.DesignationAt(c1, DesignationDefOf.Mine) != null)
                        return false;
                }
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
            {
                //Debug.Log($"Thing {thing} at thingdef {thingDef} stacklimit of {thingDef.stackLimit}");
                return false;
            }
            var stockpile = thing.Map.zoneManager.ZoneAt(thing.Position) as Zone_Stockpile;
            if (stockpile == null)
            {
                //Debug.Log($"{thing.Position} not a valid stockpile, ignoring");
                return false;
            }

            //if (LogTick) Debug.Log($"Potential compacting at {thing.Position} in {stockpile} of type {thingDef}: only has {thing.stackCount}/{thingDef.stackLimit}");
            return true;
        }

        public Thing FindThingToStackOnto(Pawn pawn, Thing thisThing)
        {
            var stockpile = thisThing.Map.zoneManager.ZoneAt(thisThing.Position) as Zone_Stockpile;
            if (stockpile == null)
            {
                //Log.Warning($"{target.Position} not a valid stockpile, ignoring");
                return null;
            }
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
            if (job == null) return false;

            var jobTarget = job.targetA;
            var jobDestination = job.targetB;
            return jobTarget == target;
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
                return cachedJob;
            }
            //Debug.Log($"JobOnThing  {new { pawn, target }}");
            if (!CanHaul(pawn, target))
            {
                return null;
            }

            if (!IsThingCompactible(target))
            {
                return null;
            }
            var destination = FindThingToStackOnto(pawn, target);
            if (destination == null)
            {
                //Log.Message($"Didn't find any cell to haul to. {target} at {target.Position}");
                return null;
            }


            var stockpile = target.Map.zoneManager.ZoneAt(target.Position) as Zone_Stockpile;
            //Log.Message($"Found cell to haul {target.stackCount} of {target} to : {foundStackableThing.Position} in zone {stockpile}, having {foundStackableThing.stackCount}/{foundStackableThing.def.stackLimit}");


            if (!CanHaul(pawn, target, destination))
            {
                return null;
            }

            //if (!HaulablePlaceValidator(target, pawn, destination.Position))
            //{
            //    Debug.Log($"{pawn} failed validator hauling {target.stackCount} {target} to {destination.stackCount} {destination}@{destination.Position} ");
            //    return null;
            //}

            // reserve target and destination
            if (!pawn.CanReserveAndReach(target, PathEndMode.ClosestTouch, pawn.NormalMaxDanger()) || !pawn.Reserve(target))
            {
                Debug.LogWarning($"{pawn} failed to reserve target {target.stackCount} {target}");
                pawn.ClearReservations();
                return null;
            }
            if (!pawn.CanReserveAndReach(destination, PathEndMode.OnCell, pawn.NormalMaxDanger()) || !pawn.Reserve(destination))
            {
                Debug.LogWarning($"{pawn} failed to reserve destination {destination.stackCount} {destination}");
                pawn.ClearReservations();
                return null;
            }
            var job = HaulAIUtility.HaulMaxNumToCellJob(pawn, target, destination.Position, true);

            _HaulingJobs.Add(pawn, job);

            Log.Message($"{pawn} hauling {target.stackCount} of {target} to {destination.Position} in zone {stockpile}, having {destination.stackCount}/{destination.def.stackLimit}");
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
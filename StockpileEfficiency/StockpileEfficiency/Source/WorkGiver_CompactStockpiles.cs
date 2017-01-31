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
            // Debug.Log($"GetPriority {new {pawn, targetInfo, result}}");
            return result;
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 cell)
        {
            var result = base.HasJobOnCell(pawn, cell);
            // Debug.Log($"HasJobOnCell {new {pawn, cell, result}}");
            return result;
        }


        public override bool HasJobOnThingForced(Pawn pawn, Thing thing)
        {
            var result = base.HasJobOnThingForced(pawn, thing);
            // Debug.Log($"HasJobOnThingForced {new {pawn, thing, result}}");
            return result;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell)
        {
            var result = base.JobOnCell(pawn, cell);
            // Debug.Log($"JobOnCell {new {pawn, cell, result}}");
            return result;
        }

        public override Job JobOnThingForced(Pawn pawn, Thing thing)
        {
            var result = base.JobOnThingForced(pawn, thing);
            // Debug.Log($"JobOnThingForced {new {pawn, thing, result}}");
            return result;
        }

        public override Job NonScanJob(Pawn pawn)
        {
            var result = base.NonScanJob(pawn);
            //Debug.Log($"NonScanJob {new { pawn, result }}");
            return result;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var result = base.PotentialWorkThingsGlobal(pawn)?.ToList();
            //Debug.Log($"PotentialWorkThingsGlobal {new { pawn, ResultCount = result?.Count }}");
            return result;
        }

        public override bool ShouldSkip(Pawn pawn)
        {
            //pawn.IsHashIntervalTick(50);
            var result = !pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Any();
            //Debug.Log($"ShouldSkip {new { pawn, result }}");
            return result;
        }

        public override int LocalRegionsToScanFirst
            => LogProperty("LocalRegionsToScanFirst", base.LocalRegionsToScanFirst);

        public override PathEndMode PathEndMode
            => LogProperty("PathEndMode", base.PathEndMode);

        public override ThingRequest PotentialWorkThingRequest
            => LogProperty("PotentialWorkThingRequest", ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));

        public override bool Prioritized
            => LogProperty("Prioritized", base.Prioritized);

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            //Debug.Log($"PotentialWorkCellsGlobal {new { pawn }}");
            var stockpiles = pawn.Map.zoneManager.AllZones.OfType<Zone_Stockpile>();
            foreach (var stockpile in stockpiles)
            {
                //Debug.Log($"Checking stockpike {stockpile}");
                var stockpileThings = stockpile.AllContainedThings.Where(t => t.def.EverHaulable);

                var grouped = stockpileThings.GroupBy(t => t.def);
                foreach (var group in grouped)
                {
                    foreach (var thing in group)
                    {
                        var thingDef = thing.def;
                        if (IsThingCompactible(thing)) continue;
                        //Debug.Log($"Potential compacting at {thing.Position} of type {thingDef}: only has {thing.stackCount}/{thingDef.stackLimit}");
                        yield return thing.Position;
                    }
                }
            }
        }

        private bool LogTick => false;//Find.TickManager.TicksGame % 10 == 0;

        public override bool HasJobOnThing(Pawn pawn, Thing thing)
        {
            //Debug.Log($"HasJobOnThing {new { pawn, thing }}");

            //Debug.Log($"Pawn {pawn} intervalTick(10)? {pawn.IsHashIntervalTick(10)}");
            //if (!pawn.IsHashIntervalTick(10)) return false;

            if (!CanHaul(pawn, thing)) return false;

            if (!IsThingCompactible(thing)) return false;

            var otherThing = FindThingToStackOnto(thing);

            if (otherThing == null) return false;

            if (CanHaul(pawn, thing, otherThing, true))
            // Reserve both source and destination to prevent other pawns from trying to haul either
            {
                pawn.Reserve(thing);
                pawn.Reserve(otherThing);
                return true;
            }
            return false;
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

            if (LogTick)
                Debug.Log($"Potential compacting at {thing.Position} in {stockpile} of type {thingDef}: only has {thing.stackCount}/{thingDef.stackLimit}");
            return true;
        }

        public Thing FindThingToStackOnto(Thing thisThing)
        {
            var stockpile = thisThing.Map.zoneManager.ZoneAt(thisThing.Position) as Zone_Stockpile;
            if (stockpile == null)
            {
                //Log.Warning($"{thisThing.Position} not a valid stockpile, ignoring");
                return null;
            }

            //Debug.Log($"Thing {thisThing} is in valid stockpile");
            var otherThing = stockpile.AllContainedThings
                .Where(other => other.thingIDNumber != thisThing.thingIDNumber)
                .Where(thisThing.CanStackWith)
                .Where(other => other.stackCount < other.def.stackLimit)
                .Where(other => other.stackCount >= thisThing.stackCount)
                //.OrderByDescending(other => other.stackCount)
                .FirstOrDefault();

            if (otherThing == null)
            {
                return null;
            }

            //Debug.Log($"Found thing to stack onto: {otherThing} has {otherThing.stackCount}/{otherThing.def.stackLimit}");
            return otherThing;
        }

        public override Job JobOnThing(Pawn pawn, Thing thisThing)
        {
            //Debug.Log($"JobOnThing  {new { pawn, thisThing }}");
            if (!pawn.CanReserve(thisThing)) return null;

            if (!IsThingCompactible(thisThing))
            {
                return null;
            }
            var foundStackableThing = FindThingToStackOnto(thisThing);
            if (foundStackableThing == null)
            {
                Log.Message($"Didn't find any cell to haul to. {thisThing} at {thisThing.Position}");
                return null;
            }
            if (!pawn.CanReserve(foundStackableThing)) return null;

            var stockpile = thisThing.Map.zoneManager.ZoneAt(thisThing.Position) as Zone_Stockpile;
            //Log.Message($"Found cell to haul {thisThing.stackCount} of {thisThing} to : {foundStackableThing.Position} in zone {stockpile}, having {foundStackableThing.stackCount}/{foundStackableThing.def.stackLimit}");


            var job = HaulAIUtility.HaulMaxNumToCellJob(pawn, thisThing, foundStackableThing.Position, true);


            return job;
        }

        public bool CanHaul(Pawn pawn, Thing target, Thing destination = null, bool logValues = false)
        {
            var canHaulTarget =
                (HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, target)
                    || HaulAIUtility.PawnCanAutomaticallyHaul(pawn, target)) && pawn.CanReserve(target);


            var canHaulDestination = true;

            if (destination != null)
            {
                canHaulDestination =
                    (HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, destination)
                        || HaulAIUtility.PawnCanAutomaticallyHaul(pawn, destination)) && pawn.CanReserve(target);
            }

            if (LogTick)
                Debug.Log($"Pawn {pawn} can haul {target}={canHaulTarget} to {destination}={canHaulDestination}: {canHaulTarget && canHaulDestination}");
            return canHaulTarget && canHaulDestination;
        }

        private static T LogProperty<T>(string propertyName, T property)
        {
            //Debug.Log($"{propertyName}={property}");
            return property;
        }
    }
}
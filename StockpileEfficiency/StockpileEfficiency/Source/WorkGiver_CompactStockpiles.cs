using System;
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
        private readonly Dictionary<Pawn, Job> _haulingJobs = new Dictionary<Pawn, Job>();

        private int _lastTick;

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);

        // Remove the `&& false` to log values on certain ticks
        private static bool LogTick => Find.TickManager.TicksGame % 10 == 0 && false;

        private static TickManager TickManager => Find.TickManager;

        public override bool ShouldSkip(Pawn pawn)
        {
            return pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling().Count == 0;
        }

        public bool CanHaul(Pawn pawn, Thing target, Thing destination = null, bool logValues = false)
        {
            // check if target thing isn't already reserved and can be hauled
            var canHaulTarget = HaulAIUtility.PawnCanAutomaticallyHaul(pawn, target);
            if (!canHaulTarget) return false;

            // default to true if no destination specified
            var canHaulToDestination = true;

            if (destination != null)
                // check destination cell to make sure it's not blocked or reserved
                canHaulToDestination = StoreUtility.IsGoodStoreCell(destination.Position, pawn.Map, target, pawn,
                    pawn.Faction);

            LogMessage(() => $"Pawn {pawn} can haul {target}={canHaulTarget} to {destination}={canHaulToDestination}: {canHaulTarget && canHaulToDestination}");

            return canHaulTarget && canHaulToDestination;
        }

        private bool IsThingCompactible(Thing thing)
        {
            var thingDef = thing.def;
            // shouldn't ever be true, but good to safeguard anyway
            if (!thingDef.EverHaulable)
            {
                LogMessage(() => $"Thingdef {thingDef} not haulable, skipping");
                return false;
            }
            // check if thing is already at max stack size
            if (thing.stackCount >= thingDef.stackLimit)
                return false;

            // make sure thing is in a stockpile
            var stockpile = thing.Map.zoneManager.ZoneAt(thing.Position) as Zone_Stockpile;
            if (stockpile == null)
                return false;

            LogMessage(() => $"Potential compacting at {thing.Position} in {stockpile} of type {thingDef}: only has {thing.stackCount}/{thingDef.stackLimit}");
            return true;
        }

        public Thing FindThingToStackOnto(Pawn pawn, Thing target)
        {
            var stockpile = target.Map.zoneManager.ZoneAt(target.Position) as Zone_Stockpile;
            if (stockpile == null)
                return null;

            LogMessage(() => $"Thing {target} is in valid stockpile");

            // check the stockpile for a thing that:
            var otherThings = stockpile.AllContainedThings
                    .Where(other => other.thingIDNumber != target.thingIDNumber) // isn't same as target
                    .Where(target.CanStackWith) // can stack with target
                    .Where(other => other.stackCount < other.def.stackLimit) // isn't full
                    .Where(other => other.stackCount >= target.stackCount) // isn't less full than target (always try to make bigger stacks)
                    .Where(other => CanHaul(pawn, target, other)) // isn't reserved, unreachable, etc
                                                                  //.OrderByDescending(other => other.stackCount)
                ;

            var destination = otherThings.FirstOrDefault();
            if (destination == null) return null;
            LogMessage(() => $"Found cell to haul {target.stackCount} of {target} to : {destination.Position} in zone {stockpile}, having {destination.stackCount}/{destination.def.stackLimit}");
            return destination;
        }

        public override Job JobOnThing(Pawn pawn, Thing target)
        {
            Job job = GetCachedJob(pawn, target);

            // verify the job we found is hauling our target
            if (job != null)
            {
                if (job.targetA == target)
                    return job;
                return null;
            }

            if (!CanHaul(pawn, target))
                return null;

            if (!IsThingCompactible(target))
                return null;

            var destination = FindThingToStackOnto(pawn, target);
            if (destination == null)
                return null;


            // should always be true, but it's good to safeguard against collisions
            if (!CanHaul(pawn, target, destination))
                return null;


            job = HaulAIUtility.HaulMaxNumToCellJob(pawn, target, destination.Position, false);

            _haulingJobs.Add(pawn, job);

            LogMessage(() => $"{pawn} hauling {target.stackCount} of {target} to {destination.Position} in zone {target.Map.zoneManager.ZoneAt(target.Position) as Zone_Stockpile}, having {destination.stackCount}/{destination.def.stackLimit}");
            return job;
        }

        private Job GetCachedJob(Pawn pawn, Thing target)
        {
            // this is so the second time through, when the game asks for the job, we don't re-create the job, and we also only allow one compaction job per pawn.
            // it helps prevent reservation collisions as well as being more performant
            if (_lastTick != TickManager.TicksGame)
            {
                LogMessage(() => "New tick, clearing pawn list");
                _haulingJobs.Clear();
                _lastTick = TickManager.TicksGame;
            }
            if (!_haulingJobs.TryGetValue(pawn, out Job cachedJob)) return null;

            LogMessage(() => $"Found cached job for {pawn} hauling {target.stackCount} {target.def.defName}");
            return cachedJob;
        }

        private static T LogProperty<T>(string propertyName, T property)
        {
            //LogMessage($"{propertyName}={property}");
            return property;
        }

        private void LogMessage(Func<string> message)
        {
            if (LogTick)
                Debug.Log(message());
        }
    }
}
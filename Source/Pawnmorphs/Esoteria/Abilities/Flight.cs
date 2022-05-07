﻿using Pawnmorph.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Pawnmorph.Abilities
{
    internal class Flight : MutationAbility
    {
        private LocalTargetInfo? _target;
        Skyfallers.FlightSkyFaller _skyfaller;

        protected override MutationAbilityType Type => MutationAbilityType.Target;
        protected override TargetingParameters TargetParameters => new TargetingParameters()
        {
            canTargetLocations = true,
            validator = ((TargetInfo x) => DropCellFinder.IsGoodDropSpot(x.Cell, x.Map, allowFogged: true, canRoofPunch: false))
        };

        public Flight(MutationAbilityDef def) : base(def)
        {
        }

        protected override bool OnTryCast(LocalTargetInfo? target)
        {
            if (target == null || Pawn.Spawned == false)
                return false;

            if (Pawn.Position.Roofed(Pawn.Map))
            {
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "FailFly_Roofed".Translate());
                return false;
            }

            _target = target;
            return true;
        }

        protected override void OnInitialize()
        {
            if (_skyfaller != null)
            {
                _skyfaller.OnLanded += OnLanded;
            }
        }


        protected override void OnTick()
        {
            if (state == MutationAbilityState.Casting)
            {
                if (_target == null)
                {
                    state = MutationAbilityState.None;
                    return;
                }

                _skyfaller = new Skyfallers.FlightSkyFaller(_target.Value);
                _skyfaller.OnLanded += OnLanded;

                Map map = Pawn.Map;
                IntVec3 position = Pawn.Position;

                Pawn.DeSpawn();
                _skyfaller.innerContainer.TryAddOrTransfer(Pawn);
                _skyfaller.def.graphicData = null;

                GenSpawn.Spawn(_skyfaller, position, map);
                state = MutationAbilityState.Active;
            }
        }

        private void OnLanded(Skyfallers.FlightSkyFaller skyfaller)
        {
            GenSpawn.Spawn(Pawn, skyfaller.Position, skyfaller.Map);
            _skyfaller = null;
            StartCooldown();
        }

        protected override string OnIsDisabled()
        {
            float? lift = StatsUtility.GetStat(Pawn, PMStatDefOf.PM_Lift, 60);
            Log.Message("Checking if disabled: " + lift);
            if (lift.HasValue && lift >= 1.0f)
                return null;
            else
                return "FailFly_Lift".Translate();
        }
    }
}

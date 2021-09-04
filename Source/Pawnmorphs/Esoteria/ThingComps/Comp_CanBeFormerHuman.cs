﻿using Pawnmorph.DefExtensions;
using RimWorld;
using Verse;
using Verse.AI;

namespace Pawnmorph.ThingComps
{
    /// <summary>
    /// Comp for Pawn Things that can make their associated pawn a former human
    /// </summary>
    public abstract class Comp_CanBeFormerHuman : ThingComp, IMentalStateRecoveryReceiver
    {
        private bool triggered = false;

        private CompProperties_CanBeFormerHuman Props => props as CompProperties_CanBeFormerHuman;
        private Pawn Pawn => parent as Pawn;

        /// <summary>
        /// Called after the parent thing is spawned
        /// </summary>
        /// <param name="respawningAfterLoad">if set to <c>true</c> [respawning after load].</param>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (respawningAfterLoad) return;

            if (parent?.def?.IsChaomorph() == true)
                LessonAutoActivator.TeachOpportunity(PMConceptDefOf.Chaomorphs, OpportunityType.GoodToKnow);
        }

        /// <summary>
        /// Called every tick
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();

            // Make the animal a former human on the first tick rather than on spawning
            // TODO - figure out why we're doing it this way and whether there's a better spot to put it
            if (!triggered)
            {
                triggered = true;

                if (ShouldMakeFormerHuman())
                {
                    bool isManhunter = Pawn.MentalStateDef == MentalStateDefOf.Manhunter
                                    || Pawn.MentalStateDef == MentalStateDefOf.ManhunterPermanent;

                    float sapience = Rand.Value;
                    FormerHumanUtilities.MakeAnimalSapient((Pawn)parent, sapience, !isManhunter);
                    FormerHumanUtilities.NotifyRelatedPawnsFormerHuman((Pawn)parent,
                                                                       FormerHumanUtilities.RELATED_WILD_FORMER_HUMAN_LETTER,
                                                                       FormerHumanUtilities.RELATED_WILD_FORMER_HUMAN_LETTER_LABEL);
                }
            }
        }

        /// <summary>
        /// Whether or not this pawn can be a former human
        /// </summary>
        /// <returns><c>true</c>, if the pawn is eligable, <c>false</c> otherwise.</returns>
        private bool CanBeFormerHuman()
        {
            if (!PawnmorpherMod.Settings.enableWildFormers)
                return false;

            if (parent.def.GetModExtension<FormerHumanSettings>()?.neverFormerHuman == true)
                return false;

            // Don't make animals belonging to any faction former humans
            if (parent.Faction != null)
                return false;

            // Don't make animals with existing relationships to other animals former humans
            var pawn = Pawn;
            if (pawn.relations?.DirectRelations
                    .Any(r => r.def == PawnRelationDefOf.Child
                           || r.def == PawnRelationDefOf.Parent) ?? false)
                return false;

            // Don't let manhunters be former humans
            if (Pawn.MentalStateDef == MentalStateDefOf.Manhunter
             || Pawn.MentalStateDef == MentalStateDefOf.ManhunterPermanent)
                return false;

            // Make sure the animal is old enough to be a former human
            return TransformerUtility.ConvertAge(pawn, ThingDefOf.Human.race) > FormerHumanUtilities.MIN_FORMER_HUMAN_AGE;
        }

        /// <summary>
        /// Whether to make this pawn a former human or not
        /// </summary>
        /// <returns><c>true</c>, if the pawn should be made a former human, <c>false</c> otherwise.</returns>
        private bool ShouldMakeFormerHuman()
        {
            // Don't make a pawn former human twice
            if (Pawn.IsFormerHuman())
                return false;

            // Always-former-human animals skip the can-be check
            if (Props?.Always == true)
                return true;

            // Check if the animal is suitable to be a former human
            if (CanBeFormerHuman())
                return false;

            return Rand.Value < PawnmorpherMod.Settings.formerChance;
        }

        /// <summary>
        /// Exposes the comp data to be saved/loaded from XML
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref triggered, nameof(triggered));
        }

        /// <summary>
        /// Called when the pawn recovered from the given mental state.
        /// </summary>
        /// <param name="mentalState">State of the mental.</param>
        public void OnRecoveredFromMentalState(MentalState mentalState)
        {
            // Let former-human manhunters attempt to join the colony after they recover from manhunting
            if (mentalState.def == MentalStateDefOf.ManhunterPermanent || mentalState.def == MentalStateDefOf.Manhunter)
            {
                if (Pawn.Faction == null && Pawn.IsFormerHuman() && Pawn.IsRelatedToColonistPawn())
                {
                    Pawn.SetFaction(Faction.OfPlayer);
                }
            }
        }
    }
}
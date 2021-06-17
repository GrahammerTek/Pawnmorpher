﻿// DebugActions.cs created by Iron Wolf for Pawnmorph on 03/18/2020 1:42 PM
// last updated 03/18/2020  1:42 PM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Pawnmorph.Chambers;
using Pawnmorph.Hediffs;
using Pawnmorph.Jobs;
using Pawnmorph.Social;
using Pawnmorph.TfSys;
using Pawnmorph.ThingComps;
using Pawnmorph.User_Interface;
using Pawnmorph.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace Pawnmorph.DebugUtils
{
    static class PMDebugActions
    {
        private const string PM_CATEGORY = "Pawnmorpher";


        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.Action)]
        static void TagAllMutations()
        {
            var cd = Find.World.GetComponent<ChamberDatabase>();

            var mutations = DefDatabase<MutationCategoryDef>.AllDefs.Where(d => d.genomeProvider)
                                                            .SelectMany(d => d.AllMutations)
                                                            .Distinct();
            foreach (MutationDef mutationDef in mutations)
            {
                if (cd.StoredMutations.Contains(mutationDef)) continue;
                cd.AddToDatabase(mutationDef);
            }

        }

        [DebugAction(category = PM_CATEGORY,actionType = DebugActionType.Action)]
        static void GiveBuildupToAllPawns()
        {
            var map = Find.CurrentMap;
            StringBuilder builder = new StringBuilder(); 
            foreach (Pawn pawn in PawnsFinder.AllMaps_SpawnedPawnsInFaction(Faction.OfPlayer).MakeSafe())
            {
                if(pawn == null) continue;
                builder.AppendLine(TryGiveMutagenBuildupToPawn(pawn)); 
            }

            Log.Message(builder.ToString()); 
        }

        static string TryGiveMutagenBuildupToPawn(Pawn pawn)
        {
            var buildup = MutagenicBuildupUtilities.AdjustMutagenicBuildup(null, pawn, 0.1f);
            if (buildup > 0)
            {
                return $"gave {buildup} buildup to {pawn.Name}";
            }
            else return $"could not give buildup to {pawn.Name}";
        }


        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.Action)]
        static void TagAllAnimals()
        {
            var gComp = Find.World.GetComponent<PawnmorphGameComp>();
            var database = Find.World.GetComponent<ChamberDatabase>();

            StringBuilder sBuilder = new StringBuilder();
            foreach (var kindDef in DefDatabase<PawnKindDef>.AllDefs)
            {
                var thingDef = kindDef.race; 
                if(thingDef.race?.Animal != true) continue;

                if (!database.TryAddToDatabase(kindDef, out string reason))
                {
                    sBuilder.AppendLine($"unable to store {kindDef.label} because {reason}");
                }
                else
                {
                    sBuilder.AppendLine($"added {kindDef.label} to the database");
                }
            }

            Log.Message(sBuilder.ToString());

        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void GetInfluenceDebugInfo(Pawn pawn)
        {
            var mutTracker = pawn?.GetMutationTracker();
            if (mutTracker == null)
            {
                Log.Message("no mutation tracker");
                return; 
            }

            Log.Message(AnimalClassUtilities.GenerateDebugInfo(mutTracker.AllMutations)); 

        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void TryExitSapienceState(Pawn pawn)
        {
            var sapienceT = pawn?.GetSapienceTracker();
            if (sapienceT?.CurrentState == null) return;
            var stateName = sapienceT.CurrentState.StateDef.defName; 
            try
            {
                sapienceT.ExitState();
            }
            catch (Exception e)
            {
                Log.Error($"caught {e.GetType().Name} while trying to exit sapience state {stateName}!\n{e.ToString().Indented("|\t")}");
            }
        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void AddMutation(Pawn pawn)
        {
            if (pawn == null) return;
            Find.WindowStack.Add(new DebugMenu_AddMutations(pawn)); 
        }


        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        static void TagAnimal()
        {
            
            var db = Find.World.GetComponent<ChamberDatabase>();
            var options = GetTaggableAnimalActions(db).ToList();
            if (options.Count == 0) return; 
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(GetTaggableAnimalActions(db)));

        }

        static IEnumerable<DebugMenuOption> GetTaggableAnimalActions([NotNull] ChamberDatabase db)
        {
            foreach (PawnKindDef pawnKindDef in DefDatabase<PawnKindDef>.AllDefs)
            {
                if(!pawnKindDef.race.IsValidAnimal() || db.TaggedAnimals.Contains(pawnKindDef)) continue;
                var tmpPk = pawnKindDef;
                yield return new DebugMenuOption(pawnKindDef.label,DebugMenuOptionMode.Action,
                                                 () => db.AddToDatabase(tmpPk));
            }   
        }

        


        static IEnumerable<DebugMenuOption> GetAddMutationOptions([NotNull] Pawn pawn)
        {

            bool CanAddMutationToPawn(MutationDef mDef)
            {
                if (mDef.parts == null)
                {
                    return pawn.health.hediffSet.GetFirstHediffOfDef(mDef) == null; 
                }

                foreach (BodyPartDef bodyPartDef in mDef.parts)
                {
                    foreach (BodyPartRecord record in pawn.GetAllNonMissingParts().Where(p => p.def == bodyPartDef))
                    {
                        if (!pawn.health.hediffSet.HasHediff(mDef, record)) return true; 
                    }
                }

                return false; 
            }

            void CreateMutationDialog(MutationDef mDef)
            {
                var nMenu = new DebugMenu_AddMutation(mDef, pawn);
                Find.WindowStack.Add(nMenu); 
            }


            foreach (MutationDef mutation in MutationDef.AllMutations.Where(CanAddMutationToPawn))
            {
                var tMu = mutation;
                yield return new DebugMenuOption(mutation.label, DebugMenuOptionMode.Action, () => CreateMutationDialog(tMu)); 
            }

        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void RecruitFormerHuman(Pawn pawn)
        {
            var sapienceState = pawn?.GetSapienceState();
            if (sapienceState?.IsFormerHuman == true)
            {
                Worker_FormerHumanRecruitAttempt.DoRecruit(pawn.Map.mapPawns.FreeColonists.FirstOrDefault(), pawn, 1f);
                DebugActionsUtility.DustPuffFrom(pawn);
            }
        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void ReduceSapience(Pawn pawn)
        {
            var sTracker = pawn?.GetComp<SapienceTracker>();
            if (sTracker == null) return; 

            sTracker.SetSapience(Mathf.Max(0, sTracker.Sapience -0.2f ));
        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void IncreaseSapience(Pawn pawn)
        {
            var sTracker = pawn?.GetComp<SapienceTracker>();
            if (sTracker == null) return;

            sTracker.SetSapience(sTracker.Sapience + 0.2f);

        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void MakeAnimalSapientFormerHuman(Pawn pawn)
        {
            if (pawn == null) return;
            if (pawn.GetSapienceState() != null) return;
            if (!pawn.RaceProps.Animal) return;

            FormerHumanUtilities.MakeAnimalSapient(pawn); 

        }
        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void MakeAnimalFormerHuman(Pawn pawn)
        {
            if (pawn == null) return;
            if (pawn.GetSapienceState() != null) return;
            if (!pawn.RaceProps.Animal) return;

            FormerHumanUtilities.MakeAnimalSapient(pawn, Rand.Range(0.1f, 1f));

        }

        [DebugAction(category = PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns)]
        static void TryRevertTransformedPawn(Pawn pawn)
        {
            if (pawn == null) return;
            var gComp = Find.World.GetComponent<PawnmorphGameComp>();
            (TransformedPawn pawn, TransformedStatus status)? tfPawn = gComp?.GetTransformedPawnContaining(pawn);
            TransformedPawn transformedPawn = tfPawn?.pawn;
            if (transformedPawn == null || tfPawn?.status != TransformedStatus.Transformed) return;
            MutagenDef mut = transformedPawn.mutagenDef ?? MutagenDefOf.defaultMutagen;
            mut.MutagenCached.TryRevert(transformedPawn); 
        }


        [DebugAction("General","Explosion (mutagenic small)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        static void SmallExplosionMutagenic()
        {
            GenExplosion.DoExplosion(UI.MouseCell(), Find.CurrentMap, 10, PMDamageDefOf.MutagenCloud, null); 
        }

        [DebugAction("General", "Explosion (mutagenic large)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        static void ExplosionMutagenic()
        {
            GenExplosion.DoExplosion(UI.MouseCell(), Find.CurrentMap, 10, PMDamageDefOf.MutagenCloud_Large, null);
        }

        [DebugAction("Pawnmorpher", "Open action menu", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void OpenActionMenu()
        {
            Find.WindowStack.Add(new Pawnmorpher_DebugDialogue());
        }

        [DebugAction(category=PM_CATEGORY, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void OpenPartPickerMenu(Pawn pawn)
        {
            if (pawn == null) return;
            Find.WindowStack.Add(new Dialog_PartPicker(pawn, true));
        }
    }
}
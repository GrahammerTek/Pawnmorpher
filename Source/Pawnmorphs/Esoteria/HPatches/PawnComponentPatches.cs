﻿// PawnComponentPatches.cs created by Iron Wolf for Pawnmorph on 11/27/2019 1:00 PM
// last updated 11/27/2019  1:00 PM

using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

#pragma warning disable 1591

#if true
namespace Pawnmorph.HPatches
{
    public static class PawnComponentPatches
    {
        [HarmonyPatch(typeof(PawnComponentsUtility))]
        [HarmonyPatch("AddAndRemoveDynamicComponents")]
        public static class AddRemoveComponentsPatch
        {
            internal static void Postfix(Pawn pawn)
            {
                if (pawn.RaceProps.Animal)
                {
                    Hediff formerHumanHediff = pawn.health.hediffSet.GetFirstHediffOfDef(TfHediffDefOf.TransformedHuman);
                    if (formerHumanHediff?.CurStageIndex == 2)
                    {
                        AddSapientAnimalComponents(pawn);
                    }
                    else if (formerHumanHediff?.CurStageIndex < 2 && pawn.drafter != null)
                    {
                        //remove sapient animal comps if the animal is now feral 
                        RemoveSapientAnimalComponents(pawn); 

                    }
                }
            }

            private static void RemoveSapientAnimalComponents(Pawn pawn)
            {
                Log.Message($"removing drafter from {pawn.Name}");
                //remove the drafter component if the animal is now feral 
                pawn.drafter.Drafted = false;
                pawn.drafter = null;
                if (pawn.MapHeld != null)
                {
                    pawn.equipment?.DropAllEquipment(pawn.PositionHeld, pawn.Faction?.IsPlayer != true);
                    pawn.apparel?.DropAll(pawn.PositionHeld, pawn.Faction?.IsPlayer != true);
                }
                else
                {
                    pawn.equipment?.DestroyAllEquipment();
                    pawn.apparel?.DestroyAll();
                }

                pawn.apparel = null;
                pawn.equipment = null;
                pawn.story = null;
                pawn.skills = null;
                pawn.jobs = null;
                var saComp = pawn.GetComp<Comp_SapientAnimal>();
                if (saComp != null)
                {
                    pawn.AllComps.Remove(saComp); 
                }
            }

            private static void AddSapientAnimalComponents(Pawn pawn)
            {
                //add the drafter and equipment components 
                //if 
                if (pawn.Faction?.IsPlayer == true)
                {
                    if (pawn.drafter == null)
                    {
                        pawn.drafter = new Pawn_DraftController(pawn);
                        pawn.jobs = pawn.jobs ?? new Pawn_JobTracker(pawn);
                    }

                    if (pawn.workSettings == null)
                    {
                        pawn.workSettings = new Pawn_WorkSettings(pawn);
                    }
                }

                pawn.equipment = pawn.equipment ?? new Pawn_EquipmentTracker(pawn);
                pawn.story = pawn.story ?? new Pawn_StoryTracker(pawn); //need to add story component to not break hospitality 
                pawn.apparel = pawn.apparel ?? new  Pawn_ApparelTracker(pawn); //need this to not break thoughts and stuff 
                pawn.skills = pawn.skills ?? new Pawn_SkillTracker(pawn); //need this for thoughts 
                Comp_SapientAnimal nComp = pawn.GetComp<Comp_SapientAnimal>();
                bool addedComp = false;
                
                if (nComp == null)
                {
                    addedComp = true; 
                    nComp = new Comp_SapientAnimal {parent = pawn};
                    pawn.AllComps.Add(nComp); 

                }



                if (pawn.Faction?.IsPlayer == true && pawn.workSettings != null) //make sure to initialize only after adding all the comps 
                {
                    pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    pawn.workSettings.DisableAll();
                }

                //now initialize the comp 
                if (addedComp)
                {
                    nComp.Initialize(new CompProperties());//just pass in empty props 
                }
                
            }
        }
    }
}
#endif
﻿// RaceGenerator.cs modified by Iron Wolf for Pawnmorph on 08/02/2019 7:12 PM
// last updated 08/02/2019  7:12 PM


//uncomment to test custom draw sizes 
//#define TEST_BODY_SIZE 


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlienRace;
using JetBrains.Annotations;
using Pawnmorph.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace Pawnmorph.Hybrids
{
	/// <summary> Static class responsible for generating the implicit races.</summary>
	public static class RaceGenerator
	{
		private const float HEALTH_SCALE_LERP_VALUE = 0.4f;
		private const float HUNGER_LERP_VALUE = 0.3f;
		private static List<ThingDef_AlienRace> _lst;

		/// <summary>
		/// Gets the list of explicite race morphs patched externally.
		/// </summary>
		public static List<MorphDef> ExplicitPatchedRaces { get; private set; } = new List<MorphDef>();

		[NotNull]
		private static readonly Dictionary<ThingDef, MorphDef> _raceLookupTable = new Dictionary<ThingDef, MorphDef>();

		/// <summary>an enumerable collection of all implicit races generated by the MorphDefs</summary>
		/// includes unused implicit races generated if the MorphDef has an explicit hybrid race 
		/// <value>The implicit races.</value>
		[NotNull]
		public static IEnumerable<ThingDef_AlienRace> ImplicitRaces => _lst ?? (_lst = GenerateAllImpliedRaces().ToList());

		/// <summary> Try to find the morph def associated with the given race.</summary>
		/// <param name="race"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public static bool TryGetMorphOfRace(ThingDef race, out MorphDef result)
		{
			return _raceLookupTable.TryGetValue(race, out result);
		}

		/// <summary> Gets the morph Def associated with this race, if any.</summary>
		/// <param name="race"></param>
		/// <returns></returns>
		[CanBeNull]
		public static MorphDef GetMorphOfRace(this ThingDef race)
		{
			return _raceLookupTable.TryGetValue(race);
		}

		private static RaceProperties GenerateHybridProperties([NotNull] RaceProperties human, [NotNull] RaceProperties animal, [NotNull] HybridRaceSettings morph)
		{
			// (float hSize, float hHRate) = GetFoodStats(human, animal);


			RaceProperties properties = new RaceProperties
			{
				thinkTreeMain = human.thinkTreeMain, //most of these are just guesses, have to figure out what's safe to change and what isn't 
				thinkTreeConstant = human.thinkTreeConstant,
				intelligence = human.intelligence,
				makesFootprints = true,
				lifeExpectancy = morph.lifeExpectancy ?? human.lifeExpectancy,
				leatherDef = animal.leatherDef,
				nameCategory = human.nameCategory,
				body = morph.body ?? human.body,
				baseBodySize = human.baseBodySize,
				baseHealthScale = human.baseHealthScale,
				baseHungerRate = morph.baseHungerRate ?? human.baseHungerRate,
				hasGenders = human.hasGenders,
				foodType = GenerateFoodFlags(animal.foodType),
				gestationPeriodDays = human.gestationPeriodDays,
				wildness = animal.wildness / 2,
				meatColor = animal.meatColor,
				meatMarketValue = animal.meatMarketValue,
				manhunterOnDamageChance = animal.manhunterOnDamageChance,
				manhunterOnTameFailChance = animal.manhunterOnTameFailChance,
				litterSizeCurve = human.litterSizeCurve,
				lifeStageAges = MakeLifeStages(human.lifeStageAges, animal.lifeStageAges),
				soundMeleeHitPawn = animal.soundMeleeHitPawn,
				roamMtbDays = animal.roamMtbDays,
				soundMeleeHitBuilding = animal.soundMeleeHitBuilding,
				trainability = GetTrainability(animal.trainability),
				soundMeleeMiss = animal.soundMeleeMiss,
				specialShadowData = human.specialShadowData,
				soundCallIntervalRange = animal.soundCallIntervalRange,
				ageGenerationCurve = human.ageGenerationCurve,
				willNeverEat = animal.willNeverEat.MakeSafe().Concat(human.willNeverEat.MakeSafe()).ToList(),
				hediffGiverSets = human.hediffGiverSets.ToList(),
				meatDef = animal.meatDef,
				meatLabel = animal.meatLabel,
				useMeatFrom = animal.useMeatFrom,
				deathActionWorkerClass = animal.deathActionWorkerClass, // Boommorphs should explode.
				corpseDef = human.corpseDef,
				packAnimal = animal.packAnimal
			};

			typeof(RaceProperties).GetField("bloodDef", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(properties, animal.BloodDef);
			typeof(RaceProperties).GetField("fleshType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(properties, animal.FleshType);

			return properties;
		}

		private static TrainabilityDef GetTrainability(TrainabilityDef animalTrainability)
		{
			//hybrid trainability should be 1 above that of a humans 
			if (animalTrainability == null) return TrainabilityDefOf.Intermediate;
			if (animalTrainability == TrainabilityDefOf.None) return TrainabilityDefOf.Intermediate;
			if (animalTrainability == TrainabilityDefOf.Intermediate) return TrainabilityDefOf.Advanced;
			if (animalTrainability == TrainabilityDefOf.Advanced) return TrainabilityDefOf.Advanced;
			return animalTrainability;
		}

		static (float bodySize, float hungerSize) GetFoodStats([NotNull] RaceProperties human, [NotNull] RaceProperties animal)
		{
			//'gamma' is a ratio describing how long it takes an animal to become hungry 
			//larger values mean the animal needs to eat less often 
			float gammaH = human.baseBodySize / human.baseHungerRate;
			float gammaA = animal.baseBodySize / animal.baseHungerRate;

			float f = Mathf.Pow(Math.Abs((gammaA - gammaH) / gammaH), 0.5f);
			//scale things back a bit if the animal has very different hunger characteristics then humans  
			float a = 1 / (1f + f);

			a = Mathf.Clamp(a, 0, 1);

			float hGamma = Mathf.Lerp(gammaA, gammaH, a);
			//body size is just an average of animal and human 
			float hBSize = Mathf.Lerp(animal.baseBodySize, human.baseBodySize, a);
			float hHRate = hBSize / hGamma; //calculate the hunger rate the hybrid should have to have an average gamma value between the animal and human 

			return (hBSize, hHRate);
		}

		private static List<LifeStageAge> MakeLifeStages(List<LifeStageAge> human, List<LifeStageAge> animal)
		{
			List<LifeStageAge> ls = new List<LifeStageAge>();

			float convert = ((float)animal.Count) / human.Count;
			for (int i = 0; i < human.Count; i++)
			{
				int j = (int)(convert * i);
				j = Mathf.Min(j, animal.Count - 1);
				var hStage = human[i];
				var aStage = animal[j];

				var newStage = new LifeStageAge()
				{
					minAge = hStage.minAge,
					def = hStage.def,
					soundAngry = aStage.soundAngry,
					soundCall = aStage.soundCall,
					soundDeath = aStage.soundDeath,
					soundWounded = aStage.soundWounded
				};
				ls.Add(newStage);

			}

			return ls;
		}

		private static float GetBodySize(RaceProperties animal, RaceProperties human)
		{
			var f = Mathf.Lerp(human.baseBodySize, animal.baseBodySize, 0.5f);
			return Mathf.Max(f, human.baseBodySize * 0.7f);
		}

		private static float GetHungerRate(RaceProperties animal, RaceProperties human)
		{

			var f = Mathf.Lerp(human.baseHungerRate, animal.baseHungerRate, 0.7f);
			return f;
		}

		[NotNull]
		private static IEnumerable<ThingDef_AlienRace> GenerateAllImpliedRaces()
		{
			ThingDef_AlienRace human;

			try
			{
				human = (ThingDef_AlienRace)ThingDef.Named("Human");
			}
			catch (InvalidCastException e)
			{
				throw new
					ModInitializationException($"could not convert human ThingDef to {nameof(ThingDef_AlienRace)}! is HAF up to date?", e);
			}

			if (PawnmorpherMod.Settings.raceReplacements != null)
			{
				foreach (var item in PawnmorpherMod.Settings.raceReplacements)
				{
					MorphDef morph = DefDatabase<MorphDef>.GetNamed(item.Key, false);
					if (morph != null)
					{
						if (morph.ExplicitHybridRace == null)
						{
							ThingDef race = DefDatabase<ThingDef>.GetNamed(item.Value, false);
							if (race != null && race is ThingDef_AlienRace alien)
							{
								morph.raceSettings.explicitHybridRace = alien;
								morph.hybridRaceDef = alien;
							}
						}
					}
				}
			}



			ILookup<string, string> animalAssociationLookup = null;
			if (PawnmorpherMod.Settings.animalAssociations != null)
			{
				animalAssociationLookup = PawnmorpherMod.Settings.animalAssociations.ToLookup(x => x.Value, x => x.Key);
			}


			IEnumerable<MorphDef> morphs = DefDatabase<MorphDef>.AllDefs;
			// ReSharper disable once PossibleNullReferenceException
			foreach (MorphDef morphDef in morphs)
			{
				ThingDef_AlienRace race = GenerateImplicitRace(human, morphDef);
				_raceLookupTable[race] = morphDef;

				if (morphDef.ExplicitHybridRace == null) //still generate the race so we don't break saves, but don't set them 
				{
					morphDef.hybridRaceDef = race;
				}
				else
				{
					if (PawnmorpherMod.Settings.raceReplacements?.ContainsKey(morphDef.defName) == false)
						ExplicitPatchedRaces.Add(morphDef);

					_raceLookupTable[morphDef.ExplicitHybridRace] = morphDef;
				}



				if (animalAssociationLookup != null)
				{
					if (animalAssociationLookup.Contains(morphDef.defName))
					{
						morphDef.associatedAnimals.AddRange(animalAssociationLookup[morphDef.defName].Select(x => DefDatabase<ThingDef>.GetNamed(x)));
						morphDef.ResolveReferences();
					}
				}






				CreateImplicitMeshes(race);
				race.ResolveReferences();
				DoHarStuff(race);
				yield return race;
			}

		}

		private static void CreateImplicitMeshes(ThingDef_AlienRace race)
		{
			try
			{
				//generate any meshes the implied race might need 
				if (race.alienRace?.graphicPaths != null)
				{
					race.alienRace.generalSettings?.alienPartGenerator?.GenerateMeshsAndMeshPools();
				}
			}
			catch (Exception e)
			{
				throw new ModInitializationException($"while updating graphics for {race.defName}, caught {e.GetType().Name}", e);

			}
		}

		/// <summary>
		/// Generate general settings for the hybrid race given the human settings and morph def.
		/// </summary>
		/// <param name="human">The human.</param>
		/// <param name="morph">The morph.</param>
		/// <param name="impliedRace">The implied race.</param>
		/// <returns></returns>
		private static GeneralSettings GenerateHybridGeneralSettings(GeneralSettings human, MorphDef morph, ThingDef_AlienRace impliedRace)
		{
			var traitSettings = morph.raceSettings.traitSettings;
			return new GeneralSettings
			{
				alienPartGenerator = GenerateHybridGenerator(human.alienPartGenerator, morph, impliedRace),
				humanRecipeImport = true,
				forcedRaceTraitEntries = traitSettings?.forcedTraits
				// Black list is not currently supported, Rimworld doesn't like it when you remove traits.
			};
		}

		private static AlienPartGenerator GenerateHybridGenerator(AlienPartGenerator human, MorphDef morph, ThingDef_AlienRace impliedRace)
		{
			AlienPartGenerator gen = new AlienPartGenerator //TODO use reflection to copy these fields? 
			{
				bodyTypes = human.bodyTypes.MakeSafe().ToList(),
				headTypes = human.headTypes.MakeSafe().ToList(),
				offsetDefaults = human.offsetDefaults.MakeSafe().ToList(),
				headOffset = human.headOffset,
				headOffsetSpecific = human.headOffsetSpecific,
				headOffsetDirectional = human.headOffsetDirectional,
				bodyAddons = GenerateBodyAddons(human.bodyAddons, morph),
				colorChannels = human.colorChannels,
				alienProps = impliedRace
			};

			return gen;
		}

		static (Vector2 body, Vector2 head)? GetDebugBodySizes(MorphDef morph)
		{

			var pkDef = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(pk => pk.race == morph.race);//get the first pawnkindDef that uses the morph's race 

			if (pkDef?.lifeStages == null || pkDef.lifeStages.Count == 0) //if there are no pawnkinds to choose from just return null 
			{
				return null;
			}

			var lastStage = pkDef.lifeStages.Last();

			float cSize = Mathf.Lerp(1, lastStage.bodyGraphicData.drawSize.x, 0.5f); //take the average of the animals draw size and a humans, which is a default of 1 
			return (cSize * Vector2.one, cSize * Vector2.one);
		}


		private static List<AlienPartGenerator.BodyAddon> GenerateBodyAddons(List<AlienPartGenerator.BodyAddon> human, MorphDef morph)
		{
			List<AlienPartGenerator.BodyAddon> returnList = new List<AlienPartGenerator.BodyAddon>();

#if TEST_BODY_SIZE
            var bodySizes = GetDebugBodySizes(morph);
            Vector2? bodySize = bodySizes?.body;
            Vector2? headSize = bodySizes?.head;

            if (bodySizes != null)
            {
                Log.Message($"{morph.defName} draw sizes are: bodySize={bodySizes.Value.body}, headSize={bodySizes.Value.head}");
            }
#else
			Vector2? bodySize = morph?.raceSettings?.graphicsSettings?.customDrawSize;
			Vector2? headSize = morph?.raceSettings?.graphicsSettings?.customHeadDrawSize;
#endif

			List<string> headParts = new List<string>();
			headParts.Add("Jaw");
			headParts.Add("Ear");
			headParts.Add("left ear");
			headParts.Add("right ear");
			headParts.Add("Skull");

			List<string> bodyParts = new List<string>();
			bodyParts.Add("Arm");
			bodyParts.Add("Tail");
			bodyParts.Add("Waist");

			FieldInfo colorChannel = HarmonyLib.AccessTools.Field(typeof(AlienPartGenerator.BodyAddon), "colorChannel");
			foreach (AlienPartGenerator.BodyAddon addon in human)
			{
				addon.scaleWithPawnDrawsize = true;

				AlienPartGenerator.BodyAddon temp = new AlienPartGenerator.BodyAddon()
				{
					path = addon.path,
					bodyPart = addon.bodyPart,
					offsets = GenerateBodyAddonOffsets(addon.offsets, morph),
					linkVariantIndexWithPrevious = addon.linkVariantIndexWithPrevious,
					angle = addon.angle,
					inFrontOfBody = addon.inFrontOfBody,
					layerInvert = addon.layerInvert,
					drawnOnGround = addon.drawnOnGround,
					drawnInBed = addon.drawnInBed,
					drawForMale = addon.drawForMale,
					drawForFemale = addon.drawForFemale,
					drawSize = addon.drawSize,
					variantCount = addon.variantCount,
					defaultOffset = addon.defaultOffset,
					defaultOffsets = addon.defaultOffsets,
					hediffGraphics = addon.hediffGraphics,
					backstoryGraphics = addon.backstoryGraphics,
					hiddenUnderApparelFor = addon.hiddenUnderApparelFor,
					hiddenUnderApparelTag = addon.hiddenUnderApparelTag,
					backstoryRequirement = addon.backstoryRequirement,
					drawRotated = addon.drawRotated,
					drawSizePortrait = addon.drawSizePortrait,
					scaleWithPawnDrawsize = addon.scaleWithPawnDrawsize,
					alignWithHead = addon.alignWithHead
				};
				if (temp.ColorChannel != addon.ColorChannel)
					colorChannel.SetValue(temp, addon.ColorChannel);

				if (headParts.Contains(temp.bodyPartLabel))
				{
					if (headSize != null)
						temp.drawSize = headSize.GetValueOrDefault();
					if (bodySize != null)
					{
						if (temp?.offsets?.south?.bodyTypes != null)
							foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.south.bodyTypes)
								bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
						if (temp?.offsets?.north?.bodyTypes != null)
							foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.north.bodyTypes)
								bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
						if (temp?.offsets?.east?.bodyTypes != null)
							foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.east.bodyTypes)
								bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
						if (temp?.offsets?.west?.bodyTypes != null)
							foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.west.bodyTypes)
								bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
					}
				}

				if (bodySize != null && bodyParts.Contains(temp.bodyPartLabel))
				{
					temp.drawSize = bodySize.GetValueOrDefault();
				}

				returnList.Add(temp);
			}

			return returnList;
		}

		private static AlienPartGenerator.DirectionalOffset GenerateBodyAddonOffsets(AlienPartGenerator.DirectionalOffset human, MorphDef morph)
		{
			AlienPartGenerator.DirectionalOffset returnValue = new AlienPartGenerator.DirectionalOffset();
			if (human.south != null)
				returnValue.south = GenerateRotationOffsets(human.south, morph);
			if (human.north != null)
				returnValue.north = GenerateRotationOffsets(human.north, morph);
			if (human.east != null)
				returnValue.east = GenerateRotationOffsets(human.east, morph);
			if (human.west != null)
				returnValue.west = GenerateRotationOffsets(human.west, morph);
			return returnValue;
		}

		private static AlienPartGenerator.RotationOffset GenerateRotationOffsets(AlienPartGenerator.RotationOffset human, MorphDef morph)
		{
			AlienPartGenerator.RotationOffset returnValue = new AlienPartGenerator.RotationOffset()
			{
				portraitBodyTypes = human.portraitBodyTypes,
				portraitHeadTypes = human.portraitHeadTypes,
				headTypes = human.headTypes,
				layerOffset = human.layerOffset,
				offset = human.offset
			};

			if (human.bodyTypes != null)
				returnValue.bodyTypes = GenerateBodyTypeOffsets(human.bodyTypes, morph);

			return returnValue;
		}

		private static List<AlienPartGenerator.BodyTypeOffset> GenerateBodyTypeOffsets(List<AlienPartGenerator.BodyTypeOffset> human, MorphDef morph)
		{
			List<AlienPartGenerator.BodyTypeOffset> returnList = new List<AlienPartGenerator.BodyTypeOffset>();
			foreach (AlienPartGenerator.BodyTypeOffset bodyTypeOffset in human)
			{
				AlienPartGenerator.BodyTypeOffset temp = new AlienPartGenerator.BodyTypeOffset()
				{
					offset = bodyTypeOffset.offset,
					bodyType = bodyTypeOffset.bodyType
				};
				returnList.Add(temp);
			}

			return returnList;
		}

		/// <summary> Generate the alien race restriction setting from the human default and the given morph.</summary>
		/// <param name="human"></param>
		/// <param name="morph"></param>
		/// <returns></returns>
		private static RaceRestrictionSettings GenerateHybridRestrictionSettings(RaceRestrictionSettings human, MorphDef morph)
		{
			return morph.raceSettings?.restrictionSettings ?? human;
		}

		private static ThingDef_AlienRace.AlienSettings GenerateHybridAlienSettings(ThingDef_AlienRace.AlienSettings human, MorphDef morph, ThingDef_AlienRace impliedRace)
		{
			GeneralSettings generalSettings = GenerateHybridGeneralSettings(human.generalSettings, morph, impliedRace);
			return new ThingDef_AlienRace.AlienSettings
			{
				generalSettings = GenerateHybridGeneralSettings(human.generalSettings, morph, impliedRace),
				graphicPaths = GenerateGraphicPaths(human.graphicPaths, morph, generalSettings),
				styleSettings = human.styleSettings,
				raceRestriction = GenerateHybridRestrictionSettings(human.raceRestriction, morph),
				relationSettings = human.relationSettings,
				thoughtSettings = morph.raceSettings.GenerateThoughtSettings(human.thoughtSettings, morph)
			};
		}

		private static GraphicPaths GenerateGraphicPaths(GraphicPaths humanGraphicPaths, MorphDef morph, GeneralSettings generalSettings)
		{
			GraphicPaths temp = new GraphicPaths();

			temp.head.headtypeGraphics = new List<AlienPartGenerator.ExtendedHeadtypeGraphic>();
			foreach (HeadTypeDef item2 in generalSettings.alienPartGenerator.HeadTypes)
			{
				temp.head.headtypeGraphics.Add(new AlienPartGenerator.ExtendedHeadtypeGraphic
				{
					headType = item2,
					path = item2.graphicPath,
				});
			}


			return temp;
		}

		static List<StatModifier> GenerateHybridStatModifiers(List<StatModifier> humanModifiers, List<StatModifier> animalModifiers, List<StatModifier> statMods)
		{
			humanModifiers = humanModifiers ?? new List<StatModifier>();
			animalModifiers = animalModifiers ?? new List<StatModifier>();

			Dictionary<StatDef, float> valDict = new Dictionary<StatDef, float>();
			foreach (StatModifier humanModifier in humanModifiers)
			{
				valDict[humanModifier.stat] = humanModifier.value;
			}

			//just average them for now
			foreach (StatModifier animalModifier in animalModifiers)
			{
				float val;
				if (valDict.TryGetValue(animalModifier.stat, out val))
				{
					val = Mathf.Lerp(val, animalModifier.value, 0.5f); //average the 2 
				}
				else val = (animalModifier.value + animalModifier.stat.defaultBaseValue) / 2.0f;

				valDict[animalModifier.stat] = val;
			}

			//now handle any statMods if they exist 
			if (statMods != null)
				foreach (StatModifier statModifier in statMods)
				{
					float v = valDict.TryGetValue(statModifier.stat) + statModifier.value;
					valDict[statModifier.stat] = v;
				}

			List<StatModifier> outMods = new List<StatModifier>();
			foreach (KeyValuePair<StatDef, float> keyValuePair in valDict)
			{
				outMods.Add(new StatModifier()
				{
					stat = keyValuePair.Key,
					value = keyValuePair.Value
				});
			}

			return outMods;
		}

		static FoodTypeFlags GenerateFoodFlags(FoodTypeFlags animalFlags)
		{
			animalFlags |= FoodTypeFlags.Meal | FoodTypeFlags.Processed; //make sure all hybrids can eat meals and drugs
																		 //need to figure out a way to let them graze but not pick up plants 
			return animalFlags;
		}

		[NotNull]
		private static ThingDef_AlienRace GenerateImplicitRace([NotNull] ThingDef_AlienRace humanDef, [NotNull] MorphDef morph)
		{
			ThingDef animal = morph.race;
			var impliedRace = new ThingDef_AlienRace
			{
				defName = morph.defName + "Race_Implied", //most of these are guesses, should figure out what's safe to change and what isn't 
				label = morph.label,
				race = GenerateHybridProperties(humanDef.race, animal?.race, morph.raceSettings),
				thingCategories = humanDef.thingCategories,
				thingClass = humanDef.thingClass,
				category = humanDef.category,
				selectable = humanDef.selectable,
				tickerType = humanDef.tickerType,
				altitudeLayer = humanDef.altitudeLayer,
				useHitPoints = humanDef.useHitPoints,
				hasTooltip = humanDef.hasTooltip,
				soundImpactDefault = animal?.soundImpactDefault ?? humanDef.soundImpactDefault,
				statBases = GenerateHybridStatModifiers(humanDef.statBases, animal?.statBases, morph.raceSettings.statModifiers),
				inspectorTabs = humanDef.inspectorTabs.ToList(), //do we want any custom tabs? 
				comps = humanDef.comps.ToList(),
				drawGUIOverlay = humanDef.drawGUIOverlay,
				description = string.IsNullOrEmpty(morph.description) ? animal?.description : morph.description,
				modContentPack = morph.modContentPack,
				inspectorTabsResolved = humanDef.inspectorTabsResolved?.ToList() ?? new List<InspectTabBase>(),
				recipes = new List<RecipeDef>(humanDef.recipes.MakeSafe()), //this is where the surgery operations live
				filth = animal?.filth ?? ThingDefOf.Human?.filth,
				filthLeaving = animal?.filthLeaving ?? ThingDefOf.Human?.filthLeaving,
				uiIcon = animal?.uiIcon,
				uiIconOffset = animal?.uiIconOffset ?? default(Vector2),
				uiIconScale = animal?.uiIconScale ?? 1,
				uiIconColor = animal?.uiIconColor ?? Color.white,
				soundDrop = animal?.soundDrop ?? humanDef.soundDrop,
				soundInteract = animal?.soundInteract ?? humanDef.soundInteract,
				soundPickup = animal?.soundPickup ?? humanDef.soundPickup,
				socialPropernessMatters = humanDef.socialPropernessMatters,
				stuffCategories = humanDef.stuffCategories?.ToList(),
				designationCategory = humanDef.designationCategory,
				tradeTags = humanDef.tradeTags?.ToList(),
				tradeability = humanDef.tradeability,
				fillPercent = morph.raceSettings.coverPercent
			};

			// Verbs from animals are provided by mutations.
			impliedRace.tools = new List<Tool>(humanDef.tools.MakeSafe()); //.Concat(animal.tools.MakeSafe())
			var verbField = typeof(ThingDef).GetField("verbs", BindingFlags.NonPublic | BindingFlags.Instance);
			var vLst = impliedRace.Verbs.MakeSafe().Concat(animal.Verbs.MakeSafe()).ToList();
			verbField.SetValue(impliedRace, vLst);
			impliedRace.alienRace = GenerateHybridAlienSettings(humanDef.alienRace, morph, impliedRace);

			// Add racial components
			if (morph.raceSettings.comps?.Count > 0)
				impliedRace.comps.AddRange(morph.raceSettings.comps);

			return impliedRace;
		}

		/// <summary>
		/// Determines whether this race is a morph hybrid race
		/// </summary>
		/// <param name="raceDef">The race definition.</param>
		/// <returns>
		///   <c>true</c> if the race is a morph hybrid race; otherwise, <c>false</c>.
		/// </returns>
		/// <exception cref="ArgumentNullException">raceDef</exception>
		public static bool IsMorphRace([NotNull] ThingDef raceDef)
		{
			if (raceDef == null) throw new ArgumentNullException(nameof(raceDef));
			return _raceLookupTable.ContainsKey(raceDef);
		}

		// AlienRace.HarmonyPatches..cctor
		private static void DoHarStuff(ThingDef_AlienRace ar)
		{
			foreach (ThoughtDef restrictedThought in ar.alienRace.thoughtSettings.restrictedThoughts)
			{
				if (!ThoughtSettings.thoughtRestrictionDict.ContainsKey(restrictedThought))
				{
					ThoughtSettings.thoughtRestrictionDict.Add(restrictedThought, new List<ThingDef_AlienRace>());
				}
				ThoughtSettings.thoughtRestrictionDict[restrictedThought].Add(ar);
			}
			foreach (ThingDef apparel in ar.alienRace.raceRestriction.apparelList)
			{
				RaceRestrictionSettings.apparelRestricted.Add(apparel);
				ar.alienRace.raceRestriction.whiteApparelList.Add(apparel);
			}
			foreach (ThingDef weapon in ar.alienRace.raceRestriction.weaponList)
			{
				RaceRestrictionSettings.weaponRestricted.Add(weapon);
				ar.alienRace.raceRestriction.whiteWeaponList.Add(weapon);
			}
			foreach (ThingDef building in ar.alienRace.raceRestriction.buildingList)
			{
				RaceRestrictionSettings.buildingRestricted.Add(building);
				ar.alienRace.raceRestriction.whiteBuildingList.Add(building);
			}
			foreach (RecipeDef recipe in ar.alienRace.raceRestriction.recipeList)
			{
				RaceRestrictionSettings.recipeRestricted.Add(recipe);
				ar.alienRace.raceRestriction.whiteRecipeList.Add(recipe);
			}
			foreach (ThingDef plant in ar.alienRace.raceRestriction.plantList)
			{
				RaceRestrictionSettings.plantRestricted.Add(plant);
				ar.alienRace.raceRestriction.whitePlantList.Add(plant);
			}
			foreach (TraitDef trait in ar.alienRace.raceRestriction.traitList)
			{
				RaceRestrictionSettings.traitRestricted.Add(trait);
				ar.alienRace.raceRestriction.whiteTraitList.Add(trait);
			}
			foreach (ThingDef food in ar.alienRace.raceRestriction.foodList)
			{
				RaceRestrictionSettings.foodRestricted.Add(food);
				ar.alienRace.raceRestriction.whiteFoodList.Add(food);
			}
			foreach (ThingDef pet in ar.alienRace.raceRestriction.petList)
			{
				RaceRestrictionSettings.petRestricted.Add(pet);
				ar.alienRace.raceRestriction.whitePetList.Add(pet);
			}
			foreach (ResearchProjectDef item in ar.alienRace.raceRestriction.researchList.SelectMany((ResearchProjectRestrictions rl) => rl?.projects))
			{
				if (!RaceRestrictionSettings.researchRestrictionDict.ContainsKey(item))
				{
					RaceRestrictionSettings.researchRestrictionDict.Add(item, new List<ThingDef_AlienRace>());
				}
				RaceRestrictionSettings.researchRestrictionDict[item].Add(ar);
			}
			foreach (GeneDef gene in ar.alienRace.raceRestriction.geneList)
			{
				RaceRestrictionSettings.geneRestricted.Add(gene);
				ar.alienRace.raceRestriction.whiteGeneList.Add(gene);
			}
			foreach (XenotypeDef xenotypeDef in ar.alienRace.raceRestriction.xenotypeList)
			{
				RaceRestrictionSettings.xenotypeRestricted.Add(xenotypeDef);
				ar.alienRace.raceRestriction.whiteXenotypeList.Add(xenotypeDef);
			}
			foreach (ThingDef reproduction in ar.alienRace.raceRestriction.reproductionList)
			{
				RaceRestrictionSettings.reproductionRestricted.Add(reproduction);
				ar.alienRace.raceRestriction.whiteReproductionList.Add(reproduction);
			}
			if (ar.alienRace.generalSettings.corpseCategory != ThingCategoryDefOf.CorpsesHumanlike)
			{
				ThingCategoryDefOf.CorpsesHumanlike.childThingDefs.Remove(ar.race.corpseDef);
				if (ar.alienRace.generalSettings.corpseCategory != null)
				{
					ar.race.corpseDef.thingCategories = new List<ThingCategoryDef> { ar.alienRace.generalSettings.corpseCategory };
					ar.alienRace.generalSettings.corpseCategory.childThingDefs.Add(ar.race.corpseDef);
					ar.alienRace.generalSettings.corpseCategory.ResolveReferences();
				}
				ThingCategoryDefOf.CorpsesHumanlike.ResolveReferences();
			}
			ar.alienRace.generalSettings.alienPartGenerator.GenerateMeshsAndMeshPools();
			if (ar.alienRace.generalSettings.humanRecipeImport && ar != ThingDefOf.Human)
			{
				(ar.recipes ?? (ar.recipes = new List<RecipeDef>())).AddRange(ThingDefOf.Human.recipes.Where((RecipeDef rd) => !rd.targetsBodyPart || rd.appliedOnFixedBodyParts.NullOrEmpty() || rd.appliedOnFixedBodyParts.Any((BodyPartDef bpd) => ar.race.body.AllParts.Any((BodyPartRecord bpr) => bpr.def == bpd))));
				DefDatabase<RecipeDef>.AllDefsListForReading.ForEach(delegate (RecipeDef rd)
				{
					List<ThingDef> recipeUsers = rd.recipeUsers;
					if (recipeUsers != null && recipeUsers.Contains(ThingDefOf.Human))
					{
						rd.recipeUsers.Add(ar);
					}
				});
				ar.recipes.RemoveDuplicates();
			}
			ar.alienRace.raceRestriction?.workGiverList?.ForEach(PawnmorphPatches.PatchHumanoidRace);
		}
	}
}
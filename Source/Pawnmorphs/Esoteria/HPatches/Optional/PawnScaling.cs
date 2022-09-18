﻿using Pawnmorph.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static AlienRace.AlienPartGenerator;
using static RimWorld.PawnUtility;

namespace Pawnmorph.HPatches.Optional
{
    [OptionalPatch("PMPawnScalingCaption", "PMPawnScalingDescription", nameof(_enabled), false)]
    [HarmonyLib.HarmonyPatch]
    static class PawnScaling
    {
        static Dictionary<float, AlienGraphicMeshSet> _meshCache;
        static bool _enabled = false;

        static bool Prepare(MethodBase original)
        {
            if (original == null && _enabled)
            {
                StatsUtility.GetEvents(PMStatDefOf.PM_BodySize).StatChanged += PawnScaling_StatChanged;
                _meshCache = new Dictionary<float, AlienGraphicMeshSet>();
            }

            return _enabled;
        }

        // Trigger pawn graphics update at the end of the tick if body size stat changes.
        private static void PawnScaling_StatChanged(Verse.Pawn pawn, RimWorld.StatDef stat, float oldValue, float newValue)
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                ResolveAllGraphics(pawn);
            });
        }

        // Updates all draw sizes on comp to specified size.
        private static void SetCompScales(AlienComp comp, float size)
        {
            comp.customDrawSize = new Vector2(size, size);
            comp.customHeadDrawSize = new Vector2(size, size);
            comp.customPortraitDrawSize = new Vector2(size, size);
            comp.customPortraitHeadDrawSize = new Vector2(size, size);
        }

        // Override HAR comp scales.
        [HarmonyLib.HarmonyPatch(typeof(AlienComp), nameof(AlienComp.PostSpawnSetup)), HarmonyLib.HarmonyPostfix]
        private static void PostSpawnSetup(bool respawningAfterLoad, AlienComp __instance)
        {
            SetCompScales(__instance, GetScale(((Pawn)__instance.parent).BodySize));
        }


        [HarmonyLib.HarmonyAfter(new string[] { "erdelf.HumanoidAlienRaces" })]
        [HarmonyLib.HarmonyPatch(typeof(Verse.PawnGraphicSet), nameof(Verse.PawnGraphicSet.ResolveAllGraphics)), HarmonyLib.HarmonyPostfix]
        private static void ResolveAllGraphics(Pawn ___pawn)
        {
            float bodysize = ___pawn.BodySize;
            if (bodysize == 1)
                return;

            AlienComp comp = CompCacher<AlienComp>.GetCompCached(___pawn);
            if (comp != null)
            {
                float size = GetScale(bodysize);

                // Set draw sizes
                SetCompScales(comp, size);

                // Generate new pawn textures of target size.
                if (_meshCache.TryGetValue(size, out var mesh) == false)
                {
                    mesh = new AlienGraphicMeshSet()
                    {
                        bodySet = new GraphicMeshSet(1.5f * size, 1.5f * size),
                        headSet = new GraphicMeshSet(1.5f * size, 1.5f * size),
                        hairSetAverage = new GraphicMeshSet(1.5f * size, 1.5f * size)
                    };
                    _meshCache.Add(size, mesh);
                }

                comp.alienGraphics = mesh;
                comp.alienHeadGraphics = mesh;
                comp.alienPortraitGraphics = mesh;
                comp.alienPortraitHeadGraphics = mesh;
            };
        }

        // Apply scale to body addon offsets.
        [HarmonyLib.HarmonyPatch(typeof(AlienRace.HarmonyPatches), nameof(AlienRace.HarmonyPatches.DrawAddonsFinalHook)), HarmonyLib.HarmonyPostfix]
        private static void DrawAddonsFinalHook(Pawn pawn, AlienRace.AlienPartGenerator.BodyAddon addon, ref Graphic graphic, ref Vector3 offsetVector, ref float angle, ref Material mat)
        {
            float value = GetScale(pawn.BodySize);
            offsetVector.x *= value;
            // Don't affect y layer
            offsetVector.z *= value;
        }


        [HarmonyLib.HarmonyAfter(new string[] { "erdelf.HumanoidAlienRaces" })]
        [HarmonyLib.HarmonyPatch(typeof(RimWorld.PawnCacheRenderer), nameof(RimWorld.PawnCacheRenderer.RenderPawn)), HarmonyLib.HarmonyPrefix]
        private static void CacheRenderPawnPrefix(Pawn pawn, ref float cameraZoom, bool portrait)
        {
            if (portrait)
            {
                cameraZoom = 1f / GetScale(pawn.BodySize);
            }
        }

        [HarmonyLib.HarmonyPatch(typeof(GlobalTextureAtlasManager), nameof(GlobalTextureAtlasManager.TryGetPawnFrameSet)), HarmonyLib.HarmonyPrefix]
        private static bool TryGetPawnFrameSetPrefix(Pawn pawn)
        {
            if (pawn.BodySize > 1.0f)
                return false;

            return true;
        }


        // Calculate the rendered size based on body size
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetScale(float bodysize)
        {
            return Mathf.Sqrt(bodysize);
        }

        // Offset rendered pawn from actual position to move selection box to their feet.
        [HarmonyLib.HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawAt)), HarmonyLib.HarmonyPrefix]
        private static void DrawAt(ref Vector3 drawLoc, bool flip, Pawn __instance)
        {
            // Don't offset draw position of animals sprites, and only care about those with more than 1 body size.
            if (__instance.RaceProps.Humanlike && __instance.BodySize != 1)
            {
                // Draw location is the full position not an offset, so find offset based on scale assing a ratio of 1 to 1.
                // Offset drawn pawn sprite with half the height upward. 1 bodysize = 1 height.
                // Only offset when standing.
                if (__instance.GetPosture() == RimWorld.PawnPosture.Standing)
                    drawLoc.z += (GetScale(__instance.BodySize) - 1) / 4f;
            }
        }

        // Apply scale offset to head position.
        [HarmonyLib.HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BaseHeadOffsetAt)), HarmonyLib.HarmonyPostfix]
        private static void BaseHeadOffsetAt(Rot4 rotation, ref Vector3 __result, Pawn ___pawn)
        {
            if (___pawn.BodySize == 1)
                return;

            float size = Mathf.Floor(GetScale(___pawn.BodySize) * 10) / 10;
            __result.z = __result.z * size;
            __result.x = __result.x * size;
        }
    }
}

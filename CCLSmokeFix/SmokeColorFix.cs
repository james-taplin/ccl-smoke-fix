using System.Linq;
using DV.Simulation.Controllers;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using UnityEngine;
using ColorReader = DV.Simulation.Controllers.ParticlesPortReadersController.ParticleColorPortReader;

namespace CCLSmokeFix
{
    // CCL steam locos ship the stack-smoke colour readers with the wrong alpha on the
    // SteamSmokeThick exhaust-flow reader: min alpha 1.0 where the vanilla S282 has 0.0.
    // At idle/low exhaust flow vanilla thick smoke is therefore nearly invisible, while on
    // CCL locos it is ~96% opaque, producing the dense dark plume that hides fog and
    // overdraws other smoke. Before a CCL loco's controller initialises, copy the vanilla
    // S282 reader colours/curve onto any matching stack-smoke reader.
    [HarmonyPatch(typeof(ParticlesPortReadersController), nameof(ParticlesPortReadersController.Init))]
    internal static class SmokeColorFix
    {
        private static ColorReader[] vanillaReaders;

        private static void Prefix(ParticlesPortReadersController __instance)
        {
            if (!Main.Settings.fixSmokeColor) return;
            var car = TrainCar.Resolve(__instance.gameObject);
            if (car == null || car.carLivery == null || car.carLivery.GetType() == typeof(TrainCarLivery)) return; // vanilla car
            if (__instance.particleColorPortReaders == null) return;

            var vanilla = GetVanillaReaders();
            if (vanilla == null) return;

            foreach (var reader in __instance.particleColorPortReaders)
            {
                string system = StackSystemName(reader.particlesParent);
                if (system == null) continue;
                var source = vanilla.FirstOrDefault(v =>
                    v.changeType == reader.changeType &&
                    PortSuffix(v.portId) == PortSuffix(reader.portId) &&
                    StackSystemName(v.particlesParent) == system);
                if (source == null) continue;
                if (reader.startColorMin == source.startColorMin && reader.startColorMax == source.startColorMax) continue;

                Main.Log($"{car.carLivery.id} {system} {reader.portId} {reader.changeType}: min {reader.startColorMin} -> {source.startColorMin}, max {reader.startColorMax} -> {source.startColorMax}");
                reader.startColorMin = source.startColorMin;
                reader.startColorMax = source.startColorMax;
                reader.colorLerpCurve = new AnimationCurve(source.colorLerpCurve.keys);
            }
        }

        private static ColorReader[] GetVanillaReaders()
        {
            if (vanillaReaders != null) return vanillaReaders;
            var prefab = TrainCarType.LocoSteamHeavy.ToV2()?.prefab;
            var ctrl = prefab ? prefab.GetComponentInChildren<ParticlesPortReadersController>(true) : null;
            if (ctrl == null || ctrl.particleColorPortReaders == null)
            {
                Main.Log("Could not find the vanilla S282 smoke colour readers; smoke colour fix disabled.");
                vanillaReaders = new ColorReader[0];
                return vanillaReaders;
            }
            vanillaReaders = ctrl.particleColorPortReaders.ToArray();
            return vanillaReaders;
        }

        // "SteamSmoke" or "SteamSmokeThick" if the reader drives exactly that stack system, else null.
        private static string StackSystemName(GameObject parent)
        {
            if (parent == null) return null;
            var systems = parent.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length != 1) return null;
            string name = systems[0].name.Replace("(Clone)", "").Trim();
            return name == "SteamSmoke" || name == "SteamSmokeThick" ? name : null;
        }

        private static string PortSuffix(string portId)
        {
            if (string.IsNullOrEmpty(portId)) return "";
            int dot = portId.LastIndexOf('.');
            return dot < 0 ? portId : portId.Substring(dot + 1);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityModManagerNet;

namespace CCLSmokeFix
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool fixSmokeColor = true;

        public override void Save(UnityModManager.ModEntry modEntry) => Save(this, modEntry);
    }

    public static class Main
    {
        private static UnityModManager.ModEntry mod;
        private static string lastReport = "";
        public static Settings Settings;

        private static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            new HarmonyLib.Harmony(modEntry.Info.Id).PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = e => Settings.Save(e);
            return true;
        }

        public static void Log(string msg) => mod.Logger.Log(msg);

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.fixSmokeColor = GUILayout.Toggle(Settings.fixSmokeColor, " Match CCL stack smoke colour/opacity to the vanilla S282 (applies to locos spawned or loaded after changing)");
            GUILayout.Space(10);
            GUILayout.Label("Diagnostics: writes every live loco particle system to a text file in this mod's folder.");
            if (GUILayout.Button("Write particle report", GUILayout.Width(220)))
            {
                try { lastReport = Report.Write(mod.Path); }
                catch (Exception e) { lastReport = "Failed: " + e; mod.Logger.LogException(e); }
            }
            if (lastReport.Length > 0) GUILayout.Label(lastReport);
        }
    }

    internal static class Report
    {
        public static string Write(string modPath)
        {
            var cam = Camera.main;
            var sb = new StringBuilder();
            sb.AppendLine($"time={DateTime.Now:s} unity={Application.unityVersion} cam={(cam ? cam.name : "none")} camPos={(cam ? cam.transform.position.ToString("F1") : "-")}");
            if (cam) sb.AppendLine($"cam: depthMode={cam.depthTextureMode} opaqueSort={cam.opaqueSortMode} transparencySortMode={cam.transparencySortMode} graphicsSettingsSort={GraphicsSettings.transparencySortMode}");
            sb.AppendLine("fog=" + RenderSettings.fog + " fogMode=" + RenderSettings.fogMode + " fogDensity=" + RenderSettings.fogDensity);
            sb.AppendLine();

            int cars = 0;
            foreach (var car in UnityEngine.Object.FindObjectsOfType<TrainCar>())
            {
                var renderers = car.GetComponentsInChildren<ParticleSystemRenderer>(true);
                if (renderers.Length == 0) continue;
                cars++;
                var livery = car.carLivery;
                string liveryType = livery ? livery.GetType().FullName : "null";
                float camDist = cam ? Vector3.Distance(cam.transform.position, car.transform.position) : -1f;
                sb.AppendLine($"=== CAR {car.ID} livery={(livery ? livery.id : "?")} liveryType={liveryType} layer={car.gameObject.layer} lossyScale={car.transform.lossyScale:F3} camDist={camDist:F1}");
                foreach (var sg in car.GetComponentsInChildren<SortingGroup>(true))
                    sb.AppendLine($"  SortingGroup {Path(sg.transform, car.transform)} order={sg.sortingOrder} layer={sg.sortingLayerID} enabled={sg.enabled}");
                foreach (var lod in car.GetComponentsInChildren<LODGroup>(true))
                    sb.AppendLine($"  LODGroup {Path(lod.transform, car.transform)} lods={lod.lodCount} enabled={lod.enabled}");

                foreach (var r in renderers)
                {
                    var ps = r.GetComponent<ParticleSystem>();
                    var main = ps ? ps.main : default;
                    var mats = r.sharedMaterials;
                    string matInfo = string.Join(" | ", mats.Select(m => m == null ? "null" :
                        $"{m.name}#{m.GetInstanceID()} sh={(m.shader ? m.shader.name : "null")} q={m.renderQueue} kw=[{string.Join(",", m.shaderKeywords)}]"));
                    var parentGroup = r.GetComponentInParent<SortingGroup>();
                    sb.AppendLine($"  PS {Path(r.transform, car.transform)}");
                    sb.AppendLine($"     active={r.gameObject.activeInHierarchy} enabled={r.enabled} visible={r.isVisible} layer={r.gameObject.layer} lossyScale={r.transform.lossyScale:F3} worldPos={r.transform.position:F1}");
                    sb.AppendLine($"     sortLayer={r.sortingLayerID} sortOrder={r.sortingOrder} fudge={r.sortingFudge} sortMode={r.sortMode} renderMode={r.renderMode} align={r.alignment} minSize={r.minParticleSize} maxSize={r.maxParticleSize} prio={r.rendererPriority} motionVec={r.motionVectorGenerationMode} propBlock={r.HasPropertyBlock()} parentSortingGroup={(parentGroup ? parentGroup.name + ":" + parentGroup.sortingOrder : "none")}");
                    sb.AppendLine($"     bounds c={r.bounds.center:F1} s={r.bounds.size:F1} boundsCamDist={(cam ? Vector3.Distance(cam.transform.position, r.bounds.center) : -1f):F1}");
                    if (ps)
                    {
                        sb.AppendLine($"     sim={main.simulationSpace} custom={(main.customSimulationSpace ? Path(main.customSimulationSpace, null) : "none")} scaling={main.scalingMode} count={ps.particleCount} max={main.maxParticles} culling={main.cullingMode} playing={ps.isPlaying}");
                        var em = ps.emission;
                        sb.AppendLine($"     startColor={Curve(main.startColor)} startSize={Curve(main.startSize)} sizeMul={main.startSizeMultiplier} lifetime={Curve(main.startLifetime)} speed={Curve(main.startSpeed)} rate={Curve(em.rateOverTime)} rateMul={em.rateOverTimeMultiplier}");
                        var col = ps.colorOverLifetime;
                        if (col.enabled) sb.AppendLine($"     colorOverLifetime={Curve(col.color)}");
                    }
                    if (r.HasPropertyBlock())
                    {
                        var mpb = new MaterialPropertyBlock();
                        r.GetPropertyBlock(mpb);
                        sb.AppendLine($"     mpb empty={mpb.isEmpty} _Color={mpb.GetColor("_Color")} _TintColor={mpb.GetColor("_TintColor")}");
                    }
                    foreach (var m in mats)
                        if (m != null && m.name.EndsWith(" (Instance)"))
                            sb.Append(MaterialDiff(m));
                    sb.AppendLine($"     mats: {matInfo}");
                    var comps = r.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing");
                    sb.AppendLine($"     comps: {string.Join(",", comps)}");
                }
                foreach (var ctrl in car.GetComponentsInChildren<DV.Simulation.Controllers.ParticlesPortReadersController>(true))
                {
                    sb.AppendLine($"  PortReadersController {Path(ctrl.transform, car.transform)}");
                    if (ctrl.particleColorPortReaders != null)
                        foreach (var c in ctrl.particleColorPortReaders)
                            sb.AppendLine($"    ColorReader parent={(c.particlesParent ? Path(c.particlesParent.transform, car.transform) : "null")} port={c.portId} type={c.changeType} min={c.startColorMin} max={c.startColorMax} mod=(x{c.inputModifier?.valueMultiplier} +{c.inputModifier?.valueOffset}) curve={Keys(c.colorLerpCurve)}");
                    if (ctrl.particlePortReaders != null)
                        foreach (var p in ctrl.particlePortReaders)
                        {
                            sb.AppendLine($"    PortReader parent={(p.particlesParent ? Path(p.particlesParent.transform, car.transform) : "null")}");
                            if (p.particleUpdaters != null)
                                foreach (var u in p.particleUpdaters)
                                    sb.AppendLine($"      port={u.portId} mod=(x{u.inputModifier?.valueMultiplier} +{u.inputModifier?.valueOffset}) props=[{string.Join("; ", (u.propertiesToUpdate ?? new System.Collections.Generic.List<DV.Simulation.Controllers.ParticlesPortReadersController.ParticlePortReader.PropertyChangeDefinition>()).Select(d => d.propertyType + ":" + Keys(d.propertyChangeCurve)))}]");
                        }
                }
                sb.AppendLine();
            }

            string file = System.IO.Path.Combine(modPath, $"particle_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(file, sb.ToString());
            return $"Wrote {cars} cars to {file}";
        }

        private static string Curve(ParticleSystem.MinMaxGradient g) =>
            g.mode == ParticleSystemGradientMode.Color ? $"{g.mode}:{g.color}" :
            g.mode == ParticleSystemGradientMode.TwoColors ? $"{g.mode}:{g.colorMin}..{g.colorMax}" :
            $"{g.mode}:{Grad(g.gradient)}|{Grad(g.gradientMin)}..{Grad(g.gradientMax)}";

        private static string Grad(Gradient g) => g == null ? "-" :
            "[" + string.Join(",", g.colorKeys.Select(k => $"{k.time:F2}=({k.color.r:F2},{k.color.g:F2},{k.color.b:F2})")) + " a:" +
            string.Join(",", g.alphaKeys.Select(k => $"{k.time:F2}={k.alpha:F2}")) + "]";

        private static string Curve(ParticleSystem.MinMaxCurve c) =>
            c.mode == ParticleSystemCurveMode.Constant ? $"{c.constant:F3}" :
            c.mode == ParticleSystemCurveMode.TwoConstants ? $"{c.constantMin:F3}..{c.constantMax:F3}" :
            $"{c.mode}x{c.curveMultiplier:F3}";

        private static string Keys(AnimationCurve c) => c == null ? "null" :
            string.Join(",", c.keys.Select(k => $"({k.time:F2},{k.value:F2})"));

        // Compares a renderer's instanced material against the shared original with the same name.
        private static string MaterialDiff(Material inst)
        {
            string baseName = inst.name.Substring(0, inst.name.Length - " (Instance)".Length);
            var orig = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == baseName && m.shader == inst.shader);
            if (orig == null) return $"     diff {inst.name}: no shared original found\n";
            var sb = new StringBuilder();
            var sh = inst.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                string n = sh.GetPropertyName(i);
                string a, b;
                switch (sh.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: a = inst.GetColor(n).ToString(); b = orig.GetColor(n).ToString(); break;
                    case ShaderPropertyType.Vector: a = inst.GetVector(n).ToString(); b = orig.GetVector(n).ToString(); break;
                    case ShaderPropertyType.Texture: a = inst.GetTexture(n)?.name ?? "null"; b = orig.GetTexture(n)?.name ?? "null"; break;
                    default: a = inst.GetFloat(n).ToString("R"); b = orig.GetFloat(n).ToString("R"); break;
                }
                if (a != b) sb.Append($" {n}: inst={a} orig={b};");
            }
            string ik = string.Join(",", inst.shaderKeywords), ok = string.Join(",", orig.shaderKeywords);
            if (ik != ok) sb.Append($" keywords inst=[{ik}] orig=[{ok}];");
            if (inst.renderQueue != orig.renderQueue) sb.Append($" queue inst={inst.renderQueue} orig={orig.renderQueue};");
            return $"     diff {baseName} vs #{orig.GetInstanceID()}:{(sb.Length == 0 ? " identical" : sb.ToString())}\n";
        }

        private static string Path(Transform t, Transform stopAt)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var cur = t; cur != null && cur != stopAt; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}

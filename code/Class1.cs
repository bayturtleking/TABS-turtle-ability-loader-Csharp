using BepInEx;
using DM;
using Landfall.TABS;
using Landfall.TABS.UnitEditor;
using Landfall.TABS.Workshop;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace TurtAbilitySystem
{
    public abstract class turtabilitybase : Move
    {
        public abstract float cooldown { get; }

        public abstract float range { get; }
        private float nextfiretimee;

        public void TriggerFromEvent()
        {
            if (Time.time < nextfiretimee) return;
            nextfiretimee = Time.time + cooldown;
            var enemytorso = FindNearestEnemyTorso(range);
            var enemydata = enemytorso != null
                ? ((Component)enemytorso).GetComponentInParent<DataHandler>()
                : null;

            DoMove(null, enemytorso, enemydata);
        }

        private Rigidbody FindNearestEnemyTorso(float searchradius)
        {
            var selfdata = transform.root.GetComponentInChildren<DataHandler>();
            if (selfdata == null || selfdata.torso == null) return null;

            var origin = selfdata.torso.position;
            var hits = Physics.OverlapSphere(origin, searchradius);
            var selfunit = transform.root.GetComponent<Unit>();

            Rigidbody best = null;
            float bestdist = float.MaxValue;
            foreach (var hit in hits)
            {
                var unit = hit.GetComponentInParent<Unit>();
                if (unit == null || unit == selfunit) continue;

                var data = unit.GetComponentInChildren<DataHandler>();
                if (data == null || data.torso == null) continue;

                float dist = Vector3.Distance(origin, data.torso.position);
                if (dist < bestdist)
                {
                    bestdist = dist;
                    best = data.torso.GetComponent<Rigidbody>();
                }
            }
            return best;
        }
    }
    public class turtonlyifequip : MonoBehaviour
    {
        private void Awake() => Recheck();
        private void OnEnable() => Recheck();

        private void Recheck()
        {
            bool equipped = transform.root.GetComponent<Unit>() != null;
            var ability = GetComponent<turtabilitybase>();
            var conditional = GetComponent<ConditionalEvent>();
            if (ability != null) ((Behaviour)ability).enabled = equipped;
            if (conditional != null) conditional.enabled = equipped;
        }
    }

    public class turtabilitydef
    {
        public string name;

        public string filepath;

        public int localid;

        public Type compiledtype;
    }

    [BepInPlugin("bayturtleking.turtabilityloader", "Turt Ability Loader", "0.1.0")]
    public class turtabilityloadermod : BaseUnityPlugin
    {
        private readonly string turtabilityfolder = Path.Combine(Paths.PluginPath, "TurtAbilities");
        private readonly string ownfolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private List<turtabilitydef> loadeddefs = new List<turtabilitydef>();

        private static bool isresolvhook;

        private void Awake()
        {
            hookresolve();
            StartCoroutine(addwhenre());
        }

        private void hookresolve()
        {
            if (isresolvhook) return;
            isresolvhook = true;
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Logger.LogInfo("res hook watching " + dir);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var wantname = new AssemblyName(args.Name).Name;
                var candidate = Path.Combine(dir, wantname + ".dll");
                if (File.Exists(candidate))
                {
                    Logger.LogInfo("davinci resolved " + wantname + " arrow-> " + candidate);
                    return Assembly.LoadFrom(candidate);
                }
                Logger.LogWarning("couldnt davinci resolve " + wantname + ", even tho look for " + candidate);
                return null;
            };
        }

        private IEnumerator addwhenre()
        {
            while (ContentDatabase.Instance() == null)
            {
                yield return null;
            }
            loadturtabilitiesfrom(ownfolder, recursive: true);
            loadturtabilitiesfrom(turtabilityfolder, createifmissing: true);
        }

        private void loadturtabilitiesfrom(string folder, bool createifmissing = false, bool recursive = false)
        {
            if (!Directory.Exists(folder))
            {
                if (createifmissing)
                {
                    Directory.CreateDirectory(folder);
                    Logger.LogInfo("no turt abilities folder found :(. so i made one :) " + folder);
                }
                else
                {
                    Logger.LogWarning("expected turt abilities folder but it aint there: " + folder);
                }
                return;
            }

            var searchopt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(folder, "*.turtability", searchopt);
            Logger.LogInfo("we got " + files.Length + " .turtabilities in " + folder + "!! yay");
            foreach (var file in files)
            {
                var def = compileturtability(file);
                if (def != null)
                {
                    loadeddefs.Add(def);
                    addability(def);
                }
            }
        }

        private turtabilitydef compileturtability(string filepath)
        {
            var source = File.ReadAllText(filepath);
            var name = Path.GetFileNameWithoutExtension(filepath);
            var tree = CSharpSyntaxTree.ParseText(source, path: filepath);
            var refs = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                string loc;
                try { loc = asm.Location; }
                catch { continue; }
                if (string.IsNullOrEmpty(loc) || !seen.Add(loc)) continue;
                try { refs.Add(MetadataReference.CreateFromFile(loc)); }
                catch (Exception ex)
                {
                    Logger.LogWarning(name + ": could no reference :( " + asm.GetName().Name + ": " + ex.Message);
                }
            }

            var comp = CSharpCompilation.Create(
                assemblyName: "turtability_" + name + "_" + Guid.NewGuid().ToString("N"),
                syntaxTrees: new[] { tree },
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var stream = new MemoryStream())
            {
                var emitresult = comp.Emit(stream);

                if (!emitresult.Success)
                {
                    Logger.LogError(name + " failed to compile:");
                    foreach (var d in emitresult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        var linespan = d.Location.GetLineSpan();
                        Logger.LogError("  line " + (linespan.StartLinePosition.Line + 1) + ": " + d.GetMessage());
                    }
                    return null;
                }

                stream.Seek(0, SeekOrigin.Begin);
                var compiledasm = Assembly.Load(stream.ToArray());

                var abilitytype = compiledasm.GetTypes()
                    .FirstOrDefault(t => typeof(turtabilitybase).IsAssignableFrom(t) && !t.IsAbstract);

                if (abilitytype == null)
                {
                    Logger.LogError(name + " compiled good but it dont have a class inheriting turtabilitybase soooo get it togetherr");
                    return null;
                }

                return new turtabilitydef
                {
                    name = name,
                    filepath = filepath,
                    localid = Math.Abs(name.GetHashCode()),
                    compiledtype = abilitytype
                };
            }
        }

        private void addability(turtabilitydef def)
        {
            var val = new GameObject(def.name);
            val.SetActive(false);
            DontDestroyOnLoad(val);
            val.hideFlags = (HideFlags)52;

            var obj = val.AddComponent<SpecialAbility>();

            var component = val.GetComponent<CharacterItem>();
            if (component != null)
            {
                component.tags = new List<CharacterItem.Tag>
                {
                    new CharacterItem.Tag((CharacterItem.TagType)0, "Turtle Ability Loader")
                };
            }

            var entity = new DatabaseEntity((WorkshopContentType)5)
            {
                GUID = new DatabaseID(765421243, def.localid),
                Name = def.name
            };
            doprivstuff(obj, "m_entity", entity);
            var abilitycomponent = (turtabilitybase)val.AddComponent(def.compiledtype);
            ((Behaviour)abilitycomponent).enabled = false;

            var conditional = val.AddComponent<ConditionalEvent>();
            conditional.enabled = false;

            var instance = new ConditionalEventInstance();
            instance.delay = 0f;
            instance.turnOnEvent = new UnityEvent();
            instance.turnOffEvent = new UnityEvent();
            instance.continuousEvent = new UnityEvent();
            instance.conditions = new EventCondition[]
            {
                new EventCondition
                {
                    conditionType = (EventCondition.ConditionType)0,
                    startOnCD = true,
                    alwaysResetCounter = true,
                    value = abilitycomponent.cooldown,
                    counter = abilitycomponent.cooldown
                },
                new EventCondition
                {
                    conditionType = (EventCondition.ConditionType)1,
                    whichRange = (EventCondition.WhichRange)0,
                    valueType = (EventCondition.ValueType)1,
                    value = abilitycomponent.range,
                    startOnCD = true
                }
            };
            instance.turnOnEvent.AddListener(new UnityAction(abilitycomponent.TriggerFromEvent));
            conditional.events = new[] { instance };
            val.AddComponent<turtonlyifequip>();
            var landfallcontentdatabase = ContentDatabase.Instance().LandfallContentDatabase;
            var nonstreamable = getprivatefield<Dictionary<DatabaseID, UnityEngine.Object>>(ContentDatabase.Instance().AssetLoader, "m_nonStreamableAssets");
            var combatmoves = getprivatefield<Dictionary<DatabaseID, GameObject>>(landfallcontentdatabase, "m_combatMoves");
            nonstreamable[entity.GUID] = val;
            combatmoves[entity.GUID] = val;

            try
            {
                val.SetActive(true);
            }
            catch (NullReferenceException)
            {
            }

            Logger.LogInfo(def.name + " is now an abilityy, loaded at " + def.filepath);
        }
        private static void doprivstuff(object obj, string fieldname, object value)
        {
            getanyfieldd(obj.GetType(), fieldname)?.SetValue(obj, value);
        }

        private static T getprivatefield<T>(object obj, string fieldname)
        {
            var fieldinfo = getanyfieldd(obj.GetType(), fieldname);
            return fieldinfo == null ? default(T) : (T)fieldinfo.GetValue(obj);
        }

        private static FieldInfo getanyfieldd(Type t, string fieldname)
        {
            while (t != null)
            {
                var field = t.GetField(fieldname, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field;
                t = t.BaseType;
            }
            return null;
        }
    }
}
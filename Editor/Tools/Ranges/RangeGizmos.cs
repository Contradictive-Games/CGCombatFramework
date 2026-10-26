using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal enum RangeScope { SelectedUnits, AllUnits }
    internal enum RangeAim { UnitAim, Mouse }

    internal static class RangeGizmos
    {
        public const string k_Title = "Range Gizmos";
        public const string k_On = "Ranges.On";
        public const string k_Scope = "Ranges.Scope";
        public const string k_Aim = "Ranges.Aim";
        public const string k_Labels = "Ranges.Labels";
        public const string k_Spread = "Ranges.Spread";
        public const string k_Paths = "Ranges.Paths";
        public const string k_Hits = "Ranges.Hits";
        public const string k_Fill = "Ranges.Fill";
        private const string k_Preview = "Ranges.Preview";
        private const string k_Menu = StudioWindows.k_Menu + k_Title;
        private const int k_Depth = 12;
        private const float k_Step = 0.02f;
        private const float k_FillAlpha = 0.07f;

        private static readonly Color[] palette =
        {
            new(0.30f, 0.55f, 0.96f), new(0.91f, 0.57f, 0.23f), new(0.25f, 0.73f, 0.31f),
            new(0.86f, 0.38f, 0.64f), new(0.64f, 0.44f, 0.97f), new(0.22f, 0.77f, 0.81f)
        };
        private static readonly RaycastHit[] hits = new RaycastHit[32];
        private static GUIStyle label;

        private struct Shot
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public Vector3 AimPoint;
            public Unit Source;
            public Color Color;
        }

        public static bool On => StudioPrefs.Get(k_On, false);

        public static FirableDefinition Preview
        {
            get
            {
                string guid = StudioPrefs.GetString(k_Preview);
                return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<FirableDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            }
            set => StudioPrefs.Store(k_Preview, value != null ? AssetChanges.GuidOf(value) : string.Empty);
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
            StudioPrefs.Changed -= SceneView.RepaintAll;
            StudioPrefs.Changed += SceneView.RepaintAll;
            StudioWindows.RegisterWindow(k_Title, () => Show(null));
            StudioWindows.RegisterAssetAction("Show Range in Scene", asset => asset is FirableDefinition, asset => Show((FirableDefinition)asset));
        }

        [MenuItem(k_Menu, false, 27)]
        private static void ToggleFromMenu()
        {
            if(On) StudioPrefs.Set(k_On, false);
            else Show(null);
        }

        [MenuItem(k_Menu, true)]
        private static bool ValidateMenu()
        {
            Menu.SetChecked(k_Menu, On);
            return true;
        }

        public static void Show(FirableDefinition firable)
        {
            if(firable != null) Preview = firable;
            StudioPrefs.Set(k_On, true);
            RangeOverlay.Reveal();
            SceneView.RepaintAll();
        }

        private static void Draw(SceneView view)
        {
            if(!On) return;
            var evt = Event.current;
            bool mouse = (RangeAim)StudioPrefs.GetInt(k_Aim, 0) == RangeAim.Mouse;
            if(mouse && evt.type == EventType.MouseMove) view.Repaint();
            if(evt.type != EventType.Repaint) return;
            if(StudioPrefs.Get(k_Hits, true)) Physics.SyncTransforms();
            label ??= new GUIStyle(EditorStyles.miniBoldLabel);

            int index = 0;
            foreach(var unit in Units())
            {
                if(unit == null) continue;
                Aim(unit, mouse, out var origin, out var direction, out var aimPoint);
                foreach(var (firable, slot) in Firables(unit))
                {
                    var shot = new Shot { Origin = origin, Direction = direction, AimPoint = aimPoint, Source = unit, Color = palette[index++ % palette.Length] };
                    string title = string.IsNullOrEmpty(slot) ? StudioKinds.NameOf(firable) : $"{slot}: {StudioKinds.NameOf(firable)}";
                    Text(origin + Vector3.up * 0.35f, title, shot.Color);
                    foreach(var emission in firable.Emissions ?? Array.Empty<IEmission>())
                    {
                        Emission(emission, shot, 0);
                    }
                }
            }
        }

        private static IEnumerable<Unit> Units()
        {
            if((RangeScope)StudioPrefs.GetInt(k_Scope, 0) == RangeScope.AllUnits)
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                return stage != null ? stage.prefabContentsRoot.GetComponentsInChildren<Unit>() : Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
            }
            var units = new List<Unit>();
            foreach(var selected in Selection.gameObjects)
            {
                var unit = selected.GetComponentInParent<Unit>();
                if(unit != null && !units.Contains(unit) && !EditorUtility.IsPersistent(unit)) units.Add(unit);
            }
            return units;
        }

        private static IEnumerable<(FirableDefinition firable, string slot)> Firables(Unit unit)
        {
            var preview = Preview;
            if(preview != null)
            {
                yield return (preview, "Preview");
                yield break;
            }
            var loadout = unit.GetComponent<FirableLoadout>();
            if(loadout == null) yield break;
            if(Application.isPlaying && unit.IsSpawned && loadout.Equipped.Count > 0)
            {
                foreach(var pair in loadout.Equipped)
                {
                    if(pair.Value?.Definition != null) yield return (pair.Value.Definition, pair.Key != null ? StudioKinds.NameOf(pair.Key) : null);
                }
                yield break;
            }
            foreach(var entry in FirableDamage.Read<FirableLoadout.SlotEntry[]>(loadout, "startingLoadout") ?? Array.Empty<FirableLoadout.SlotEntry>())
            {
                if(entry.Firable != null) yield return (entry.Firable, entry.Slot != null ? StudioKinds.NameOf(entry.Slot) : null);
            }
        }

        private static void Aim(Unit unit, bool mouse, out Vector3 origin, out Vector3 direction, out Vector3 aimPoint)
        {
            var tf = unit.transform;
            origin = tf.position;
            direction = tf.forward;
            aimPoint = origin + direction * 10f;
            bool flat = false;
            switch(unit.GetComponent<IAimProvider>())
            {
                case TransformAimProvider transformAim:
                    origin = transformAim.Origin;
                    aimPoint = transformAim.AimPoint;
                    direction = transformAim.Direction;
                    break;
                case GroundPlaneAimProvider ground:
                    origin = ground.Origin;
                    flat = true;
                    if(Application.isPlaying && ground.AimPoint != Vector3.zero)
                    {
                        aimPoint = ground.AimPoint;
                        direction = ground.Direction;
                    }
                    else
                    {
                        direction = Flatten(ground.Muzzle.forward, tf.forward);
                        aimPoint = origin + direction * 10f;
                    }
                    break;
                case IAimProvider other when Application.isPlaying:
                    origin = other.Origin;
                    aimPoint = other.AimPoint;
                    direction = other.Direction;
                    break;
            }

            if(!mouse) return;
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if(Blocking(unit, ray.origin, ray.direction, 5000f, out var hit, out _)) aimPoint = hit.point;
            else if(new Plane(Vector3.up, origin).Raycast(ray, out float enter)) aimPoint = ray.GetPoint(enter);
            else return;
            var to = aimPoint - origin;
            if(flat) to = Flatten(to, direction);
            if(to.sqrMagnitude > 0.0001f) direction = to.normalized;
        }

        private static void Emission(object node, Shot shot, int depth)
        {
            if(node == null || depth > k_Depth) return;
            switch(node)
            {
                case Delivery delivery:
                    Query(FirableDamage.Read<IHitQuery>(delivery, "query"), shot);
                    foreach(var payload in FirableDamage.Read<IPayload[]>(delivery, "payloads") ?? Array.Empty<IPayload>())
                    {
                        Payload(payload, shot, depth + 1);
                    }
                    return;
                case ProjectileEmission projectile:
                    Projectile(projectile, shot, depth);
                    return;
                case RadialEmission radial:
                    Radial(radial, shot, depth);
                    return;
                case EmitFromEmission moved:
                    var point = Center(FirableDamage.Read<QueryCenter>(moved, "from"), shot) + FirableDamage.Read<Vector3>(moved, "offset");
                    var next = shot;
                    next.Origin = point;
                    next.Direction = Steer(FirableDamage.Read<EmitDirection>(moved, "direction"), shot, point);
                    Dotted(shot.Origin, point, shot.Color);
                    Children(moved, next, depth);
                    return;
            }
            Children(node, shot, depth);
        }

        private static void Children(object node, Shot shot, int depth)
        {
            foreach(var (child, _) in FirableDamage.Children(node))
            {
                if(child is IEmission) Emission(child, shot, depth + 1);
                else Payload(child, shot, depth + 1);
            }
            if(!(node is WeightedRandomEmission)) return;
            foreach(var option in FirableDamage.Read<WeightedEmission[]>(node, "options") ?? Array.Empty<WeightedEmission>())
            {
                foreach(var emission in option?.Emissions ?? Array.Empty<IEmission>())
                {
                    Emission(emission, shot, depth + 1);
                }
            }
        }

        private static void Payload(object payload, Shot shot, int depth)
        {
            if(payload == null || depth > k_Depth) return;
            if(payload is EmitAtPayload emitAt)
            {
                var next = shot;
                if(FirableDamage.Read<EmitFrom>(emitAt, "emitFrom") != EmitFrom.SourceAim)
                {
                    next.Origin = shot.AimPoint;
                    Dotted(shot.Origin, shot.AimPoint, shot.Color);
                }
                foreach(var emission in FirableDamage.Read<IEmission[]>(emitAt, "emissions") ?? Array.Empty<IEmission>())
                {
                    Emission(emission, next, depth + 1);
                }
                return;
            }
            Children(payload, shot, depth);
        }

        private static void Query(IHitQuery query, Shot shot)
        {
            if(query == null) return;
            var color = shot.Color;
            switch(query)
            {
                case RaycastQuery _:
                    Raycast(query, shot);
                    break;
                case SphereQuery _:
                    var center = Center(FirableDamage.Read<QueryCenter>(query, "center"), shot);
                    float radius = FirableDamage.Read<float>(query, "radius");
                    Sphere(center, radius, color);
                    Text(center + Vector3.up * radius, $"Sphere {radius:0.##}m", color);
                    break;
                case ConeQuery _:
                    float angle = FirableDamage.Read<float>(query, "angle");
                    float range = FirableDamage.Read<float>(query, "range");
                    var forward = FirableDamage.Read<bool>(query, "flatten") ? Flatten(shot.Direction, shot.Source.transform.forward) : shot.Direction;
                    Cone(shot.Origin, forward, angle, range, color);
                    Text(shot.Origin + forward * range, $"Cone {angle:0}° · {range:0.##}m", color);
                    break;
                case BoxQuery _:
                    Box(query, shot);
                    break;
                case ChainQuery _:
                    float first = FirableDamage.Read<float>(query, "firstRange");
                    float jump = FirableDamage.Read<float>(query, "jumpRange");
                    int jumps = FirableDamage.Read<int>(query, "jumps");
                    Disc(shot.Origin, first, color, true);
                    Disc(shot.AimPoint, jump, color, false);
                    Text(shot.Origin + Vector3.up * 0.2f + shot.Direction * first, $"Chain · first {first:0.##}m · {jumps} jumps of {jump:0.##}m", color);
                    break;
                case EveryoneQuery _:
                    float distance = FirableDamage.Read<float>(query, "maxDistance");
                    if(distance > 0f) Disc(shot.Origin, distance, color, false);
                    Text(shot.Origin + Vector3.up * 0.6f, distance > 0f ? $"Everyone within {distance:0.##}m" : "Everyone on the map", color);
                    break;
                default:
                    Text(shot.Origin + Vector3.up * 0.6f, BlockTypes.DisplayName(query.GetType()), color);
                    break;
            }
        }

        private static void Raycast(IHitQuery query, Shot shot)
        {
            var color = shot.Color;
            float range = FirableDamage.Read<float>(query, "range");
            float radius = FirableDamage.Read<float>(query, "radius");
            float spread = FirableDamage.Read<float>(query, "spreadDegrees");
            bool sideways = FirableDamage.Read<bool>(query, "horizontalSpreadOnly");
            var dir = shot.Direction.sqrMagnitude > 0.0001f ? shot.Direction.normalized : Vector3.forward;
            var end = shot.Origin + dir * range;

            float reach = range;
            if(StudioPrefs.Get(k_Hits, true) && Blocking(shot.Source, shot.Origin, dir, range, out var hit, out var hitUnit))
            {
                reach = hit.distance;
                Marker(hit.point, color);
                Text(hit.point + Vector3.up * 0.25f, hitUnit != null ? $"hits {hitUnit.DisplayName} at {hit.distance:0.#}m" : $"blocked at {hit.distance:0.#}m", color);
            }
            Line(shot.Origin, shot.Origin + dir * reach, color, 2f);
            if(reach < range) Dotted(shot.Origin + dir * reach, end, color);

            if(radius > 0f)
            {
                using(new Handles.DrawingScope(color))
                {
                    Handles.DrawWireDisc(shot.Origin, dir, radius);
                    Handles.DrawWireDisc(end, dir, radius);
                    var side = Vector3.Cross(dir, Vector3.up).sqrMagnitude > 0.0001f ? Vector3.Cross(dir, Vector3.up).normalized : Vector3.right;
                    var up = Vector3.Cross(side, dir).normalized;
                    foreach(var offset in new[] { side, -side, up, -up })
                    {
                        Handles.DrawLine(shot.Origin + offset * radius, end + offset * radius);
                    }
                }
            }

            if(spread > 0f && StudioPrefs.Get(k_Spread, true))
            {
                var rotation = Quaternion.LookRotation(dir);
                var left = rotation * Quaternion.Euler(0f, -spread, 0f) * Vector3.forward;
                Faint(shot.Origin, shot.Origin + left * range, color);
                Faint(shot.Origin, shot.Origin + rotation * Quaternion.Euler(0f, spread, 0f) * Vector3.forward * range, color);
                if(!sideways)
                {
                    Faint(shot.Origin, shot.Origin + rotation * Quaternion.Euler(-spread, 0f, 0f) * Vector3.forward * range, color);
                    Faint(shot.Origin, shot.Origin + rotation * Quaternion.Euler(spread, 0f, 0f) * Vector3.forward * range, color);
                }
                using(new Handles.DrawingScope(Faded(color, 0.5f)))
                {
                    Handles.DrawWireArc(shot.Origin, rotation * Vector3.up, left, spread * 2f, range);
                }
            }
            string text = $"Raycast {range:0.##}m";
            if(radius > 0f) text += $" · {radius:0.##}m wide";
            if(spread > 0f) text += $" · ±{spread:0.#}°";
            Text(end, text, color);
        }

        private static void Box(IHitQuery query, Shot shot)
        {
            float length = FirableDamage.Read<float>(query, "length");
            float width = FirableDamage.Read<float>(query, "width");
            float height = FirableDamage.Read<float>(query, "height");
            var forward = FirableDamage.Read<bool>(query, "flatten") ? Flatten(shot.Direction, shot.Source.transform.forward) : shot.Direction.normalized;
            var center = shot.Origin + forward * (length * 0.5f);
            using(new Handles.DrawingScope(shot.Color, Matrix4x4.TRS(center, Quaternion.LookRotation(forward), Vector3.one)))
            {
                Handles.DrawWireCube(Vector3.zero, new Vector3(width, height, length));
            }
            Text(shot.Origin + forward * length, $"Box {length:0.##} × {width:0.##} × {height:0.##}m", shot.Color);
        }

        private static void Radial(RadialEmission radial, Shot shot, int depth)
        {
            int count = Mathf.Max(1, FirableDamage.Read<int>(radial, "count"));
            float arc = FirableDamage.Read<float>(radial, "arc");
            float offset = FirableDamage.Read<float>(radial, "offset");
            bool ring = arc >= 359.9f;
            float reach = Vector3.Distance(shot.Origin, shot.AimPoint);
            for(int i = 0; i < count; i++)
            {
                float t = ring ? (float)i / count : count == 1 ? 0.5f : (float)i / (count - 1);
                float angle = offset + (ring ? t * 360f : (t - 0.5f) * arc);
                var next = shot;
                next.Direction = Quaternion.AngleAxis(angle, Vector3.up) * shot.Direction;
                next.AimPoint = shot.Origin + next.Direction * reach;
                foreach(var emission in FirableDamage.Read<IEmission[]>(radial, "emissions") ?? Array.Empty<IEmission>())
                {
                    Emission(emission, next, depth + 1);
                }
            }
        }

        private static void Projectile(ProjectileEmission projectile, Shot shot, int depth)
        {
            var color = shot.Color;
            float speed = FirableDamage.Read<float>(projectile, "speed");
            float lifetime = FirableDamage.Read<float>(projectile, "lifetime");
            float gravity = FirableDamage.Read<float>(projectile, "gravity");
            float radius = FirableDamage.Read<float>(projectile, "radius");
            float spread = FirableDamage.Read<float>(projectile, "spreadDegrees");
            float homing = FirableDamage.Read<float>(projectile, "homingTurnRate");
            bool arc = FirableDamage.Read<bool>(projectile, "arcToAimPoint");

            var velocity = arc && gravity > 0f ? ArcVelocity(shot.Origin, shot.AimPoint, shot.Direction, speed, gravity) : shot.Direction.normalized * speed;
            var points = new List<Vector3> { shot.Origin };
            var position = shot.Origin;
            Unit hitUnit = null;
            bool hitSomething = false;
            float travelled = 0f;
            bool paths = StudioPrefs.Get(k_Paths, true);
            bool test = StudioPrefs.Get(k_Hits, true);
            for(float time = 0f; time < lifetime; time += k_Step)
            {
                if(gravity > 0f) velocity += Vector3.down * (gravity * k_Step);
                var move = velocity * k_Step;
                float length = move.magnitude;
                if(test && length > 0.0001f && Blocking(shot.Source, position, move / length, length + radius, out var hit, out hitUnit))
                {
                    position += move / length * hit.distance;
                    points.Add(position);
                    hitSomething = true;
                    break;
                }
                position += move;
                travelled += length;
                points.Add(position);
            }

            if(paths)
            {
                using(new Handles.DrawingScope(color))
                {
                    Handles.DrawAAPolyLine(2f, points.ToArray());
                }
            }
            else Dotted(shot.Origin, position, color);
            Marker(position, color);
            if(radius > 0f) Sphere(position, radius, Faded(color, 0.7f));
            if(spread > 0f && StudioPrefs.Get(k_Spread, true))
            {
                var rotation = Quaternion.LookRotation(shot.Direction.sqrMagnitude > 0.0001f ? shot.Direction : Vector3.forward);
                float preview = Mathf.Min(4f, speed * lifetime);
                Faint(shot.Origin, shot.Origin + rotation * Quaternion.Euler(0f, -spread, 0f) * Vector3.forward * preview, color);
                Faint(shot.Origin, shot.Origin + rotation * Quaternion.Euler(0f, spread, 0f) * Vector3.forward * preview, color);
            }
            if(homing > 0f) Disc(position, FirableDamage.Read<float>(projectile, "homingRadius"), color, true);

            string text = $"Projectile {speed:0.#}m/s · {lifetime:0.##}s";
            if(hitSomething) text += hitUnit != null ? $" · hits {hitUnit.DisplayName}" : " · hits the world";
            else text += $" · {travelled:0.#}m";
            if(homing > 0f) text += " · homing";
            Text(position + Vector3.up * 0.3f, text, color);

            var landed = shot;
            landed.Origin = position;
            landed.AimPoint = position;
            if(velocity.sqrMagnitude > 0.0001f) landed.Direction = velocity.normalized;
            foreach(var emission in FirableDamage.Read<IEmission[]>(projectile, "onStop") ?? Array.Empty<IEmission>())
            {
                Emission(emission, landed, depth + 1);
            }
            foreach(var emission in FirableDamage.Read<IEmission[]>(projectile, "onHitUnit") ?? Array.Empty<IEmission>())
            {
                Emission(emission, landed, depth + 1);
            }
        }

        private static Vector3 ArcVelocity(Vector3 from, Vector3 to, Vector3 fallback, float speed, float gravity)
        {
            var flat = to - from;
            float height = flat.y;
            flat.y = 0f;
            float distance = flat.magnitude;
            if(distance < 0.01f || speed <= 0f) return fallback.normalized * speed;
            float time = distance / speed;
            float vertical = height / time + 0.5f * gravity * time;
            return flat / distance * speed + Vector3.up * vertical;
        }

        private static bool Blocking(Unit self, Vector3 origin, Vector3 direction, float distance, out RaycastHit first, out Unit unit)
        {
            first = default;
            unit = null;
            var scene = self != null ? self.gameObject.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
            int count = scene.Raycast(origin, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for(int i = 0; i < count; i++)
            {
                var owner = hits[i].collider.GetComponentInParent<Unit>();
                if(self != null && owner == self) continue;
                if(hits[i].distance >= best) continue;
                best = hits[i].distance;
                first = hits[i];
                unit = owner;
                found = true;
            }
            return found;
        }

        private static Vector3 Center(QueryCenter center, Shot shot)
        {
            switch(center)
            {
                case QueryCenter.Source: return shot.Source != null ? shot.Source.transform.position : shot.Origin;
                case QueryCenter.AimPoint:
                case QueryCenter.Target: return shot.AimPoint;
                default: return shot.Origin;
            }
        }

        private static Vector3 Steer(EmitDirection steer, Shot shot, Vector3 point)
        {
            switch(steer)
            {
                case EmitDirection.Down: return Vector3.down;
                case EmitDirection.TowardAimPoint:
                case EmitDirection.TowardTarget:
                    var toward = shot.AimPoint - point;
                    return toward.sqrMagnitude > 0.0001f ? toward.normalized : shot.Direction;
                default: return shot.Direction;
            }
        }

        private static Vector3 Flatten(Vector3 vector, Vector3 fallback)
        {
            vector.y = 0f;
            if(vector.sqrMagnitude > 0.0001f) return vector.normalized;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        private static void Cone(Vector3 origin, Vector3 forward, float angle, float range, Color color)
        {
            var from = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward;
            if(StudioPrefs.Get(k_Fill, true))
            {
                using(new Handles.DrawingScope(Faded(color, k_FillAlpha)))
                {
                    Handles.DrawSolidArc(origin, Vector3.up, from, angle, range);
                }
            }
            using(new Handles.DrawingScope(color))
            {
                Handles.DrawWireArc(origin, Vector3.up, from, angle, range, 2f);
                if(angle < 360f)
                {
                    Handles.DrawLine(origin, origin + from * range, 2f);
                    Handles.DrawLine(origin, origin + Quaternion.AngleAxis(angle, Vector3.up) * from * range, 2f);
                }
            }
        }

        private static void Sphere(Vector3 center, float radius, Color color)
        {
            if(StudioPrefs.Get(k_Fill, true))
            {
                using(new Handles.DrawingScope(Faded(color, k_FillAlpha)))
                {
                    Handles.DrawSolidDisc(center, Vector3.up, radius);
                }
            }
            using(new Handles.DrawingScope(color))
            {
                Handles.DrawWireDisc(center, Vector3.up, radius, 2f);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
        }

        private static void Disc(Vector3 center, float radius, Color color, bool dotted)
        {
            if(radius <= 0f) return;
            using(new Handles.DrawingScope(dotted ? Faded(color, 0.5f) : color))
            {
                Handles.DrawWireDisc(center, Vector3.up, radius, dotted ? 1f : 2f);
            }
        }

        private static void Marker(Vector3 point, Color color)
        {
            using(new Handles.DrawingScope(color))
            {
                float size = HandleUtility.GetHandleSize(point) * 0.08f;
                Handles.DrawSolidDisc(point, Camera.current != null ? -Camera.current.transform.forward : Vector3.up, size);
            }
        }

        private static void Line(Vector3 from, Vector3 to, Color color, float thickness)
        {
            using(new Handles.DrawingScope(color))
            {
                Handles.DrawLine(from, to, thickness);
            }
        }

        private static void Faint(Vector3 from, Vector3 to, Color color)
        {
            using(new Handles.DrawingScope(Faded(color, 0.45f)))
            {
                Handles.DrawLine(from, to);
            }
        }

        private static void Dotted(Vector3 from, Vector3 to, Color color)
        {
            using(new Handles.DrawingScope(Faded(color, 0.6f)))
            {
                Handles.DrawDottedLine(from, to, 4f);
            }
        }

        private static void Text(Vector3 position, string text, Color color)
        {
            if(!StudioPrefs.Get(k_Labels, true)) return;
            label.normal.textColor = color;
            Handles.Label(position, text, label);
        }

        private static Color Faded(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}

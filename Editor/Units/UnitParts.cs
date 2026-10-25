using System;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    [Flags]
    internal enum UnitPart
    {
        None = 0,
        Stats = 1 << 0,
        Resources = 1 << 1,
        Health = 1 << 2,
        StatusEffects = 1 << 3,
        Loadout = 1 << 4,
        ViewBinder = 1 << 5,
        Knockback = 1 << 6,
        TransformAim = 1 << 7,
        GroundAim = 1 << 8
    }

    internal sealed class PartInfo
    {
        public readonly UnitPart Part;
        public readonly Type Type;
        public readonly string Label;
        public readonly string Description;
        public readonly string Style;
        public readonly UnitPart Requires;
        public readonly UnitPart Excludes;

        public PartInfo(UnitPart part, Type type, string label, string description, string style, UnitPart requires = UnitPart.None, UnitPart excludes = UnitPart.None)
        {
            Part = part;
            Type = type;
            Label = label;
            Description = description;
            Style = style;
            Requires = requires;
            Excludes = excludes;
        }
    }

    internal static class UnitParts
    {
        public static readonly PartInfo[] All =
        {
            new PartInfo(UnitPart.Stats, typeof(StatsComponent), "Stats", "Base stat values from a stat profile. Damage steps, scalers and costs read these.", "cg-kind-stat"),
            new PartInfo(UnitPart.Resources, typeof(ResourceComponent), "Resources", "Health, shields, mana and other pools from the profile. A pool's max can follow a stat.", "cg-kind-resource"),
            new PartInfo(UnitPart.Health, typeof(HealthComponent), "Health", "Takes damage through a damage pipeline and dies when its health pool runs out.", "cg-kind-damage", UnitPart.Resources),
            new PartInfo(UnitPart.StatusEffects, typeof(StatusEffectsComponent), "Status Effects", "Lets effects be applied, with optional innate effects on every spawn.", "cg-kind-effect"),
            new PartInfo(UnitPart.Loadout, typeof(FirableLoadout), "Firable Loadout", "Slots that hold firables, equipped when the unit spawns.", "cg-kind-firable"),
            new PartInfo(UnitPart.ViewBinder, typeof(UnitViewBinder), "View Binder", "Binds IUnitView components in children, like health bars and hit flashes.", "cg-kind-view"),
            new PartInfo(UnitPart.Knockback, typeof(RigidbodyKnockback), "Rigidbody Knockback", "Knockback and teleports through a Rigidbody, which is added for you.", "cg-kind-modifier"),
            new PartInfo(UnitPart.TransformAim, typeof(TransformAimProvider), "Transform Aim", "Aims where a transform faces, or at its target. Good for AI and turrets.", "cg-kind-aim", UnitPart.None, UnitPart.GroundAim),
            new PartInfo(UnitPart.GroundAim, typeof(GroundPlaneAimProvider), "Ground Plane Aim", "Aims at the mouse on the ground plane. Good for top-down players.", "cg-kind-aim", UnitPart.None, UnitPart.TransformAim)
        };

        public static PartInfo Of(UnitPart part)
        {
            foreach(var info in All)
            {
                if(info.Part == part) return info;
            }
            return null;
        }

        public static UnitPart Present(GameObject prefab)
        {
            var parts = UnitPart.None;
            if(prefab == null) return parts;
            foreach(var info in All)
            {
                if(prefab.GetComponent(info.Type) != null) parts |= info.Part;
            }
            return parts;
        }

        public static UnitPart Toggle(UnitPart parts, UnitPart part, bool on)
        {
            var info = Of(part);
            if(!on)
            {
                parts &= ~part;
                foreach(var other in All)
                {
                    if((other.Requires & part) != 0) parts &= ~other.Part;
                }
                return parts;
            }
            parts |= part;
            if(info != null)
            {
                parts |= info.Requires;
                parts &= ~info.Excludes;
            }
            return parts;
        }

        public static int Count(UnitPart parts)
        {
            int count = 0;
            foreach(var info in All)
            {
                if((parts & info.Part) != 0) count++;
            }
            return count;
        }
    }
}

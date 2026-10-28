using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;

namespace CapstoneDesign.Runtime.LocalState
{
    [Serializable]
    public sealed class GardenAppearanceCredit
    {
        public string CycleId, MissionId;
        public int Category;
        internal GardenAppearanceCredit Copy() => (GardenAppearanceCredit)MemberwiseClone();
    }

    // Additive schema-1 data: older builds ignore this element and retain the original
    // wallet, progress and purchase receipts. This is appearance, never another wallet.
    [Serializable]
    public sealed class GardenPlantAppearance
    {
        public int Version = 1, MappingVersion = 1;
        // Compatibility shape prior, NOT completed activities or reward receipts.
        // It preserves the former chamomile when no known activity history exists and
        // keeps the existing three-purchase progression without a new activity gate.
        public int[] BaselineProfileCounts = { 4, 0, 0, 0, 0, 2 };
        public PlantRecipe Recipe;
        public List<GardenAppearanceCredit> Credits = new List<GardenAppearanceCredit>();
        // Minimal appearance-only ledger retained when detailed mission records are reset.
        public List<GardenAppearanceCredit> ArchivedCredits = new List<GardenAppearanceCredit>();

        internal GardenPlantAppearance Copy()
        {
            return new GardenPlantAppearance {
                Version = Version, MappingVersion = MappingVersion,
                BaselineProfileCounts = (int[])BaselineProfileCounts.Clone(),
                Recipe = GardenPlantVisuals.CopyRecipe(Recipe),
                Credits = Credits.Select(c => c.Copy()).ToList(),
                ArchivedCredits = ArchivedCredits.Select(c => c.Copy()).ToList()
            };
        }
    }

    public static class GardenPlantVisuals
    {
        public const string InstanceId = "garden-main";
        public const string GrowthPlantId = "plant:P06";
        const float BaseScale = 1.9f, BlossomGrowth = .3f, ScalePerGrowth = .25f, MaximumExtraScale = .18f;
        // This is the existing renderer's limit, not a new recipe maturity rule.
        // Saved progress beyond it remains valid and is never reduced on load.
        public const float MaxVisualGrowth = BlossomGrowth + MaximumExtraScale / ScalePerGrowth;
        static readonly int[] CompatibilityPrior = { 4, 0, 0, 0, 0, 2 };

        // Explicit, versioned content mapping. Free text, mood, sensor values and
        // unknown/imported mission IDs never determine a plant's appearance.
        static readonly GrowthCategory[] LegacyCourseCategories = {
            GrowthCategory.Breath, GrowthCategory.Senses, GrowthCategory.Body, GrowthCategory.Senses,
            GrowthCategory.Breath, GrowthCategory.Senses, GrowthCategory.SelfCare, GrowthCategory.Reflection,
            GrowthCategory.Body, GrowthCategory.Breath, GrowthCategory.Reflection, GrowthCategory.Senses,
            GrowthCategory.Reflection, GrowthCategory.SelfCare, GrowthCategory.SelfCare, GrowthCategory.Body,
            GrowthCategory.SelfCare, GrowthCategory.SelfCare, GrowthCategory.Senses, GrowthCategory.Reflection,
            GrowthCategory.SelfCare, GrowthCategory.Movement, GrowthCategory.Senses, GrowthCategory.Senses,
            GrowthCategory.Breath, GrowthCategory.Reflection, GrowthCategory.SelfCare, GrowthCategory.SelfCare
        };
        static readonly GrowthCategory[] FreeCategories = {
            GrowthCategory.SelfCare, GrowthCategory.Senses, GrowthCategory.SelfCare, GrowthCategory.Body,
            GrowthCategory.Movement, GrowthCategory.SelfCare, GrowthCategory.Reflection, GrowthCategory.Senses,
            GrowthCategory.SelfCare, GrowthCategory.Reflection, GrowthCategory.Reflection, GrowthCategory.Senses,
            GrowthCategory.Reflection, GrowthCategory.Reflection, GrowthCategory.Movement, GrowthCategory.Movement,
            GrowthCategory.Breath
        };
        // MBCT order, including sound/thought variants, is frozen for mapping v1.
        static readonly GrowthCategory[] MbctCategories = {
            GrowthCategory.Senses, GrowthCategory.Body, GrowthCategory.Breath, GrowthCategory.Senses, GrowthCategory.Body, GrowthCategory.SelfCare,
            GrowthCategory.Senses, GrowthCategory.Senses, GrowthCategory.Movement, GrowthCategory.Breath, GrowthCategory.Body, GrowthCategory.Senses,
            GrowthCategory.Senses, GrowthCategory.Breath, GrowthCategory.Movement, GrowthCategory.Senses, GrowthCategory.Breath, GrowthCategory.Senses,
            GrowthCategory.Senses, GrowthCategory.Reflection, GrowthCategory.Reflection, GrowthCategory.Breath, GrowthCategory.Reflection, GrowthCategory.Body,
            GrowthCategory.Reflection, GrowthCategory.Reflection, GrowthCategory.Reflection, GrowthCategory.SelfCare, GrowthCategory.Breath, GrowthCategory.Reflection,
            GrowthCategory.Reflection, GrowthCategory.Reflection, GrowthCategory.SelfCare, GrowthCategory.Reflection, GrowthCategory.Body, GrowthCategory.Breath,
            GrowthCategory.SelfCare, GrowthCategory.SelfCare, GrowthCategory.Senses, GrowthCategory.SelfCare, GrowthCategory.Senses, GrowthCategory.Breath,
            GrowthCategory.SelfCare, GrowthCategory.SelfCare, GrowthCategory.Senses, GrowthCategory.Body, GrowthCategory.Breath, GrowthCategory.SelfCare
        };

        public static bool TryCategory(string missionId, out GrowthCategory category)
        {
            int sourceOrder=MbctPolicy.LegacySourceOrder(missionId);
            if(sourceOrder>0) { category=MbctCategories[sourceOrder-1];return true; }
            return TryCategory(missionId, "course:MBCT", MbctCategories, out category)
                || TryCategory(missionId, "course:P", LegacyCourseCategories, out category)
                || TryCategory(missionId, "free-mission:M", FreeCategories, out category);
        }
        static bool TryCategory(string missionId, string prefix, GrowthCategory[] categories, out GrowthCategory category)
        {
            category = default(GrowthCategory);
            int order;
            if (missionId == null || !missionId.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(missionId.Substring(prefix.Length), out order)
                || order < 1 || order > categories.Length
                || missionId != prefix + order.ToString("00", System.Globalization.CultureInfo.InvariantCulture)) return false;
            category = categories[order - 1];
            return true;
        }

        public static float TotalGrowth(GardenState state) => state.LegacyGrowth
            + state.Plants.Where(p => p.PlantId == GrowthPlantId).Sum(p => p.Growth);
        public static float ScaleForGrowth(float growth)
        {
            // The derived limit can round just below the clamp in float arithmetic.
            // Treat that shared boundary as the exact maximum for both display and purchasing.
            float extra = growth >= MaxVisualGrowth ? MaximumExtraScale
                : Math.Min(MaximumExtraScale, Math.Max(0, (growth - BlossomGrowth) * ScalePerGrowth));
            return BaseScale * (1 + extra);
        }
        public static bool CanGrow(GardenState state) => state != null
            && ScaleForGrowth(TotalGrowth(state)) < ScaleForGrowth(MaxVisualGrowth);
        public static int StageForGrowth(float growth) => growth < .099f ? 0 : growth < .199f ? 1 : growth < .299f ? 2 : 3;
        public static int StepForGrowth(float growth)
        {
            switch (StageForGrowth(growth)) { case 0: return 0; case 1: return 3; case 2: return 4; default: return 12; }
        }

        // The caller owns a transaction candidate or a detached preview. No save,
        // reward, random identity or independent state store is created here.
        public static bool Sync(GardenState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            bool changed = false;
            if (state.PlantAppearance == null)
            {
                var recipe = PlantGenerator.Create(state.InstallationId, InstanceId, 12, PlantGrowthMode.NutrientPurchases);
                recipe.Counts = (int[])CompatibilityPrior.Clone();
                state.PlantAppearance = new GardenPlantAppearance { Recipe = recipe };
                changed = true;
            }
            var appearance = state.PlantAppearance;
            if (!appearance.Recipe.Mature)
            {
                var retained = appearance.ArchivedCredits.Concat(appearance.Credits).ToArray();
                var seen = new HashSet<string>(retained.Select(c => Key(c.CycleId, c.MissionId)), StringComparer.Ordinal);
                var daily = retained.GroupBy(c => c.CycleId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
                foreach (var session in state.Sessions.Where(s => s.Status == ParticipationState.RewardCommitted)
                    .OrderBy(s => DateTimeOffset.Parse(s.EndedUtc, System.Globalization.CultureInfo.InvariantCulture))
                    .ThenBy(s => s.SessionId, StringComparer.Ordinal))
                {
                    GrowthCategory category; int count;
                    string key = Key(session.InstanceId, session.MissionId);
                    if (seen.Contains(key) || !TryCategory(session.MissionId, out category)
                        || (daily.TryGetValue(session.InstanceId, out count) && count >= 3)) continue;
                    appearance.Credits.Add(new GardenAppearanceCredit { CycleId = session.InstanceId, MissionId = session.MissionId, Category = (int)category });
                    appearance.Recipe.Counts[(int)category] = checked(appearance.Recipe.Counts[(int)category] + 1);
                    seen.Add(key); daily[session.InstanceId] = count + 1; changed = true;
                }
            }
            int target = StepForGrowth(TotalGrowth(state));
            if (appearance.Recipe.GrowthStep > target) throw new InvalidDataException("Saved plant appearance is ahead of garden progress.");
            while (appearance.Recipe.GrowthStep < target)
            {
                appearance.Recipe = PlantGenerator.Advance(appearance.Recipe);
                changed = true;
            }
            return changed;
        }

        public static void PreviewGrowth(GardenState detachedState, float amount)
        {
            ApplyGrowth(detachedState, amount);
        }

        // Shared by the detached preview and the service's transaction candidate.
        // Clamp only a new purchase; never normalize existing saved progress.
        internal static bool ApplyGrowth(GardenState state, float amount)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanGrow(state)) return false;
            var plant = state.Plants.FirstOrDefault(p => p.PlantId == GrowthPlantId);
            float before = plant == null ? 0 : plant.Growth;
            float after = Math.Min(before + amount, MaxVisualGrowth - state.LegacyGrowth);
            if (after <= before) return false;
            if (plant == null) { plant = new PlantProgress { PlantId = GrowthPlantId }; state.Plants.Add(plant); }
            plant.Growth = after;
            Sync(state);
            return true;
        }

        static string Key(string cycle, string mission) => cycle + "|" + mission;
        public static void Validate(GardenState state)
        {
            var appearance = state.PlantAppearance;
            if (appearance == null) return; // Optional for pre-integration schema-1 saves.
            if (appearance.Version != 1 || appearance.MappingVersion != 1 || appearance.BaselineProfileCounts == null
                || !appearance.BaselineProfileCounts.SequenceEqual(CompatibilityPrior) || appearance.Credits == null || appearance.ArchivedCredits == null)
                throw new InvalidDataException("Unsupported plant appearance state.");
            PlantGenerator.Validate(appearance.Recipe);
            var recipe = appearance.Recipe;
            if (recipe.InstanceId != InstanceId || recipe.MaxGrowthSteps != 12 || recipe.GrowthMode != PlantGrowthMode.NutrientPurchases
                || recipe.GrowthStep != StepForGrowth(TotalGrowth(state)))
                throw new InvalidDataException("Plant appearance disagrees with garden progress.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var completed = new HashSet<string>(state.Sessions.Where(s => s.Status == ParticipationState.RewardCommitted)
                .Select(s => Key(s.InstanceId, s.MissionId)), StringComparer.Ordinal);
            var counts = (int[])CompatibilityPrior.Clone();
            foreach (var credit in appearance.ArchivedCredits.Concat(appearance.Credits))
            {
                GrowthCategory category;
                if (credit == null || !TryCategory(credit.MissionId, out category) || credit.Category != (int)category
                    || !seen.Add(Key(credit.CycleId, credit.MissionId))
                    || !state.DailyDecisions.Any(day => day.CycleId == credit.CycleId)
                    || (appearance.Credits.Contains(credit) && !completed.Contains(Key(credit.CycleId, credit.MissionId))))
                    throw new InvalidDataException("Plant appearance credit has no matching completion.");
                counts[credit.Category] = checked(counts[credit.Category] + 1);
            }
            if (appearance.ArchivedCredits.Concat(appearance.Credits).GroupBy(c => c.CycleId).Any(g => g.Count() > 3) || !recipe.Counts.SequenceEqual(counts))
                throw new InvalidDataException("Plant appearance profile disagrees with its credits.");
        }

        // Snapshot reads detach every nested recipe value without XML serialization,
        // geometry generation or a mesh allocation on the UI refresh path.
        internal static PlantRecipe CopyRecipe(PlantRecipe p)
        {
            return new PlantRecipe {
                MaxGrowthSteps = p.MaxGrowthSteps, GrowthMode = p.GrowthMode, GeneratorVersion = p.GeneratorVersion,
                CatalogVersion = p.CatalogVersion, ProfileVersion = p.ProfileVersion, GrowthStep = p.GrowthStep,
                MeshVersion = p.MeshVersion, InstanceId = p.InstanceId, BaseSeed = p.BaseSeed, SpeciesId = p.SpeciesId,
                Counts = (int[])p.Counts.Clone(), LockedProfile = p.LockedProfile == null ? null : (double[])p.LockedProfile.Clone(),
                Height = p.Height, Radius = p.Radius, Bend = p.Bend, Spread = p.Spread, Heading = p.Heading, LeafPhase = p.LeafPhase,
                TargetAxes = p.TargetAxes, TargetLeaves = p.TargetLeaves, TargetHeads = p.TargetHeads,
                Axes = p.Axes.Select(a => new AxisRecipe { Parent = a.Parent, Birth = a.Birth, AttachT = a.AttachT, Length = a.Length,
                    Azimuth = a.Azimuth, Spread = a.Spread, Anchor = a.Anchor }).ToList(),
                Leaves = p.Leaves.Select(l => new LeafRecipe { Parent = l.Parent, Birth = l.Birth, Length = l.Length, Aspect = l.Aspect,
                    Edge = l.Edge, Angle = l.Angle, Attachment = l.Attachment, Scale = l.Scale, Anchor = l.Anchor, Direction = l.Direction }).ToList(),
                Heads = p.Heads.Select(h => new HeadRecipe { Terminal = h.Terminal, Parent = h.Parent, Florets = h.Florets, Petals = h.Petals,
                    Birth = h.Birth, Diameter = h.Diameter, Attachment = h.Attachment, Scale = h.Scale, Anchor = h.Anchor, Tip = h.Tip, Color = h.Color }).ToList(),
                Fruits = p.Fruits.Select(f => new FruitRecipe { Head = f.Head, Floret = f.Floret, EvaluationStep = f.EvaluationStep,
                    Present = f.Present, Radius = f.Radius }).ToList()
            };
        }
    }
}

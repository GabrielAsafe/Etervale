using PristonTale.Gameplay.UI.Widgets;
using PristonTale.Simulation.Skills;

namespace PristonTale.Tests;

internal static class AndroidShortcutPersistenceTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android shortcut persistence preserves profiles and removes old keys", RoundTripAsync);
    }

    private static async Task RoundTripAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"etervale-shortcuts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "mobile-skill-shortcuts.json");
            const uint right = 0x101;
            const uint left = 0x102;
            Skill[] skills =
            [
                new() { Code = right, Name = "Right", Tier = JobTier.Job1, Category = SkillCategory.ActiveAttack, MouseUse = SkillMouseUse.Right },
                new() { Code = left, Name = "Left", Tier = JobTier.Job1, Category = SkillCategory.ActiveAttack, MouseUse = SkillMouseUse.Left }
            ];
            var bindings = new HudSkillBindings();
            bindings.SyncLearned(skills, [new(right, 1), new(left, 1)]);
            Assert.True(bindings.Assign(0, right, SkillMouseUse.Right), "assign F1");
            Assert.True(bindings.Assign(1, left, SkillMouseUse.Left), "assign F2");
            var store = new MobileSkillShortcutStore(path);
            var first = store.SaveAsync("fighter:One", bindings);
            Assert.True(bindings.Assign(7, right, SkillMouseUse.Right), "move F1 to F8");
            var second = store.SaveAsync("fighter:One", bindings);
            await Task.WhenAll(first, second);
            var another = new HudSkillBindings();
            Assert.True(another.Assign(2, HudSkillBindings.BasicAttackId, SkillMouseUse.Left), "assign second profile");
            await store.SaveAsync("fighter:Two", another);

            var loaded = new MobileSkillShortcutStore(path);
            var restored = new HudSkillBindings();
            restored.SyncLearned(skills, [new(right, 1), new(left, 1)]);
            loaded.Restore("fighter:One", restored);
            Assert.True(restored.Shortcut(0).SkillId == 0, "old key resurrected");
            Assert.True(restored.Shortcut(1) == new HudSkillShortcut(left, SkillMouseUse.Left), "F2 not restored");
            Assert.True(restored.Shortcut(7) == new HudSkillShortcut(right, SkillMouseUse.Right), "F8 not restored");
            var other = new HudSkillBindings();
            loaded.Restore("fighter:Two", other);
            Assert.True(other.Shortcut(2).SkillId == HudSkillBindings.BasicAttackId && other.Shortcut(7).SkillId == 0, "profiles mixed");
            var unlearned = new HudSkillBindings();
            loaded.Restore("fighter:One", unlearned);
            Assert.True(unlearned.Shortcut(1).SkillId == 0 && unlearned.Shortcut(7).SkillId == 0, "unlearned skills restored");
            File.WriteAllText(path, "{invalid");
            var invalid = new MobileSkillShortcutStore(path);
            invalid.Restore("fighter:One", unlearned);
            Assert.True(unlearned.Shortcut(1).SkillId == 0, "invalid file produced a binding");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

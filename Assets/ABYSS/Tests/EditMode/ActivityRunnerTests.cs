using System.Collections.Generic;
using Abyss.Logic;
using NUnit.Framework;

namespace Abyss.Tests
{
    public class ActivityRunnerTests
    {
        static ActivityModel ReefLike() => new ActivityModel
        {
            Id = "reef",
            Steps = new List<StepModel>
            {
                new StepModel { Id = "find3", Kind = StepKind.Discover, TargetIds = { "a", "b", "c" }, RequireAllTargets = true },
                new StepModel { Id = "go", Kind = StepKind.ReachDestination, TargetIds = { "hub" } },
            }
        };

        [Test]
        public void StartsOnFirstStep()
        {
            var r = new ActivityRunner();
            r.Start(ReefLike());
            Assert.AreEqual(RunnerState.Running, r.State);
            Assert.AreEqual("find3", r.CurrentStep.Id);
        }

        [Test]
        public void DiscoverNeedsAllTargetsInAnyOrder()
        {
            var r = new ActivityRunner();
            r.Start(ReefLike());
            Assert.IsTrue(r.Report(StepKind.Discover, "c"));
            Assert.IsTrue(r.Report(StepKind.Discover, "a"));
            Assert.AreEqual("find3", r.CurrentStep.Id);
            Assert.IsTrue(r.Report(StepKind.Discover, "b"));
            Assert.AreEqual("go", r.CurrentStep.Id);
        }

        [Test]
        public void DuplicateHitDoesNotCountTwice()
        {
            var r = new ActivityRunner();
            r.Start(ReefLike());
            Assert.IsTrue(r.Report(StepKind.Discover, "a"));
            Assert.IsFalse(r.Report(StepKind.Discover, "a"));
            Assert.AreEqual(1, r.HitTargets.Count);
        }

        [Test]
        public void WrongTargetFiresEventAndDoesNotAdvance()
        {
            var r = new ActivityRunner();
            string wrong = null;
            r.WrongTarget += id => wrong = id;
            r.Start(ReefLike());
            Assert.IsFalse(r.Report(StepKind.Discover, "nope"));
            Assert.AreEqual("nope", wrong);
            Assert.AreEqual("find3", r.CurrentStep.Id);
        }

        [Test]
        public void WalkingThroughWrongMarkerIsNotFlagged()
        {
            var r = new ActivityRunner();
            bool flagged = false;
            r.WrongTarget += _ => flagged = true;
            r.Start(ReefLike());
            r.Report(StepKind.ReachDestination, "hub");
            Assert.IsFalse(flagged);
        }

        [Test]
        public void InspectCountsForDiscover()
        {
            var r = new ActivityRunner();
            r.Start(ReefLike());
            Assert.IsTrue(r.Report(StepKind.Inspect, "a"));
        }

        [Test]
        public void CompletesAndFiresEvent()
        {
            var r = new ActivityRunner();
            ActivityModel done = null;
            r.ActivityCompleted += a => done = a;
            r.Start(ReefLike());
            foreach (var t in new[] { "a", "b", "c" }) r.Report(StepKind.Discover, t);
            r.Report(StepKind.ReachDestination, "hub");
            Assert.AreEqual(RunnerState.Completed, r.State);
            Assert.IsNotNull(done);
            Assert.IsNull(r.CurrentStep);
            Assert.AreEqual(1f, r.Progress01);
        }

        [Test]
        public void ProgressCountsPartialTargets()
        {
            var r = new ActivityRunner();
            r.Start(ReefLike());
            Assert.AreEqual(0f, r.Progress01, 1e-4);
            r.Report(StepKind.Discover, "a");
            Assert.AreEqual(1f / 4f, r.Progress01, 1e-4);
        }

        [Test]
        public void RestartGoesBackToStart()
        {
            var r = new ActivityRunner();
            r.Start(ReefLike());
            foreach (var t in new[] { "a", "b", "c" }) r.Report(StepKind.Discover, t);
            r.Restart();
            Assert.AreEqual("find3", r.CurrentStep.Id);
            Assert.AreEqual(0, r.HitTargets.Count);
        }

        [Test]
        public void AnyTargetStepClearsOnFirstHit()
        {
            var r = new ActivityRunner();
            r.Start(new ActivityModel
            {
                Id = "x",
                Steps = { new StepModel { Id = "s", Kind = StepKind.Interact, TargetIds = { "p", "q" }, RequireAllTargets = false } }
            });
            r.Report(StepKind.Interact, "q");
            Assert.AreEqual(RunnerState.Completed, r.State);
        }

        [Test]
        public void InvalidActivityThrows()
        {
            var r = new ActivityRunner();
            Assert.Throws<System.InvalidOperationException>(() => r.Start(new ActivityModel { Id = "empty" }));
            Assert.Throws<System.InvalidOperationException>(() => r.Start(new ActivityModel
            {
                Id = "dupe",
                Steps = { new StepModel { Id = "s", TargetIds = { "a" } }, new StepModel { Id = "s", TargetIds = { "b" } } }
            }));
        }
    }

    public class SettingsAndProgressTests
    {
        [Test]
        public void DefaultsAreTheComfortableOnes()
        {
            var s = new SettingsData();
            Assert.IsTrue(s.TeleportEnabled);
            Assert.IsFalse(s.SmoothMoveEnabled);
            Assert.AreEqual(TurnMode.Snap, s.Turn);
            Assert.IsTrue(s.Vignette);
        }

        [Test]
        public void ClampFixesBadValuesAndKeepsAWayToMove()
        {
            var s = new SettingsData { TextScale = 9f, MasterVolume = -1f, SnapAngle = float.NaN, TeleportEnabled = false, SmoothMoveEnabled = false };
            s.Clamp();
            Assert.AreEqual(SettingsData.MaxTextScale, s.TextScale);
            Assert.AreEqual(0f, s.MasterVolume);
            Assert.AreEqual(15f, s.SnapAngle);
            Assert.IsTrue(s.TeleportEnabled);
        }

        [Test]
        public void StationaryIsAllowedToHaveNoMovement()
        {
            var s = new SettingsData { TeleportEnabled = false, SmoothMoveEnabled = false, Stationary = true };
            s.Clamp();
            Assert.IsFalse(s.TeleportEnabled);
        }

        [Test]
        public void MuteZeroesVolume()
        {
            var s = new SettingsData { Muted = true };
            Assert.AreEqual(0f, s.EffectiveVolume(1f));
        }

        [Test]
        public void ProgressTracksCompletionsAndDiscoveries()
        {
            var p = new ProgressStore();
            int changes = 0;
            p.Changed += () => changes++;
            p.MarkCompleted("reef", 100);
            p.MarkCompleted("reef", 200);
            Assert.IsTrue(p.IsCompleted("reef"));
            Assert.AreEqual(1, p.CompletedCount);
            Assert.IsTrue(p.MarkDiscovered("clam"));
            Assert.IsFalse(p.MarkDiscovered("clam"));
            p.ResetAll();
            Assert.IsFalse(p.IsCompleted("reef"));
            Assert.AreEqual(4, changes);
        }

        [Test]
        public void LoadHandlesNullLists()
        {
            var p = new ProgressStore();
            p.Load(new ProgressData { Activities = null, Discovered = null });
            Assert.IsFalse(p.IsDiscovered("x"));
            Assert.AreEqual(0, p.CompletedCount);
        }
    }
}

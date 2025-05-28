using System.Reflection;
using NUnit.Framework;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Entities;
using UnityEngine.LowLevel;
// ReSharper disable InconsistentNaming

namespace Kowloon.DebugDraw.Tests
{
    [BurstCompile(CompileSynchronously = true)]
    public class ECSTestsCommonBase
    {
        [SetUp]
        public virtual void Setup()
        {
        }

        [TearDown]
        public virtual void TearDown()
        {
        }

        [BurstDiscard]
        public static void TestBurstCompiled(ref bool falseIfNot)
        {
            falseIfNot = false;
        }

        [BurstCompile(CompileSynchronously = true)]
        public static bool IsBurstEnabled()
        {
            bool burstCompiled = true;
            TestBurstCompiled(ref burstCompiled);
            return burstCompiled;
        }
    }

    public abstract class ECSTestsFixture : ECSTestsCommonBase
    {
        protected World m_PreviousWorld;
        protected World World;
        protected PlayerLoopSystem m_PreviousPlayerLoop;
        protected EntityManager m_Manager;
        protected EntityManager.EntityManagerDebug m_ManagerDebug;

        protected int StressTestEntityCount = 1000;
        private bool JobsDebuggerWasEnabled;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            // unit tests preserve the current player loop to restore later, and start from a blank slate.
            m_PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());

            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            World = World.DefaultGameObjectInjectionWorld = new World("Test World");
            World.UpdateAllocatorEnableBlockFree = true;
            m_Manager = World.EntityManager;
            m_ManagerDebug = new EntityManager.EntityManagerDebug(m_Manager);

            // Many ECS tests will only pass if the Jobs Debugger enabled;
            // force it enabled for all tests, and restore the original value at teardown.
            JobsDebuggerWasEnabled = JobsUtility.JobDebuggerEnabled;
            JobsUtility.JobDebuggerEnabled = true;

            MethodInfo clearSystemIdsMethod = typeof(JobsUtility).GetMethod("ClearSystemIds", BindingFlags.Static | BindingFlags.NonPublic);
            clearSystemIdsMethod?.Invoke(null, null);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            // In case entities journaling is initialized, clear it
            EntitiesJournaling.Clear();
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            if (World != null && World.IsCreated)
            {
                // Note that World.Dispose() already completes all jobs. But some tests may leave tests running when
                // they return, but we can't safely run an internal consistency check with jobs running, so we
                // explicitly complete them here as well.
                World.EntityManager.CompleteAllTrackedJobs();

                // TODO(DOTS-9429): We should not need to explicitly destroy all systems here.
                // World.Dispose() already handles this. However, we currently need to destroy all systems before
                // calling CheckInternalConsistency, or else some tests trigger false positives (due to EntityQuery
                // filters holding references to shared component values, etc.).
                // We can't safely destroy all systems while jobs are running, so this call must come after the
                // CompleteAllTrackedJobs() call above.
                World.DestroyAllSystemsAndLogException(out bool errorsWhileDestroyingSystems);
                Assert.IsFalse(errorsWhileDestroyingSystems,
                    "One or more exceptions were thrown while destroying systems during test teardown; consult the log for details.");

                m_ManagerDebug.CheckInternalConsistency();

                World.Dispose();
                World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = default;
            }

            JobsUtility.JobDebuggerEnabled = JobsDebuggerWasEnabled;
            MethodInfo clearSystemIdsMethod = typeof(JobsUtility).GetMethod("ClearSystemIds", BindingFlags.Static | BindingFlags.NonPublic);
            clearSystemIdsMethod?.Invoke(null, null);

            PlayerLoop.SetPlayerLoop(m_PreviousPlayerLoop);

            base.TearDown();
        }
    }
}

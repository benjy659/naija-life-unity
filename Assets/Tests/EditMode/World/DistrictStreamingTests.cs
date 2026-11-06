// M2.3 edit-mode checks for the streaming numbers (docs/specs/oshodi-greybox.md
// section 7). The walk itself (wake at 600 m, sleep past 700 m, no frame over
// 200 ms) is checked by playing Bootstrap.unity; these catch a wrong tile
// rectangle or marker before anyone has to walk 800 m to find out.

using System.Linq;
using NaijaLife.World.Editor;
using NUnit.Framework;
using UnityEngine;

namespace NaijaLife.World.Tests
{
    public class DistrictStreamingTests
    {
        static DistrictTile Tile()
        {
            var t = ScriptableObject.CreateInstance<DistrictTile>();
            t.bounds = new Rect(-120f, -150f, 240f, 300f);
            t.wakeDistance = 600f;
            t.sleepDistance = 700f;
            return t;
        }

        [Test]
        public void DistanceIsZeroInsideAndFlatOutside()
        {
            var t = Tile();
            Assert.AreEqual(0f, t.DistanceTo(new Vector3(0f, 7f, 0f)), 1e-4f);
            Assert.AreEqual(100f, t.DistanceTo(new Vector3(0f, 0f, -250f)), 1e-3f);
            Assert.AreEqual(50f, t.DistanceTo(new Vector3(-150f, 0f, 190f)), 1e-3f);   // corner: 30, 40 -> 50
        }

        [Test]
        public void StreamingTestStartsAsleepAndWakesOnTheWayNorth()
        {
            var data = GreyboxBuildFile.Load();
            var start = data.markers.First(m => m.id == "streaming-test-start");
            var t = Tile();
            float d = t.DistanceTo(new Vector3(start.x, start.y, start.z));
            Assert.Greater(d, t.wakeDistance, "the test must start with the tile asleep");
            Assert.Less(d, t.sleepDistance + 100f, "the start is so far south the walk takes too long");
            Assert.GreaterOrEqual(t.sleepDistance - t.wakeDistance, 50f, "wake and sleep need a gap so the tile never flickers");
        }

        [Test]
        public void SetupWritesTheTileTheSpecAsksFor()
        {
            var t = UnityEditor.AssetDatabase.LoadAssetAtPath<DistrictTile>(OshodiBootstrapSetup.TileAssetPath);
            if (t == null) Assert.Ignore("Run Naija Streets > Set up Oshodi streaming (Bootstrap) first.");
            Assert.AreEqual(OshodiBootstrapSetup.CoreAddress, t.sceneAddress);
            Assert.AreEqual(new Rect(-120f, -150f, 240f, 300f), t.bounds);
            Assert.AreEqual(600f, t.wakeDistance);
            Assert.AreEqual(700f, t.sleepDistance);
        }
    }
}

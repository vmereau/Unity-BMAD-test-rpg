using NUnit.Framework;
using Game.Editor;

namespace Tests.EditMode
{
    public class ItemAssetBuilderTests
    {
        private const string DIR = "Assets/Drop/";
        private const string MESHY = DIR + "Meshy_AI_Thing_123_texture";

        private static readonly string[] FullSet =
        {
            MESHY + ".fbx",
            MESHY + ".png",
            MESHY + "_normal.png",
            MESHY + "_metallic.png",
            MESHY + "_roughness.png",
            DIR + "thing icon.png",
        };

        [Test]
        public void ClassifySourceFiles_FullSet_SortsEveryRole()
        {
            ItemSourceFiles files = ItemAssetBuilder.ClassifySourceFiles(FullSet);

            Assert.That(files.Errors, Is.Empty);
            Assert.That(files.Fbx, Is.EqualTo(new[] { MESHY + ".fbx" }));
            Assert.AreEqual(MESHY + ".png", files.BaseMap);
            Assert.AreEqual(MESHY + "_normal.png", files.NormalMap);
            Assert.AreEqual(MESHY + "_metallic.png", files.MetallicMap);
            Assert.AreEqual(MESHY + "_roughness.png", files.RoughnessMap);
            Assert.That(files.IconCandidates, Is.EqualTo(new[] { DIR + "thing icon.png" }));
        }

        [Test]
        public void ClassifySourceFiles_NoIcon_IsValidWithNoCandidates()
        {
            ItemSourceFiles files = ItemAssetBuilder.ClassifySourceFiles(new[] { MESHY + ".fbx", MESHY + ".png" });

            Assert.That(files.Errors, Is.Empty);
            Assert.That(files.IconCandidates, Is.Empty);
            Assert.IsNull(files.NormalMap);
        }

        [Test]
        public void ClassifySourceFiles_TwoIcons_ReportsAmbiguity()
        {
            ItemSourceFiles files = ItemAssetBuilder.ClassifySourceFiles(new[]
            {
                MESHY + ".fbx", MESHY + ".png", DIR + "a.png", DIR + "b.jpg",
            });

            Assert.That(files.Errors, Has.Count.EqualTo(1));
            Assert.That(files.Errors[0], Does.Contain("icon"));
        }

        [Test]
        public void ClassifySourceFiles_MissingBaseMap_ReportsError()
        {
            ItemSourceFiles files = ItemAssetBuilder.ClassifySourceFiles(new[] { MESHY + ".fbx", MESHY + "_normal.png" });

            Assert.That(files.Errors, Has.Some.Contains("base color"));
        }

        [Test]
        public void ClassifySourceFiles_NoOrTwoFbx_ReportsError()
        {
            Assert.That(ItemAssetBuilder.ClassifySourceFiles(new[] { MESHY + ".png" }).Errors,
                Has.Some.Contains("FBX"));
            Assert.That(ItemAssetBuilder.ClassifySourceFiles(new[] { MESHY + ".png", DIR + "a.fbx", DIR + "b.FBX" }).Errors,
                Has.Some.Contains("FBX"));
        }

        [Test]
        public void ClassifySourceFiles_MetaFiles_AreIgnored()
        {
            ItemSourceFiles files = ItemAssetBuilder.ClassifySourceFiles(new[]
            {
                MESHY + ".fbx", MESHY + ".fbx.meta", MESHY + ".png", MESHY + ".png.meta", DIR + "thing icon.png.meta",
            });

            Assert.That(files.Errors, Is.Empty);
            Assert.That(files.Fbx, Has.Count.EqualTo(1));
            Assert.That(files.IconCandidates, Is.Empty);
        }
    }
}

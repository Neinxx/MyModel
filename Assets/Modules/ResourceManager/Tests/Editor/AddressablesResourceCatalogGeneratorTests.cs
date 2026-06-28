using NUnit.Framework;
using ResourceManagerModule.Editor;

namespace ResourceManagerModule.Tests
{
    public sealed class AddressablesResourceCatalogGeneratorTests
    {
        [Test]
        public void NormalizeKeys_RemovesEmptyDuplicatesAndSorts()
        {
            var keys = AddressablesResourceCatalogGenerator.NormalizeKeys(new[]
            {
                "z_key",
                null,
                string.Empty,
                "a_key",
                "z_key"
            });

            CollectionAssert.AreEqual(new[] { "a_key", "z_key" }, keys);
        }
    }
}

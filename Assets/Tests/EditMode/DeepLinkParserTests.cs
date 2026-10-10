using NUnit.Framework;
using Aayam.Models;

namespace Aayam.Tests.EditMode
{
    public class DeepLinkParserTests
    {
        [Test]
        public void TryParseProductId_ValidLink_ReturnsId()
        {
            bool ok = DeepLinkParser.TryParseProductId("aayam://product/HANDLE_001", out string id);
            Assert.IsTrue(ok);
            Assert.AreEqual("HANDLE_001", id);
        }

        [Test]
        public void TryParseProductId_WithQueryString_StripsIt()
        {
            bool ok = DeepLinkParser.TryParseProductId("aayam://product/HANDLE_001?utm=qr", out string id);
            Assert.IsTrue(ok);
            Assert.AreEqual("HANDLE_001", id);
        }

        [Test]
        public void TryParseProductId_WithFragment_StripsIt()
        {
            bool ok = DeepLinkParser.TryParseProductId("aayam://product/HANDLE_001#section", out string id);
            Assert.IsTrue(ok);
            Assert.AreEqual("HANDLE_001", id);
        }

        [Test]
        public void TryParseProductId_WrongScheme_ReturnsFalse()
        {
            bool ok = DeepLinkParser.TryParseProductId("https://product/HANDLE_001", out string id);
            Assert.IsFalse(ok);
            Assert.IsNull(id);
        }

        [Test]
        public void TryParseProductId_MissingProductSegment_ReturnsFalse()
        {
            bool ok = DeepLinkParser.TryParseProductId("aayam://category/handles", out string id);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParseProductId_EmptyId_ReturnsFalse()
        {
            bool ok = DeepLinkParser.TryParseProductId("aayam://product/", out string id);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParseProductId_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(DeepLinkParser.TryParseProductId(null, out _));
            Assert.IsFalse(DeepLinkParser.TryParseProductId("", out _));
            Assert.IsFalse(DeepLinkParser.TryParseProductId("   ", out _));
        }

        [Test]
        public void TryParseProductId_CaseInsensitiveScheme_Works()
        {
            bool ok = DeepLinkParser.TryParseProductId("AAYAM://PRODUCT/HANDLE_001", out string id);
            Assert.IsTrue(ok);
            Assert.AreEqual("HANDLE_001", id);
        }

        [Test]
        public void TryParseProductId_TrailingSlash_StripsIt()
        {
            bool ok = DeepLinkParser.TryParseProductId("aayam://product/HANDLE_001/extra", out string id);
            Assert.IsTrue(ok);
            Assert.AreEqual("HANDLE_001", id);
        }
    }
}

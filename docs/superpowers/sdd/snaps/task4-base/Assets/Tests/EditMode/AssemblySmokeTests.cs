using NUnit.Framework;

namespace Demo.Tests
{
    public class AssemblySmokeTests
    {
        [Test]
        public void 测试程序集_已正确装配()
        {
            // 只断言 NotNull 等于没断言：程序集永远不为 null，asmdef 装配错了照样过。
            // 断言名字才能真正验证 asmdef 落在了预期的程序集里。
            Assert.AreEqual("Demo.Tests.EditMode",
                typeof(AssemblySmokeTests).Assembly.GetName().Name);
        }

        [Test]
        public void 编辑器程序集_可被测试程序集引用()
        {
            var type = typeof(Demo.EditorTools.NamespaceAnchor);
            Assert.IsNotNull(type);
            Assert.AreEqual("Demo.EditorTools", type.Namespace);
        }
    }
}

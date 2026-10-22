namespace Proxy.Tests
{
    public class EnvFileTests
    {
        [Fact]
        public void Parse_SimpleLine_ReadsKeyAndValue()
        {
            var values = EnvFile.Parse("KEY=value");

            Assert.Single(values);
            Assert.Equal("value", values["KEY"]);
        }

        [Fact]
        public void Parse_ValueContainingEquals_KeepsEverythingAfterFirstEquals()
        {
            var values = EnvFile.Parse("PASSWORD=a=b==c");

            Assert.Equal("a=b==c", values["PASSWORD"]);
        }

        [Fact]
        public void Parse_BlankAndCommentLines_AreSkipped()
        {
            var values = EnvFile.Parse("\n# a comment\nKEY=value\n\n   \n#OTHER=ignored\n");

            Assert.Single(values);
            Assert.Equal("value", values["KEY"]);
        }

        [Fact]
        public void Parse_WhitespaceAroundKeyAndValue_IsTrimmed()
        {
            var values = EnvFile.Parse("  KEY  =  value  ");

            Assert.Equal("value", values["KEY"]);
        }

        [Fact]
        public void Parse_LineWithoutEquals_IsSkipped()
        {
            var values = EnvFile.Parse("NOEQUALS\nKEY=value");

            Assert.Single(values);
            Assert.Equal("value", values["KEY"]);
        }
    }
}

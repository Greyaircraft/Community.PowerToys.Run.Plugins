using Community.PowerToys.Run.Plugin.PoetSearch;
using FluentAssertions;

namespace Community.PowerToys.Run.Plugin.PoetSearch.UnitTests
{
    [TestClass]
    public class PoemDataTests
    {
        private static string WriteTempJson(string json)
        {
            var path = Path.Combine(Path.GetTempPath(), $"poems_test_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json);
            return path;
        }

        private static string WriteTempGz(string json)
        {
            var path = Path.Combine(Path.GetTempPath(), $"poems_test_{Guid.NewGuid():N}.json.gz");
            using var fs = File.Create(path);
            using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal);
            using var writer = new StreamWriter(gz);
            writer.Write(json);
            return path;
        }

        private const string SampleJson = """
            [
              { "title": "静夜思", "author": "李白", "paragraphs": ["床前明月光，疑是地上霜。", "举头望明月，低头思故乡。"], "source": "全唐诗" },
              { "title": "水调歌头", "author": "苏轼", "paragraphs": ["明月几时有，把酒问青天。"], "source": "宋词" },
              { "title": "登鹳雀楼", "author": "王之涣", "paragraphs": ["白日依山尽，黄河入海流。"], "source": "全唐诗" }
            ]
            """;

        [TestMethod]
        public void Load_plain_json_should_succeed()
        {
            var path = WriteTempJson(SampleJson);
            try
            {
                var data = new PoemData();
                data.Load(path).Should().BeTrue();
                data.Count.Should().Be(3);
                data.Loaded.Should().BeTrue();
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_gzip_json_should_succeed()
        {
            var path = WriteTempGz(SampleJson);
            try
            {
                var data = new PoemData();
                data.Load(path).Should().BeTrue();
                data.Count.Should().Be(3);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_missing_file_should_return_false()
        {
            var data = new PoemData();
            data.Load(Path.Combine(Path.GetTempPath(), "definitely_missing_poems.json"))
                .Should().BeFalse();
            data.Loaded.Should().BeFalse();
        }

        [TestMethod]
        public void Load_invalid_json_should_return_false()
        {
            var path = WriteTempJson("{ not valid json !!!");
            try
            {
                var data = new PoemData();
                data.Load(path).Should().BeFalse();
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Search_by_title_should_find_poem()
        {
            var path = WriteTempJson(SampleJson);
            try
            {
                var data = new PoemData();
                data.Load(path);
                var results = data.Search("静夜思");
                results.Should().ContainSingle();
                results[0].Title.Should().Be("静夜思");
                results[0].Author.Should().Be("李白");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Search_by_author_should_find_poem()
        {
            var path = WriteTempJson(SampleJson);
            try
            {
                var data = new PoemData();
                data.Load(path);
                var results = data.Search("苏轼");
                results.Should().ContainSingle();
                results[0].Title.Should().Be("水调歌头");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Search_by_content_should_find_poem()
        {
            var path = WriteTempJson(SampleJson);
            try
            {
                var data = new PoemData();
                data.Load(path);
                var results = data.Search("黄河入海流");
                results.Should().ContainSingle();
                results[0].Title.Should().Be("登鹳雀楼");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Search_random_should_return_poem()
        {
            var path = WriteTempJson(SampleJson);
            try
            {
                var data = new PoemData();
                data.Load(path);
                var results = data.Search("随机");
                results.Should().ContainSingle();
                results[0].Title.Should().NotBeNullOrEmpty();
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Search_unknown_should_return_empty()
        {
            var path = WriteTempJson(SampleJson);
            try
            {
                var data = new PoemData();
                data.Load(path);
                data.Search("不存在的内容xyz").Should().BeEmpty();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}

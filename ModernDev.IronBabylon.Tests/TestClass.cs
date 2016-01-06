using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace ModernDev.IronBabylon.Tests
{
    [TestFixture]
    public class TestClass
    {
        public static string FixturesPath => Path.Combine(Directory.GetParent(Assembly.GetAssembly(typeof(TestClass)).Location).FullName, "Fixtures");
        private static JsonSerializer Serializer => new JsonSerializer
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new CustomDataContractResolver()
        };

        public static string Parse(string code, ParserOptions options = null)
        {
            dynamic ast;

            try
            {
                ast = IronBabylon.Parse(code, options);
            }
            catch (SyntaxErrorException ex)
            {
                ast = new ExpandoObject();
                ast.Throws = ex.Message;
            }

            return ObjectToJSON(ast);
        }

        private static string ObjectToJSON(object obj)
        {
            var sw = new StringWriter();
            var jtw = new JsonTextWriter(sw);

            Serializer.Serialize(jtw, obj);

            return sw.ToString().Replace("\r\n", "\n");
        }

        public static IEnumerable<string> GetTestCaseDirs(string path)
        {
            var dirs = new List<string>();
            
            foreach (var dir in Directory.GetDirectories(path, "*"))
            {
                if (new DirectoryInfo(dir).GetDirectories("*").Any())
                {
                    dirs.AddRange(GetTestCaseDirs(dir));
                }
                else
                {
                    dirs.Add(dir);
                }
            }

            return dirs.Select(dir => dir.Replace(FixturesPath + "\\", ""));
        }

        private static string[] GetTestFixturesFolders() 
            => GetTestCaseDirs(FixturesPath).ToArray();

        private static object[] GetTheCodeAndAST(string folderSrc)
        {
            folderSrc = $"{FixturesPath}\\{folderSrc}";
            string actualJs = null;
            string ast = null;
            var options = folderSrc.ToLowerInvariant().Contains("\\flow") ||
                                  folderSrc.ToLowerInvariant().Contains("es2015-import-declaration") ||
                                  folderSrc.ToLowerInvariant().Contains("es2015-export-declaration")
                        ? new ParserOptions("module")
                        : ParserOptions.Default;

            var fInfo = new FileInfo(Path.Combine(folderSrc, "actual.js"));

            if (fInfo.Exists)
            {
                using (var sr = fInfo.OpenText())
                {
                    actualJs = sr.ReadToEnd();
                }
            }

            fInfo = new FileInfo(Path.Combine(folderSrc, "expected.json"));

            if (fInfo.Exists)
            {
                using (var sr = fInfo.OpenText())
                {
                    ast = sr.ReadToEnd();
                }
            }

            fInfo = new FileInfo(Path.Combine(folderSrc, "options.json"));

            if (fInfo.Exists)
            {
                using (var sr = fInfo.OpenText())
                {
                    var optionsJson = sr.ReadToEnd();

                    if (!optionsJson.ToLowerInvariant().Contains("throw"))
                    {
                        options = JsonConvert.DeserializeObject<ParserOptions>(optionsJson);
                    }
                }
            }

            return new object[]
            {
                actualJs,
                ast,
                options
            };
        }

        [TestCase]
        public void FixturesFolderExists()
        {
            Assert.AreEqual(Directory.Exists(FixturesPath), true);
        }

        [Test, TestCaseSource(nameof(GetTestFixturesFolders))]
        public void IronBabylonTest(string folderSrc)
        {
            Assert.NotNull(folderSrc);
            Assert.IsNotEmpty(folderSrc);

            var data = GetTheCodeAndAST(folderSrc);
            var js = data[0] as string;
            var expectedAst = data[1] as string;
            var parserOptions = data[2] as ParserOptions;
            var ast = Parse(js, parserOptions);
            
            Assert.AreEqual(expectedAst, ast);
        }
    }

    public class CustomDataContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var name = property.PropertyName;
            
            property.ShouldSerialize = inst =>
            {
                var type = property.DeclaringType;
                var val = property.DeclaringType.GetProperties()
                    .First(p => p.Name == name)
                    .GetValue(Convert.ChangeType(inst, type));

                if (val is IList && val.GetType().IsGenericType)
                {
                    return (val as IList).Count > 0;
                }

                if (val is bool)
                {
                    return (bool)val;
                }

                //return property.PropertyName != "UpdateContext" && property.PropertyName != "Tokens";
                return !new[] { "UpdateContext", "updateContext" }.Contains(property.PropertyName);
            };

            if (property.PropertyName == "Location")
            {
                property.PropertyName = "Loc";
            }

            property.PropertyName = char.ToLowerInvariant(name[0]) + name.Substring(1);

            return property;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace SharpScript
{
    public class SharpHtml
    {
        public readonly HtmlDocument Document;
        public readonly IEnumerable<string> SharpAssemblies;
        public readonly string SharpCode;

        public SharpHtml(Uri uri)
        {
            if (uri.IsFile)
            {
                (Document = new HtmlDocument()).Load(Uri.UnescapeDataString(uri.AbsolutePath));
            }
            else
            {
                Document = new HtmlWeb().Load(uri.AbsoluteUri);
            }

            var docNode = Document.DocumentNode;

            #region Assembly list forming
            var sharpAssemblyMetas =
                docNode
                .Descendants("meta")
                .Where(meta => meta.GetAttributeValue("name", "") == "assembly")
                .ToList();

            SharpAssemblies = sharpAssemblyMetas.Select(meta => meta.GetAttributeValue("content", ""));

            var sharpUsings =
                sharpAssemblyMetas
                .SelectMany(meta =>
                {
                    var assemblyPath = meta.GetAttributeValue("content", "");

                    return
                        meta.GetAttributeValue("data-using", "")
                        .Split(';')
                        .Select(usingItem => usingItem.Trim())
                        .Where(usingItem => usingItem != "")
                        .Select(usingItem => usingItem.StartsWith(".") ? assemblyPath + (usingItem != "." ? usingItem : "") : usingItem);
                });

            sharpUsings = new[] { "System", "SharpScript" }.Concat(sharpUsings);
            #endregion

            var sharpScripts =
                docNode
                .Descendants("script")
                .Where(script => script.GetAttributeValue("type", "") == "text/x-csharp")
                .SelectMany((script, index) =>
                {
                    var className = script.GetAttributeValue("id", "Script_" + index);
                    var classCode = script.InnerHtml;
                    script.SetAttributeValue("type", "text/javascript");
                    script.InnerHtml = string.Format("window.external.SharpExecute('{0}');", className);
                    return new[] { "class " + className, "{", classCode, "}", string.Empty };
                });

            SharpCode =
                string.Join
                (
                    Environment.NewLine,
                    new[]
                    {
                        sharpUsings.Select(usingItem => "using " + usingItem + ";"),
                        new[]
                        {
                            string.Empty,
                            "public class External : SharpScript.External { }"
                        },
                        sharpScripts
                    }
                    .SelectMany(strings => strings)
                );
        }
    }
}

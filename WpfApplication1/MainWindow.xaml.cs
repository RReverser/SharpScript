#define SHARPSCRIPT_CACHE
// #define SHARPSCRIPT_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;
using HtmlAgilityPack;
using System.CodeDom.Compiler;
using System.Reflection;
using System.IO;

namespace SharpScript
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class HtmlInteropClass
    {
        private const BindingFlags MethodFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod;

        public Assembly Assembly { get; private set; }

        public void SharpExecute(string className)
        {
            if (Assembly == null) return;

            var methods =
                Assembly.GetType(className)
                .GetMethods(MethodFlags)
                .Where(method => method.GetCustomAttributes(typeof(ExecuteAttribute), false).Length > 0);

            foreach (var method in methods)
            {
                try
                {
                    method.Invoke(null, new object[0]);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }
            }
        }

        public HtmlInteropClass(IEnumerable<string> assemblies, string code)
        {
            #if SHARPSCRIPT_DEBUG
                File.WriteAllText("E:\\test.cs", code);
            #endif

            var assemblyFileName = code.GetHashCode().ToString("X" + 2 * sizeof(int)) + ".dll";

            #if SHARPSCRIPT_CACHE
                if (File.Exists(assemblyFileName) && (DateTime.Now - File.GetLastWriteTime(assemblyFileName)).TotalHours < 3)
                {
                    Assembly = Assembly.LoadFrom(assemblyFileName);
                    return;
                }
            #endif

            File.Delete(assemblyFileName);

            #region Compiler parameters
            var compilerParams =
                new CompilerParameters
                {
                    CompilerOptions = "/t:library /optimize",
                    GenerateInMemory = true,
                    #if SHARPSCRIPT_CACHE
                        GenerateExecutable = true,
                        OutputAssembly = assemblyFileName,
                    #endif
                    ReferencedAssemblies =
                    {
                        "System.dll",
                        "System.Core.dll",
                        "Microsoft.CSharp.dll",
                        "SharpScriptHelper.dll",
                        "../Microsoft.mshtml.dll"
                    }
                };

            compilerParams.ReferencedAssemblies.AddRange
            (
                assemblies
                .Select(libName => File.Exists(libName) ? libName : libName + ".dll")
                .ToArray()
            );
            #endregion

            var compilerResults = CodeDomProvider.CreateProvider("CSharp").CompileAssemblyFromSource(compilerParams, code);

            if (compilerResults.Errors.HasErrors)
            {
                var errors = compilerResults.Errors.Cast<CompilerError>();
                MessageBox.Show(string.Join(Environment.NewLine, errors));
                return;
            }

            Assembly = compilerResults.CompiledAssembly;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Go_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri(Url.Text);

            HtmlDocument document;

            if (uri.IsFile)
            {
                (document = new HtmlDocument()).Load(Uri.UnescapeDataString(uri.AbsolutePath));
            }
            else
            {
                document = new HtmlWeb().Load(uri.AbsoluteUri);
            }

            var docNode = document.DocumentNode;

            #region Assembly list forming
            var sharpAssemblyMetas =
                docNode
                .Descendants("meta")
                .Where(meta => meta.GetAttributeValue("name", "") == "assembly")
                .ToList();

            var sharpAssemblies = sharpAssemblyMetas.Select(meta => meta.GetAttributeValue("content", ""));

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

            Browser.ObjectForScripting =
                new HtmlInteropClass
                (
                    sharpAssemblies,
                    string.Join
                    (
                        Environment.NewLine,
                        new []
                        {
                            sharpUsings.Select(usingItem => "using " + usingItem + ";"),
                            new []
                            {
                                string.Empty,
                                "public class External : SharpScript.External { }"
                            },
                            sharpScripts
                        }
                        .SelectMany(strings => strings)
                    )
                );

            Browser.NavigateToString(docNode.OuterHtml);
        }

        private void Browser_Navigated(object sender, NavigationEventArgs e)
        {
            var htmlInterop = Browser.ObjectForScripting as HtmlInteropClass;

            if (htmlInterop == null || htmlInterop.Assembly == null) return;

            htmlInterop
            .Assembly
            .GetType("External")
            .GetProperty("Document", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.SetProperty)
            .SetValue(null, Browser.Document, null);
        }
    }
}

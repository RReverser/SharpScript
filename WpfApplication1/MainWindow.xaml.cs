// #define SHARPSCRIPT_CACHE
#define SHARPSCRIPT_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HtmlAgilityPack;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Reflection;
using System.IO;

namespace SharpScript
{
    [System.Runtime.InteropServices.ComVisibleAttribute(true)]
    public class HtmlInteropClass
    {
        private const BindingFlags MethodFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod;

        private WebBrowser browser;
        public Assembly Assembly { get; private set; }

        /*
        public IEnumerable<MethodInfo> SharpMethods(string className)
        {
            return
                assembly.GetType(className)
                .GetMethods(MethodFlags)
                .Where(method => method.GetCustomAttribute<ExecuteAttribute>() == null);
        }

        public object SharpCall(string className)
        {
            MessageBox.Show("SharpCall: " + className);
            return null;
        }
         */

        public void SharpExecute(string className)
        {
            (browser.ObjectForScripting as HtmlInteropClass)
            .Assembly
            .GetType("External")
            .GetProperty("document", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.SetProperty)
            .SetValue(null, browser.Document, null);

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

        public HtmlInteropClass(WebBrowser webBrowser, IEnumerable<string> assemblies, string code)
        {
            browser = webBrowser;

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
                new CompilerParameters()
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
                #if SHARPSCRIPT_DEBUG
                    MessageBox.Show(string.Join(Environment.NewLine, errors));
                #endif
            }

            Assembly = compilerResults.CompiledAssembly;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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
                .Where(meta => meta.GetAttributeValue("name", "") == "assembly");

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

            sharpUsings = Enumerable.Concat(new[] { "System", "SharpScript" }, sharpUsings);
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

            browser.ObjectForScripting =
                new HtmlInteropClass
                (
                    browser,
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

            browser.NavigateToString(docNode.OuterHtml);
        }

        private void browser_Navigated(object sender, NavigationEventArgs e)
        {

        }
    }
}

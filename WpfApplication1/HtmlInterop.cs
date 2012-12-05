using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace SharpScript
{
    [ComVisible(true)]
    public class HtmlInterop
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

        public HtmlInterop(IEnumerable<string> assemblies, string code)
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
}
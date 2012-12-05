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
            var sharpHtml = new SharpHtml(new Uri(Url.Text));
            Browser.ObjectForScripting = new HtmlInterop(sharpHtml.SharpAssemblies, sharpHtml.SharpCode);
            Browser.NavigateToString(sharpHtml.Document.DocumentNode.OuterHtml);
        }

        private void Browser_Navigated(object sender, NavigationEventArgs e)
        {
            var htmlInterop = Browser.ObjectForScripting as HtmlInterop;

            if (htmlInterop == null || htmlInterop.Assembly == null) return;

            htmlInterop
            .Assembly
            .GetType("External")
            .GetProperty("Document", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.SetProperty)
            .SetValue(null, Browser.Document, null);
        }
    }
}

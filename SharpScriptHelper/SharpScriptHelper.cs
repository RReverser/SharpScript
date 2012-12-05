using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using mshtml;

namespace SharpScript
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExecuteAttribute : Attribute
    {
    }

    public class ExternalVars
    {
        public dynamic this[string name]
        {
            get { return External.Get(name); }
            set { External.Set(name, value); }
        }
    }

    public class External
    {
        public static IHTMLWindow2 Window { get { return Document != null ? Document.parentWindow : null; } }
        public static IHTMLDocument2 Document { get; protected set; }

        public static ExternalVars Vars = new ExternalVars();

        public static dynamic Get(string name)
        {
            return Window.GetType().InvokeMember(name, BindingFlags.GetProperty, null, Window, null);
        }

        public static T Get<T>(string name)
        {
            var resType = typeof(T);
            object comObject = Get(name);

            object resObject;

            #region Setting resObject with separate hack for passing array objects
            if (resType.IsArray)
            {
                var comType = comObject.GetType();

                Func<string, object> getComProp = propName => comType.InvokeMember(propName, BindingFlags.GetProperty, null, comObject, null);

                var elemType = resType.GetElementType();
                var length = (int)getComProp("length");

                var array = Array.CreateInstance(elemType, length);
                for (var i = 0; i < length; i++)
                {
                    var value = getComProp(i.ToString(CultureInfo.InvariantCulture));
                    array.SetValue(Convert.ChangeType(value, elemType), i);
                }

                resObject = array;
            }
            else
            {
                resObject = comObject;
            }
            #endregion Setting resObject with separate hack for passing array objects

            return (T)resObject;
        }

        public static void Set(string name, object value)
        {
            Eval(string.Format("window['{0}'] = null", name));
            Window.GetType().InvokeMember(name, BindingFlags.SetProperty, null, Window, new[] { value });
        }

        public static dynamic Eval(string code)
        {
            return Window.GetType().InvokeMember("eval", BindingFlags.InvokeMethod, null, Window, new object[] { code });
        }
    }
}

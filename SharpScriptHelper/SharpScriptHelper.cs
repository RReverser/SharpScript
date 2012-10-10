using System;
using System.Linq;
using System.Reflection;
using mshtml;

namespace SharpScript
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExecuteAttribute : Attribute
    {
    }

    public class External
    {
        public static IHTMLWindow2 window { get { return document != null ? document.parentWindow : null; } }
        public static IHTMLDocument2 document { get; protected set; }

        public static dynamic Get(string name)
        {
            return window.GetType().InvokeMember(name, BindingFlags.GetProperty, null, window, null);
        }

        public static T Get<T>(string name)
        {
            var resType = typeof(T);
            object comObject = Get(name);

            object resObject;

            if (resType.IsArray)
            {
                var comType = comObject.GetType();

                Func<string, object> getComProp = propName => comType.InvokeMember(propName, BindingFlags.GetProperty, null, comObject, null);

                var elemType = resType.GetElementType();
                var length = (int)getComProp("length");

                var array = Array.CreateInstance(elemType, length);
                for (var i = 0; i < length; i++)
                {
                    var value = getComProp(i.ToString());
                    array.SetValue(Convert.ChangeType(value, elemType), i);
                }

                resObject = array;
            }
            else
            {
                resObject = comObject;
            }

            return (T)resObject;
        }

        public static void Set(string name, object value)
        {
            Eval(string.Format("window['{0}']=null", name));
            window.GetType().InvokeMember(name, BindingFlags.SetProperty, null, window, new[] { value });
        }

        public static dynamic Eval(string code)
        {
            return window.GetType().InvokeMember("eval", BindingFlags.InvokeMethod, null, window, new[] { code });
        }
    }
}

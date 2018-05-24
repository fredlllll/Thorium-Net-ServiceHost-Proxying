using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.CodeDom.Compiler;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Thorium.Net.ServiceHost.Proxying
{
    public static class ProxyFactory
    {
        static readonly ModuleBuilder moduleBuilder;

        static readonly Dictionary<Type, Type> proxyTypes = new Dictionary<Type, Type>();

        static ProxyFactory()
        {
            var assemblyName = new AssemblyName("ServiceInvokerAssembly");
            var appDomain = AppDomain.CurrentDomain;
            var assemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
        }

        public static T CreateInstance<T>(IServiceInvoker invoker)
        {
            if(!proxyTypes.TryGetValue(typeof(T), out Type proxyType))
            {
                proxyType = CreateProxyType<T>();
                proxyTypes[typeof(T)] = proxyType;
            }

            return (T)Activator.CreateInstance(proxyType, invoker);
        }

        static Type CreateProxyType<T>()
        {
            Type targetType = typeof(T);
            if(!targetType.IsInterface)
            {
                throw new InvalidOperationException("you can only create a proxy of an interface");
            }

            string className = targetType.Name + "_Proxy";
            MethodInfo[] methods = targetType.GetMethods();

            StringBuilder source = new StringBuilder();
            source.AppendLine("using Thorium.Net.ServiceHost.Proxying;");
            source.AppendLine("using Thorium.Net.ServiceHost;");
            source.AppendLine("using Newtonsoft.Json.Linq;");
            source.AppendLine("public class " + className + " : ProxyBaseClass, " + targetType.FullName + " {");

            //constructor
            source.AppendLine("public " + className + "(IServiceInvoker invoker):base(invoker){}");

            foreach(var method in methods)
            {
                string type = method.ReturnType.Equals(typeof(void)) ? "void" : method.ReturnType.FullName;
                source.AppendLine("public " + type + " " + method.Name + "(");
                source.AppendLine(GetMethodParameterList(method));
                source.AppendLine("){");
                source.AppendLine("return base.Invoke<" + type + ">(\"" + method.Name + "\"," + GetMethodParameters(method) + ");");
                source.AppendLine("}");
            }

            source.AppendLine("}");

            return CompileProxyType(className, source.ToString(), targetType.Assembly);
        }

        static string GetMethodParameterList(MethodInfo method)
        {
            var parameters = method.GetParameters();
            List<string> parameterStrings = new List<string>();
            foreach(var par in parameters)
            {
                parameterStrings.Add(par.ParameterType.FullName + " " + par.Name);
            }
            return string.Join(",", parameterStrings);
        }

        static string GetMethodParameters(MethodInfo method)
        {
            var parameters = method.GetParameters();
            List<string> parameterStrings = new List<string>();
            foreach(var par in parameters)
            {
                parameterStrings.Add("new System.Tuple<string,object>(\"" + par.Name + "\"," + par.Name + ")");
            }
            return string.Join(",", parameterStrings);
        }

        static Type CompileProxyType(string typeName, string source, params Assembly[] references)
        {
            var provider = CodeDomProvider.CreateProvider("c#");
            var options = new CompilerParameters();
            options.ReferencedAssemblies.AddRange(new string[] {
                typeof(ProxyBaseClass).Assembly.Location, //thorium.net.ServiceHost.Proxying
                typeof(IServiceInvoker).Assembly.Location, //thorium.net
                typeof(JToken).Assembly.Location, // newtonsoft.json
            }.Concat(references.Select(x => x.Location)).ToArray());

            var results = provider.CompileAssemblyFromSource(options, source);

            if(results.Errors.Count > 0)
            {
                StringBuilder errors = new StringBuilder();
                errors.AppendLine("Errors occured when compiling proxy:");
                foreach(var error in results.Errors)
                {
                    errors.AppendLine(error.ToString());
                }
                throw new Exception(errors.ToString());
            }
            else
            {
                var t = results.CompiledAssembly.GetType(typeName);
                return t;
            }
        }
    }
}

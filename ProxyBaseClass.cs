using System;
using Newtonsoft.Json.Linq;

namespace Thorium.Net.ServiceHost.Proxying
{
    public abstract class ProxyBaseClass
    {
        private readonly IServiceInvoker invoker;

        protected ProxyBaseClass(IServiceInvoker invoker)
        {
            this.invoker = invoker;
        }

        protected T Invoke<T>(string name, params Tuple<string, object>[] args)
        {
            JToken arg = null;
            if(args.Length == 1 && args[0].Item2 != null)
            {
                arg = JToken.FromObject(args[0].Item2);
            }
            else if(args.Length > 1)
            {
                JObject argo = new JObject();
                arg = argo;
                for(int i = 0; i < args.Length; i++)
                {
                    argo[args[i].Item1] = JToken.FromObject(args[i].Item2);
                }
            }

            JToken result = invoker.Invoke(name, arg);
            return result.Value<T>();
        }
    }
}

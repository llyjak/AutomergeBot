using Newtonsoft.Json.Linq;

namespace PerfectGym.AutomergeBot.Models
{
    public class InfoModelBase
    {
        protected static TValue SafeGet<TValue>(JObject jObject, string path)
        {
            var props = path.Split('.');

            JObject current = jObject;
            JToken lastJToken = null;

            foreach (var prop in props)
            {
                if (current != null && current.TryGetValue(prop, out lastJToken))
                {
                    current = lastJToken as JObject;
                }
                else
                {
                    return default(TValue);
                }
            }

            return lastJToken.Value<TValue>();
        }
    }
}
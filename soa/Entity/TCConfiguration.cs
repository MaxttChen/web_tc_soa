using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace soa.Entity
{
    public static class TCConfiguration
    {

        public static JObject tc;

        public static String get(String key)
        {
            String value = String.Empty;
            try
            {
                value = tc[key].ToString();
            }catch(Exception e)
            {
                value = string.Empty;
            }
            return value;
        }

        public static void load()
        {
            string jsonText = string.Empty;

            using(var reader = new StreamReader(@".\template\TCConfiguration.json"))
            {
                jsonText = reader.ReadToEnd();
            }
            tc = JObject.Parse(jsonText);


        }

    }
}

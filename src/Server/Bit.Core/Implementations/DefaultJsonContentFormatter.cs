﻿using Bit.Core.Contracts;
using Newtonsoft.Json;

namespace Bit.Core.Implementations
{
    public class DefaultJsonContentFormatter : IContentFormatter
    {
        private static IContentFormatter _current;

        public static IContentFormatter Current
        {
            get
            {
                if (_current == null)
                    _current = new DefaultJsonContentFormatter();

                return _current;
            }
            set => _current = value;
        }

        public virtual T DeSerialize<T>(string objAsStr)
        {
            return JsonConvert.DeserializeObject<T>(objAsStr, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                TypeNameHandling = TypeNameHandling.All
            });
        }

        public virtual string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                Formatting = Formatting.Indented
            });
        }
    }
}
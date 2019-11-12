// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.AspNetCore.JsonPatch.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class PocoAdapter : IAdapter
    {
        private readonly IContractResolver _contractResolver;

        public PocoAdapter(IContractResolver contractResolver)
        {
            _contractResolver = contractResolver;
        }

        public virtual bool TryAdd(object target,
            string segment,
            object value,
            out string errorMessage)
        {
            if (!TryGetJsonProperty(target, _contractResolver, segment, out var jsonProperty))
            {
                errorMessage = Resources.FormatTargetLocationAtPathSegmentNotFound(segment);
                return false;
            }

            if (!jsonProperty.Writable)
            {
                errorMessage = Resources.FormatCannotUpdateProperty(segment);
                return false;
            }

            if (!TryConvertValue(value, jsonProperty.PropertyType, out var convertedValue))
            {
                errorMessage = Resources.FormatInvalidValueForProperty(value);
                return false;
            }

            jsonProperty.ValueProvider.SetValue(target, convertedValue);

            errorMessage = null;
            return true;
        }

        public virtual bool TryGet(object target,
            string segment,
            out object value,
            out string errorMessage)
        {
            if (!TryGetJsonProperty(target, _contractResolver, segment, out var jsonProperty))
            {
                errorMessage = Resources.FormatTargetLocationAtPathSegmentNotFound(segment);
                value = null;
                return false;
            }

            if (!jsonProperty.Readable)
            {
                errorMessage = Resources.FormatCannotReadProperty(segment);
                value = null;
                return false;
            }

            value = jsonProperty.ValueProvider.GetValue(target);
            errorMessage = null;
            return true;
        }

        public virtual bool TryRemove(object target,
            string segment,
            out string errorMessage)
        {
            if (!TryGetJsonProperty(target, _contractResolver, segment, out var jsonProperty))
            {
                errorMessage = Resources.FormatTargetLocationAtPathSegmentNotFound(segment);
                return false;
            }

            if (!jsonProperty.Writable)
            {
                errorMessage = Resources.FormatCannotUpdateProperty(segment);
                return false;
            }

            // Setting the value to "null" will use the default value in case of value types, and
            // null in case of reference types
            object value = null;
            if (jsonProperty.PropertyType.GetTypeInfo().IsValueType
                && Nullable.GetUnderlyingType(jsonProperty.PropertyType) == null)
            {
                value = Activator.CreateInstance(jsonProperty.PropertyType);
            }

            jsonProperty.ValueProvider.SetValue(target, value);

            errorMessage = null;
            return true;
        }

        public virtual bool TryReplace(object target,
            string segment,
            object value,
            out string errorMessage)
        {
            if (!TryGetJsonProperty(target, _contractResolver, segment, out var jsonProperty))
            {
                errorMessage = Resources.FormatTargetLocationAtPathSegmentNotFound(segment);
                return false;
            }

            if (!jsonProperty.Writable)
            {
                errorMessage = Resources.FormatCannotUpdateProperty(segment);
                return false;
            }

            if (!TryConvertValue(value, jsonProperty.PropertyType, out var convertedValue))
            {
                errorMessage = Resources.FormatInvalidValueForProperty(value);
                return false;
            }

            jsonProperty.ValueProvider.SetValue(target, convertedValue);

            errorMessage = null;
            return true;
        }

        public virtual bool TryTest(object target,
            string segment,
            object value,
            out string errorMessage)
        {
            if (!TryGetJsonProperty(target, _contractResolver, segment, out var jsonProperty))
            {
                errorMessage = Resources.FormatTargetLocationAtPathSegmentNotFound(segment);
                return false;
            }

            if (!jsonProperty.Readable)
            {
                errorMessage = Resources.FormatCannotReadProperty(segment);
                return false;
            }

            if (!TryConvertValue(value, jsonProperty.PropertyType, out var convertedValue))
            {
                errorMessage = Resources.FormatInvalidValueForProperty(value);
                return false;
            }

            var currentValue = jsonProperty.ValueProvider.GetValue(target);
            if (!JToken.DeepEquals(JsonConvert.SerializeObject(currentValue), JsonConvert.SerializeObject(convertedValue)))
            {
                errorMessage = Resources.FormatValueNotEqualToTestValue(currentValue, value, segment);
                return false;
            }

            errorMessage = null;
            return true;
        }

        public virtual bool TryTraverse(object target,
            string segment,
            out object value,
            out string errorMessage)
        {
            if (target == null)
            {
                value = null;
                errorMessage = null;
                return false;
            }

            if (TryGetJsonProperty(target, _contractResolver, segment, out var jsonProperty))
            {
                value = jsonProperty.ValueProvider.GetValue(target);
                errorMessage = null;
                return true;
            }

            value = null;
            errorMessage = Resources.FormatTargetLocationAtPathSegmentNotFound(segment);
            return false;
        }

        protected virtual bool TryGetJsonProperty(
            object target,
            IContractResolver contractResolver,
            string segment,
            out JsonProperty jsonProperty)
        {
            if (contractResolver.ResolveContract(target.GetType()) is JsonObjectContract jsonObjectContract)
            {
                var pocoProperty = jsonObjectContract
                    .Properties
                    .FirstOrDefault(p => string.Equals(p.PropertyName, segment, StringComparison.OrdinalIgnoreCase));

                if (pocoProperty != null)
                {
                    jsonProperty = pocoProperty;
                    return true;
                }
            }

            jsonProperty = null;
            return false;
        }

        protected virtual bool TryConvertValue(object value, Type propertyType, out object convertedValue)
        {
            var conversionResult = ConversionResultProvider.ConvertTo(value, propertyType, _contractResolver);
            if (!conversionResult.CanBeConverted)
            {
                convertedValue = null;
                return false;
            }

            convertedValue = conversionResult.ConvertedInstance;
            return true;
        }
    }
}

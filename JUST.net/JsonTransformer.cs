﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JUST
{
    public static class JsonTransformer
    {
        public static readonly JUSTContext GlobalContext = new JUSTContext();

        static JsonTransformer()
        {
            if (JsonConvert.DefaultSettings == null)
            {
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                {
                    DateParseHandling = DateParseHandling.None
                };
            }
        }

        public static string Transform(string transformerJson, string inputJson, JUSTContext localContext = null)
        {
            return Transform(transformerJson, JsonConvert.DeserializeObject<JToken>(inputJson), localContext);
        }

        public static string Transform(string transformerJson, JToken input, JUSTContext localContext = null)
        {
            JToken transformerToken = JsonConvert.DeserializeObject<JToken>(transformerJson);
            var result = Transform(transformerToken, input, localContext);
            string output = JsonConvert.SerializeObject(result);
            return output;
        }

        public static JArray Transform(JArray transformerArray, string input, JUSTContext localContext = null)
        {
            return Transform(transformerArray, JsonConvert.DeserializeObject<JToken>(input), localContext);
        }

        public static JArray Transform(JArray transformerArray, JToken input, JUSTContext localContext = null)
        {
            var result = new JArray();
            for (int i = 0; i < transformerArray.Count; i++)
            {
                var transformer = transformerArray[i];
                JToken afterTransform = Transform(transformer, input, localContext);
                result.Add(afterTransform.HasValues ? afterTransform : transformerArray[i]);
            }
            return result;
        }

        private static JToken Transform(JToken transformer, JToken input, JUSTContext localContext = null)
        {
            switch (transformer.Type)
            {
                case JTokenType.Object:
                    return Transform(transformer as JObject, input, localContext);
                case JTokenType.Array:
                    return Transform(transformer as JArray, input, localContext);
                default:
                    throw new NotSupportedException($"Transformer of type '{transformer.Type}' not supported!");
            }
        }

        public static JToken Transform(JObject transformer, string input, JUSTContext localContext = null)
        {
            return Transform(transformer, JsonConvert.DeserializeObject<JToken>(input), localContext);
        }

        public static JToken Transform(JObject transformer, JToken input, JUSTContext localContext = null)
        {
            (localContext ?? GlobalContext).Input = input;
            RecursiveEvaluate(transformer, null, null, localContext);
            if (transformer.First is JProperty prop && prop.Name.StartsWith("#loop"))
            {
                return transformer.First.First;
            }
            return transformer;
        }

        #region RecursiveEvaluate


        private static void RecursiveEvaluate(JToken parentToken, JArray parentArray, JToken currentArrayToken, JUSTContext localContext)
        {
            if (parentToken == null)
                return;

            JEnumerable<JToken> tokens = parentToken.Children();

            List<JToken> selectedTokens = null;
            Dictionary<string, JToken> tokensToReplace = null;
            List<JToken> tokensToDelete = null;

            List<string> loopProperties = null;
            JArray arrayToForm = null;
            JObject dictToForm = null;
            List<JToken> tokenToForm = null;
            List<JToken> tokensToAdd = null;

            bool isLoop = false;

            foreach (JToken childToken in tokens)
            {
                if (childToken.Type == JTokenType.Array && (parentToken as JProperty)?.Name.Trim() != "#")
                {
                    IEnumerable<object> itemsToAdd = TransformArray(childToken.Children(), parentArray, currentArrayToken, localContext);
                    JArray arrayToken = childToken as JArray;
                    arrayToken.RemoveAll();
                    foreach (object itemToAdd in itemsToAdd)
                    {
                        if (itemToAdd is Array)
                        {
                            foreach (var item in itemToAdd as Array)
                            {
                                arrayToken.Add(Utilities.GetNestedData(item));
                            }
                        }
                        else
                        {
                            arrayToken.Add(JToken.FromObject(itemToAdd));
                        }
                    }
                }

                if (childToken.Type == JTokenType.Property)
                {
                    JProperty property = childToken as JProperty;

                    if (property.Name != null && property.Name == "#" && property.Value.Type == JTokenType.Array)
                    {
                        JArray values = property.Value as JArray;

                        JEnumerable<JToken> arrayValues = values.Children();

                        foreach (JToken arrayValue in arrayValues)
                        {
                            if (arrayValue.Type == JTokenType.String &&
                                ExpressionHelper.TryParseFunctionNameAndArguments(
                                    arrayValue.Value<string>().Trim(), out string functionName, out string arguments))
                            {
                                if (functionName == "copy")
                                {
                                    if (selectedTokens == null)
                                        selectedTokens = new List<JToken>();

                                    selectedTokens.Add(Copy(arguments, localContext));
                                }
                                else if (functionName == "replace")
                                {
                                    if (tokensToReplace == null)
                                        tokensToReplace = new Dictionary<string, JToken>();

                                    var replaceResult = Replace(arguments, localContext);
                                    tokensToReplace.Add(replaceResult.Key, replaceResult.Value);
                                }
                                else if (functionName == "delete")
                                {
                                    if (tokensToDelete == null)
                                        tokensToDelete = new List<JToken>();

                                    tokensToDelete.Add(Delete(arguments, localContext));
                                }
                            }
                        }
                    }

                    if (property.Name != null && property.Value.ToString().Trim().StartsWith("#")
                        && !property.Name.Contains("#eval")  && !property.Name.Contains("#ifgroup")
                        && !property.Name.Contains("#loop"))
                    {
                        object newValue = ParseFunction(property.Value.ToString(), parentArray, currentArrayToken, localContext);
                        property.Value = GetToken(newValue, localContext);
                    }

                    /* For looping*/
                    isLoop = false;

                    if (property.Name != null && property.Name.Contains("#eval"))
                    {
                        ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out string functionName, out string functionString);
                        object functionResult = ParseFunction(functionString, parentArray, currentArrayToken, localContext);

                        JProperty clonedProperty = new JProperty(functionResult.ToString(),
                            property.Value.Type != JTokenType.Null ? 
                                ReflectionHelper.GetTypedValue(
                                    property.Value.Type, 
                                    ParseFunction(
                                        property.Value.Value<string>(), 
                                        parentArray, 
                                        currentArrayToken, 
                                        localContext), 
                                    GetEvaluationMode(localContext)) : 
                                null);

                        if (loopProperties == null)
                            loopProperties = new List<string>();

                        loopProperties.Add(property.Name);

                        if (tokensToAdd == null)
                        {
                            tokensToAdd = new List<JToken>();
                        }
                        tokensToAdd.Add(clonedProperty);
                    }

                    if (property.Name != null && property.Name.Contains("#ifgroup"))
                    {
                        ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out string functionName, out string functionString);
                        object functionResult = ParseFunction(functionString, parentArray, currentArrayToken, localContext);
                        bool result = false;

                        try
                        {
                            result = (bool)ReflectionHelper.GetTypedValue(typeof(bool), functionResult, GetEvaluationMode(localContext));
                        }
                        catch
                        {
                            if (IsStrictMode(localContext)) { throw; }
                            result =  false;
                        }

                        if (result == true)
                        {
                            if (loopProperties == null)
                                loopProperties = new List<string>();

                            loopProperties.Add(property.Name);

                            RecursiveEvaluate(childToken, parentArray, currentArrayToken, localContext);

                            if (tokenToForm == null)
                            {
                                tokenToForm = new List<JToken>();                               
                            }

                            foreach (JToken grandChildToken in childToken.Children())
                                tokenToForm.Add(grandChildToken.DeepClone());
                        }
                        else
                        {
                            if (loopProperties == null)
                            {
                                loopProperties = new List<string>();
                            }

                            loopProperties.Add(property.Name);
                        }

                        isLoop = true;
                    }

                    if (property.Name != null && property.Name.Contains("#loop"))
                    {
                        ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out string functionName, out string arguments);
                        var token = currentArrayToken != null && functionName == "loopwithincontext" ? currentArrayToken : GetInputToken(localContext);

                        var strArrayToken = ParseArgument(parentArray, currentArrayToken, arguments, localContext) as string;

                        bool isDictionary = false;
                        JToken arrayToken;
                        try
                        {
                            arrayToken = token.SelectToken(strArrayToken);
                            if (arrayToken is JObject)
                            {
                                isDictionary = true;
                                JArray arr = new JArray();
                                var dict = arrayToken.ToDictionary(t => (t as JProperty).Name, t => (t as JProperty).Value);
                                foreach (var item in dict)
                                {
                                    var obj = new JObject();
                                    obj.Add(item.Key, item.Value);
                                    arr.Add(obj);
                                }

                                arrayToken = arr;
                            }
                        }
                        catch
                        {
                            var multipleTokens = token.SelectTokens(strArrayToken);
                            arrayToken = new JArray(multipleTokens);
                        }

                        if (arrayToken == null)
                        {
                            arrayToForm = new JArray();
                        }
                        else
                        {
                            JArray array = (JArray)arrayToken;

                            IEnumerator<JToken> elements = array.GetEnumerator();

                            if (!isDictionary)
                            {
                                while (elements.MoveNext())
                                {
                                    if (arrayToForm == null)
                                        arrayToForm = new JArray();

                                    JToken clonedToken = childToken.DeepClone();

                                    RecursiveEvaluate(clonedToken, array, elements.Current, localContext);

                                    foreach (JToken replacedProperty in clonedToken.Children())
                                    {
                                        arrayToForm.Add(replacedProperty);
                                    }
                                }
                            }
                            else
                            {
                                while (elements.MoveNext())
                                {
                                    if (dictToForm == null)
                                        dictToForm = new JObject();

                                    JToken clonedToken = childToken.DeepClone();
                                    RecursiveEvaluate(clonedToken, array, elements.Current, localContext);
                                    foreach (JToken replacedProperty in clonedToken.Children().Select(t => t.First))
                                    {
                                        dictToForm.Add(replacedProperty);
                                    }
                                }
                            }
                        }
                        if (loopProperties == null)
                            loopProperties = new List<string>();

                        loopProperties.Add(property.Name);
                        isLoop = true;
                    }
                    /*End looping */
                }

                if (childToken.Type == JTokenType.String && childToken.Value<string>().Trim().StartsWith("#") 
                    && parentArray != null && currentArrayToken != null)
                {
                    object newValue = ParseFunction(childToken.Value<string>(), parentArray, currentArrayToken, localContext);

                    JToken replaceToken = GetToken(newValue, localContext);
                    childToken.Replace(replaceToken);
                }

                if (!isLoop)
                    RecursiveEvaluate(childToken, parentArray, currentArrayToken, localContext);
            }

            if (selectedTokens != null)
            {
                foreach (JToken selectedToken in selectedTokens)
                {
                    if (selectedToken != null)
                    {
                        JEnumerable<JToken> copyChildren = selectedToken.Children();

                        foreach (JToken copyChild in copyChildren)
                        {
                            JProperty property = copyChild as JProperty;

                            (parentToken as JObject).Add(property.Name, property.Value);
                        }
                    }
                }
            }

            if (tokensToReplace != null)
            {
                foreach (KeyValuePair<string, JToken> tokenToReplace in tokensToReplace)
                {
                    JToken selectedToken = (parentToken as JObject).SelectToken(tokenToReplace.Key);

                    if (selectedToken != null && selectedToken is JObject)
                    {
                        JObject selectedObject = selectedToken as JObject;
                        selectedObject.RemoveAll();

                        JEnumerable<JToken> copyChildren = tokenToReplace.Value.Children();

                        foreach (JToken copyChild in copyChildren)
                        {
                            JProperty property = copyChild as JProperty;
                            selectedObject.Add(property.Name, property.Value);
                        }
                    }
                    if (selectedToken != null && selectedToken is JValue)
                    {
                        JValue selectedObject = selectedToken as JValue;
                        selectedObject.Value = tokenToReplace.Value.ToString();
                    }
                }
            }

            if (tokensToDelete != null)
            {
                foreach (string selectedToken in tokensToDelete)
                {
                    JToken tokenToRemove = parentToken.SelectToken(selectedToken);

                    if (tokenToRemove != null)
                        tokenToRemove.Ancestors().First().Remove();
                }
            }
            if (tokensToAdd != null)
            {
                foreach (JToken token in tokensToAdd)
                {
                    (parentToken as JObject).Add((token as JProperty).Name, (token as JProperty).Value);
                }
            }

            if (tokenToForm != null)
            {
                foreach (JToken token in tokenToForm)
                {
                    foreach (JProperty childToken in token.Children())
                        (parentToken as JObject).Add(childToken.Name, childToken.Value);
                }
            }
            if (parentToken is JObject jObject)
            {
                jObject.Remove("#");
            }

            if (loopProperties != null)
            {
                if (parentToken.Parent == null && parentToken.Children().Count() == 1)
                {
                    if (parentToken.Parent == null && parentToken.Type == JTokenType.Object)
                    {
                        (parentToken.First as JProperty).Value = arrayToForm;
                        arrayToForm = null;
                    }
                    else if (parentToken.Parent != null && parentToken.Parent is JArray arr)
                    {
                        foreach (var item in arrayToForm)
                        {
                            arr.Add(item);
                        }

                        if (!parentToken.HasValues)
                        {
                            var tmp = parentToken;
                            parentToken = arr;
                            tmp.Remove();
                        }
                    }
                    else
                    {
                    }
                }
                else
                {
                    foreach (string propertyToDelete in loopProperties)
                    {
                        (parentToken as JObject).Remove(propertyToDelete);
                    }
                }
            }
            if (arrayToForm != null)
            {
                if (parentToken.Parent != null && parentToken.Parent is JArray arr)
                {
                    var index = arr.IndexOf(arr.FirstOrDefault(t => !t.HasValues));
                    foreach (var item in arrayToForm.Reverse())
                    {
                        arr.Insert(index+1, item);
                    }
                    arr.RemoveAt(index);
                    if (!parentToken.HasValues)
                    {
                        parentToken = arr;
                    }
                }
                else
                {
                    parentToken.Replace(arrayToForm);
                }
            }
            if (dictToForm != null)
            {
                parentToken.Replace(dictToForm);
            }
        }
        #endregion

        private static JToken GetToken(object newValue, JUSTContext localContext)
        {
            JToken result = null;
            if (newValue != null)
            {
                if (newValue is JToken token)
                {
                    result = token;
                }
                else
                {
                    try
                    {
                        //JToken newToken = JToken.FromObject(newValue);
                        if (newValue is IEnumerable<object> newArray)
                        {
                            result = new JArray(newArray);
                        }
                        else
                        {
                            result = new JValue(newValue);
                        }
                    }
                    catch
                    {
                        if (IsStrictMode(localContext))
                        {
                            throw;
                        }

                        if (IsFallbackToDefault(localContext))
                        {
                            result = JValue.CreateNull();
                        }
                    }
                }
            }
            else
            {
                result = JValue.CreateNull();
            }

            return result;
        }

        private static IEnumerable<object> TransformArray(JEnumerable<JToken> children, JArray parentArray, JToken currentArrayToken, JUSTContext localContext)
        {
            var result = new List<object>();

            foreach (JToken arrEl in children)
            {
                object itemToAdd = arrEl.Value<JToken>();

                if (arrEl.Type == JTokenType.String && arrEl.ToString().Trim().StartsWith("#"))
                {
                    object value = ParseFunction(arrEl.ToString(), parentArray, currentArrayToken, localContext);
                    itemToAdd = value;
                }

                result.Add(itemToAdd);
            }

            return result;
        }

        #region Copy
        private static JToken Copy(string argument, JUSTContext localContext)
        {
            var jsonPath = ParseArgument(null, null, argument, localContext) as string;
            if (jsonPath == null)
            {
                throw new ArgumentException("Invalid jsonPath for #copy!");
            }
            JToken selectedToken = GetInputToken(localContext).SelectToken(jsonPath);
            return selectedToken;
        }

        #endregion

        #region Replace
        private static KeyValuePair<string, JToken> Replace(string arguments, JUSTContext localContext)
        {
            string[] argumentArr = ExpressionHelper.GetArguments(arguments);
            if (argumentArr.Length < 2)
            {
                throw new Exception("Function #replace needs two arguments - 1. jsonPath to be replaced, 2. token to replace with.");
            }
            var key = ParseArgument(null, null, argumentArr[0], localContext) as string;
            if (key == null)
            {
                throw new ArgumentException("Invalid jsonPath for #replace!");
            }
            object str = ParseArgument(null, null, argumentArr[1], localContext);

            JToken newToken = GetToken(str, localContext); 
            return new KeyValuePair<string, JToken>(key, newToken);
        }

        #endregion

        #region Delete
        private static string Delete(string argument, JUSTContext localContext)
        {
            var result = ParseArgument(null, null, argument, localContext) as string;
            if (result == null)
            {
                throw new ArgumentException("Invalid jsonPath for #delete!");
            }
            return result;
        }
        #endregion

        #region ParseFunction

        private static object ParseFunction(string functionString, JArray array, JToken currentArrayElement, JUSTContext localContext)
        {
            try
            {
                object output = null;

                string functionName, argumentString;
                if (!ExpressionHelper.TryParseFunctionNameAndArguments(functionString, out functionName, out argumentString))
                {
                    return functionName;
                }

                string[] arguments = ExpressionHelper.GetArguments(argumentString);
                var listParameters = new List<object>();

                if (functionName == "ifcondition")
                {
                    var condition = ParseArgument(array, currentArrayElement, arguments[0], localContext);
                    var value = ParseArgument(array, currentArrayElement, arguments[1], localContext);
                    var index = condition.ToString().ToLower() == value.ToString().ToLower() ? 2 : 3;
                    output = ParseArgument(array, currentArrayElement, arguments[index], localContext);
                }
                else
                {
                    int i = 0;
                    for (; i < (arguments?.Length ?? 0); i++)
                    {
                        listParameters.Add(ParseArgument(array, currentArrayElement, arguments[i], localContext));
                    }

                    listParameters.Add(localContext ?? GlobalContext);
                    var parameters = listParameters.ToArray();

                    if (new[] { "currentvalue", "currentindex", "lastindex", "lastvalue" }.Contains(functionName))
                        output = ReflectionHelper.caller(null, "JUST.Transformer", functionName, new object[] { array, currentArrayElement }, true, localContext ?? GlobalContext);
                    else if (new[] { "currentvalueatpath", "lastvalueatpath" }.Contains(functionName))
                        output = ReflectionHelper.caller(null, "JUST.Transformer", functionName, new object[] { array, currentArrayElement, parameters[0] }, true, localContext ?? GlobalContext);
                    else if (functionName == "currentproperty")
                        output = ReflectionHelper.caller(null, "JUST.Transformer", functionName, 
                            new object[] { array, currentArrayElement, localContext ?? GlobalContext }, 
                            false, localContext ?? GlobalContext);
                    else if (functionName == "customfunction")
                        output = CallCustomFunction(parameters, localContext);
                    else if (localContext?.IsRegisteredCustomFunction(functionName) ?? false)
                    {
                        var methodInfo = localContext.GetCustomMethod(functionName);
                        output = ReflectionHelper.InvokeCustomMethod(methodInfo, parameters, true, localContext ?? GlobalContext);
                    }
                    else if (GlobalContext.IsRegisteredCustomFunction(functionName))
                    {
                        var methodInfo = GlobalContext.GetCustomMethod(functionName);
                        output = ReflectionHelper.InvokeCustomMethod(methodInfo, parameters, true, localContext ?? GlobalContext);
                    }
                    else if (Regex.IsMatch(functionName, ReflectionHelper.EXTERNAL_ASSEMBLY_REGEX))
                    {
                        output = ReflectionHelper.CallExternalAssembly(functionName, parameters, localContext ?? GlobalContext);
                    }
                    else if (new[] { "xconcat", "xadd",
                        "mathequals", "mathgreaterthan", "mathlessthan", "mathgreaterthanorequalto", "mathlessthanorequalto",
                        "stringcontains", "stringequals"}.Contains(functionName))
                    {
                        object[] oParams = new object[1];
                        oParams[0] = parameters;
                        output = ReflectionHelper.caller(null, "JUST.Transformer", functionName, oParams, true, localContext ?? GlobalContext);
                    }
                    else if (functionName == "applyover")
                    {
                        var contextInput = GetInputToken(localContext);
                        var input = JToken.Parse(Transform(parameters[0].ToString(), contextInput.ToString(), localContext));
                        (localContext ?? GlobalContext).Input = input;
                        output = ParseFunction(parameters[1].ToString().Trim('\''), array, currentArrayElement, localContext ?? GlobalContext);
                        (localContext ?? GlobalContext).Input = contextInput;
                    }
                    else
                    {
                        var input = ((JUSTContext)parameters.Last()).Input;
                        if (currentArrayElement != null && functionName != "valueof")
                        {
                            ((JUSTContext)parameters.Last()).Input = currentArrayElement;
                        }
                        output = ReflectionHelper.caller(null, "JUST.Transformer", functionName, parameters, true, localContext ?? GlobalContext);
                        ((JUSTContext)parameters.Last()).Input = input;
                    }
                }

                return output;
            }
            catch (Exception ex)
            {
                throw new Exception("Error while calling function : " + functionString + " - " + ex.Message, ex);
            }
        }

        private static object ParseArgument(JArray array, JToken currentArrayElement, string argument, JUSTContext localContext)
        {
            string trimmedArgument = argument;

            if (argument.Contains("#"))
                trimmedArgument = argument.Trim();

            if (trimmedArgument.StartsWith("#"))
            {
                return ParseFunction(trimmedArgument, array, currentArrayElement, localContext);
            }
            else
                return trimmedArgument;
        }

        private static object CallCustomFunction(object[] parameters, JUSTContext localContext)
        {
            object[] customParameters = new object[parameters.Length - 3];
            string functionString = string.Empty;
            string dllName = string.Empty;
            int i = 0;
            foreach (object parameter in parameters)
            {
                if (i == 0)
                    dllName = parameter.ToString();
                else if (i == 1)
                    functionString = parameter.ToString();
                else
                if (i != (parameters.Length - 1))
                    customParameters[i - 2] = parameter;

                i++;
            }

            int index = functionString.LastIndexOf(".");

            string className = functionString.Substring(0, index);
            string functionName = functionString.Substring(index + 1, functionString.Length - index - 1);

            className = className + "," + dllName;

            return ReflectionHelper.caller(null, className, functionName, customParameters, true, localContext ?? GlobalContext);

        }
        #endregion

        #region Split
        public static IEnumerable<string> SplitJson(string input, string arrayPath)
        {
            JObject inputJObject = JsonConvert.DeserializeObject<JObject>(input);

            List<JObject> jObjects = SplitJson(inputJObject, arrayPath).ToList<JObject>();

            List<string> output = null;

            foreach (JObject jObject in jObjects)
            {
                if (output == null)
                    output = new List<string>();

                output.Add(JsonConvert.SerializeObject(jObject));
            }

            return output;
        }

        public static IEnumerable<JObject> SplitJson(JObject input, string arrayPath)
        {
            List<JObject> jsonObjects = null;

            JToken tokenArr = input.SelectToken(arrayPath);

            string pathToReplace = tokenArr.Path;

            if (tokenArr != null && tokenArr is JArray)
            {
                JArray array = tokenArr as JArray;

                foreach (JToken tokenInd in array)
                {

                    string path = tokenInd.Path;

                    JToken clonedToken = input.DeepClone();

                    JToken foundToken = clonedToken.SelectToken("$." + path);
                    JToken tokenToReplcae = clonedToken.SelectToken("$." + pathToReplace);

                    tokenToReplcae.Replace(foundToken);

                    if (jsonObjects == null)
                        jsonObjects = new List<JObject>();

                    jsonObjects.Add(clonedToken as JObject);


                }
            }
            else
                throw new Exception("ArrayPath must be a valid JSON path to a JSON array.");

            return jsonObjects;
        }
        #endregion

        private static int GetIndexOfFunctionEnd(string totalString)
        {
            int index = -1;

            int startIndex = totalString.IndexOf("#");

            int startBrackettCount = 0;
            int endBrackettCount = 0;

            for (int i = startIndex; i < totalString.Length; i++)
            {
                if (totalString[i] == '(')
                    startBrackettCount++;
                if (totalString[i] == ')')
                    endBrackettCount++;

                if (endBrackettCount == startBrackettCount && endBrackettCount > 0)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        private static JToken GetInputToken(JUSTContext localContext)
        {
            return localContext?.Input ?? GlobalContext.Input;
        }

        private static EvaluationMode GetEvaluationMode(JUSTContext localContext)
        {
            return localContext?.EvaluationMode ?? GlobalContext.EvaluationMode;
        }

        private static bool IsStrictMode(JUSTContext localContext)
        {
            return GetEvaluationMode(localContext) == EvaluationMode.Strict;
        }

        private static bool IsFallbackToDefault(JUSTContext localContext)
        {
            return GetEvaluationMode(localContext) == EvaluationMode.FallbackToDefault;
        }
    }
}

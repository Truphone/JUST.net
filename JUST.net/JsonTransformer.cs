﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JUST.net.Selectables;

namespace JUST
{
    public class JsonTransformer : JsonTransformer<JsonPathSelectable>
    {
        public JsonTransformer(JUSTContext context = null) : base(context)
        {
        }
    }

    public class JsonTransformer<T> where T: ISelectableToken
    {
        private readonly JUSTContext GlobalContext;

        public JsonTransformer(JUSTContext context = null)
        {
            GlobalContext = context ?? new JUSTContext();

            if (JsonConvert.DefaultSettings == null)
            {
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                {
                    DateParseHandling = DateParseHandling.None
                };
            }
        }

        public string Transform(string transformerJson, string inputJson)
        {
            return Transform(transformerJson, JsonConvert.DeserializeObject<JToken>(inputJson));
        }

        public string Transform(string transformerJson, JToken input)
        {
            JToken transformerToken = JsonConvert.DeserializeObject<JToken>(transformerJson);
            var result = Transform(transformerToken, input);
            string output = JsonConvert.SerializeObject(result);
            return output;
        }

        public JArray Transform(JArray transformerArray, string input)
        {
            return Transform(transformerArray, JsonConvert.DeserializeObject<JToken>(input));
        }

        public JArray Transform(JArray transformerArray, JToken input)
        {
            var result = new JArray();
            for (int i = 0; i < transformerArray.Count; i++)
            {
                var transformer = transformerArray[i];
                JToken afterTransform = Transform(transformer, input);
                result.Add(afterTransform.HasValues ? afterTransform : transformerArray[i]);
            }
            return result;
        }

        private JToken Transform(JToken transformer, JToken input)
        {
            switch (transformer.Type)
            {
                case JTokenType.Object:
                    return Transform(transformer as JObject, input);
                case JTokenType.Array:
                    return Transform(transformer as JArray, input);
                default:
                    throw new NotSupportedException($"Transformer of type '{transformer.Type}' not supported!");
            }
        }

        public JToken Transform(JObject transformer, string input)
        {
            return Transform(transformer, JsonConvert.DeserializeObject<JToken>(input));
        }

        public JToken Transform(JObject transformer, JToken input)
        {
            GlobalContext.Input = input;
            RecursiveEvaluate(transformer, null, null);
            if (transformer.First is JProperty prop && prop.Name.StartsWith("#loop"))
            {
                return transformer.First.First;
            }
            return transformer;
        }

        #region RecursiveEvaluate


        private void RecursiveEvaluate(JToken parentToken, JArray parentArray, JToken currentArrayToken)
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
                    IEnumerable<object> itemsToAdd = TransformArray(childToken.Children(), parentArray, currentArrayToken);
                    BuildArrayToken(childToken as JArray, itemsToAdd);
                }

                else if (childToken.Type == JTokenType.Property && childToken is JProperty property && property.Name != null)
                {
                    /* For looping*/
                    isLoop = false;

                    if (property.Name == "#" && property.Value.Type == JTokenType.Array && property.Value is JArray values)
                    {
                        BulkOperations(values.Children(), ref selectedTokens, ref tokensToReplace, ref tokensToDelete);
                    }
                    else if (property.Name.Contains("#eval"))
                    {
                        EvalOperation(property, parentArray, currentArrayToken, ref loopProperties, ref tokensToAdd);
                    }
                    else if (property.Name.Contains("#ifgroup"))
                    {
                        ConditionalGroupOperation(property, parentArray, currentArrayToken, ref loopProperties, ref tokenToForm, childToken);

                        isLoop = true;
                    }
                    else if (property.Name.Contains("#loop"))
                    {
                        LoopOperation(property, parentArray, currentArrayToken, ref loopProperties, ref arrayToForm, ref dictToForm, childToken);
                        isLoop = true;
                    }
                    else if (property.Value.ToString().Trim().StartsWith("#"))
                    {
                        property.Value = GetToken(ParseFunction(property.Value.ToString(), parentArray, currentArrayToken));
                    }
                    /*End looping */
                }
                else if (childToken.Type == JTokenType.String && childToken.Value<string>().Trim().StartsWith("#")
                    && parentArray != null && currentArrayToken != null)
                {
                    object newValue = ParseFunction(childToken.Value<string>(), parentArray, currentArrayToken);
                    childToken.Replace(GetToken(newValue));
                }

                if (!isLoop)
                    RecursiveEvaluate(childToken, parentArray, currentArrayToken);
            }

            parentToken = PostOperationsBuildUp(parentToken, selectedTokens, tokensToReplace, tokensToDelete, loopProperties, arrayToForm, dictToForm, tokenToForm, tokensToAdd);
        }

        private JToken PostOperationsBuildUp(JToken parentToken, List<JToken> selectedTokens, Dictionary<string, JToken> tokensToReplace, List<JToken> tokensToDelete, List<string> loopProperties, JArray arrayToForm, JObject dictToForm, List<JToken> tokenToForm, List<JToken> tokensToAdd)
        {
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
                    JToken selectedToken = GetSelectableToken(parentToken as JObject, GlobalContext).Select(tokenToReplace.Key);

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
                    JToken tokenToRemove = GetSelectableToken(parentToken, GlobalContext).Select(selectedToken);

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

            return parentToken;
        }

        private void LoopOperation(JProperty property, JArray parentArray, JToken currentArrayToken, ref List<string> loopProperties, ref JArray arrayToForm, ref JObject dictToForm, JToken childToken)
        {
            ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out string functionName, out string arguments);
            var token = currentArrayToken != null && functionName == "loopwithincontext" ? currentArrayToken : GlobalContext.Input;

            var strArrayToken = ParseArgument(parentArray, currentArrayToken, arguments) as string;

            bool isDictionary = false;
            JToken arrayToken;
            var selectable = GetSelectableToken(token, GlobalContext);
            try
            {
                arrayToken = selectable.Select(strArrayToken);
                if (arrayToken is IDictionary<string, JToken> dict) //JObject is a dictionary
                {
                    isDictionary = true;
                    JArray arr = new JArray();
                    foreach (var item in dict)
                    {
                        arr.Add(new JObject { { item.Key, item.Value } });
                    }

                    arrayToken = arr;
                }
            }
            catch
            {
                var multipleTokens = selectable.SelectMultiple(strArrayToken);
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

                        RecursiveEvaluate(clonedToken, array, elements.Current);

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
                        RecursiveEvaluate(clonedToken, array, elements.Current);
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
        }

        private void ConditionalGroupOperation(JProperty property, JArray parentArray, JToken currentArrayToken, ref List<string> loopProperties, ref List<JToken> tokenToForm, JToken childToken)
        {
            ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out string functionName, out string arguments);
            object functionResult = ParseFunction(arguments, parentArray, currentArrayToken);
            bool result = false;

            try
            {
                result = (bool)ReflectionHelper.GetTypedValue(typeof(bool), functionResult, GlobalContext.EvaluationMode);
            }
            catch
            {
                if (IsStrictMode(GlobalContext)) { throw; }
                result = false;
            }

            if (result == true)
            {
                if (loopProperties == null)
                    loopProperties = new List<string>();

                loopProperties.Add(property.Name);

                RecursiveEvaluate(childToken, parentArray, currentArrayToken);

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
        }

        private void EvalOperation(JProperty property, JArray parentArray, JToken currentArrayToken, ref List<string> loopProperties, ref List<JToken> tokensToAdd)
        {
            ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out string functionName, out string arguments);
            object functionResult = ParseFunction(arguments, parentArray, currentArrayToken);

            JProperty clonedProperty = new JProperty(functionResult.ToString(),
                property.Value.Type != JTokenType.Null ?
                    ReflectionHelper.GetTypedValue(
                        property.Value.Type,
                        ParseFunction(
                            property.Value.Value<string>(),
                            parentArray,
                            currentArrayToken),
                        GlobalContext.EvaluationMode) :
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

        private void BulkOperations(JEnumerable<JToken> arrayValues, ref List<JToken> selectedTokens, ref Dictionary<string, JToken> tokensToReplace, ref List<JToken> tokensToDelete)
        {
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

                        selectedTokens.Add(Copy(arguments));
                    }
                    else if (functionName == "replace")
                    {
                        if (tokensToReplace == null)
                            tokensToReplace = new Dictionary<string, JToken>();

                        var replaceResult = Replace(arguments);
                        tokensToReplace.Add(replaceResult.Key, replaceResult.Value);
                    }
                    else if (functionName == "delete")
                    {
                        if (tokensToDelete == null)
                            tokensToDelete = new List<JToken>();

                        tokensToDelete.Add(Delete(arguments));
                    }
                }
            }
        }

        private static void BuildArrayToken(JArray arrayToken, IEnumerable<object> itemsToAdd)
        {
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
        #endregion

        private JToken GetToken(object newValue)
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
                        if (IsStrictMode(GlobalContext))
                        {
                            throw;
                        }

                        if (IsFallbackToDefault(GlobalContext))
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

        private IEnumerable<object> TransformArray(JEnumerable<JToken> children, JArray parentArray, JToken currentArrayToken)
        {
            var result = new List<object>();

            foreach (JToken arrEl in children)
            {
                object itemToAdd = arrEl.Value<JToken>();

                if (arrEl.Type == JTokenType.String && arrEl.ToString().Trim().StartsWith("#"))
                {
                    object value = ParseFunction(arrEl.ToString(), parentArray, currentArrayToken);
                    itemToAdd = value;
                }

                result.Add(itemToAdd);
            }

            return result;
        }

        #region Copy
        private JToken Copy(string argument)
        {
            if (!(ParseArgument(null, null, argument) is string path))
            {
                throw new ArgumentException($"Invalid path for #copy: '{argument}' resolved to null");
            }
            JToken selectedToken = GetSelectableToken(GlobalContext.Input,GlobalContext).Select(path);
            return selectedToken;
        }

        #endregion

        #region Replace
        private KeyValuePair<string, JToken> Replace(string arguments)
        {
            string[] argumentArr = ExpressionHelper.GetArguments(arguments);
            if (argumentArr.Length < 2)
            {
                throw new Exception("Function #replace needs two arguments - 1. jsonPath to be replaced, 2. token to replace with.");
            }
            if (!(ParseArgument(null, null, argumentArr[0]) is string key))
            {
                throw new ArgumentException("Invalid jsonPath for #replace!");
            }
            object str = ParseArgument(null, null, argumentArr[1]);

            JToken newToken = GetToken(str); 
            return new KeyValuePair<string, JToken>(key, newToken);
        }

        #endregion

        #region Delete
        private string Delete(string argument)
        {
            var result = ParseArgument(null, null, argument) as string;
            if (result == null)
            {
                throw new ArgumentException("Invalid jsonPath for #delete!");
            }
            return result;
        }
        #endregion

        #region ParseFunction

        private object ParseFunction(string functionString, JArray array, JToken currentArrayElement)
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
                    var condition = ParseArgument(array, currentArrayElement, arguments[0]);
                    var value = ParseArgument(array, currentArrayElement, arguments[1]);
                    var index = condition.ToString().ToLower() == value.ToString().ToLower() ? 2 : 3;
                    output = ParseArgument(array, currentArrayElement, arguments[index]);
                }
                else
                {
                    int i = 0;
                    for (; i < (arguments?.Length ?? 0); i++)
                    {
                        listParameters.Add(ParseArgument(array, currentArrayElement, arguments[i]));
                    }

                    listParameters.Add(GlobalContext);
                    var parameters = listParameters.ToArray();

                    if (new[] { "currentvalue", "currentindex", "lastindex", "lastvalue" }.Contains(functionName))
                        output = ReflectionHelper.Caller<T>(null, "JUST.Transformer`1", functionName, new object[] { array, currentArrayElement }, true, GlobalContext);
                    else if (new[] { "currentvalueatpath", "lastvalueatpath" }.Contains(functionName))
                        output = ReflectionHelper.Caller<T>(
                            null, 
                            "JUST.Transformer`1", 
                            functionName, 
                            new [] { array, currentArrayElement }.Concat(parameters).ToArray(), 
                            true, 
                            GlobalContext);
                    else if (functionName == "currentproperty")
                        output = ReflectionHelper.Caller<T>(null, "JUST.Transformer`1", functionName, 
                            new object[] { array, currentArrayElement, GlobalContext }, 
                            false, GlobalContext);
                    else if (functionName == "customfunction")
                        output = CallCustomFunction(parameters);
                    else if (GlobalContext?.IsRegisteredCustomFunction(functionName) ?? false)
                    {
                        var methodInfo = GlobalContext.GetCustomMethod(functionName);
                        output = ReflectionHelper.InvokeCustomMethod<T>(methodInfo, parameters, true, GlobalContext);
                    }
                    else if (GlobalContext.IsRegisteredCustomFunction(functionName))
                    {
                        var methodInfo = GlobalContext.GetCustomMethod(functionName);
                        output = ReflectionHelper.InvokeCustomMethod<T>(methodInfo, parameters, true, GlobalContext);
                    }
                    else if (Regex.IsMatch(functionName, ReflectionHelper.EXTERNAL_ASSEMBLY_REGEX))
                    {
                        output = ReflectionHelper.CallExternalAssembly<T>(functionName, parameters, GlobalContext);
                    }
                    else if (new[] { "xconcat", "xadd",
                        "mathequals", "mathgreaterthan", "mathlessthan", "mathgreaterthanorequalto", "mathlessthanorequalto",
                        "stringcontains", "stringequals"}.Contains(functionName))
                    {
                        object[] oParams = new object[1];
                        oParams[0] = parameters;
                        output = ReflectionHelper.Caller<T>(null, "JUST.Transformer`1", functionName, oParams, true, GlobalContext);
                    }
                    else if (functionName == "applyover")
                    {
                        var contextInput = GlobalContext.Input;
                        var input = JToken.Parse(Transform(parameters[0].ToString(), contextInput.ToString()));
                        GlobalContext.Input = input;
                        output = ParseFunction(parameters[1].ToString().Trim('\''), array, currentArrayElement);
                        GlobalContext.Input = contextInput;
                    }
                    else
                    {
                        var input = ((JUSTContext)parameters.Last()).Input;
                        if (currentArrayElement != null && functionName != "valueof")
                        {
                            ((JUSTContext)parameters.Last()).Input = currentArrayElement;
                        }
                        output = ReflectionHelper.Caller<T>(null, "JUST.Transformer`1", functionName, parameters, true, GlobalContext);
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

        private object ParseArgument(JArray array, JToken currentArrayElement, string argument)
        {
            string trimmedArgument = argument;

            if (argument.Contains("#"))
                trimmedArgument = argument.Trim();

            if (trimmedArgument.StartsWith("#"))
            {
                return ParseFunction(trimmedArgument, array, currentArrayElement);
            }
            else
                return trimmedArgument;
        }

        private object CallCustomFunction(object[] parameters)
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

            return ReflectionHelper.Caller<T>(null, className, functionName, customParameters, true, GlobalContext);

        }
        #endregion

        #region Split
        public static IEnumerable<string> SplitJson(string input, string arrayPath, JUSTContext context)
        {
            JObject inputJObject = JsonConvert.DeserializeObject<JObject>(input);

            List<JObject> jObjects = SplitJson(inputJObject, arrayPath, context).ToList();

            List<string> output = null;

            foreach (JObject jObject in jObjects)
            {
                if (output == null)
                    output = new List<string>();

                output.Add(JsonConvert.SerializeObject(jObject));
            }

            return output;
        }

        public static IEnumerable<JObject> SplitJson(JObject input, string arrayPath, JUSTContext context)
        {
            List<JObject> jsonObjects = null;

            JToken tokenArr = GetSelectableToken(input, context).Select(arrayPath);

            string pathToReplace = tokenArr.Path;

            if (tokenArr != null && tokenArr is JArray)
            {
                JArray array = tokenArr as JArray;

                foreach (JToken tokenInd in array)
                {

                    string path = tokenInd.Path;

                    JToken clonedToken = input.DeepClone();

                    var selectable = GetSelectableToken(clonedToken, context);
                    JToken foundToken = selectable.Select(selectable.RootReference + path);
                    JToken tokenToReplce = selectable.Select(selectable.RootReference + pathToReplace);

                    tokenToReplce.Replace(foundToken);

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

        private static bool IsStrictMode(JUSTContext context)
        {
            return context.EvaluationMode == EvaluationMode.Strict;
        }

        private static bool IsFallbackToDefault(JUSTContext context)
        {
            return context.EvaluationMode == EvaluationMode.FallbackToDefault;
        }

        private static T GetSelectableToken(JToken token, JUSTContext context)
        {
            return context.Resolve<T>(token);
        }
    }
}

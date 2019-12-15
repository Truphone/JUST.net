﻿using System.Globalization;
using NUnit.Framework;

namespace JUST.UnitTests
{
    [TestFixture, Category("ReadMe")]
    public class ReadmeTests
    {
        [Test]
        public void ValueOf()
        {
            const string transformer = "{\"root\": {\"menu1\": \"#valueof($.menu.popup.menuitem[?(@.value=='New')].onclick)\", \"menu2\": \"#valueof($.menu.popup.menuitem[?(@.value=='Open')].onclick)\"}}";

            var result = JsonTransformer.Transform(transformer, ExampleInputs.Menu);

            Assert.AreEqual("{\"root\":{\"menu1\":{\"action\":\"CreateNewDoc()\"},\"menu2\":\"OpenDoc()\"}}", result);
        }

        [Test]
        public void IfCondition()
        {
            const string input = "{ \"menu\": { \"id\" : \"github\", \"repository\" : \"JUST\" } }";
            const string transformer = "{ \"ifconditiontesttrue\": \"#ifcondition(#valueof($.menu.id),github,#valueof($.menu.repository),fail)\", \"ifconditiontestfalse\": \"#ifcondition(#valueof($.menu.id),xml,#valueof($.menu.repository),fail)\" }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"ifconditiontesttrue\":\"JUST\",\"ifconditiontestfalse\":\"fail\"}", result);
        }

        [Test]
        public void StringMathFunctions()
        {
            const string input = "{ \"stringref\": \"thisisandveryunuasualandlongstring\", \"numbers\": [ 1, 2, 3, 4, 5 ] }";
            const string transformer = "{ \"stringresult\": { \"lastindexofand\": \"#lastindexof(#valueof($.stringref),and)\", \"firstindexofand\": \"#firstindexof(#valueof($.stringref),and)\", \"substring\": \"#substring(#valueof($.stringref),9,11)\", \"concat\": \"#concat(#valueof($.menu.id.file),#valueof($.menu.value.Window))\", \"length_string\": \"#length(#valueof($.stringref))\", \"length_array\": \"#length(#valueof($.numbers))\" }, \"mathresult\": { \"add\": \"#add(#valueof($.numbers[0]),3)\", \"subtract\": \"#subtract(#valueof($.numbers[4]),#valueof($.numbers[0]))\", \"multiply\": \"#multiply(2,#valueof($.numbers[2]))\", \"divide\": \"#divide(9,3)\", \"round\": \"#round(10.005,2)\" } }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"stringresult\":{\"lastindexofand\":21,\"firstindexofand\":6,\"substring\":\"veryunuasua\",\"concat\":\"\",\"length_string\":34,\"length_array\":5},\"mathresult\":{\"add\":4,\"subtract\":4,\"multiply\":6,\"divide\":3,\"round\":10.01}}", result);
        }

        [Test]
        public void Operators()
        {
            const string input = "{ \"d\": [ \"one\", \"two\", \"three\" ], \"numbers\": [ 1, 2, 3, 4, 5 ] }";
            const string transformer = "{ \"mathresult\": { \"third_element_equals_3\": \"#ifcondition(#mathequals(#valueof($.numbers[2]),3),true,yes,no)\", \"third_element_greaterthan_2\": \"#ifcondition(#mathgreaterthan(#valueof($.numbers[2]),2),true,yes,no)\", \"third_element_lessthan_4\": \"#ifcondition(#mathlessthan(#valueof($.numbers[2]),4),true,yes,no)\", \"third_element_greaterthanorequals_4\": \"#ifcondition(#mathgreaterthanorequalto(#valueof($.numbers[2]),4),true,yes,no)\", \"third_element_lessthanoreuals_2\": \"#ifcondition(#mathlessthanorequalto(#valueof($.numbers[2]),2),true,yes,no)\", \"one_stringequals\": \"#ifcondition(#stringequals(#valueof($.d[0]),one),true,yes,no)\", \"one_stringcontains\": \"#ifcondition(#stringcontains(#valueof($.d[0]),n),true,yes,no)\" } }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"mathresult\":{\"third_element_equals_3\":\"yes\",\"third_element_greaterthan_2\":\"yes\",\"third_element_lessthan_4\":\"yes\",\"third_element_greaterthanorequals_4\":\"no\",\"third_element_lessthanoreuals_2\":\"no\",\"one_stringequals\":\"yes\",\"one_stringcontains\":\"yes\"}}", result);
        }

        [Test]
        public void AggregateFunctions()
        {
            const string input = "{ \"d\": [ \"one\", \"two\", \"three\" ], \"numbers\": [ 1, 2, 3, 4, 5 ] }";
            const string transformer = "{ \"conacted\": \"#concatall(#valueof($.d))\", \"sum\": \"#sum(#valueof($.numbers))\", \"avg\": \"#average(#valueof($.numbers))\", \"min\": \"#min(#valueof($.numbers))\", \"max\": \"#max(#valueof($.numbers))\" }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"conacted\":\"onetwothree\",\"sum\":15,\"avg\":3,\"min\":1,\"max\":5}", result);
        }

        [Test]
        public void AggregateFunctionsMultidimensionalArrays()
        {
            const string input = "{ \"x\": [ { \"v\": { \"a\": \"a1,a2,a3\", \"b\": \"1\", \"c\": \"10\" } }, { \"v\": { \"a\": \"b1,b2\", \"b\": \"2\", \"c\": \"20\" } }, { \"v\": { \"a\": \"c1,c2,c3\", \"b\": \"3\", \"c\": \"30\" } } ] }";
            const string transformer = "{ \"arrayconacted\": \"#concatallatpath(#valueof($.x),$.v.a)\", \"arraysum\": \"#sumatpath(#valueof($.x),$.v.c)\", \"arrayavg\": \"#averageatpath(#valueof($.x),$.v.c)\", \"arraymin\": \"#minatpath(#valueof($.x),$.v.b)\", \"arraymax\": \"#maxatpath(#valueof($.x),$.v.b)\" }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"arrayconacted\":\"a1,a2,a3b1,b2c1,c2,c3\",\"arraysum\":60,\"arrayavg\":20,\"arraymin\":1,\"arraymax\":3}", result);
        }

        [Test]
        public void TypeConvertions()
        {
            const string input = "{ \"booleans\": { \"affirmative_string\": \"true\", \"negative_string\": \"false\", \"affirmative_int\": 123, \"negative_int\": 0, }, \"strings\": { \"integer\": 123, \"decimal\": 12.34, \"affirmative_boolean\": true, \"negative_boolean\": false }, \"integers\": { \"string\": \"123\", \"decimal\": 1.23, \"affirmative_boolean\": true, \"negative_boolean\": false }, \"decimals\": { \"integer\": 123, \"string\": \"1.23\" }}";
            const string transformer = "{ \"booleans\": { \"affirmative_string\": \"#toboolean(#valueof($.booleans.affirmative_string))\", \"negative_string\":\"#toboolean(#valueof($.booleans.negative_string))\", \"affirmative_int\":\"#toboolean(#valueof($.booleans.affirmative_int))\", \"negative_int\": \"#toboolean(#valueof($.booleans.negative_int))\", }, \"strings\": { \"integer\": \"#tostring(#valueof($.strings.integer))\", \"decimal\":\"#tostring(#valueof($.strings.decimal))\", \"affirmative_boolean\": \"#tostring(#valueof($.strings.affirmative_boolean))\", \"negative_boolean\": \"#tostring(#valueof($.strings.negative_boolean))\" }, \"integers\": { \"string\":\"#tointeger(#valueof($.integers.string))\", \"decimal\": \"#tointeger(#valueof($.integers.decimal))\", \"affirmative_boolean\":\"#tointeger(#valueof($.integers.affirmative_boolean))\", \"negative_boolean\":\"#tointeger(#valueof($.integers.negative_boolean))\" }, \"decimals\": { \"integer\":\"#todecimal(#valueof($.decimals.integer))\", \"string\": \"#todecimal(#valueof($.decimals.string))\" }}";

            var result = JsonTransformer.Transform(transformer, input);

            var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            Assert.AreEqual($"{{\"booleans\":{{\"affirmative_string\":true,\"negative_string\":false,\"affirmative_int\":true,\"negative_int\":false}},\"strings\":{{\"integer\":\"123\",\"decimal\":\"12{decimalSeparator}34\",\"affirmative_boolean\":\"True\",\"negative_boolean\":\"False\"}},\"integers\":{{\"string\":123,\"decimal\":1,\"affirmative_boolean\":1,\"negative_boolean\":0}},\"decimals\":{{\"integer\":123.0,\"string\":1.23}}}}", result);
        }

        [Test]
        public void BulkFunctions()
        {
            const string transformer = "{ \"#\": [ \"#copy($)\", \"#delete($.tree.branch.bird)\", \"#replace($.tree.branch.extra,#valueof($.tree.ladder))\" ], \"othervalue\" : \"othervalue\" }";

            var result = JsonTransformer.Transform(transformer, ExampleInputs.Tree);

            Assert.AreEqual("{\"othervalue\":\"othervalue\",\"tree\":{\"branch\":{\"leaf\":\"green\",\"flower\":\"red\",\"extra\":{\"wood\":\"treehouse\"}},\"ladder\":{\"wood\":\"treehouse\"}}}", result);
        }

        [Test]
        public void ArrayLooping()
        {
            const string input = "{ \"tree\": { \"branch\": { \"leaf\": \"green\", \"flower\": \"red\", \"bird\": \"crow\", \"extra\": { \"twig\": \"birdnest\" } }, \"ladder\": { \"wood\": \"treehouse\" } }, \"numbers\": [ 1, 2, 3, 4 ], \"arrayobjects\": [ {\"country\": {\"name\": \"norway\",\"language\": \"norsk\"}}, { \"country\": { \"name\": \"UK\", \"language\": \"english\" } }, { \"country\": { \"name\": \"Sweden\", \"language\": \"swedish\" } }], \"animals\": { \"cat\": { \"legs\": 4, \"sound\": \"meow\" }, \"dog\": { \"legs\": 4, \"sound\": \"woof\" }, \"human\": { \"number_of_legs\": 2, \"sound\": \"@!#$?\" } }, \"spell_numbers\": { \"3\": \"three\", \"2\": \"two\", \"1\": \"one\" } }";
            const string transformer = "{ \"iteration\": { \"#loop($.numbers)\": { \"CurrentValue\": \"#currentvalue()\", \"CurrentIndex\": \"#currentindex()\", \"IsLast\": \"#ifcondition(#currentindex(),#lastindex(),yes,no)\", \"LastValue\": \"#lastvalue()\" } }, \"iteration2\": { \"#loop($.arrayobjects)\": { \"CurrentValue\": \"#currentvalueatpath($.country.name)\", \"CurrentIndex\": \"#currentindex()\", \"IsLast\": \"#ifcondition(#currentindex(),#lastindex(),yes,no)\", \"LastValue\": \"#lastvalueatpath($.country.language)\" } }, \"sounds\": { \"#loop($.animals)\": { \"#eval(#currentproperty())\": \"#currentvalueatpath($..sound)\" } }, \"number_index\": { \"#loop($.spell_numbers)\": { \"#eval(#currentindex())\": \"#currentvalueatpath(#concat($.,#currentproperty()))\" } }, \"othervalue\": \"othervalue\" }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"iteration\":[{\"CurrentValue\":1,\"CurrentIndex\":0,\"IsLast\":\"no\",\"LastValue\":4},{\"CurrentValue\":2,\"CurrentIndex\":1,\"IsLast\":\"no\",\"LastValue\":4},{\"CurrentValue\":3,\"CurrentIndex\":2,\"IsLast\":\"no\",\"LastValue\":4},{\"CurrentValue\":4,\"CurrentIndex\":3,\"IsLast\":\"yes\",\"LastValue\":4}],\"iteration2\":[{\"CurrentValue\":\"norway\",\"CurrentIndex\":0,\"IsLast\":\"no\",\"LastValue\":\"swedish\"},{\"CurrentValue\":\"UK\",\"CurrentIndex\":1,\"IsLast\":\"no\",\"LastValue\":\"swedish\"},{\"CurrentValue\":\"Sweden\",\"CurrentIndex\":2,\"IsLast\":\"yes\",\"LastValue\":\"swedish\"}],\"sounds\":{\"cat\":\"meow\",\"dog\":\"woof\",\"human\":\"@!#$?\"},\"number_index\":{\"0\":\"three\",\"1\":\"two\",\"2\":\"one\"},\"othervalue\":\"othervalue\"}", result);
        }

        [Test]
        public void NestedArrayLooping()
        {
            const string input = "{ \"NestedLoop\": { \"Organization\": { \"Employee\": [ { \"Name\": \"E2\", \"Details\": [ { \"Country\": \"Iceland\", \"Age\": \"30\", \"Name\": \"Sven\", \"Language\": \"Icelandic\" } ] }, { \"Name\": \"E1\", \"Details\": [ { \"Country\": \"Denmark\", \"Age\": \"30\", \"Name\": \"Svein\", \"Language\": \"Danish\" } ] } ] } } }";
            const string transformer = "{ \"hello\": { \"#loop($.NestedLoop.Organization.Employee)\": { \"CurrentName\": \"#currentvalueatpath($.Name)\", \"Details\": { \"#loopwithincontext($.Details)\": { \"CurrentCountry\": \"#currentvalueatpath($.Country)\" } } } } }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"hello\":[{\"CurrentName\":\"E2\",\"Details\":[{\"CurrentCountry\":\"Iceland\"}]},{\"CurrentName\":\"E1\",\"Details\":[{\"CurrentCountry\":\"Denmark\"}]}]}", result);
        }

        [Test]
        public void ArrayGrouping()
        {
            const string input = "{ \"Forest\": [ { \"type\": \"Mammal\", \"qty\": 1, \"name\": \"Hippo\" }, { \"type\": \"Bird\", \"qty\": 2, \"name\": \"Sparrow\" }, { \"type\": \"Amphibian\", \"qty\": 300, \"name\": \"Lizard\" }, { \"type\": \"Bird\", \"qty\": 3, \"name\": \"Parrot\" }, { \"type\": \"Mammal\", \"qty\": 1, \"name\": \"Elephant\" }, { \"type\": \"Mammal\", \"qty\": 10, \"name\": \"Dog\" } ] }";
            const string transformer = "{ \"Result\": \"#grouparrayby($.Forest,type,all)\" }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"Result\":[{\"type\":\"Mammal\",\"all\":[{\"qty\":1,\"name\":\"Hippo\"},{\"qty\":1,\"name\":\"Elephant\"},{\"qty\":10,\"name\":\"Dog\"}]},{\"type\":\"Bird\",\"all\":[{\"qty\":2,\"name\":\"Sparrow\"},{\"qty\":3,\"name\":\"Parrot\"}]},{\"type\":\"Amphibian\",\"all\":[{\"qty\":300,\"name\":\"Lizard\"}]}]}", result);
        }

        [Test]
        public void ComplexNestedFunctions()
        {
            const string input = "{ \"Name\": \"Kari\", \"Surname\": \"Nordmann\", \"MiddleName\": \"Inger\", \"ContactInformation\": \"Karl johans gate:Oslo:88880000\" , \"PersonalInformation\": \"45:Married:Norwegian\"}";
            const string transformer = "{ \"FullName\": \"#concat(#concat(#concat(#valueof($.Name), ),#concat(#valueof($.MiddleName), )),#valueof($.Surname))\",	\"Contact Information\": { \"Street Name\": \"#substring(#valueof($.ContactInformation),0,#firstindexof(#valueof($.ContactInformation),:))\", \"City\": \"#substring(#valueof($.ContactInformation),#add(#firstindexof(#valueof($.ContactInformation),:),1),#subtract(#subtract(#lastindexof(#valueof($.ContactInformation),:),#firstindexof(#valueof($.ContactInformation),:)),1))\", \"PhoneNumber\": \"#substring(#valueof($.ContactInformation),#add(#lastindexof(#valueof($.ContactInformation),:),1),#subtract(#lastindexof(#valueof($.ContactInformation),),#lastindexof(#valueof($.ContactInformation),:)))\" }, \"Personal Information\": { \"Age\": \"#substring(#valueof($.PersonalInformation),0,#firstindexof(#valueof($.PersonalInformation),:))\", \"Civil Status\": \"#substring(#valueof($.PersonalInformation),#add(#firstindexof(#valueof($.PersonalInformation),:),1),#subtract(#subtract(#lastindexof(#valueof($.PersonalInformation),:),#firstindexof(#valueof($.PersonalInformation),:)),1))\", \"Ethnicity\": \"#substring(#valueof($.PersonalInformation),#add(#lastindexof(#valueof($.PersonalInformation),:),1),#subtract(#lastindexof(#valueof($.PersonalInformation),),#lastindexof(#valueof($.PersonalInformation),:)))\" }}";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"FullName\":\"Kari Inger Nordmann\",\"Contact Information\":{\"Street Name\":\"Karl johans gate\",\"City\":\"Oslo\",\"PhoneNumber\":\"88880000\"},\"Personal Information\":{\"Age\":\"45\",\"Civil Status\":\"Married\",\"Ethnicity\":\"Norwegian\"}}", result);
        }

        [Test]
        public void MultipleArgumentConstantFunctions()
        {
            const string input = "{ \"Name\": \"Kari\", \"Surname\": \"Nordmann\", \"MiddleName\": \"Inger\", \"ContactInformation\": \"Karl johans gate:Oslo:88880000\" , \"PersonalInformation\": \"45:Married:Norwegian\",\"AgeOfMother\": 67,\"AgeOfFather\": 70 }";
            const string transformer = "{ \"FullName\": \"#xconcat(#valueof($.Name),#constant_comma(),#valueof($.MiddleName),#constant_comma(),#valueof($.Surname))\", \"AgeOfParents\": \"#xadd(#valueof($.AgeOfMother),#valueof($.AgeOfFather))\" }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"FullName\":\"Kari,Inger,Nordmann\",\"AgeOfParents\":137}", result);
        }

        [Test]
        public void CheckForExistance()
        {
            const string transformer = "{ \"BuyDateString\": \"#ifcondition(#exists($.BuyDate),true,#concat(Buy Date : ,#valueof($.BuyDate)),NotExists)\", \"BuyDateString2\": \"#ifcondition(#existsandnotempty($.BuyDate),true,#concat(Buy Date : ,#valueof($.BuyDate)),EmptyOrNotExists)\", \"ExpireDateString\": \"#ifcondition(#exists($.ExpireDate),true,#concat(Expire Date : ,#valueof($.ExpireDate)),NotExists)\", \"ExpireDateString2\": \"#ifcondition(#existsandnotempty($.ExpireDate),true,#concat(Expire Date : ,#valueof($.ExpireDate)),EmptyOrNotExists)\", \"SellDateString\": \"#ifcondition(#exists($.SellDate),true,#concat(Sell Date : ,#valueof($.SellDate)),NotExists)\", \"SellDateString2\": \"#ifcondition(#existsandnotempty($.SellDate),true,#concat(Sell Date : ,#valueof($.SellDate)),EmptyOrNotExists)\" }";
            const string input = "{ \"BuyDate\": \"2017-04-10T11:36:39+03:00\", \"ExpireDate\": \"\" }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"BuyDateString\":\"Buy Date : 2017-04-10T11:36:39+03:00\",\"BuyDateString2\":\"Buy Date : 2017-04-10T11:36:39+03:00\",\"ExpireDateString\":\"Expire Date : \",\"ExpireDateString2\":\"EmptyOrNotExists\",\"SellDateString\":\"NotExists\",\"SellDateString2\":\"EmptyOrNotExists\"}", result);
        }

        [Test]
        public void ConditionalTransformation()
        {
            const string input = "{ \"Tree\": { \"Branch\": \"leaf\", \"Flower\": \"Rose\" } }";
            string transformer = "{ \"Result\": { \"Header\": \"JsonTransform\", \"#ifgroup(#exists($.Tree.Branch))\": { \"State\": { \"Value1\": \"#valueof($.Tree.Branch)\", \"Value2\": \"#valueof($.Tree.Flower)\" } } } }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"Result\":{\"Header\":\"JsonTransform\",\"State\":{\"Value1\":\"leaf\",\"Value2\":\"Rose\"}}}", result);

            transformer = "{ \"Result\": { \"Header\": \"JsonTransform\", \"#ifgroup(#exists($.Tree.Root))\": { \"State\": { \"Value1\": \"#valueof($.Tree.Branch)\", \"Value2\": \"#valueof($.Tree.Flower)\" } } } }";

            result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"Result\":{\"Header\":\"JsonTransform\"}}", result);
        }

        [Test]
        public void DynamicProperties()
        {
            const string input = "{ \"Tree\": { \"Branch\": \"leaf\", \"Flower\": \"Rose\" } }";
            const string transformer = "{ \"Result\": { \"#eval(#valueof($.Tree.Flower))\": \"x\" } }";

            var result = JsonTransformer.Transform(transformer, input);

            Assert.AreEqual("{\"Result\":{\"Rose\":\"x\"}}", result);
        }

        [Test]
        public void ApplyOver()
        {
            var input = "{\"d\": [ \"one\", \"two\", \"three\" ], \"values\": [ \"z\", \"c\", \"n\" ]}";
            var transformer = "{ \"result\": \"#applyover({ 'condition': { '#loop($.values)': { 'test': '#ifcondition(#stringcontains(#valueof($.d[0]),#currentvalue()),true,yes,no)' } } }, '#exists($.condition[?(@.test=='yes')])')\" }";
            var context = new JUSTContext
            {
                EvaluationMode = EvaluationMode.Strict
            };
            var result = JsonTransformer.Transform(transformer, input, context);

            Assert.AreEqual("{\"result\":true}", result);
        }

        [Test]
        public void Issue77()
        {
            var input = "[{\"id\": 1, \"cnt\": 100, \"rowNum\": 1, \"col\": 1},{\"id\": 2, \"cnt\": 89, \"rowNum\": 1, \"col\": 1 }]";
            var transformer = "[{ \"#loop($)\": { \"key\": \"#currentvalueatpath($.id)\", \"quantity\": \"#currentvalueatpath($.cnt)\" } }]";
            var context = new JUSTContext
            {
                EvaluationMode = EvaluationMode.Strict
            };
            var result = JsonTransformer.Transform(transformer, input, context);
            Assert.AreEqual("[{\"key\":1,\"quantity\":100},{\"key\":2,\"quantity\":89}]", result);
        }

        [Test]
        public void Issue77_2()
        {
            var input = "[{\"id\": 1, \"cnt\": 100, \"rowNum\": 1, \"col\": 1},{\"id\": 2, \"cnt\": 89, \"rowNum\": 1, \"col\": 1 }]";
            var transformer = "{ \"#loop($)\": { \"key\": \"#currentvalueatpath($.id)\", \"quantity\": \"#currentvalueatpath($.cnt)\" } }";
            var context = new JUSTContext
            {
                EvaluationMode = EvaluationMode.Strict
            };
            var result = JsonTransformer.Transform(transformer, input, context);
            Assert.AreEqual("[{\"key\":1,\"quantity\":100},{\"key\":2,\"quantity\":89}]", result);
        }

        [Test]
        public void Issue77_3()
        {
            var input = "[{\"id\": 1, \"cnt\": 100, \"rowNum\": 1, \"col\": 1},{\"id\": 2, \"cnt\": 89, \"rowNum\": 1, \"col\": 1 }]";
            var transformer = "[{ \"a\": \"#valueof($)\" }, { \"#loop($)\": { \"key\": \"#currentvalueatpath($.id)\", \"quantity\": \"#currentvalueatpath($.cnt)\" } }]";
            var context = new JUSTContext
            {
                EvaluationMode = EvaluationMode.Strict
            };
            var result = JsonTransformer.Transform(transformer, input, context);
            Assert.AreEqual("[{\"a\":[{\"id\":1,\"cnt\":100,\"rowNum\":1,\"col\":1},{\"id\":2,\"cnt\":89,\"rowNum\":1,\"col\":1}]},{\"key\":1,\"quantity\":100},{\"key\":2,\"quantity\":89}]", result);
        }

        [Test]
        public void Issue77_4()
        {
            var input = "[{\"id\": 1, \"cnt\": 100, \"rowNum\": 1, \"col\": 1},{\"id\": 2, \"cnt\": 89, \"rowNum\": 1, \"col\": 1 }]";
            var transformer = "[[{ \"#loop($)\": { \"key\": \"#currentvalueatpath($.id)\", \"quantity\": \"#currentvalueatpath($.cnt)\" } }], { \"a\": \"#valueof($)\" }]";
            var context = new JUSTContext
            {
                EvaluationMode = EvaluationMode.Strict
            };
            var result = JsonTransformer.Transform(transformer, input, context);
            Assert.AreEqual("[[{\"key\":1,\"quantity\":100},{\"key\":2,\"quantity\":89}],{\"a\":[{\"id\":1,\"cnt\":100,\"rowNum\":1,\"col\":1},{\"id\":2,\"cnt\":89,\"rowNum\":1,\"col\":1}]}]", result);
        }
    }
}

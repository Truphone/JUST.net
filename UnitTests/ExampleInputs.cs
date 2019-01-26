﻿namespace JUST.UnitTests
{
    public class ExampleInputs
    {
        internal const string Menu = "{ \"menu\": { \"id\": { \"file\": \"csv\" }, \"value\": { \"Window\": \"popup\" }, \"popup\": { \"menuitem\": [ { \"value\": \"New\", \"onclick\": { \"action\": \"CreateNewDoc()\" } }, { \"value\": \"Open\", \"onclick\": \"OpenDoc()\" }, { \"value\": \"Close\", \"onclick\": \"CloseDoc()\" } ] } } }";
        internal const string ArrayX = "{ \"x\": [ { \"v\": { \"a\": \"a1,a2,a3\", \"b\": \"1\", \"c\": \"10\" } }, { \"v\": { \"a\": \"b1,b2\", \"b\": \"2\", \"c\": \"20\" } }, { \"v\": { \"a\": \"c1,c2,c3\", \"b\": \"3\", \"c\": \"30\" } } ]}";
    }
}

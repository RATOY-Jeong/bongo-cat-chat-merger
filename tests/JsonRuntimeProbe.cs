using System;using System.Reflection;using System.Collections.Generic;using BongoChatBridge;
public static class JsonRuntimeProbe
{
 public static int Main(string[] args){try{var json=Assembly.LoadFrom(args[0]);JsonRuntimeCompatibility.Verify(json);var type=json.GetType("Newtonsoft.Json.Linq.JObject",true);var obj=type.GetMethod("Parse",new[]{typeof(string)}).Invoke(null,new object[]{"{\"room\":\"test\",\"name\":\"한글 日本語\",\"members\":[1,2]}"});if(!obj.ToString().Contains("한글 日本語"))throw new Exception("Unicode roundtrip failed");Console.WriteLine("PASS JObject/JArray Unicode parsing and interface contracts: "+json.GetName().FullName);return 0;}catch(Exception ex){while(ex.InnerException!=null)ex=ex.InnerException;Console.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message);return 1;}}
}
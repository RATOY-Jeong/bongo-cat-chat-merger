using System;using System.ComponentModel;using System.Reflection;
namespace BongoChatBridge
{
 public static class JsonRuntimeCompatibility
 {
  public static void Verify(Assembly json)
  {
   if(json==null||json.GetName().Name!="Newtonsoft.Json")throw new InvalidOperationException("JSON runtime unavailable.");
   // A stripped game Json.NET can load for simple serialization but fail only when Discord touches JObject.
   // Check before reporting ready so a broken lobby callback cannot masquerade as a healthy connection.
   var type=json.GetType("Newtonsoft.Json.Linq.JObject",true);type.GetInterfaceMap(typeof(ICustomTypeDescriptor));type.GetInterfaceMap(typeof(INotifyPropertyChanged));
   var parse=type.GetMethod("Parse",new[]{typeof(string)});if(parse==null||parse.Invoke(null,new object[]{"{\"compatibility\":true}"})==null)throw new InvalidOperationException("JSON object parsing unavailable.");
  }
 }
}
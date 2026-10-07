using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using BookNest.Api.Controllers;

var assembly=typeof(AuthController).Assembly;
var nullability=new NullabilityInfoContext();
string Name(string text)=>JsonNamingPolicy.CamelCase.ConvertName(text);
string TypeName(Type t) => Nullable.GetUnderlyingType(t) is {} underlying ? TypeName(underlying)+"?" : t.IsArray?TypeName(t.GetElementType()!)+"[]":t.Name;
string Rules(IEnumerable<Attribute> attrs)=>string.Join("; ",attrs.Select(a=>a switch {
    RequiredAttribute=>"Required",MaxLengthAttribute x=>$"Max length {x.Length}",MinLengthAttribute x=>$"Min length {x.Length}",
    StringLengthAttribute x=>$"Length {x.MinimumLength}–{x.MaximumLength}",RangeAttribute x=>$"Range {x.Minimum}–{x.Maximum}",
    RegularExpressionAttribute x=>$"Pattern: {x.Pattern}",EmailAddressAttribute=>"Valid email format",_=>""}).Where(s=>s!=""));
object Parameter(ParameterInfo p)=>new{name=Name(p.Name!),type=TypeName(p.ParameterType),nullable=nullability.Create(p).ReadState==NullabilityState.Nullable || Nullable.GetUnderlyingType(p.ParameterType)!=null,
    required=p.GetCustomAttribute<RequiredAttribute>()!=null,hasDefault=p.HasDefaultValue,defaultValue=p.HasDefaultValue?p.DefaultValue:null,validation=Rules(p.GetCustomAttributes().ToArray())};
var endpoints=new List<object>();
foreach(var c in assembly.GetTypes().Where(t=>t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract).OrderBy(t=>t.Name))
{
    var prefix=c.GetCustomAttribute<RouteAttribute>()!.Template;
    foreach(var m in c.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
    {
        var http=m.GetCustomAttribute<HttpMethodAttribute>();if(http==null)continue;
        var route="/"+prefix.Trim('/')+(string.IsNullOrEmpty(http.Template)?"":"/"+http.Template.Trim('/'));
        var auth=m.GetCustomAttribute<AuthorizeAttribute>()??c.GetCustomAttribute<AuthorizeAttribute>();
        var anonymous=m.GetCustomAttribute<AllowAnonymousAttribute>()!=null;
        endpoints.Add(new{controller=c.Name,action=m.Name,method=http.HttpMethods.Single(),route,auth=anonymous || auth==null?"Public":auth.Policy=="Admin"?"Admin bearer":"User bearer",
            parameters=m.GetParameters().Where(p=>p.ParameterType!=typeof(CancellationToken)).Select(Parameter).ToArray()});
    }
}
var schemas=assembly.GetTypes().Where(t=>t.Namespace=="BookNest.Api.Controllers" && t.IsClass && !t.IsSubclassOf(typeof(ControllerBase)) && t.Name.EndsWith("Request") || t==typeof(CheckoutLine) || t==typeof(PaymentCallback) || t==typeof(ReviewModeration))
    .OrderBy(t=>t.Name).Select(t=>new{name=t.Name,kind="Request",fields=t.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First().GetParameters().Select(Parameter).ToArray()}).ToArray();
var entities=assembly.GetTypes().Where(t=>t.Namespace=="BookNest.Api.Domain").OrderBy(t=>t.Name).Select(t=>new{name=t.Name,kind="Response",fields=t.GetProperties().Where(p=>p.GetCustomAttribute<JsonIgnoreAttribute>()==null).Select(p=>new{
    name=Name(p.Name),type=TypeName(p.PropertyType),nullable=nullability.Create(p).ReadState==NullabilityState.Nullable || Nullable.GetUnderlyingType(p.PropertyType)!=null}).ToArray()}).ToArray();
File.WriteAllText(args[0],JsonSerializer.Serialize(new{endpoints,schemas,entities},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Exported {endpoints.Count} controller operations, {schemas.Length} request models and {entities.Length} entity response models.");

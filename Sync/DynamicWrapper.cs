using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Linq.Expressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// From http://houseofbilz.com/archives/2009/09/18/introducing-dynamicwrapper/
//  Modified to suit my needs
namespace Sync
{
    // From http://houseofbilz.com/archives/2009/09/18/introducing-dynamicwrapper/
    // Modified to suit my needs
    /// <summary>
    /// This is the target of all function calls.
    /// </summary>
    public interface IModelHolder
    {
        object MethodCall(string methodName, object[] parameters);
    }
    /// <summary>
    /// This becomes the base class for our generated fake model
    /// </summary>
    public class ModelBase
    {
        public IModelHolder ModelHolder { get; set; }
    }

    public class DynamicWrapper
    {
        private static readonly Dictionary<string, Type> _wrapperDictionary = new Dictionary<string, Type>();

        private static Type GetWrapper(Type interfaceType)
        {
            string key = interfaceType.FullName;
            if (!_wrapperDictionary.ContainsKey(key))
            {
                _wrapperDictionary[key] = GenerateWrapperType(interfaceType);
            }

            return _wrapperDictionary[key];
        }

        private static Type GenerateWrapperType(Type interfaceType)
        {
            var assembly = Thread.GetDomain().DefineDynamicAssembly(new AssemblyName(interfaceType.Assembly.GetName().Name + "|dynamic"), AssemblyBuilderAccess.Run);

            var moduleBuilder = assembly.DefineDynamicModule("DynamicWrapperModule", false);

            var wrapperName = string.Format("{0}_Wrapper", interfaceType.Name);

            TypeBuilder wrapperBuilder = moduleBuilder.DefineType(
                wrapperName,
                TypeAttributes.NotPublic | TypeAttributes.Sealed,
                typeof(ModelBase),
                new[] { interfaceType });

            var wrapperMethod = new WrapperMethodBuilder(wrapperBuilder);

            foreach (MethodInfo method in interfaceType.AllMethods())
            {
                /*
                if (method.ReturnType != typeof(void))
                {
                    Console.WriteLine("Warning, not faking " + method.Name + " because it has a return value.");
                    continue;
                }
                */
                wrapperMethod.Generate(method);
            }

            return wrapperBuilder.CreateType();
        }

        public static T CreateClientModel<T>(IModelHolder modelHolder) where T : class
        {
            var dynamicType = GetWrapper(typeof(T));
            var dynamicWrapper = (ModelBase)Activator.CreateInstance(dynamicType);

            dynamicWrapper.ModelHolder = modelHolder;

            return dynamicWrapper as T;
        }

        public class MethodInfoSerializable
        {
            public string name;
            public string returnType;
            public string[] types;
        }
        public static string MethodSerialize(MethodInfo method)
        {
            return JsonConvert.SerializeObject(new MethodInfoSerializable()
            {
                name = method.Name,
                returnType = method.ReturnType.Name,
                types = method.GetParameters().Select(x => x.ParameterType.Name).ToArray()
            });
        }
    }

    class WrapperMethodBuilder
    {
        private readonly TypeBuilder _wrapperBuilder;

        public WrapperMethodBuilder(TypeBuilder proxyBuilder)
        {
            _wrapperBuilder = proxyBuilder;
        }

        public void Generate(MethodInfo method)
        {
            if (method.IsGenericMethod)
                method = method.GetGenericMethodDefinition();


            var parameters = method.GetParameters();
            var parameterTypes = parameters.Select(parameter => parameter.ParameterType).ToArray();

            var methodBuilder = _wrapperBuilder.DefineMethod(
                method.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                method.ReturnType,
                parameterTypes);

            if (method.IsGenericMethod)
            {
                methodBuilder.DefineGenericParameters(
                    method.GetGenericArguments().Select(arg => arg.Name).ToArray());
            }

            ILGenerator ilGenerator = methodBuilder.GetILGenerator();

            // Push ModelHolder onto the stack
            // Call ModelHolder
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, typeof(ModelBase).GetProperties().Where(x => x.Name == "ModelHolder").First().GetMethod);

            // Push the function name on the stack
            ilGenerator.Emit(OpCodes.Ldstr, DynamicWrapper.MethodSerialize(method));

            //ilGenerator.Emit(OpCodes.Ldstr, method.Name);

            // Push object[] paramseters
            // Create object[] array
            ilGenerator.Emit(OpCodes.Ldc_I4_S, parameters.Length);
            ilGenerator.Emit(OpCodes.Newarr, typeof(object));

            // Set values in array
            for (int i = 0; i < parameters.Length; i++)
            {
                ilGenerator.Emit(OpCodes.Dup);
                ilGenerator.Emit(OpCodes.Ldc_I4_S, i);

                Type type = parameters[i].ParameterType;
                ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                if (type.IsPrimitive)
                {
                    ilGenerator.Emit(OpCodes.Box, type);
                }
                //Put stack value into array position
                ilGenerator.Emit(OpCodes.Stelem_Ref);
            }

            var methodCall = typeof(IModelHolder).GetMethod("MethodCall");
            ilGenerator.EmitCall(OpCodes.Callvirt, methodCall, new Type[] { typeof(string), typeof(object[]) });

            if (method.ReturnType != typeof(void))
            {
                if (method.ReturnType.IsPrimitive)
                {
                    ilGenerator.Emit(OpCodes.Unbox_Any, method.ReturnType);
                }
                else if(method.ReturnType != typeof(object))
                {
                    ilGenerator.Emit(OpCodes.Castclass, method.ReturnType);
                }
            }
            else
            {
                ilGenerator.Emit(OpCodes.Pop);
            }

            ilGenerator.Emit(OpCodes.Ret);
        }

    }


    public static class TypeExtensions
    {
        public static MethodInfo GetGenericMethod(this Type type, string name, params Type[] parameterTypes)
        {
            var methods = type.GetMethods().Where(method => method.Name == name);

            foreach (var method in methods)
            {
                if (method.HasParameters(parameterTypes))
                    return method;
            }

            return null;
        }

        public static bool HasParameters(this MethodInfo method, params Type[] parameterTypes)
        {
            var methodParameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

            if (methodParameters.Length != parameterTypes.Length)
                return false;

            for (int i = 0; i < methodParameters.Length; i++)
                if (methodParameters[i].ToString() != parameterTypes[i].ToString())
                    return false;

            return true;
        }

        public static IEnumerable<Type> AllInterfaces(this Type target)
        {
            foreach (var IF in target.GetInterfaces())
            {
                yield return IF;
                foreach (var childIF in IF.AllInterfaces())
                {
                    yield return childIF;
                }
            }
        }

        public static IEnumerable<MethodInfo> AllMethods(this Type target)
        {
            var allTypes = target.AllInterfaces().ToList();
            allTypes.Add(target);

            return from type in allTypes
                   from method in type.GetMethods()
                   select method;
        }
    }

    //http://stackoverflow.com/questions/10313979/methodinfo-invoke-performance-issue
    public class FastMethodInfo
    {
        private delegate object ReturnValueDelegate(object instance, object[] arguments);
        private delegate void VoidDelegate(object instance, object[] arguments);
        private MethodInfo methodInfo;
        private Type[] types;

        public FastMethodInfo(MethodInfo methodInfo)
        {
            this.methodInfo = methodInfo;
            types = methodInfo.GetParameters().Select(x => x.ParameterType).ToArray();

            var instanceExpression = Expression.Parameter(typeof(object), "instance");
            var argumentsExpression = Expression.Parameter(typeof(object[]), "arguments");
            var argumentExpressions = new List<Expression>();
            var parameterInfos = methodInfo.GetParameters();
            for (var i = 0; i < parameterInfos.Length; ++i)
            {
                var parameterInfo = parameterInfos[i];
                argumentExpressions.Add(Expression.Convert(Expression.ArrayIndex(argumentsExpression, Expression.Constant(i)), parameterInfo.ParameterType));
            }
            var callExpression = Expression.Call(!methodInfo.IsStatic ? Expression.Convert(instanceExpression, methodInfo.ReflectedType) : null, methodInfo, argumentExpressions);
            if (callExpression.Type == typeof(void))
            {
                var voidDelegate = Expression.Lambda<VoidDelegate>(callExpression, instanceExpression, argumentsExpression).Compile();
                Delegate = (instance, arguments) => { voidDelegate(instance, arguments); return null; };
            }
            else
                Delegate = Expression.Lambda<ReturnValueDelegate>(Expression.Convert(callExpression, typeof(object)), instanceExpression, argumentsExpression).Compile();
        }

        private ReturnValueDelegate Delegate { get; }

        public object Invoke(object instance, params object[] arguments)
        {
            var typedArguments = new object[arguments.Length];
            for(int i = 0; i < arguments.Length; i++)
            {
                typedArguments[i] = MakeType(arguments[i], types[i]);
            }
            return Delegate(instance, typedArguments);
        }

        public static object MakeType(object val, Type type)
        {
            if (type == typeof(string)) return val;

            if (type.IsPrimitive)
            {
                return Convert.ChangeType(val, type);
            }
            else
            {
                if (val.GetType() == typeof(JArray))
                {
                    return((JArray)val).ToObject(type);
                }
                else if (val.GetType() == typeof(JObject))
                {
                    return((JObject)val).ToObject(type);
                }
                else
                {
                    return JsonConvert.DeserializeObject((string)val, type);
                }
            }
        }
    }
}

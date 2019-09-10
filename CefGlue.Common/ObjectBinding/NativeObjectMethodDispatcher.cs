using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xilium.CefGlue.Common.Events;
using Xilium.CefGlue.Common.Helpers;
using Xilium.CefGlue.Common.RendererProcessCommunication;
using Xilium.CefGlue.Common.Serialization;

namespace Xilium.CefGlue.Common.ObjectBinding
{
    internal class NativeObjectMethodDispatcher
    {
        private readonly NativeObjectRegistry _objectRegistry;

        public NativeObjectMethodDispatcher(MessageDispatcher dispatcher, NativeObjectRegistry objectRegistry)
        {
            _objectRegistry = objectRegistry;

            dispatcher.RegisterMessageHandler(Messages.NativeObjectCallRequest.Name, HandleNativeObjectCallRequest);
        }

        private void HandleNativeObjectCallRequest(MessageReceivedEventArgs args)
        {
            // message and arguments must be deserialized at this point because it will be disposed after
            var message = Messages.NativeObjectCallRequest.FromCefMessage(args.Message);
            var arguments = CefValueSerialization.DeserializeCefList<object>(message.Arguments);

            Task.Run(() =>
            {
                var resultMessage = new Messages.NativeObjectCallResult()
                {
                    CallId = message.CallId
                };

                try
                {
                    var targetObj = _objectRegistry.Get(message.ObjectName);
                    if (targetObj != null)
                    {
                        var nativeMethod = targetObj.GetNativeMethod(message.MemberName);
                        if (nativeMethod != null)
                        {
                            var result = ExecuteMethod(targetObj.Target, nativeMethod, arguments, targetObj.MethodHandler);

                            resultMessage.Result = CefValueSerialization.Serialize(result);
                            resultMessage.Success = true;
                        }
                        else
                        {
                            resultMessage.Success = false;
                            resultMessage.Exception = $"Object does not have a {message.ObjectName} method.";
                        }
                    }
                    else
                    {
                        resultMessage.Success = false;
                        resultMessage.Exception = $"Object named {message.ObjectName} was not found. Make sure it was registered before.";
                    }
                }
                catch (Exception e)
                {
                    resultMessage.Success = false;
                    resultMessage.Exception = e.Message;
                }
                
                args.Browser.SendProcessMessage(CefProcessId.Renderer, resultMessage.ToCefProcessMessage());
            });
        }

        private object ExecuteMethod(object targetObj, MethodInfo method, object[] args, JavascriptObjectMethodCallHandler methodHandler)
        {
            var convertedArgs = ConvertArguments(method, args);

            if (methodHandler != null)
            {
                return methodHandler(() => method.Invoke(targetObj, convertedArgs));
            }

            // TODO improve call perf
            return method.Invoke(targetObj, convertedArgs);
        }

        private static object[] ConvertArguments(MethodInfo method, object[] args)
        {
            var parameters = method.GetParameters();
            var mandatoryParams = parameters.TakeWhile(p => !IsParamArray(p)).ToArray();

            if (args.Length < mandatoryParams.Length)
            {
                throw new ArgumentException($"Number of arguments provided does not match the number of {method.Name} method required parameters.");
            }

            var argIndex = 0;
            var convertedArgs = new List<object>(mandatoryParams.Length + 1);
            
            for (; argIndex < mandatoryParams.Length; argIndex++)
            {
                convertedArgs.Add(JavascriptToNativeTypeConverter.ConvertToNative(args[argIndex], mandatoryParams[argIndex].ParameterType));
            }

            var optionalParam = parameters.Skip(mandatoryParams.Length).FirstOrDefault();
            if (optionalParam != null)
            {
                var optionalArgs = args.Skip(argIndex).ToArray();
                if (optionalArgs.Any())
                {
                    convertedArgs.Add(JavascriptToNativeTypeConverter.ConvertToNative(optionalArgs, optionalParam.ParameterType));
                }
            }

            return convertedArgs.ToArray();
        }

        private static bool IsParamArray(ParameterInfo paramInfo)
        {
            return paramInfo.GetCustomAttribute(typeof(ParamArrayAttribute), false) != null;
        }
    }
}
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sync
{
    public class FunctionCall
    {
        public string methodHash;
        public object[] parameters;
        public long sequenceId;
    }
    public class FunctionReturn
    {
        public long sequenceId;
        public object value;
    }
    public class CallWriter : IModelHolder
    {
        Stream stream;
        Type modelInterface;
        Dictionary<string, Type> returnTypes = new Dictionary<string, Type>();
        public CallWriter(Stream stream, Type modelInterface)
        {
            this.stream = stream;

            this.modelInterface = modelInterface;

            returnTypes = modelInterface.AllMethods().ToDictionary(x => DynamicWrapper.MethodSerialize(x), x => x.ReturnType);

            new Thread(ReturnReadLoop).Start();
        }
        long nextSequenceId = 0;
        class ReturnHolder
        {
            //Is this efficient?
            public SemaphoreSlim semaphore = new SemaphoreSlim(0, 1);
            public object result;
        }
        Dictionary<long, ReturnHolder> callbacks = new Dictionary<long, ReturnHolder>();
        private void ReturnReadLoop()
        {
            var reader = new StreamReader(stream);
            while (true)
            {
                string returnJSON = reader.ReadLine();
                var returnResult = JsonConvert.DeserializeObject<FunctionReturn>(returnJSON);

                ReturnHolder callback;
                lock (callbacks)
                {
                    callback = callbacks[returnResult.sequenceId];
                    callbacks.Remove(returnResult.sequenceId);
                }
                callback.result = returnResult.value;
                callback.semaphore.Release();
            }
        }
        public object MethodCall(string methodHash, object[] parameters)
        {
            var functionCall = new FunctionCall() { methodHash = methodHash, parameters = parameters, sequenceId = this.nextSequenceId++ };
            var functionJSON = JsonConvert.SerializeObject(functionCall);

            var bytes = Encoding.UTF8.GetBytes(functionJSON + "\n");
            stream.Write(bytes, 0, bytes.Length);

            // Wait for the return value, if it has a return type
            var returnType = returnTypes[methodHash];
            // Treat all void functions as async
            if (returnType == typeof(void))
            {
                return null;
            }

            var returnHolder = new ReturnHolder();
            lock (callbacks)
            {
                callbacks[functionCall.sequenceId] = returnHolder;
            }

            returnHolder.semaphore.Wait();

            return FastMethodInfo.MakeType(returnHolder.result, returnType);
        }
    }

    public class CallReader
    {
        //Private, because it must be thread safe, and always increasing in size.
        private int BUFFER_SIZE = 2 << 12;

        Stream stream;
        object instance;

        Dictionary<string, FastMethodInfo> methods;
        Dictionary<string, Type> returnTypes = new Dictionary<string, Type>();

        public CallReader(Stream stream, object instance)
        {
            this.stream = stream;
            this.instance = instance;

            methods = instance.GetType().AllMethods().Where(x => !x.IsAbstract).ToDictionary(x => DynamicWrapper.MethodSerialize(x), x => new FastMethodInfo(x));
            returnTypes = instance.GetType().AllMethods().Where(x => !x.IsAbstract).ToDictionary(x => DynamicWrapper.MethodSerialize(x), x => x.ReturnType);

            new Thread(ReadLoop).Start();
        }

        private void ReadLoop()
        {
            var reader = new StreamReader(stream);

            while(true)
            {
                string functionJSON = reader.ReadLine();
                if (functionJSON == null) continue;
                FunctionCall functionCall = JsonConvert.DeserializeObject<FunctionCall>(functionJSON);
                object result = methods[functionCall.methodHash].Invoke(instance, functionCall.parameters);

                Type returnType = returnTypes[functionCall.methodHash];
                if(returnType != typeof(void))
                {
                    string returnJSON = JsonConvert.SerializeObject(new FunctionReturn() { sequenceId = functionCall.sequenceId, value = result });
                    var bytes = Encoding.UTF8.GetBytes(returnJSON + "\n");
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
        }
    }
}

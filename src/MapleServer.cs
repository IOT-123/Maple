/*
 * 2018-07-11 Nic Roche: modify Context, add invokeHandlerMethod() & send404
 * 
 * 
*/

using System;
using Microsoft.SPOT;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Text;
using System.Reflection;

namespace Maple
{
    public partial class MapleServer
    {
        private const int MAPLE_SERVER_BROADCASTPORT = 17756;

        private HttpListener server;
        private Thread connection;
        private ArrayList handlers;
        private Type resourceRequestHandler = null;

        /// <param name="prefix">http or https</param>
        public MapleServer(string prefix, int port)
        {
            handlers = new ArrayList();
            server = new HttpListener(prefix, port);
            server.Start();
        }

        public void Start()
        {
            // spin up server
            Init();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">Name of the server to be broadcast over UDP</param>
        /// <param name="ipaddress">IP Address of the server to be broadcast over UDP</param>
        /// <param name="interval">Interval to broadcast in millis</param>
        public void Start(string name, string ipaddress, int interval=5000)
        {
            // start up broadcast on separate thread
            new Thread(new ThreadStart(delegate { Broadcast(name + "=" + ipaddress, MAPLE_SERVER_BROADCASTPORT, interval); })).Start();

            // spin up server
            Init();
        }

        public void Stop()
        {
            if (connection.IsAlive)
            {
                connection.Abort();
            }
        }

        public void AddHandler(IRequestHandler handler)
        {
            this.handlers.Add(handler);
        }

        public void RemoveHandler(IRequestHandler handler)
        {
            this.handlers.Remove(handler);
        }

        public MapleServer() : this("http", -1) { }

        protected void Init()
        {
            ThreadStart starter = delegate { Context(handlers, resourceRequestHandler); };
            connection = new Thread(starter);
            connection.Start();
        }

        /// <param name="data">string to be broadcast over UDP</param>
        /// <param name="interval">millis</param>
        protected void Broadcast(string data, int broadcastPort, int broadcastInterval)
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse("255.255.255.255"), broadcastPort);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                while (true)
                {
                    socket.SendTo(UTF8Encoding.UTF8.GetBytes(data), remoteEndPoint);
                    Debug.Print("UDP Broadcast: " + data + ", port: " + broadcastPort);
                    Thread.Sleep(broadcastInterval);
                }
            }
        }

        protected void Context(ArrayList requestHandlers, Type resourceHandler)
        {
            if (requestHandlers.Count == 0)
            {
                // Get classes that implement IRequestHandler
                var type = typeof(IRequestHandler);
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    var types = assembly.GetTypes();
                    foreach (var t in types)
                    {
                        if (t.BaseType != null)
                        {
                            var interfaces = t.BaseType.GetInterfaces();
                            if (interfaces.Length > 0) {
                                foreach (var inter in interfaces) {
                                    if (resourceHandler == null && inter == typeof(IResourceRequestHandler)) {
                                        resourceHandler = t;
                                    } else if (inter == typeof(IRequestHandler)) {
                                        requestHandlers.Add(t);
                                    }
                                }
                            }
                        }
                    }
                }
            }


            while (true)
            {
                try
                {
                    HttpListenerContext context = server.GetContext();
                    string[] urlQuery = context.Request.RawUrl.Substring(1).Split('?');
                    string[] urlParams = urlQuery[0].Split('/');
                    string methodName = context.Request.HttpMethod + urlParams[0];
                    Debug.Print("Received " + context.Request.HttpMethod + " " + context.Request.RawUrl + " - Invoking " + methodName);
                    // convention for method is "{http method}{method name}"
                    // would love to convert this to two attributes: HttpGet, Mapping/Route
                    bool wasMethodFound = false;

                    foreach (var handler in requestHandlers)
                    {
                        Type handlerType = handler is Type ? (Type)handler : handler.GetType();
                        var methods = handlerType.GetMethods();
                        foreach (var method in methods)
                        {
                            if (method.Name.ToLower() == methodName.ToLower())
                            {
                                object target = handler;
                                if (handler is Type)
                                {
                                    target = ((Type)handler).GetConstructor(new Type[] { }).Invoke(new object[] { });
                                }
                                try
                                {
                                    ((IRequestHandler)target).Context = context;
                                    method.Invoke(target, null);
                                }
                                catch (Exception ex)
                                {
                                    Debug.Print(ex.Message);
                                    context.Response.StatusCode = 500;
                                    context.Response.Close();
                                }
                                wasMethodFound = true;
                                break;
                            }
                        }
                        if (wasMethodFound) break;
                    }

                    if (!wasMethodFound) {
                        if (resourceHandler == null) {
                            send404(context);
                        } else { 
                            Type handlerType = resourceHandler is Type ? (Type)resourceHandler : resourceHandler.GetType();
                            ((IRequestHandler)resourceHandler).Context = context;
                            object[] parametersArray;
                            // resource method names changed from IRequestHandler conventions for visibility
                            var resourceMethodName = "Resource";
                            var httpMethod = context.Request.HttpMethod.ToUpper();
                            switch (httpMethod)
                            {
                                case "GET":
                                case "DELETE":
                                case "OPTIONS":
                                    var methodPrefix = httpMethod == "GET" ? "read" : httpMethod == "DELETE" ? "remove" : "preflight";
                                    resourceMethodName = methodPrefix + resourceMethodName;
                                    parametersArray = new object[] { urlQuery[0] }; // path
                                    invokeHandlerMethod(context, handlerType, resourceHandler, resourceMethodName, parametersArray);
                                    break;
                                case "PUT":
                                case "POST":
                                    resourceMethodName = httpMethod == "PUT" ? "create" + resourceMethodName : "update" + resourceMethodName;
                                    parametersArray = new object[] { urlQuery[0], context.Request.InputStream }; // path
                                    invokeHandlerMethod(context, handlerType, resourceHandler, resourceMethodName, parametersArray);
                                    break;
                                default:
                                    send404(context);
                                    break;
                            }
                        }
                    }
                }
                catch (SocketException e) {
                    Debug.Print("Socket Exception: " + e.ToString());
                } catch (Exception ex) {
                    Debug.Print(ex.ToString());
                }
            }
        }

        private void invokeHandlerMethod(HttpListenerContext context, Type handlerType, Type handler, string resourceMethodName, object[] parametersArray)
        {
            MethodInfo resourceMethod = handlerType.GetMethod(resourceMethodName);
            if (resourceMethod == null)
            {
                send404(context);
                return;
            }
            bool methodSuccess = (bool)resourceMethod.Invoke(handler, parametersArray);
            if (!methodSuccess)
            {
                send404(context);
                return;
            }
        }

        private void send404(HttpListenerContext context)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
        }

    }

}



// nuget pack Maple.csproj -Prop Configuration=Release -Prop Platform=AnyCPU
/*
 * 2018-07-11 Nic Roche: add to repository
 * 
 * 
*/

using Maple;
using System.IO;

namespace Maple_Resource_Sample
{
    public class RequestHandler : RequestHandlerBase, IResourceRequestHandler
    {
        // if handling the response on an error condition in the method, return true, false sends a 404
        // his.Context.Response.ContentType = ContentTypes.XXX can be used instead of base.setContentType()
        public RequestHandler() { }

        public bool readResource(string path)
        {
            // read logic here

            base.setContentType();
            this.Context.Response.StatusCode = 200;
            this.Send("<html>hello html world</html>");
            return true;
        }

        public bool removeResource(string path)
        {
            // remove logic here

            //if (path == "/")
            //    return _server.send(500, "text/plain", "BAD PATH");
            //if (!SPIFFS.exists(path))
            //    return _server.send(404, "text/plain", "FileNotFound");

            base.setContentType();
            this.Context.Response.StatusCode = 200;
            this.Context.Response.Close();
            return true;
        }

        public bool preflightResource(string path)
        {
            // preflight logic here


            this.Context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            this.Context.Response.Headers.Add("Access-Control-Max-Age", "10000");
            this.Context.Response.Headers.Add("Access-Control-Allow-Methods", "PUT,POST,GET,OPTIONS");
            this.Context.Response.Headers.Add("Access-Control-Allow-Headers", "*");
            this.Context.Response.StatusCode = 204;
            this.Context.Response.Close();
            return true;
        }

        public bool createResource(string path, Stream inputStream)
        {
            // create logic here

            //if (path == "/")
            //    return _server.send(500, "text/plain", "BAD PATH");
            //if (SPIFFS.exists(path))
            //    return _server.send(500, "text/plain", "FILE EXISTS");
            //File file = SPIFFS.open(path, "w");
            //if (file)
            //    file.close();
            //else
            //    return _server.send(500, "text/plain", "CREATE FAILED");

            base.setContentType();
            this.Context.Response.StatusCode = 200;
            this.Context.Response.Close();
            return true;
        }

        public bool updateResource(string path, Stream inputStream) {
            // update logic here

            //    return _server.send(500, "text/plain", "BAD PATH");

            base.setContentType();
            this.Context.Response.StatusCode = 200;
            this.Context.Response.Close();
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    // https://codehosting.net/blog/BlogEngine/post/Simple-C-Web-Server.aspx
    /// <summary>
    /// Must be running as Admin user
    /// </summary>
    internal class WebServer
    {
        private readonly HttpListener _listener = new HttpListener();
        /// <summary>
        /// input: rawurl, headers, inputStream, inputContentEncoding
        /// </summary>
        private readonly Func<string, NameValueCollection,
            Stream, System.Text.Encoding, string,
            Task<HttpResponseMessage>> _responderMethod;

        public WebServer(Dictionary<string, List<string>> prefixes, Func<string, NameValueCollection,
            Stream, System.Text.Encoding, string,
            Task<HttpResponseMessage>> method)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException(
                    "Needs Windows XP SP2, Server 2003 or later.");

            // URI prefixes are required, for example 
            // "http://localhost:8080/index/".
            if (prefixes == null || prefixes.Count == 0)
                throw new ArgumentException("prefixes");

            // A responder method is required
            if (method == null)
                throw new ArgumentException("method");

            _responderMethod = method;

            SetAllListeners(prefixes);
            _listener.Start();
            
        }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Console.WriteLine("Webserver running...");
                try
                {
                    while (_listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem(async (c) =>
                        {
                            var ctx = c as HttpListenerContext;
                            try
                            {
                                string rawurl = ctx.Request.RawUrl;
                                var headers = ctx.Request.Headers;
                                Stream inputStream = ctx.Request.InputStream;
                                var contentType = ctx.Request.ContentType;
                                var inputContentEncoding = ctx.Request.ContentEncoding;
                                var result = await _responderMethod(rawurl, headers, inputStream, inputContentEncoding, contentType);
                                byte[] buf = await result.Content.ReadAsByteArrayAsync();
                                ctx.Response.ContentType = "application/json; charset=UTF-8";
                                ctx.Response.StatusCode = (int)result.StatusCode;
                                ctx.Response.ContentLength64 = buf.Length;
                                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Could not connect Error: {ex.Message} Stack Trace: {ex.StackTrace}");
                            }
                            finally
                            {
                                // always close the stream
                                ctx.Response.OutputStream.Close();
                            }
                        }, _listener.GetContext());
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Could not connect Error: {ex.Message} Stack Trace: {ex.StackTrace}");
                } // suppress any exceptions
            });
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }

        private void SetAllListeners(Dictionary<string, List<string>> prefixes)
        {
            foreach (KeyValuePair<string, List<string>> ipPrefixPair in prefixes)
            {
                foreach (string prefix in ipPrefixPair.Value)
                {
                    AddListener(prefix);
                }
            }
        }

        private void AddListener(string prefix)
        {
            try
            {
                _listener.Prefixes.Add(prefix);
            }
            catch
            {
            }
        }
    }
}
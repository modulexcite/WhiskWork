﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Abb.One.MicroWebServer;
using System.IO;
using WhiskWork.Core;
using System.Net;
using WhiskWork.Web;
using WhiskWork.Generic;

namespace WhiskWork.CommondLineWebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            const string fileDirectory = @"c:\temp\agileboard";

            var router = new WebRouter(fileDirectory);
            var server = new WebServer(router.ProcessRequest,5555);

            Console.WriteLine("Started");
            server.Start();

        }
    }

    internal class WebRouter
    {
        private readonly MemoryWorkflowRepository _workflowRepository;
        private readonly MemoryWorkItemRepository _workItemRepository;
        private readonly Workflow _wp;
        private readonly string _rootDirectory;

        public WebRouter(string directory)
        {
            _workflowRepository = new MemoryWorkflowRepository();
            _workItemRepository = new MemoryWorkItemRepository();
            _wp = new Workflow(_workflowRepository, _workItemRepository);
            _rootDirectory = directory;

            _workflowRepository.Add("/scheduled", "/", 1, WorkStepType.Begin, "cr", "Scheduled");
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Normal, "cr", "Analysis");
            _workflowRepository.Add("/analysis/inprocess", "/analysis", 1, WorkStepType.Normal, "cr");
            _workflowRepository.Add("/analysis/done", "/analysis", 1, WorkStepType.Normal, "cr");
            _workflowRepository.Add("/development", "/", 2, WorkStepType.Begin, "cr", "Development");
            _workflowRepository.Add("/development/inprocess", "/development", 1, WorkStepType.Expand, "cr");
            _workflowRepository.Add("/development/inprocess/tasks", "/development/inprocess", 1, WorkStepType.Normal, "task", "Tasks");
            _workflowRepository.Add("/development/inprocess/tasks/new", "/development/inprocess/tasks", 1, WorkStepType.Begin, "task");
            _workflowRepository.Add("/development/inprocess/tasks/inprocess", "/development/inprocess/tasks", 1, WorkStepType.Normal, "task");
            _workflowRepository.Add("/development/inprocess/tasks/done", "/development/inprocess/tasks", 1, WorkStepType.End, "task");
            _workflowRepository.Add("/development/done", "/development", 2, WorkStepType.End, "cr");
            _workflowRepository.Add("/feedback", "/", 3, WorkStepType.Parallel, "cr");
            _workflowRepository.Add("/feedback/review", "/feedback", 1, WorkStepType.Normal, "cr-review", "Review");
            //_workflowRepository.Add("/feedback/demo", "/feedback", 2, WorkStepType.Normal, "cr-demo", "Demo");
            _workflowRepository.Add("/feedback/test", "/feedback", 3, WorkStepType.Normal, "cr-test", "Test");
            _workflowRepository.Add("/done", "/", 4, WorkStepType.End, "cr", "Done");
        }


        public void ProcessRequest(IHttpListenerContext httpcontext)
        {
            var reader = new StreamReader(httpcontext.Request.InputStream);
            var payload = reader.ReadToEnd();
            var path = httpcontext.Request.RawUrl;
            var httpMethod = httpcontext.Request.HttpMethod;
            
            Console.WriteLine("Payload: "+ payload);
            Console.WriteLine("Path: '{0}'",path);
            Console.WriteLine("HttpMethod: " +httpMethod);

            if(TryReturnFile(httpcontext.Response,path))
            {
                Console.WriteLine("Found file");
                return;
            }

            string actualPath;
            string workItemId;

            switch (httpMethod.ToLowerInvariant())
            {
                case "post":
                    if (IsWorkItem(path, out actualPath, out workItemId))
                    {
                        UpdateWorkItem(httpcontext.Response, actualPath, workItemId, payload);
                    }
                    else
                    {
                        CreateWorkItem(httpcontext.Response, path, payload);                        
                    }

                    break;
                case "get":
                    RenderHtml(httpcontext.Response, path);
                    break;
                case "delete":
                    if (IsWorkItem(path, out actualPath, out workItemId))
                    {
                        DeleteWorkItem(httpcontext.Response, actualPath, workItemId);
                    }

                    break;
            }

        }

        private bool IsWorkItem(string rawPath, out string pathPart, out string workItemId)
        {
            pathPart = rawPath;
            workItemId = null;

            var potentialWorkItemId = rawPath.Split('/').Last();

            if (_wp.ExistsWorkItem(potentialWorkItemId))
            {
                workItemId = potentialWorkItemId;
                pathPart = rawPath.Substring(0, rawPath.LastIndexOf('/'));
                return true;
            }

            return false;
        }

        private void DeleteWorkItem(HttpListenerResponse response, string path, string id)
        {
            WorkItem wi = _wp.GetWorkItem(id);

            if(wi.Path!=path)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            try
            {
                _wp.DeleteWorkItem(id);
            }
            catch (Exception e)
            {
                Console.WriteLine("Delete failed " + e.Message);
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            response.ContentType = "text/html";
            response.StatusCode = (int)HttpStatusCode.OK;
        }


        private void UpdateWorkItem(HttpListenerResponse response, string path, string id, string payload)
        {
            try
            {
                _wp.UpdateWorkItem(id, path, new NameValueCollection());
            }
            catch (Exception e)
            {
                Console.WriteLine("Update failed " + e.Message);
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            response.ContentType = "text/html";
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        private bool TryReturnFile(HttpListenerResponse response, string path)
        {
            var filePath = path.Remove(0,1).Replace('/', '\\');
            var fullPath = Path.Combine(_rootDirectory, filePath);

            if(!File.Exists(fullPath))
            {
                return false;
            }

            response.ContentType = GetContentType(fullPath);

            using(Stream instream = File.OpenRead(fullPath))
            {
                CopySteam(instream, response.OutputStream);
            }

            return true;
        }

        private static void CopySteam(Stream fromStream, Stream toStream)
        {
            var buffer = new byte[1024];
            int readBytes;

            while((readBytes=fromStream.Read(buffer,0,1024))>0)
            {
                toStream.Write(buffer,0,readBytes);
            }
        }

        private static string GetContentType(string path)
        {
            var fi = new FileInfo(path);

            switch(fi.Extension)
            {
                case ".css":
                    return "text/css";
                case ".html":
                    return "text/html";
                case ".js":
                    return "application/javascript";
            }

            return "text/plain";
        }

        private void RenderHtml(HttpListenerResponse response, string path)
        {
            var renderer = new HtmlRenderer(_workflowRepository, _workItemRepository);
            try
            {
                renderer.RenderFull(response.OutputStream);
            }
            catch (Exception e)
            {
                Console.WriteLine("Render failed " + e.Message);
                return;
            }
        }

        private void CreateWorkItem(HttpListenerResponse response, string path, string id)
        {
            try
            {
                _wp.CreateWorkItem(id, path);
            }
            catch(Exception e)
            {
                Console.WriteLine("Create failed "+ e.Message);
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            response.ContentType = "text/html";
            response.StatusCode = (int)HttpStatusCode.Created;
            response.Headers.Add(HttpResponseHeader.Location,path+"/"+id);
        }
    }
}
using System;
using System.Collections.Specialized;
using System.Linq;
using WhiskWork.Core;

namespace WhiskWork.Web
{
    public class WorkflowHttpHandler
    {
        private readonly IWorkStepRendererFactory _rendererFactory;
        private readonly IWorkflow _workflow;

        public WorkflowHttpHandler(IWorkflow workflow, IWorkStepRendererFactory rendererFactory)
        {
            _workflow = workflow;
            _rendererFactory = rendererFactory;
        }

        public WorkflowHttpResponse HandleRequest(WorkflowHttpRequest request)
        {
            switch (request.HttpMethod.ToLowerInvariant())
            {
                case "post":
                    return RespondToPost(request);
                case "get":
                    return RespondToGet(request);
                case "delete":
                    return RespondToDelete(request);
            }

            return WorkflowHttpResponse.MethodNotAllowed;
        }

        private WorkflowHttpResponse RespondToPost(WorkflowHttpRequest request)
        {
            IRequestMessageParser parser;
            if (!TryLocateParser(request.ContentType, out parser))
            {
                return WorkflowHttpResponse.UnsupportedMediaType;
            }

            var visitor = new HttpPostWorkflowNodeVisitor(_workflow, request.RawUrl);
            parser.Parse(request.InputStream).AcceptVisitor(visitor);

            return visitor.Response;
        }

        private WorkflowHttpResponse RespondToGet(WorkflowHttpRequest request)
        {
            var renderer = _rendererFactory.CreateRenderer(request.ContentType);
            return Render(renderer, request.RawUrl);
        }

        private WorkflowHttpResponse RespondToDelete(WorkflowHttpRequest request)
        {
            string actualPath;
            string workItemId;
            if (IsWorkItem(request.RawUrl, out actualPath, out workItemId))
            {
                return DeleteWorkItem(actualPath, workItemId);
            }
            
            return DeleteWorkStep(request.RawUrl);
        }

        private static bool TryLocateParser(string contentType, out IRequestMessageParser parser)
        {
            return RequestMessageParserFactory.TryCreate(contentType, out parser);
        }

        private static WorkflowHttpResponse Render(IWorkStepRenderer renderer, string path)
        {
            try
            {
                var response = WorkflowHttpResponse.Ok;
                renderer.Render(response.OutputStream, path);

                return response;
            }
            catch (Exception e)
            {
                Console.WriteLine("Render failed " + e.Message);
                return WorkflowHttpResponse.InternalServerError;
            }
        }

        private WorkflowHttpResponse DeleteWorkStep(string path)
        {
            if(!_workflow.ExistsWorkStep(path))
            {
                return WorkflowHttpResponse.NotFound;
            }

            return WorkflowHttpResponse.NotImplemented;
        }

        private WorkflowHttpResponse DeleteWorkItem(string path, string id)
        {
            if (!_workflow.ExistsWorkItem(id))
            {
                return WorkflowHttpResponse.NotFound;
            }

            var wi = _workflow.GetWorkItem(id);

            if (wi.Path != path)
            {
                return WorkflowHttpResponse.NotFound;
            }

            try
            {
                _workflow.DeleteWorkItem(id);
            }
            catch (Exception e)
            {
                Console.WriteLine("Delete failed " + e.Message);
                return WorkflowHttpResponse.Forbidden;
            }

            return WorkflowHttpResponse.Ok;
        }

        private bool IsWorkItem(string rawPath, out string pathPart, out string workItemId)
        {
            pathPart = rawPath;
            workItemId = null;

            string potentialWorkItemId = rawPath.Split('/').Last();

            if (_workflow.ExistsWorkItem(potentialWorkItemId))
            {
                workItemId = potentialWorkItemId;
                pathPart = rawPath.Substring(0, rawPath.LastIndexOf('/'));
                return true;
            }

            return false;
        }
    }
}
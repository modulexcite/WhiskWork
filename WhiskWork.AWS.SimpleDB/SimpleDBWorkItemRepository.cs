﻿using System;
using System.Collections.Generic;
using System.Xml;
using Amazon;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;
using WhiskWork.Core;
using Attribute=Amazon.SimpleDB.Model.Attribute;

namespace WhiskWork.AWS.SimpleDB
{
    public class SimpleDBWorkItemRepository : IWorkItemRepository
    {
        private readonly AmazonSimpleDB _client;
        private readonly string _domain;

        public SimpleDBWorkItemRepository(string domain, string accessKey, string secretKey)
        {
            _domain = domain;
            _client = AWSClientFactory.CreateAmazonSimpleDBClient(accessKey, secretKey);
            EnsureDomain(_domain);
        }

        public bool ExistsWorkItem(string id)
        {
            var getAttributesRequest = new GetAttributesRequest { ItemName = id, DomainName = _domain };
            var getAttributesResponse = _client.GetAttributes(getAttributesRequest);

            return getAttributesResponse.GetAttributesResult.Attribute.Count > 0;
        }

        public WorkItem GetWorkItem(string id)
        {
            var getAttributesRequest = new GetAttributesRequest { ItemName = id, DomainName = _domain };
            var getAttributesResponse = _client.GetAttributes(getAttributesRequest);

            var attributes = getAttributesResponse.GetAttributesResult.Attribute;
            return GenerateWorkItem(id, attributes);
        }


        public void CreateWorkItem(WorkItem workItem)
        {
            UpdateWorkItem(workItem);
        }

        public IEnumerable<WorkItem> GetWorkItems(string path)
        {
            var selectRequest = new SelectRequest();
            selectRequest.SelectExpression = string.Format("select * from {0} where Path='{1}'", _domain, path);

            var selectResponse = _client.Select(selectRequest);

            foreach (var item in selectResponse.SelectResult.Item)
            {
                yield return GenerateWorkItem(item.Name, item.Attribute);
            }
        }

        public void UpdateWorkItem(WorkItem workItem)
        {
            var putAttributeRequest = new PutAttributesRequest { DomainName = _domain, ItemName = workItem.Id };
            var attributes = putAttributeRequest.Attribute;

            attributes.Add(new ReplaceableAttribute { Name = "Path", Value = workItem.Path });
            attributes.Add(new ReplaceableAttribute { Name = "Status", Value = workItem.Status.ToString() });

            if (workItem.Parent!= null)
            {
                attributes.Add(new ReplaceableAttribute { Name = "ParentId", Value = workItem.Parent.Id });
                attributes.Add(new ReplaceableAttribute { Name = "ParentType", Value = workItem.Parent.Type.ToString() });
            }

            if (workItem.Ordinal.HasValue)
            {
                attributes.Add(new ReplaceableAttribute { Name = "Ordinal", Value = workItem.Ordinal.Value.ToString() });
            }

            foreach (var workItemClass in workItem.Classes)
            {
                attributes.Add(new ReplaceableAttribute { Name = "Classes", Value = workItemClass });
            }

            foreach (var keyValue in workItem.Properties)
            {
                attributes.Add(new ReplaceableAttribute { Name = keyValue.Key, Value = keyValue.Value });
            }

            _client.PutAttributes(putAttributeRequest);
        }

        public IEnumerable<WorkItem> GetChildWorkItems(WorkItemParent parent)
        {
            var selectRequest = new SelectRequest();
            selectRequest.SelectExpression = string.Format("select * from {0} where ParentId='{1}' and ParentType='{2}'", _domain, parent.Id, parent.Type);

            var selectResponse = _client.Select(selectRequest);

            foreach (var item in selectResponse.SelectResult.Item)
            {
                yield return GenerateWorkItem(item.Name, item.Attribute);
            }
        }

        public void DeleteWorkItem(WorkItem workItem)
        {
            var deleteAttributesRequest = new DeleteAttributesRequest { ItemName = workItem.Id, DomainName = _domain };
            _client.DeleteAttributes(deleteAttributesRequest);
        }

        private void EnsureDomain(string domain)
        {
            var listDomainsRequest = new ListDomainsRequest();
            var listDomainsResponse = _client.ListDomains(listDomainsRequest);

            if (listDomainsResponse.ListDomainsResult.DomainName.Contains(domain))
            {
                return;
            }

            var createDomainRequest = new CreateDomainRequest { DomainName = domain };
            _client.CreateDomain(createDomainRequest);
        }

        private static WorkItem GenerateWorkItem(string id, IEnumerable<Attribute> attributes)
        {
            string path = GetAttributeValue(attributes, "Path");

            var item = WorkItem.New(id, path);

            WorkItemParentType? parentType = null;
            string parentId = null;

            foreach (var attribute in attributes)
            {
                switch (attribute.Name)
                {
                    case "Path":
                        break;
                    case "Status":
                        item = item.UpdateStatus((WorkItemStatus)Enum.Parse(typeof(WorkItemStatus), attribute.Value));
                        break;
                    case "Ordinal":
                        item = item.UpdateOrdinal(XmlConvert.ToInt32(attribute.Value));
                        break;
                    case "Classes":
                        item = item.AddClass(attribute.Value);
                        break;
                    case "ParentId":
                        parentId = attribute.Value;
                        if(parentType.HasValue)
                        {
                            item = item.UpdateParent(parentId,parentType.Value);
                        }
                        break;
                    case "ParentType":
                        parentType = (WorkItemParentType)Enum.Parse(typeof(WorkItemParentType), attribute.Value);
                        if (parentId!=null)
                        {
                            item = item.UpdateParent(parentId, parentType.Value);
                        }
                        break;

                    default:
                        item = item.UpdateProperty(attribute.Name, attribute.Value);
                        break;
                }
            }

            return item;
        }

        private static string GetAttributeValue(IEnumerable<Attribute> attributes, string attributeName)
        {
            foreach (var attribute in attributes)
            {
                if (attribute.Name == attributeName)
                {
                    return attribute.Value;
                }
            }

            return null;
        }

    }
}
